using System.Globalization;

public static class TexelTuner
{
    private sealed class Sample
    {
        public required EvalFeatures Features;
        public required double Result;
    }

    private sealed class IntParameter
    {
        public required string Name;
        public required Func<EvalWeights, int> Get;
        public required Action<EvalWeights, int> Set;
        public required int Min;
        public required int Max;
    }

    public static void Run(string path)
    {
        Console.WriteLine("Loading samples...");
        List<Sample> samples = LoadSamples(path);
        Console.WriteLine($"Loaded {samples.Count} samples.");

        EvalWeights weights = Evaluation.GetCurrentWeights();

        Console.WriteLine("Finding best K...");
        double k = FindBestK(samples, weights);
        Console.WriteLine($"Best K = {k.ToString("F6", CultureInfo.InvariantCulture)}");

        Console.WriteLine("Initial loss...");
        double bestLoss = Loss(samples, weights, k);
        Console.WriteLine($"Loss = {bestLoss:F8}");

        List<IntParameter> parameters = BuildParameterList();

        // Coarse-to-fine tuning
        foreach (int step in new[] { 8, 4, 2, 1 })
        {
            Console.WriteLine($"\n=== Step {step} ===");
            bool improved;

            do
            {
                improved = false;

                foreach (IntParameter p in parameters)
                {
                    int original = p.Get(weights);
                    int plusValue = Math.Min(original + step, p.Max);
                    int minusValue = Math.Max(original - step, p.Min);

                    EvalWeights plus = weights.Clone();
                    p.Set(plus, plusValue);
                    double plusLoss = Loss(samples, plus, k);

                    EvalWeights minus = weights.Clone();
                    p.Set(minus, minusValue);
                    double minusLoss = Loss(samples, minus, k);

                    if (plusLoss < bestLoss && plusLoss <= minusLoss)
                    {
                        weights = plus;
                        bestLoss = plusLoss;
                        improved = true;
                        Console.WriteLine($"{p.Name} -> {plusValue}, loss={bestLoss:F8}");
                    }
                    else if (minusLoss < bestLoss)
                    {
                        weights = minus;
                        bestLoss = minusLoss;
                        improved = true;
                        Console.WriteLine($"{p.Name} -> {minusValue}, loss={bestLoss:F8}");
                    }
                }
            }
            while (improved);
        }

        Console.WriteLine("\n=== Final Weights ===");
        Console.WriteLine(weights.ToCSharpConstants());
        Console.WriteLine($"Final loss = {bestLoss:F8}");
        Console.WriteLine($"K = {k.ToString("F6", CultureInfo.InvariantCulture)}");
    }

    private static List<Sample> LoadSamples(string path)
    {
        List<Sample> samples = new();

        foreach (string rawLine in File.ReadLines(path))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            string[] parts = line.Split('|', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
                continue;

            double result = double.Parse(parts[0], CultureInfo.InvariantCulture);
            string fen = parts[1];

            Board.ParseFEN(fen);
            EvalFeatures features = Evaluation.ExtractFeatures();

            samples.Add(new Sample
            {
                Features = features,
                Result = result
            });
        }

        return samples;
    }

    private static double FindBestK(List<Sample> samples, EvalWeights weights)
    {
        double bestK = 0.0045;
        double bestLoss = double.MaxValue;

        for (double k = 0.0010; k <= 0.0100; k += 0.00025)
        {
            double loss = Loss(samples, weights, k);
            if (loss < bestLoss)
            {
                bestLoss = loss;
                bestK = k;
            }
        }

        return bestK;
    }

    private static double Loss(List<Sample> samples, EvalWeights weights, double k)
    {
        double sum = 0.0;

        foreach (Sample sample in samples)
        {
            int score = Evaluation.EvaluateWhitePerspective(sample.Features, weights);
            double p = 1.0 / (1.0 + Math.Exp(-k * score));

            double error = sample.Result - p;
            sum += error * error;
        }

        return sum / samples.Count;
    }

