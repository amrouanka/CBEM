using System.Globalization;
using System.Collections.Concurrent;

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
        Console.WriteLine($"Tuning {parameters.Count} parameters.");

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
        object lockObj = new();

        Parallel.ForEach(
            Partitioner.Create(0, samples.Count),
            () => 0.0,
            (range, _, localSum) =>
            {
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    Sample sample = samples[i];
                    int score = Evaluation.EvaluateWhitePerspective(sample.Features, weights);
                    double p = 1.0 / (1.0 + Math.Exp(-k * score));
                    double error = sample.Result - p;
                    localSum += error * error;
                }
                return localSum;
            },
            localSum =>
            {
                lock (lockObj) { sum += localSum; }
            }
        );

        return sum / samples.Count;
    }

    private static List<IntParameter> BuildParameterList()
    {
        List<IntParameter> parameters = new List<IntParameter>
        {
            // Positional
            new() { Name = nameof(EvalWeights.BishopPairMg), Get = w => w.BishopPairMg, Set = (w, v) => w.BishopPairMg = v, Min = 0, Max = 80 },
            new() { Name = nameof(EvalWeights.BishopPairEg), Get = w => w.BishopPairEg, Set = (w, v) => w.BishopPairEg = v, Min = 0, Max = 100 },
            new() { Name = nameof(EvalWeights.KnightMobMg), Get = w => w.KnightMobMg, Set = (w, v) => w.KnightMobMg = v, Min = 0, Max = 15 },
            new() { Name = nameof(EvalWeights.KnightMobEg), Get = w => w.KnightMobEg, Set = (w, v) => w.KnightMobEg = v, Min = 0, Max = 15 },
            new() { Name = nameof(EvalWeights.BishopMobMg), Get = w => w.BishopMobMg, Set = (w, v) => w.BishopMobMg = v, Min = 0, Max = 15 },
            new() { Name = nameof(EvalWeights.BishopMobEg), Get = w => w.BishopMobEg, Set = (w, v) => w.BishopMobEg = v, Min = 0, Max = 15 },
            new() { Name = nameof(EvalWeights.RookSemiOpenMg), Get = w => w.RookSemiOpenMg, Set = (w, v) => w.RookSemiOpenMg = v, Min = 0, Max = 50 },
            new() { Name = nameof(EvalWeights.RookSemiOpenEg), Get = w => w.RookSemiOpenEg, Set = (w, v) => w.RookSemiOpenEg = v, Min = 0, Max = 40 },
            new() { Name = nameof(EvalWeights.RookOpenMg), Get = w => w.RookOpenMg, Set = (w, v) => w.RookOpenMg = v, Min = 0, Max = 90 },
            new() { Name = nameof(EvalWeights.RookOpenEg), Get = w => w.RookOpenEg, Set = (w, v) => w.RookOpenEg = v, Min = 0, Max = 60 },
            new() { Name = nameof(EvalWeights.IsolatedMg), Get = w => w.IsolatedMg, Set = (w, v) => w.IsolatedMg = v, Min = -40, Max = 0 },
            new() { Name = nameof(EvalWeights.IsolatedEg), Get = w => w.IsolatedEg, Set = (w, v) => w.IsolatedEg = v, Min = -40, Max = 0 },
            new() { Name = nameof(EvalWeights.KingOwnOpenMg), Get = w => w.KingOwnOpenMg, Set = (w, v) => w.KingOwnOpenMg = v, Min = 0, Max = 120 },
            new() { Name = nameof(EvalWeights.KingOwnSemiOpenMg), Get = w => w.KingOwnSemiOpenMg, Set = (w, v) => w.KingOwnSemiOpenMg = v, Min = 0, Max = 60 },
            new() { Name = nameof(EvalWeights.KingAdjacentOpenMg), Get = w => w.KingAdjacentOpenMg, Set = (w, v) => w.KingAdjacentOpenMg = v, Min = 0, Max = 80 },
            new() { Name = nameof(EvalWeights.KingAdjacentSemiOpenMg), Get = w => w.KingAdjacentSemiOpenMg, Set = (w, v) => w.KingAdjacentSemiOpenMg = v, Min = 0, Max = 50 },
            new() { Name = nameof(EvalWeights.KnightOutpostMg), Get = w => w.KnightOutpostMg, Set = (w, v) => w.KnightOutpostMg = v, Min = 0, Max = 80 }
        };

        // Passed Pawns
        for (int rank = 1; rank <= 6; rank++)
        {
            int r = rank;
            parameters.Add(new() { Name = $"PassedMg[{r}]", Get = w => w.PassedMg[r], Set = (w, v) => w.PassedMg[r] = v, Min = 0, Max = 180 });
            parameters.Add(new() { Name = $"PassedEg[{r}]", Get = w => w.PassedEg[r], Set = (w, v) => w.PassedEg[r] = v, Min = 0, Max = 250 });
        }

        // PST Adjustments per piece per square
        string[] pieceNames = ["Pawn", "Knight", "Bishop", "Rook", "Queen", "King"];

        for (int piece = 0; piece < 6; piece++)
        {
            int sqStart = (piece == 0) ? 8 : 0;
            int sqEnd = (piece == 0) ? 56 : 64;

            for (int sq = sqStart; sq < sqEnd; sq++)
            {
                int p = piece, s = sq;
                parameters.Add(new()
                {
                    Name = $"{pieceNames[p]}MgPst[{s}]",
                    Get = w => w.PstMgAdjust[p, s],
                    Set = (w, v) => w.PstMgAdjust[p, s] = v,
                    Min = -30,
                    Max = 30
                });
                parameters.Add(new()
                {
                    Name = $"{pieceNames[p]}EgPst[{s}]",
                    Get = w => w.PstEgAdjust[p, s],
                    Set = (w, v) => w.PstEgAdjust[p, s] = v,
                    Min = -30,
                    Max = 30
                });
            }
        }

        return parameters;
    }
}

