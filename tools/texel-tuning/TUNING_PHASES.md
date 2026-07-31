# Texel Tuning Phases

Reference for all tuning phase configurations used by the CBEM Texel tuner.

## Quick Reference

| Phase | What | Params | Speed | Guard | Eval Mode |
| :------ | :----- | :------- | :------ | :------ | :---------- |
| 1 | Positional scalars only | 18 | ⚡ Fast | None | FixedMg/FixedEg OK |
| 2 | Passed pawns only | 12 | ⚡ Fast | IsValidPassedShape | FixedMg/FixedEg OK |
| 3 | King safety + feature + king MG PST (ranks 1-2) | 21 | ⚡ Fast | None | FixedMg/FixedEg OK |
| 4 | Positional + passed pawns | 30 | ⚡ Fast | IsValidPassedShape | FixedMg/FixedEg OK |
| 5 | PST adjustments only | ~720 | 🐌 Overnight | None | PST loop required |
| 6 | Material only | 10 | ⚡ Fast | None | FixedMg/FixedEg OK |
| 7 | Everything | ~770 | 🐌 Overnight | IsValidPassedShape | PST loop required |

## Important Notes

### FixedMg/FixedEg Mode (Fast)

For phases that do NOT tune PST values, precompute the PST score once during
`ExtractFeatures()` into `f.FixedMg` / `f.FixedEg` and start
`EvaluateWhitePerspective()` from those values. This gives ~20-50x speedup.

### PST Loop Mode (Required for Phase 5 and 7)

For phases that DO tune PST values, `EvaluateWhitePerspective()` must use:

```csharp
int mg = 0;
int eg = 0;
for (int piece = 0; piece < 6; piece++)
    for (int sq = 0; sq < 64; sq++)
    {
        int bal = f.PieceSqBalance[piece, sq];
        if (bal != 0)
        {
            mg += bal * (MgTable[piece, sq] + w.PstMgAdjust[piece, sq]);
            eg += bal * (EgTable[piece, sq] + w.PstEgAdjust[piece, sq]);
        }
    }
```

### IsValidPassedShape Guard

Any phase that tunes passed pawns must guard the tuning loop:

```csharp
double plusLoss  = IsValidPassedShape(plus)  ? Loss(samples, plus,  k) : double.MaxValue;
double minusLoss = IsValidPassedShape(minus) ? Loss(samples, minus, k) : double.MaxValue;
```

### King MG PST Restriction

- King MG PST is ALWAYS restricted to ranks 1-2 only (engine squares 48-63)
- Top 6 ranks are frozen to prevent sparse-data overfitting
- King EG PST is NEVER tuned (already optimal)

### After Every Phase

1. Copy printed values into `Evaluation.cs`
2. Sync `EvalWeights.cs` field defaults to match
3. Reset `PstMgAdjust` / `PstEgAdjust` to `new int[6, 64]` in `GetCurrentWeights()`
4. Test vs last known good version (2000+ games minimum)

## How to Run

```powershell
cd C:\Users\Rania\OneDrive\Desktop\CBEM\tools\texel-tuning
dotnet run -c Release -- texel "C:\Users\Rania\OneDrive\Desktop\CBEM\data\zurichess.txt"
```

## Usage

In `TexelTuner.cs`, change one line in `Run()`:

```csharp
List<IntParameter> parameters = BuildParameterListPhase3(); // change number
```

## Phase Code