    private static List<IntParameter> BuildParameterList()
    {
        List<IntParameter> p = new()
    {
        new() { Name = nameof(EvalWeights.BishopPairMg), Get = w => w.BishopPairMg, Set = (w, v) => w.BishopPairMg = v, Min = 0, Max = 100 },
        new() { Name = nameof(EvalWeights.BishopPairEg), Get = w => w.BishopPairEg, Set = (w, v) => w.BishopPairEg = v, Min = 0, Max = 120 },

        new() { Name = nameof(EvalWeights.KnightMobMg), Get = w => w.KnightMobMg, Set = (w, v) => w.KnightMobMg = v, Min = -8, Max = 16 },
        new() { Name = nameof(EvalWeights.KnightMobEg), Get = w => w.KnightMobEg, Set = (w, v) => w.KnightMobEg = v, Min = -8, Max = 16 },
        new() { Name = nameof(EvalWeights.BishopMobMg), Get = w => w.BishopMobMg, Set = (w, v) => w.BishopMobMg = v, Min = -8, Max = 16 },
        new() { Name = nameof(EvalWeights.BishopMobEg), Get = w => w.BishopMobEg, Set = (w, v) => w.BishopMobEg = v, Min = -8, Max = 16 },

        new() { Name = nameof(EvalWeights.RookSemiOpenMg), Get = w => w.RookSemiOpenMg, Set = (w, v) => w.RookSemiOpenMg = v, Min = 0, Max = 30 },
        new() { Name = nameof(EvalWeights.RookSemiOpenEg), Get = w => w.RookSemiOpenEg, Set = (w, v) => w.RookSemiOpenEg = v, Min = 0, Max = 30 },
        new() { Name = nameof(EvalWeights.RookOpenMg), Get = w => w.RookOpenMg, Set = (w, v) => w.RookOpenMg = v, Min = 0, Max = 40 },
        new() { Name = nameof(EvalWeights.RookOpenEg), Get = w => w.RookOpenEg, Set = (w, v) => w.RookOpenEg = v, Min = 0, Max = 40 },

        new() { Name = nameof(EvalWeights.IsolatedMg), Get = w => w.IsolatedMg, Set = (w, v) => w.IsolatedMg = v, Min = -40, Max = 0 },
        new() { Name = nameof(EvalWeights.IsolatedEg), Get = w => w.IsolatedEg, Set = (w, v) => w.IsolatedEg = v, Min = -50, Max = 0 },

        new() { Name = nameof(EvalWeights.KingOwnOpenMg), Get = w => w.KingOwnOpenMg, Set = (w, v) => w.KingOwnOpenMg = v, Min = 0, Max = 50 },
        new() { Name = nameof(EvalWeights.KingOwnSemiOpenMg), Get = w => w.KingOwnSemiOpenMg, Set = (w, v) => w.KingOwnSemiOpenMg = v, Min = 0, Max = 30 },
        new() { Name = nameof(EvalWeights.KingAdjacentOpenMg), Get = w => w.KingAdjacentOpenMg, Set = (w, v) => w.KingAdjacentOpenMg = v, Min = 0, Max = 25 },
        new() { Name = nameof(EvalWeights.KingAdjacentSemiOpenMg), Get = w => w.KingAdjacentSemiOpenMg, Set = (w, v) => w.KingAdjacentSemiOpenMg = v, Min = 0, Max = 20 },

        new() { Name = nameof(EvalWeights.KnightOutpostMg), Get = w => w.KnightOutpostMg, Set = (w, v) => w.KnightOutpostMg = v, Min = 0, Max = 40 },
    };

        for (int i = 1; i <= 6; i++)
        {
            int idx = i;
            p.Add(new IntParameter
            {
                Name = $"PassedMg[{idx}]",
                Get = w => w.PassedMg[idx],
                Set = (w, v) => w.PassedMg[idx] = v,
                Min = 0,
                Max = 60
            });

            p.Add(new IntParameter
            {
                Name = $"PassedEg[{idx}]",
                Get = w => w.PassedEg[idx],
                Set = (w, v) => w.PassedEg[idx] = v,
                Min = 0,
                Max = 250
            });
        }

        return p;
    }
}