/*
    Phase 1: Positional Bonuses & Penalties Only:


    private static List<IntParameter> BuildParameterList()
    {
        return new List<IntParameter>
        {
            // Bishop Pair
            new() { Name = nameof(EvalWeights.BishopPairMg), Get = w => w.BishopPairMg, Set = (w, v) => w.BishopPairMg = v, Min = 0, Max = 80 },
            new() { Name = nameof(EvalWeights.BishopPairEg), Get = w => w.BishopPairEg, Set = (w, v) => w.BishopPairEg = v, Min = 0, Max = 100 },

            // Mobility
            new() { Name = nameof(EvalWeights.KnightMobMg), Get = w => w.KnightMobMg, Set = (w, v) => w.KnightMobMg = v, Min = 0, Max = 15 },
            new() { Name = nameof(EvalWeights.KnightMobEg), Get = w => w.KnightMobEg, Set = (w, v) => w.KnightMobEg = v, Min = 0, Max = 15 },
            new() { Name = nameof(EvalWeights.BishopMobMg), Get = w => w.BishopMobMg, Set = (w, v) => w.BishopMobMg = v, Min = 0, Max = 15 },
            new() { Name = nameof(EvalWeights.BishopMobEg), Get = w => w.BishopMobEg, Set = (w, v) => w.BishopMobEg = v, Min = 0, Max = 15 },

            // Rook Files
            new() { Name = nameof(EvalWeights.RookSemiOpenMg), Get = w => w.RookSemiOpenMg, Set = (w, v) => w.RookSemiOpenMg = v, Min = 0, Max = 50 },
            new() { Name = nameof(EvalWeights.RookSemiOpenEg), Get = w => w.RookSemiOpenEg, Set = (w, v) => w.RookSemiOpenEg = v, Min = 0, Max = 40 },
            new() { Name = nameof(EvalWeights.RookOpenMg),     Get = w => w.RookOpenMg,     Set = (w, v) => w.RookOpenMg = v,     Min = 0, Max = 90 },
            new() { Name = nameof(EvalWeights.RookOpenEg),     Get = w => w.RookOpenEg,     Set = (w, v) => w.RookOpenEg = v,     Min = 0, Max = 60 },

            // Pawn Structure
            new() { Name = nameof(EvalWeights.IsolatedMg), Get = w => w.IsolatedMg, Set = (w, v) => w.IsolatedMg = v, Min = -40, Max = 0 },
            new() { Name = nameof(EvalWeights.IsolatedEg), Get = w => w.IsolatedEg, Set = (w, v) => w.IsolatedEg = v, Min = -40, Max = 0 },

            // King Safety (Penalties)
            new() { Name = nameof(EvalWeights.KingOwnOpenMg),          Get = w => w.KingOwnOpenMg,          Set = (w, v) => w.KingOwnOpenMg = v,          Min = 0, Max = 120 },
            new() { Name = nameof(EvalWeights.KingOwnSemiOpenMg),      Get = w => w.KingOwnSemiOpenMg,      Set = (w, v) => w.KingOwnSemiOpenMg = v,      Min = 0, Max = 60 },
            new() { Name = nameof(EvalWeights.KingAdjacentOpenMg),     Get = w => w.KingAdjacentOpenMg,     Set = (w, v) => w.KingAdjacentOpenMg = v,     Min = 0, Max = 80 },
            new() { Name = nameof(EvalWeights.KingAdjacentSemiOpenMg), Get = w => w.KingAdjacentSemiOpenMg, Set = (w, v) => w.KingAdjacentSemiOpenMg = v, Min = 0, Max = 50 },

            // Minor Pieces
            new() { Name = nameof(EvalWeights.KnightOutpostMg), Get = w => w.KnightOutpostMg, Set = (w, v) => w.KnightOutpostMg = v, Min = 0, Max = 80 },
        };
    }


    Phase 2: Passed Pawns Only:

    private static List<IntParameter> BuildParameterList()
    {
        List<IntParameter> parameters = new();

        // Ranks 1 to 6 (where 1 is closest to promotion in your inverted indexing)
        for (int rank = 1; rank <= 6; rank++)
        {
            int r = rank; // Capture local variable for lambda
            parameters.Add(new() { Name = $"PassedMg[Rank{r}]", Get = w => w.PassedMg[r], Set = (w, v) => w.PassedMg[r] = v, Min = 0, Max = 180 });
            parameters.Add(new() { Name = $"PassedEg[Rank{r}]", Get = w => w.PassedEg[r], Set = (w, v) => w.PassedEg[r] = v, Min = 0, Max = 250 });
        }

        return parameters;
    }


    Phase 3: Positional + Passed Pawns Together:

    private static List<IntParameter> BuildParameterList()
    {
        List<IntParameter> parameters = new List<IntParameter>
        {
            // Positional
            new() { Name = nameof(EvalWeights.BishopPairMg), Get = w => w.BishopPairMg, Set = (w, v) => w.BishopPairMg = v, Min = 0, Max = 80 },
            new() { Name = nameof(EvalWeights.BishopPairEg), Get = w => w.BishopPairEg, Set = (w, v) => w.BishopPairEg = v, Min = 0, Max = 100 },
            new() { Name = nameof(EvalWeights.KnightMobMg), Get = w => w.KnightMobMg, Set = (w, v) => w.KnightMobMg = v, Min = 0, Max = 15 },
            new() { Name = nameof(EvalWeights.KnightMobEg), Get = w => w.KnightMobEg, Set = (w, v) => w.KnightMobEg = v, Min = 0, Max = 15 },
            new() { Name = nameof(EvalWeights.BishopMobMg), Get = w => w.BishopMobMg, Set = (w, v) => w.BishopMobMg = v, Min = 0, Max = 15 },
            new() { Name = nameof(EvalWeights.BishopMobEg), Get = w => w.BishopMobEg, Set = (w, v) => w.BishopMobEg = v, Min = 0, Max = 15 },
            new() { Name = nameof(EvalWeights.RookSemiOpenMg), Get = w => w.RookSemiOpenMg, Set = (w, v) => w.RookSemiOpenMg = v, Min = 0, Max = 50 },
            new() { Name = nameof(EvalWeights.RookSemiOpenEg), Get = w => w.RookSemiOpenEg, Set = (w, v) => w.RookSemiOpenEg = v, Min = 0, Max = 40 },
            new() { Name = nameof(EvalWeights.RookOpenMg), Get = w => w.RookOpenMg, Set = (w, v) => w.RookOpenMg = v, Min = 0, Max = 90 },
            new() { Name = nameof(EvalWeights.RookOpenEg), Get = w => w.RookOpenEg, Set = (w, v) => w.RookOpenEg = v, Min = 0, Max = 60 },
            new() { Name = nameof(EvalWeights.IsolatedMg), Get = w => w.IsolatedMg, Set = (w, v) => w.IsolatedMg = v, Min = -40, Max = 0 },
            new() { Name = nameof(EvalWeights.IsolatedEg), Get = w => w.IsolatedEg, Set = (w, v) => w.IsolatedEg = v, Min = -40, Max = 0 },
            new() { Name = nameof(EvalWeights.KingOwnOpenMg), Get = w => w.KingOwnOpenMg, Set = (w, v) => w.KingOwnOpenMg = v, Min = 0, Max = 120 },
            new() { Name = nameof(EvalWeights.KingOwnSemiOpenMg), Get = w => w.KingOwnSemiOpenMg, Set = (w, v) => w.KingOwnSemiOpenMg = v, Min = 0, Max = 60 },
            new() { Name = nameof(EvalWeights.KingAdjacentOpenMg), Get = w => w.KingAdjacentOpenMg, Set = (w, v) => w.KingAdjacentOpenMg = v, Min = 0, Max = 80 },
            new() { Name = nameof(EvalWeights.KingAdjacentSemiOpenMg), Get = w => w.KingAdjacentSemiOpenMg, Set = (w, v) => w.KingAdjacentSemiOpenMg = v, Min = 0, Max = 50 },
            new() { Name = nameof(EvalWeights.KnightOutpostMg), Get = w => w.KnightOutpostMg, Set = (w, v) => w.KnightOutpostMg = v, Min = 0, Max = 80 }
        };

        // Passed Pawns
        for (int rank = 1; rank <= 6; rank++)
        {
            int r = rank;
            parameters.Add(new() { Name = $"PassedMg[{r}]", Get = w => w.PassedMg[r], Set = (w, v) => w.PassedMg[r] = v, Min = 0, Max = 180 });
            parameters.Add(new() { Name = $"PassedEg[{r}]", Get = w => w.PassedEg[r], Set = (w, v) => w.PassedEg[r] = v, Min = 0, Max = 250 });
        }

        // PST Adjustments per piece per square
        string[] pieceNames = ["Pawn", "Knight", "Bishop", "Rook", "Queen", "King"];

        for (int piece = 0; piece < 6; piece++)
        {
            int sqStart = (piece == 0) ? 8 : 0;
            int sqEnd = (piece == 0) ? 56 : 64;

            for (int sq = sqStart; sq < sqEnd; sq++)
            {
                int p = piece, s = sq;
                parameters.Add(new()
                {
                    Name = $"{pieceNames[p]}MgPst[{s}]",
                    Get = w => w.PstMgAdjust[p, s],
                    Set = (w, v) => w.PstMgAdjust[p, s] = v,
                    Min = -30,
                    Max = 30
                });
                parameters.Add(new()
                {
                    Name = $"{pieceNames[p]}EgPst[{s}]",
                    Get = w => w.PstEgAdjust[p, s],
                    Set = (w, v) => w.PstEgAdjust[p, s] = v,
                    Min = -30,
                    Max = 30
                });
            }
        }

        return parameters;
    }
*/