```csharp
/*
=== TEXEL TUNING PHASES ===

Phase 1: Positional Bonuses & Penalties Only
    - Safest first step
    - Tunes scalar eval terms only
    - No PST changes, no passed pawns
    - Fast: 18 parameters

Phase 2: Passed Pawns Only
    - Tunes passed pawn arrays with monotonicity constraint
    - Uses IsValidPassedShape() guard
    - Fast: 12 parameters

Phase 3: King Safety + Queenless Feature + King MG PST (Ranks 1-2 only)
    - Fixes king MG noise without touching dangerous top ranks
    - Very focused and safe
    - Fast: 21 parameters

Phase 4: Positional + Passed Pawns Together
    - Combined scalar + passed pawn rebalance
    - Uses IsValidPassedShape() guard
    - Medium: 30 parameters

Phase 5: Full PST Adjustments Only
    - Tunes all piece-square adjustments
    - Pawn squares 8..55, other pieces 0..63
    - King MG only ranks 1-2 (indices 48..63)
    - Slow: ~720 parameters

Phase 6: Material Adjustments Only
    - Tunes piece value deltas
    - Usually small changes
    - Fast: 10 parameters

Phase 7: Everything Together
    - All scalar terms + passed pawns + PST adjustments + material
    - Uses IsValidPassedShape() guard
    - King MG PST restricted to ranks 1-2
    - Very slow: ~770 parameters
*/


// ============================================================
// Phase 1: Positional Bonuses & Penalties Only
// ============================================================
private static List<IntParameter> BuildParameterListPhase1()
{
    return new List<IntParameter>
    {
        new() { Name = nameof(EvalWeights.BishopPairMg), Get = w => w.BishopPairMg, Set = (w, v) => w.BishopPairMg = v, Min = 0, Max = 80 },
        new() { Name = nameof(EvalWeights.BishopPairEg), Get = w => w.BishopPairEg, Set = (w, v) => w.BishopPairEg = v, Min = 0, Max = 100 },

        new() { Name = nameof(EvalWeights.KnightMobMg), Get = w => w.KnightMobMg, Set = (w, v) => w.KnightMobMg = v, Min = 0, Max = 15 },
        new() { Name = nameof(EvalWeights.KnightMobEg), Get = w => w.KnightMobEg, Set = (w, v) => w.KnightMobEg = v, Min = 0, Max = 15 },
        new() { Name = nameof(EvalWeights.BishopMobMg), Get = w => w.BishopMobMg, Set = (w, v) => w.BishopMobMg = v, Min = 0, Max = 15 },
        new() { Name = nameof(EvalWeights.BishopMobEg), Get = w => w.BishopMobEg, Set = (w, v) => w.BishopMobEg = v, Min = 0, Max = 15 },

        new() { Name = nameof(EvalWeights.RookSemiOpenMg), Get = w => w.RookSemiOpenMg, Set = (w, v) => w.RookSemiOpenMg = v, Min = 0, Max = 50 },
        new() { Name = nameof(EvalWeights.RookSemiOpenEg), Get = w => w.RookSemiOpenEg, Set = (w, v) => w.RookSemiOpenEg = v, Min = 0, Max = 40 },
        new() { Name = nameof(EvalWeights.RookOpenMg),     Get = w => w.RookOpenMg,     Set = (w, v) => w.RookOpenMg = v,     Min = 0, Max = 90 },
        new() { Name = nameof(EvalWeights.RookOpenEg),     Get = w => w.RookOpenEg,     Set = (w, v) => w.RookOpenEg = v,     Min = 0, Max = 60 },

        new() { Name = nameof(EvalWeights.IsolatedMg), Get = w => w.IsolatedMg, Set = (w, v) => w.IsolatedMg = v, Min = -40, Max = 0 },
        new() { Name = nameof(EvalWeights.IsolatedEg), Get = w => w.IsolatedEg, Set = (w, v) => w.IsolatedEg = v, Min = -40, Max = 0 },

        new() { Name = nameof(EvalWeights.KingOwnOpenMg),          Get = w => w.KingOwnOpenMg,          Set = (w, v) => w.KingOwnOpenMg = v,          Min = 0, Max = 120 },
        new() { Name = nameof(EvalWeights.KingOwnSemiOpenMg),      Get = w => w.KingOwnSemiOpenMg,      Set = (w, v) => w.KingOwnSemiOpenMg = v,      Min = 0, Max = 60 },
        new() { Name = nameof(EvalWeights.KingAdjacentOpenMg),     Get = w => w.KingAdjacentOpenMg,     Set = (w, v) => w.KingAdjacentOpenMg = v,     Min = 0, Max = 80 },
        new() { Name = nameof(EvalWeights.KingAdjacentSemiOpenMg), Get = w => w.KingAdjacentSemiOpenMg, Set = (w, v) => w.KingAdjacentSemiOpenMg = v, Min = 0, Max = 50 },

        new() { Name = nameof(EvalWeights.QueenlessKingCenterMg),  Get = w => w.QueenlessKingCenterMg,  Set = (w, v) => w.QueenlessKingCenterMg = v,  Min = 0, Max = 48 },

        new() { Name = nameof(EvalWeights.KnightOutpostMg), Get = w => w.KnightOutpostMg, Set = (w, v) => w.KnightOutpostMg = v, Min = 0, Max = 80 },
    };
}


// ============================================================
// Phase 2: Passed Pawns Only (with monotonicity constraint)
// ============================================================
private static List<IntParameter> BuildParameterListPhase2()
{
    return new List<IntParameter>
    {
        new() { Name = "PassedMg[1]", Get = w => w.PassedMg[1], Set = (w, v) => w.PassedMg[1] = v, Min = 0, Max = 60 },
        new() { Name = "PassedMg[2]", Get = w => w.PassedMg[2], Set = (w, v) => w.PassedMg[2] = v, Min = 0, Max = 90 },
        new() { Name = "PassedMg[3]", Get = w => w.PassedMg[3], Set = (w, v) => w.PassedMg[3] = v, Min = 0, Max = 60 },
        new() { Name = "PassedMg[4]", Get = w => w.PassedMg[4], Set = (w, v) => w.PassedMg[4] = v, Min = 0, Max = 40 },
        new() { Name = "PassedMg[5]", Get = w => w.PassedMg[5], Set = (w, v) => w.PassedMg[5] = v, Min = 0, Max = 30 },
        new() { Name = "PassedMg[6]", Get = w => w.PassedMg[6], Set = (w, v) => w.PassedMg[6] = v, Min = 0, Max = 25 },

        new() { Name = "PassedEg[1]", Get = w => w.PassedEg[1], Set = (w, v) => w.PassedEg[1] = v, Min = 0, Max = 130 },
        new() { Name = "PassedEg[2]", Get = w => w.PassedEg[2], Set = (w, v) => w.PassedEg[2] = v, Min = 0, Max = 130 },
        new() { Name = "PassedEg[3]", Get = w => w.PassedEg[3], Set = (w, v) => w.PassedEg[3] = v, Min = 0, Max = 90 },
        new() { Name = "PassedEg[4]", Get = w => w.PassedEg[4], Set = (w, v) => w.PassedEg[4] = v, Min = 0, Max = 60 },
        new() { Name = "PassedEg[5]", Get = w => w.PassedEg[5], Set = (w, v) => w.PassedEg[5] = v, Min = 0, Max = 40 },
        new() { Name = "PassedEg[6]", Get = w => w.PassedEg[6], Set = (w, v) => w.PassedEg[6] = v, Min = 0, Max = 25 },
    };
}


// ============================================================
// Phase 3: King Safety + Queenless Feature + King MG PST (Ranks 1-2)
// ============================================================
private static List<IntParameter> BuildParameterListPhase3()
{
    List<IntParameter> parameters = new List<IntParameter>
    {
        new() { Name = nameof(EvalWeights.KingOwnOpenMg),          Get = w => w.KingOwnOpenMg,          Set = (w, v) => w.KingOwnOpenMg = v,          Min = 0, Max = 120 },
        new() { Name = nameof(EvalWeights.KingOwnSemiOpenMg),      Get = w => w.KingOwnSemiOpenMg,      Set = (w, v) => w.KingOwnSemiOpenMg = v,      Min = 0, Max = 60 },
        new() { Name = nameof(EvalWeights.KingAdjacentOpenMg),     Get = w => w.KingAdjacentOpenMg,     Set = (w, v) => w.KingAdjacentOpenMg = v,     Min = 0, Max = 80 },
        new() { Name = nameof(EvalWeights.KingAdjacentSemiOpenMg), Get = w => w.KingAdjacentSemiOpenMg, Set = (w, v) => w.KingAdjacentSemiOpenMg = v, Min = 0, Max = 50 },

        new() { Name = nameof(EvalWeights.QueenlessKingCenterMg),  Get = w => w.QueenlessKingCenterMg,  Set = (w, v) => w.QueenlessKingCenterMg = v,  Min = 0, Max = 48 },
    };

    const int KingPiece = 5;
    for (int sq = 48; sq < 64; sq++)
    {
        int s = sq;
        parameters.Add(new IntParameter
        {
            Name = $"KingMgPst[{s}]",
            Get = w => w.PstMgAdjust[KingPiece, s],
            Set = (w, v) => w.PstMgAdjust[KingPiece, s] = v,
            Min = -40,
            Max = 40
        });
    }

    return parameters;
}


// ============================================================
// Phase 4: Positional + Passed Pawns Together
// ============================================================
private static List<IntParameter> BuildParameterListPhase4()
{
    List<IntParameter> parameters = new List<IntParameter>
    {
        new() { Name = nameof(EvalWeights.BishopPairMg), Get = w => w.BishopPairMg, Set = (w, v) => w.BishopPairMg = v, Min = 0, Max = 80 },
        new() { Name = nameof(EvalWeights.BishopPairEg), Get = w => w.BishopPairEg, Set = (w, v) => w.BishopPairEg = v, Min = 0, Max = 100 },

        new() { Name = nameof(EvalWeights.KnightMobMg), Get = w => w.KnightMobMg, Set = (w, v) => w.KnightMobMg = v, Min = 0, Max = 15 },
        new() { Name = nameof(EvalWeights.KnightMobEg), Get = w => w.KnightMobEg, Set = (w, v) => w.KnightMobEg = v, Min = 0, Max = 15 },
        new() { Name = nameof(EvalWeights.BishopMobMg), Get = w => w.BishopMobMg, Set = (w, v) => w.BishopMobMg = v, Min = 0, Max = 15 },
        new() { Name = nameof(EvalWeights.BishopMobEg), Get = w => w.BishopMobEg, Set = (w, v) => w.BishopMobEg = v, Min = 0, Max = 15 },

        new() { Name = nameof(EvalWeights.RookSemiOpenMg), Get = w => w.RookSemiOpenMg, Set = (w, v) => w.RookSemiOpenMg = v, Min = 0, Max = 50 },
        new() { Name = nameof(EvalWeights.RookSemiOpenEg), Get = w => w.RookSemiOpenEg, Set = (w, v) => w.RookSemiOpenEg = v, Min = 0, Max = 40 },
        new() { Name = nameof(EvalWeights.RookOpenMg),     Get = w => w.RookOpenMg,     Set = (w, v) => w.RookOpenMg = v,     Min = 0, Max = 90 },
        new() { Name = nameof(EvalWeights.RookOpenEg),     Get = w => w.RookOpenEg,     Set = (w, v) => w.RookOpenEg = v,     Min = 0, Max = 60 },

        new() { Name = nameof(EvalWeights.IsolatedMg), Get = w => w.IsolatedMg, Set = (w, v) => w.IsolatedMg = v, Min = -40, Max = 0 },
        new() { Name = nameof(EvalWeights.IsolatedEg), Get = w => w.IsolatedEg, Set = (w, v) => w.IsolatedEg = v, Min = -40, Max = 0 },

        new() { Name = nameof(EvalWeights.KingOwnOpenMg),          Get = w => w.KingOwnOpenMg,          Set = (w, v) => w.KingOwnOpenMg = v,          Min = 0, Max = 120 },
        new() { Name = nameof(EvalWeights.KingOwnSemiOpenMg),      Get = w => w.KingOwnSemiOpenMg,      Set = (w, v) => w.KingOwnSemiOpenMg = v,      Min = 0, Max = 60 },
        new() { Name = nameof(EvalWeights.KingAdjacentOpenMg),     Get = w => w.KingAdjacentOpenMg,     Set = (w, v) => w.KingAdjacentOpenMg = v,     Min = 0, Max = 80 },
        new() { Name = nameof(EvalWeights.KingAdjacentSemiOpenMg), Get = w => w.KingAdjacentSemiOpenMg, Set = (w, v) => w.KingAdjacentSemiOpenMg = v, Min = 0, Max = 50 },

        new() { Name = nameof(EvalWeights.QueenlessKingCenterMg),  Get = w => w.QueenlessKingCenterMg,  Set = (w, v) => w.QueenlessKingCenterMg = v,  Min = 0, Max = 48 },

        new() { Name = nameof(EvalWeights.KnightOutpostMg), Get = w => w.KnightOutpostMg, Set = (w, v) => w.KnightOutpostMg = v, Min = 0, Max = 80 },
    };

    for (int rank = 1; rank <= 6; rank++)
    {
        int r = rank;
        parameters.Add(new() { Name = $"PassedMg[{r}]", Get = w => w.PassedMg[r], Set = (w, v) => w.PassedMg[r] = v, Min = 0, Max = 90 });
        parameters.Add(new() { Name = $"PassedEg[{r}]", Get = w => w.PassedEg[r], Set = (w, v) => w.PassedEg[r] = v, Min = 0, Max = 130 });
    }

    return parameters;
}


// ============================================================
// Phase 5: Full PST Adjustments Only (King MG restricted to ranks 1-2)
// ============================================================
private static List<IntParameter> BuildParameterListPhase5()
{
    List<IntParameter> parameters = new();

    string[] pieceNames = ["Pawn", "Knight", "Bishop", "Rook", "Queen", "King"];

    for (int piece = 0; piece < 6; piece++)
    {
        int sqStart, sqEnd;

        if (piece == 0)
        {
            sqStart = 8;
            sqEnd = 56;
        }
        else if (piece == 5)
        {
            sqStart = 48;
            sqEnd = 64;
        }
        else
        {
            sqStart = 0;
            sqEnd = 64;
        }

        for (int sq = sqStart; sq < sqEnd; sq++)
        {
            int p = piece, s = sq;

            parameters.Add(new IntParameter
            {
                Name = $"{pieceNames[p]}MgPst[{s}]",
                Get = w => w.PstMgAdjust[p, s],
                Set = (w, v) => w.PstMgAdjust[p, s] = v,
                Min = -30,
                Max = 30
            });

            if (piece != 5)
            {
                parameters.Add(new IntParameter
                {
                    Name = $"{pieceNames[p]}EgPst[{s}]",
                    Get = w => w.PstEgAdjust[p, s],
                    Set = (w, v) => w.PstEgAdjust[p, s] = v,
                    Min = -30,
                    Max = 30
                });
            }
        }
    }

    return parameters;
}


// ============================================================
// Phase 6: Material Adjustments Only
// ============================================================
private static List<IntParameter> BuildParameterListPhase6()
{
    return new List<IntParameter>
    {
        new() { Name = nameof(EvalWeights.PawnMgAdjust),   Get = w => w.PawnMgAdjust,   Set = (w, v) => w.PawnMgAdjust = v,   Min = -20, Max = 20 },
        new() { Name = nameof(EvalWeights.PawnEgAdjust),   Get = w => w.PawnEgAdjust,   Set = (w, v) => w.PawnEgAdjust = v,   Min = -20, Max = 20 },

        new() { Name = nameof(EvalWeights.KnightMgAdjust), Get = w => w.KnightMgAdjust, Set = (w, v) => w.KnightMgAdjust = v, Min = -40, Max = 40 },
        new() { Name = nameof(EvalWeights.KnightEgAdjust), Get = w => w.KnightEgAdjust, Set = (w, v) => w.KnightEgAdjust = v, Min = -40, Max = 40 },

        new() { Name = nameof(EvalWeights.BishopMgAdjust), Get = w => w.BishopMgAdjust, Set = (w, v) => w.BishopMgAdjust = v, Min = -40, Max = 40 },
        new() { Name = nameof(EvalWeights.BishopEgAdjust), Get = w => w.BishopEgAdjust, Set = (w, v) => w.BishopEgAdjust = v, Min = -40, Max = 40 },

        new() { Name = nameof(EvalWeights.RookMgAdjust),   Get = w => w.RookMgAdjust,   Set = (w, v) => w.RookMgAdjust = v,   Min = -60, Max = 60 },
        new() { Name = nameof(EvalWeights.RookEgAdjust),   Get = w => w.RookEgAdjust,   Set = (w, v) => w.RookEgAdjust = v,   Min = -60, Max = 60 },

        new() { Name = nameof(EvalWeights.QueenMgAdjust),  Get = w => w.QueenMgAdjust,  Set = (w, v) => w.QueenMgAdjust = v,  Min = -80, Max = 80 },
        new() { Name = nameof(EvalWeights.QueenEgAdjust),  Get = w => w.QueenEgAdjust,  Set = (w, v) => w.QueenEgAdjust = v,  Min = -80, Max = 80 },
    };
}


// ============================================================
// Phase 7: Everything Together (King MG restricted to ranks 1-2)
// ============================================================
private static List<IntParameter> BuildParameterListPhase7()
{
    List<IntParameter> parameters = new List<IntParameter>
    {
        // Material adjustments
        new() { Name = nameof(EvalWeights.PawnMgAdjust),   Get = w => w.PawnMgAdjust,   Set = (w, v) => w.PawnMgAdjust = v,   Min = -20, Max = 20 },
        new() { Name = nameof(EvalWeights.PawnEgAdjust),   Get = w => w.PawnEgAdjust,   Set = (w, v) => w.PawnEgAdjust = v,   Min = -20, Max = 20 },
        new() { Name = nameof(EvalWeights.KnightMgAdjust), Get = w => w.KnightMgAdjust, Set = (w, v) => w.KnightMgAdjust = v, Min = -40, Max = 40 },
        new() { Name = nameof(EvalWeights.KnightEgAdjust), Get = w => w.KnightEgAdjust, Set = (w, v) => w.KnightEgAdjust = v, Min = -40, Max = 40 },
        new() { Name = nameof(EvalWeights.BishopMgAdjust), Get = w => w.BishopMgAdjust, Set = (w, v) => w.BishopMgAdjust = v, Min = -40, Max = 40 },
        new() { Name = nameof(EvalWeights.BishopEgAdjust), Get = w => w.BishopEgAdjust, Set = (w, v) => w.BishopEgAdjust = v, Min = -40, Max = 40 },
        new() { Name = nameof(EvalWeights.RookMgAdjust),   Get = w => w.RookMgAdjust,   Set = (w, v) => w.RookMgAdjust = v,   Min = -60, Max = 60 },
        new() { Name = nameof(EvalWeights.RookEgAdjust),   Get = w => w.RookEgAdjust,   Set = (w, v) => w.RookEgAdjust = v,   Min = -60, Max = 60 },
        new() { Name = nameof(EvalWeights.QueenMgAdjust),  Get = w => w.QueenMgAdjust,  Set = (w, v) => w.QueenMgAdjust = v,  Min = -80, Max = 80 },
        new() { Name = nameof(EvalWeights.QueenEgAdjust),  Get = w => w.QueenEgAdjust,  Set = (w, v) => w.QueenEgAdjust = v,  Min = -80, Max = 80 },

        // Positional
        new() { Name = nameof(EvalWeights.BishopPairMg),           Get = w => w.BishopPairMg,           Set = (w, v) => w.BishopPairMg = v,           Min = 0,   Max = 80 },
        new() { Name = nameof(EvalWeights.BishopPairEg),           Get = w => w.BishopPairEg,           Set = (w, v) => w.BishopPairEg = v,           Min = 0,   Max = 100 },
        new() { Name = nameof(EvalWeights.KnightMobMg),            Get = w => w.KnightMobMg,            Set = (w, v) => w.KnightMobMg = v,            Min = 0,   Max = 15 },
        new() { Name = nameof(EvalWeights.KnightMobEg),            Get = w => w.KnightMobEg,            Set = (w, v) => w.KnightMobEg = v,            Min = 0,   Max = 15 },
        new() { Name = nameof(EvalWeights.BishopMobMg),            Get = w => w.BishopMobMg,            Set = (w, v) => w.BishopMobMg = v,            Min = 0,   Max = 15 },
        new() { Name = nameof(EvalWeights.BishopMobEg),            Get = w => w.BishopMobEg,            Set = (w, v) => w.BishopMobEg = v,            Min = 0,   Max = 15 },
        new() { Name = nameof(EvalWeights.RookSemiOpenMg),         Get = w => w.RookSemiOpenMg,         Set = (w, v) => w.RookSemiOpenMg = v,         Min = 0,   Max = 50 },
        new() { Name = nameof(EvalWeights.RookSemiOpenEg),         Get = w => w.RookSemiOpenEg,         Set = (w, v) => w.RookSemiOpenEg = v,         Min = 0,   Max = 40 },
        new() { Name = nameof(EvalWeights.RookOpenMg),             Get = w => w.RookOpenMg,             Set = (w, v) => w.RookOpenMg = v,             Min = 0,   Max = 90 },
        new() { Name = nameof(EvalWeights.RookOpenEg),             Get = w => w.RookOpenEg,             Set = (w, v) => w.RookOpenEg = v,             Min = 0,   Max = 60 },
        new() { Name = nameof(EvalWeights.IsolatedMg),             Get = w => w.IsolatedMg,             Set = (w, v) => w.IsolatedMg = v,             Min = -40, Max = 0 },
        new() { Name = nameof(EvalWeights.IsolatedEg),             Get = w => w.IsolatedEg,             Set = (w, v) => w.IsolatedEg = v,             Min = -40, Max = 0 },
        new() { Name = nameof(EvalWeights.KingOwnOpenMg),          Get = w => w.KingOwnOpenMg,          Set = (w, v) => w.KingOwnOpenMg = v,          Min = 0,   Max = 120 },
        new() { Name = nameof(EvalWeights.KingOwnSemiOpenMg),      Get = w => w.KingOwnSemiOpenMg,      Set = (w, v) => w.KingOwnSemiOpenMg = v,      Min = 0,   Max = 60 },
        new() { Name = nameof(EvalWeights.KingAdjacentOpenMg),     Get = w => w.KingAdjacentOpenMg,     Set = (w, v) => w.KingAdjacentOpenMg = v,     Min = 0,   Max = 80 },
        new() { Name = nameof(EvalWeights.KingAdjacentSemiOpenMg), Get = w => w.KingAdjacentSemiOpenMg, Set = (w, v) => w.KingAdjacentSemiOpenMg = v, Min = 0,   Max = 50 },
        new() { Name = nameof(EvalWeights.QueenlessKingCenterMg),  Get = w => w.QueenlessKingCenterMg,  Set = (w, v) => w.QueenlessKingCenterMg = v,  Min = 0,   Max = 48 },
        new() { Name = nameof(EvalWeights.KnightOutpostMg),        Get = w => w.KnightOutpostMg,        Set = (w, v) => w.KnightOutpostMg = v,        Min = 0,   Max = 80 },
    };

    // Passed Pawns
    for (int rank = 1; rank <= 6; rank++)
    {
        int r = rank;
        parameters.Add(new() { Name = $"PassedMg[{r}]", Get = w => w.PassedMg[r], Set = (w, v) => w.PassedMg[r] = v, Min = 0, Max = 90 });
        parameters.Add(new() { Name = $"PassedEg[{r}]", Get = w => w.PassedEg[r], Set = (w, v) => w.PassedEg[r] = v, Min = 0, Max = 130 });
    }

    // PST Adjustments (King MG restricted to ranks 1-2)
    string[] pieceNames = ["Pawn", "Knight", "Bishop", "Rook", "Queen", "King"];

    for (int piece = 0; piece < 6; piece++)
    {
        int sqStart, sqEnd;

        if (piece == 0)
        {
            sqStart = 8;
            sqEnd = 56;
        }
        else if (piece == 5)
        {
            sqStart = 48;
            sqEnd = 64;
        }
        else
        {
            sqStart = 0;
            sqEnd = 64;
        }

        for (int sq = sqStart; sq < sqEnd; sq++)
        {
            int p = piece, s = sq;

            parameters.Add(new IntParameter
            {
                Name = $"{pieceNames[p]}MgPst[{s}]",
                Get = w => w.PstMgAdjust[p, s],
                Set = (w, v) => w.PstMgAdjust[p, s] = v,
                Min = -30,
                Max = 30
            });

            if (piece != 5)
            {
                parameters.Add(new IntParameter
                {
                    Name = $"{pieceNames[p]}EgPst[{s}]",
                    Get = w => w.PstEgAdjust[p, s],
                    Set = (w, v) => w.PstEgAdjust[p, s] = v,
                    Min = -30,
                    Max = 30
                });
            }
        }
    }

    return parameters;
}

// ============================================================
// Phase 8: Full Non-PST Tune (Material + Positional + Passed Pawns)
// ============================================================
private static List<IntParameter> BuildParameterListPhase8()
{
    return new List<IntParameter>
    {
        // Material adjustments
        new() { Name = nameof(EvalWeights.PawnMgAdjust),   Get = w => w.PawnMgAdjust,   Set = (w, v) => w.PawnMgAdjust = v,   Min = -20,  Max = 20 },
        new() { Name = nameof(EvalWeights.PawnEgAdjust),   Get = w => w.PawnEgAdjust,   Set = (w, v) => w.PawnEgAdjust = v,   Min = -20,  Max = 20 },

        new() { Name = nameof(EvalWeights.KnightMgAdjust), Get = w => w.KnightMgAdjust, Set = (w, v) => w.KnightMgAdjust = v, Min = -40,  Max = 40 },
        new() { Name = nameof(EvalWeights.KnightEgAdjust), Get = w => w.KnightEgAdjust, Set = (w, v) => w.KnightEgAdjust = v, Min = -40,  Max = 40 },

        new() { Name = nameof(EvalWeights.BishopMgAdjust), Get = w => w.BishopMgAdjust, Set = (w, v) => w.BishopMgAdjust = v, Min = -40,  Max = 40 },
        new() { Name = nameof(EvalWeights.BishopEgAdjust), Get = w => w.BishopEgAdjust, Set = (w, v) => w.BishopEgAdjust = v, Min = -40,  Max = 40 },

        new() { Name = nameof(EvalWeights.RookMgAdjust),   Get = w => w.RookMgAdjust,   Set = (w, v) => w.RookMgAdjust = v,   Min = -60,  Max = 60 },
        new() { Name = nameof(EvalWeights.RookEgAdjust),   Get = w => w.RookEgAdjust,   Set = (w, v) => w.RookEgAdjust = v,   Min = -60,  Max = 60 },

        new() { Name = nameof(EvalWeights.QueenMgAdjust),  Get = w => w.QueenMgAdjust,  Set = (w, v) => w.QueenMgAdjust = v,  Min = -100, Max = 100 },
        new() { Name = nameof(EvalWeights.QueenEgAdjust),  Get = w => w.QueenEgAdjust,  Set = (w, v) => w.QueenEgAdjust = v,  Min = -100, Max = 100 },

        // Positional scalars
        new() { Name = nameof(EvalWeights.BishopPairMg), Get = w => w.BishopPairMg, Set = (w, v) => w.BishopPairMg = v, Min = 0, Max = 80 },
        new() { Name = nameof(EvalWeights.BishopPairEg), Get = w => w.BishopPairEg, Set = (w, v) => w.BishopPairEg = v, Min = 0, Max = 100 },

        new() { Name = nameof(EvalWeights.KnightMobMg), Get = w => w.KnightMobMg, Set = (w, v) => w.KnightMobMg = v, Min = 0, Max = 15 },
        new() { Name = nameof(EvalWeights.KnightMobEg), Get = w => w.KnightMobEg, Set = (w, v) => w.KnightMobEg = v, Min = 0, Max = 15 },
        new() { Name = nameof(EvalWeights.BishopMobMg), Get = w => w.BishopMobMg, Set = (w, v) => w.BishopMobMg = v, Min = 0, Max = 15 },
        new() { Name = nameof(EvalWeights.BishopMobEg), Get = w => w.BishopMobEg, Set = (w, v) => w.BishopMobEg = v, Min = 0, Max = 15 },

        new() { Name = nameof(EvalWeights.RookSemiOpenMg), Get = w => w.RookSemiOpenMg, Set = (w, v) => w.RookSemiOpenMg = v, Min = 0, Max = 50 },
        new() { Name = nameof(EvalWeights.RookSemiOpenEg), Get = w => w.RookSemiOpenEg, Set = (w, v) => w.RookSemiOpenEg = v, Min = 0, Max = 40 },
        new() { Name = nameof(EvalWeights.RookOpenMg),     Get = w => w.RookOpenMg,     Set = (w, v) => w.RookOpenMg = v,     Min = 0, Max = 90 },
        new() { Name = nameof(EvalWeights.RookOpenEg),     Get = w => w.RookOpenEg,     Set = (w, v) => w.RookOpenEg = v,     Min = 0, Max = 60 },

        new() { Name = nameof(EvalWeights.IsolatedMg), Get = w => w.IsolatedMg, Set = (w, v) => w.IsolatedMg = v, Min = -40, Max = 0 },
        new() { Name = nameof(EvalWeights.IsolatedEg), Get = w => w.IsolatedEg, Set = (w, v) => w.IsolatedEg = v, Min = -40, Max = 0 },

        new() { Name = nameof(EvalWeights.KingOwnOpenMg),          Get = w => w.KingOwnOpenMg,          Set = (w, v) => w.KingOwnOpenMg = v,          Min = 0, Max = 120 },
        new() { Name = nameof(EvalWeights.KingOwnSemiOpenMg),      Get = w => w.KingOwnSemiOpenMg,      Set = (w, v) => w.KingOwnSemiOpenMg = v,      Min = 0, Max = 60 },
        new() { Name = nameof(EvalWeights.KingAdjacentOpenMg),     Get = w => w.KingAdjacentOpenMg,     Set = (w, v) => w.KingAdjacentOpenMg = v,     Min = 0, Max = 80 },
        new() { Name = nameof(EvalWeights.KingAdjacentSemiOpenMg), Get = w => w.KingAdjacentSemiOpenMg, Set = (w, v) => w.KingAdjacentSemiOpenMg = v, Min = 0, Max = 50 },

        new() { Name = nameof(EvalWeights.QueenlessKingCenterMg), Get = w => w.QueenlessKingCenterMg, Set = (w, v) => w.QueenlessKingCenterMg = v, Min = 0, Max = 48 },

        new() { Name = nameof(EvalWeights.KnightOutpostMg), Get = w => w.KnightOutpostMg, Set = (w, v) => w.KnightOutpostMg = v, Min = 0, Max = 80 },

        // Passed pawns
        new() { Name = "PassedMg[1]", Get = w => w.PassedMg[1], Set = (w, v) => w.PassedMg[1] = v, Min = 0, Max = 60 },
        new() { Name = "PassedMg[2]", Get = w => w.PassedMg[2], Set = (w, v) => w.PassedMg[2] = v, Min = 0, Max = 90 },
        new() { Name = "PassedMg[3]", Get = w => w.PassedMg[3], Set = (w, v) => w.PassedMg[3] = v, Min = 0, Max = 60 },
        new() { Name = "PassedMg[4]", Get = w => w.PassedMg[4], Set = (w, v) => w.PassedMg[4] = v, Min = 0, Max = 40 },
        new() { Name = "PassedMg[5]", Get = w => w.PassedMg[5], Set = (w, v) => w.PassedMg[5] = v, Min = 0, Max = 30 },
        new() { Name = "PassedMg[6]", Get = w => w.PassedMg[6], Set = (w, v) => w.PassedMg[6] = v, Min = 0, Max = 25 },

        new() { Name = "PassedEg[1]", Get = w => w.PassedEg[1], Set = (w, v) => w.PassedEg[1] = v, Min = 0, Max = 130 },
        new() { Name = "PassedEg[2]", Get = w => w.PassedEg[2], Set = (w, v) => w.PassedEg[2] = v, Min = 0, Max = 130 },
        new() { Name = "PassedEg[3]", Get = w => w.PassedEg[3], Set = (w, v) => w.PassedEg[3] = v, Min = 0, Max = 90 },
        new() { Name = "PassedEg[4]", Get = w => w.PassedEg[4], Set = (w, v) => w.PassedEg[4] = v, Min = 0, Max = 60 },
        new() { Name = "PassedEg[5]", Get = w => w.PassedEg[5], Set = (w, v) => w.PassedEg[5] = v, Min = 0, Max = 40 },
        new() { Name = "PassedEg[6]", Get = w => w.PassedEg[6], Set = (w, v) => w.PassedEg[6] = v, Min = 0, Max = 25 },
    };
}
```
