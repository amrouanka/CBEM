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
                        Console.WriteLine($"{p.Name} -> {p.Get(weights)}, loss={bestLoss:F8}");
                    }
                    else if (minusLoss < bestLoss)
                    {
                        weights = minus;
                        bestLoss = minusLoss;
                        improved = true;
                        Console.WriteLine($"{p.Name} -> {p.Get(weights)}, loss={bestLoss:F8}");
                    }
                }
            }
            while (improved);

            k = FindBestK(samples, weights);
            bestLoss = Loss(samples, weights, k);

            Console.WriteLine($"Step {step} done: K={k:F6}, loss={bestLoss:F8}");
        }

        Console.WriteLine("\n=== Final Weights ===");
        Console.WriteLine(weights.ToCSharpConstants());

        Console.WriteLine("Re-optimizing K with final weights...");
        k = FindBestK(samples, weights);
        bestLoss = Loss(samples, weights, k);
        Console.WriteLine($"Final K = {k.ToString("F6", CultureInfo.InvariantCulture)}");
        Console.WriteLine($"Final loss = {bestLoss:F8}");
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
        double bestK = 0.0090;
        double bestLoss = double.MaxValue;

        for (double k = 0.0020; k <= 0.0200; k += 0.0005)
        {
            double loss = Loss(samples, weights, k);
            if (loss < bestLoss)
            {
                bestLoss = loss;
                bestK = k;
            }
        }

        double start = Math.Max(0.0005, bestK - 0.0010);
        double end = bestK + 0.0010;

        for (double k = start; k <= end; k += 0.00005)
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
        return new List<IntParameter>
    {
        new() { Name = nameof(EvalWeights.PawnMgAdjust),   Get = w => w.PawnMgAdjust,   Set = (w, v) => w.PawnMgAdjust = v,   Min = -15, Max = 15 },
        new() { Name = nameof(EvalWeights.PawnEgAdjust),   Get = w => w.PawnEgAdjust,   Set = (w, v) => w.PawnEgAdjust = v,   Min = -15, Max = 15 },

        new() { Name = nameof(EvalWeights.KnightMgAdjust), Get = w => w.KnightMgAdjust, Set = (w, v) => w.KnightMgAdjust = v, Min = -30, Max = 30 },
        new() { Name = nameof(EvalWeights.KnightEgAdjust), Get = w => w.KnightEgAdjust, Set = (w, v) => w.KnightEgAdjust = v, Min = -30, Max = 30 },

        new() { Name = nameof(EvalWeights.BishopMgAdjust), Get = w => w.BishopMgAdjust, Set = (w, v) => w.BishopMgAdjust = v, Min = -30, Max = 30 },
        new() { Name = nameof(EvalWeights.BishopEgAdjust), Get = w => w.BishopEgAdjust, Set = (w, v) => w.BishopEgAdjust = v, Min = -30, Max = 30 },

        new() { Name = nameof(EvalWeights.RookMgAdjust),   Get = w => w.RookMgAdjust,   Set = (w, v) => w.RookMgAdjust = v,   Min = -40, Max = 40 },
        new() { Name = nameof(EvalWeights.RookEgAdjust),   Get = w => w.RookEgAdjust,   Set = (w, v) => w.RookEgAdjust = v,   Min = -40, Max = 40 },

        new() { Name = nameof(EvalWeights.QueenMgAdjust),  Get = w => w.QueenMgAdjust,  Set = (w, v) => w.QueenMgAdjust = v,  Min = -60, Max = 60 },
        new() { Name = nameof(EvalWeights.QueenEgAdjust),  Get = w => w.QueenEgAdjust,  Set = (w, v) => w.QueenEgAdjust = v,  Min = -60, Max = 60 },
    };
    }
}