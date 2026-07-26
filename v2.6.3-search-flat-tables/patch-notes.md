# Patch Notes

## Summary

This patch focuses on micro-optimizations in search, move ordering, and related hot paths while keeping heuristics, pruning conditions, reductions, margins, and numeric constants unchanged.

## 1. Array layout and data locality

- `historyMoves` was flattened from `int[2,64,64]` to `int[8192]`. This removes the slower multi-dimensional indexing path and uses a compact index of `(side << 12) | (source << 6) | target`.
- `counterMoves` was flattened to `int[768]`, indexed with `(piece << 6) | target`.
- `lmrTable` was flattened to `int[(MaxPly + 1) * 64]`, with the row base hoisted once per node as `lmrRowBase = depth << 6`.
- `pvTable` was flattened to `int[MaxPly * MaxPly]`, and the row bases were hoisted out of the move loop.
- `killerMove1` and `killerMove2` were merged into a single `killerMoves[MaxPly * 2]` array so both killers for a ply can be read with better locality.

## 2. Arithmetic elimination

- The `% 6` operation was removed from the MVV/LVA hot path by expanding `mvvLva` into a flat `12 x 16` table indexed as `(attacker << 4) | victim`.
- `depth * depth` was hoisted to `depthSquared` above the move loop.
- `depth - 1` was hoisted to `newDepth` and reused across the recursive calls.
- `nextPly` was hoisted out of the PV update block.
- The quiescence delta margin was precomputed as `deltaBase = eval + QsDeltaMargin`.
- `GetMoveTarget(move)` in quiescence is now consumed directly by `GetPieceAtSquare` without creating an extra temporary.

## 3. Stack usage

- `SkipLocalsInit` was applied to `AlphaBeta`. This avoids zero-initializing the `stackalloc int[64]` buffer on every node.
- The build requirement is to enable `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` in the project file if the attribute is used.

## 4. Branch behavior

- Shared pruning predicates were hoisted into values such as `quietNode` and `prunableNode` to reduce repeated branching.
- The NMP condition was reordered so `HasNonPawnMaterial` is evaluated last.
- `isQuiet` is computed with `&` instead of `&&`, removing an extra local in the hot path.
- The LMR condition now checks `isQuiet` before `!inCheck`.

## 5. Move ordering

- Insertion sort now exits early when elements are already in order.
- `SortMoves` now operates on `Span<int>` slices of the move and score arrays to reduce bounds-check overhead.
- Default parameter values were removed from `SortMoves`, and all call sites now pass the required arguments explicitly.

## 6. PV handling

- The PV copy loop was replaced with `Array.Copy` for better locality and lower overhead.
- `pvTable[0]` is read once into `rootMove` in `SearchPosition` instead of being read twice.

## 7. Static field and bitboard access

- `bitboards` is cached into a local in `GetPieceAtSquare` and `HasNonPawnMaterial`.
- `side` is cached in `IsInCheck`.
- `repetitionTable` is cached in `IsRepetition`.

## 8. I/O hygiene

- The UCI info line is now built into a reusable `StringBuilder` and emitted with a single `Console.WriteLine`.
- This avoids per-move allocations and repeated console writes during search.

## Deliberately left alone

- `GetPieceAtSquare` still uses its existing branch structure.
- `ScoreMove` was not forced inline, since aggressive inlining may hurt performance more than it helps.

> All heuristics, pruning conditions, reductions, margins, and numeric constants remain unchanged.