# CBEM Chess Engine — Texel Tuning Context

## Project Overview

CBEM is a C# chess engine developed incrementally from a basic alpha-beta searcher
to a competitive engine with modern search heuristics and Texel-tuned evaluation.

## Repository Structure

```
CBEM/
├── .vscode/
├── active-dev/              # Current development workspace
├── data/                    # Training datasets
│   └── zurichess.txt        # 702,900 quiet labeled positions (result|fen format)
├── tools/
│   ├── search-tuning/       # Search parameter tuning utilities
│   └── texel-tuning/        # Global Texel tuner (shared across versions)
│       ├── Evaluation.cs    # Engine eval (mirrors active-dev but with tuner hooks)
│       ├── EvalFeatures.cs  # Feature extraction data class
│       ├── EvalWeights.cs   # Tunable weight container + ToCSharpConstants()
│       ├── TexelTuner.cs    # Coordinate descent tuner with phased BuildParameterList
│       ├── TUNING_PHASES.md # Phase reference documentation
│       └── CONTEXT.md       # This file
├── v1-bundle/               # v1.0 through v1.9 (foundation stages)
└── v2-bundle/               # v2.0 through v2.7.x (advanced stages)
```

## Version History (v2 only)

| Version | Feature |
| :-------- | :-------- |
| v2.0-tt | Transposition table |
| v2.1-stable | Stability baseline |
| v2.2-rfp | Reverse futility pruning |
| v2.3-board-opt | Board state optimization |
| v2.4-new-eval | Evaluation rewrite |
| v2.5-const-tuning | Initial constant tuning |
| v2.6.2-search-opts | Search micro-optimizations |
| v2.6.3-search-flat-tables | Flattened search tables |
| v2.6.4-search-mdp | Mate distance pruning |
| v2.7.0-full-eval-tuning | First full Texel tune (PST + scalars). **Current stable baseline.** |
| v2.7.1-full-retune | Second full tune. Unproven — tested equal to v2.7.0. |

## Current Evaluation Features

### Material (MG/EG)

- Pawn, Knight, Bishop, Rook, Queen, King (king = 0)
- Tunable via `PawnMgAdjust`, `KnightMgAdjust`, etc.

### Piece-Square Tables (PeSTO-based, Texel-tuned)

- 6 pieces × 64 squares × 2 phases (MG/EG)
- Tuned via `PstMgAdjust[piece, sq]` / `PstEgAdjust[piece, sq]`
- King MG PST restricted to ranks 1-2 during tuning (sparse data protection)
- King EG PST is NOT tuned (already excellent)

### Positional Scalars

- BishopPairMg/Eg
- KnightMobMg/Eg (baseline 4)
- BishopMobMg/Eg (baseline 6)
- RookSemiOpenMg/Eg, RookOpenMg/Eg
- IsolatedMg/Eg
- KingOwnOpenMg, KingOwnSemiOpenMg
- KingAdjacentOpenMg, KingAdjacentSemiOpenMg
- KnightOutpostMg

### Queenless King Centralization (NEW)

- `QueenlessKingCenterMg`
- Activates only when both queens are off the board
- Uses `KingCenterTable[sq]` (0-4 concentric ring values)
- MG only — does not affect EG score

### Passed Pawns

- `PassedMg[0..7]` and `PassedEg[0..7]`
- Inverted indexing: index 1 = 7th rank (near promotion), index 6 = 2nd rank
- Index 0 and 7 always zero
- Monotonicity constraint enforced on indices 2..6 (index 1 exempt)
- Index 1 is exempt because every pawn on rank 7 is automatically passed

## Board Representation

- Inverted rank indexing: sq/8 = 0 is 8th rank, sq/8 = 7 is 1st rank
- Black PST mirroring: `sq ^ 56`
- Bitboard-based with magic bitboards for sliding pieces

## Texel Tuner Design

- Coordinate descent (hill climbing), one parameter at a time
- Steps: 8 → 4 → 2 → 1
- K optimized via grid search (coarse then fine)
- Loss function: MSE of sigmoid prediction vs game result
- Parallelized loss computation via `Parallel.ForEach`
- Dataset: Zurichess 702k quiet positions

## Key Design Decisions

1. King MG PST top 6 rows are FROZEN during tuning (sparse data overfitting)
2. King EG PST is NEVER tuned (already near-optimal from prior runs)
3. Passed pawn index 1 (7th rank) is exempt from monotonicity because
   every pawn on rank 7 is automatically passed — the PST absorbs file-specific value
4. `ToCSharpConstants()` reads base material from `Evaluation.GetMgMaterial()` /
   `Evaluation.GetEgMaterial()` to avoid hardcoded stale values
5. `EvalWeights.Clone()` deep-copies all arrays including `PstMgAdjust`/`PstEgAdjust`

## File Format for Positions

```
result|fen
```

Where result is: `1.0` (white win), `0.0` (black win), `0.5` (draw).

Example:

```
1.0|rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1
0.5|r1bqkb1r/pppppppp/2n2n2/8/4P3/5N2/PPPP1PPP/RNBQKB1R w KQkq - 2 3
```

## Current Best Loss

```
Final K = 0.009900
Final loss = 0.05913962
```

## How to Run the Tuner

```powershell
cd C:\Users\Rania\OneDrive\Desktop\CBEM\tools\texel-tuning
dotnet run -c Release -- texel "C:\Users\Rania\OneDrive\Desktop\CBEM\data\zurichess.txt"
```

## How to Test Engine Versions

Build and run matches using CuteChess:

- Always test against the last known good version (currently v2.7.0)
- Minimum 2000 games for meaningful results
- Engine binary is built from `active-dev/` or versioned folders
