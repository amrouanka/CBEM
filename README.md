# CBEM Chess Engine Evolution

This repository documents the step-by-step evolution of a chess engine in C#. It starts from a simple alpha-beta search engine and grows into a stronger engine with improved evaluation, pruning, move ordering, tuning, and UCI support.

## Project goal

The purpose of this project is to learn and demonstrate how a chess engine develops over time:

- board representation and legal move generation
- alpha-beta and quiescence search
- move ordering and pruning heuristics
- iterative deepening and time management
- transposition tables and aspiration windows
- evaluation improvements and tuning
- UCI protocol integration
- low-level search optimizations and cache-friendly data layouts

## Repository structure

The repository contains multiple versioned folders, each representing a stage of engine development:

- `v1-bundle/`: Foundation stages from basic alpha-beta (`v1.0`) through aspiration windows (`v1.9`).
- `v2-bundle/`: Advanced search heuristics, transposition tables, pruning, and evaluation upgrades.
- `tools/`: Standalone utilities including `search-tuning` and a global `texel-tuning` framework for optimizing evaluation weights.
- `data/`: Test suites, PGN databases, and EPD training datasets.
- `active-dev/`: The active workspace for ongoing development and experimental features.

The latest stable milestone is **v2.6.5-positional-tuning**, which integrates automated Texel tuning to mathematically optimize positional and structural evaluation parameters.

## Evolution history

### 1. v1.0-alpha-beta
- Initial engine foundation
- Basic board representation and legal move generation
- Alpha-beta search with simple material evaluation

### 2. v1.1-quiescence
- Added quiescence search
- Reduced horizon effect in tactical positions

### 3. v1.2-mvv-lva
- Introduced MVV-LVA move ordering
- Improved tactical move prioritization

### 4. v1.3-move-ordering
- Expanded move ordering strategies
- Improved search efficiency and cutoff rates

### 5. v1.4-iterative-deepening
- Added iterative deepening
- Better time management and move ordering support

### 6. v1.5-pesto-psqt
- Replaced basic evaluation with a PeSTO-style evaluation
- Added piece-square tables (PSQT) and positional awareness

### 7. v1.6-lmr
- Added Late Move Reduction (LMR)
- Reduced search depth on less promising quiet moves

### 8. v1.7-uci
- Implemented the Universal Chess Interface (UCI) protocol
- Enabled communication with GUIs and automated test harnesses

### 9. v1.8-null-move
- Added Null Move Pruning (NMP)
- Improved search speed in middlegames and endgames

### 10. v1.9-aspiration
- Added aspiration windows
- Improved iterative deepening stability and search speed

### 11. v2.0-tt
- Added a Transposition Table (TT)
- Reduced repeated search work across transpositions and iterative deepening

### 12. v2.1-stable
- Focused on correctness, bugfixes, and engine stability
- Established a solid baseline for advanced search optimizations

### 13. v2.2-rfp
- Added Reverse Futility Pruning (RFP)
- Improved forward pruning logic for positions with large static evaluation margins

### 14. v2.3-board-opt
- Optimized board state representation and handling
- Reduced CPU overhead in move application (`MakeMove` / `UnmakeMove`)

### 15. v2.4-new-eval
- Expanded and restructured the evaluation function
- Added richer positional terms and structural awareness

### 16. v2.5-const-tuning
- Applied automated tuning to core evaluation constants and search parameters
- Improved overall engine strength and weight balance

### 17. archived-v2.6.0-countermove
- Experimental: Added counter-move heuristic for quiet move ordering
- *Archived for historical reference after empirical testing*

### 18. archived-v2.6.1-history-malus
- Experimental: Added history malus penalties for failing quiet moves
- *Archived for historical reference after empirical testing*

### 19. v2.6.2-search-opts
- Applied search micro-optimizations
- Reduced overhead in move ordering, killer moves, and PV table handling

### 20. v2.6.3-search-flat-tables
- Flattened multidimensional search tables (history, counter, LMR) into 1D arrays
- Improved CPU cache locality and reduced array indexing overhead

### 21. v2.6.4-search-mdp
- Added Mate Distance Pruning (MDP)
- Early pruning of search nodes when shorter checkmates are already found

### 22. v2.6.5-positional-tuning
- Integrated a global Texel tuning framework
- Re-tuned positional bonuses, mobility, king safety, and pawn structure parameters against EPD datasets

## Build and run

Each versioned folder contains its own C# project. You can build or run a project with the standard .NET CLI:

```bash
dotnet build <project>.csproj -c Release
dotnet run --project <project>.csproj -c Release