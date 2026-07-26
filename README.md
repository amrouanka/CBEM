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

The repository contains multiple versioned folders, each representing a stage of engine development.

- The folder named search-tuning contains tuning scripts and utilities for experimenting with search and evaluation parameters.
- The latest work is in v2.6.3-search-flat-tables, which focuses on micro-optimizations such as flattened history, counter, LMR, and PV tables for better data locality and reduced overhead.

## Evolution history

### 1. v1.0-alpha-beta
- Initial engine foundation
- Basic board representation
- Legal move generation
- Alpha-beta search
- Simple evaluation

### 2. v1.1-quiescence
- Added quiescence search
- Reduced horizon effect in tactical positions

### 3. v1.2-mvv-lva
- Introduced MVV-LVA move ordering
- Improved tactical move prioritization

### 4. v1.3-move-ordering
- Expanded move ordering strategies
- Improved search efficiency

### 5. v1.4-iterative-deepening
- Added iterative deepening
- Better time management support

### 6. v1.5-pesto-eval
- Replaced the basic evaluation with a PeSTO-style evaluation
- Added piece-square tables and positional awareness

### 7. v1.6-lmr
- Added late move reduction (LMR)
- Reduced search cost on less promising moves

### 8. v1.7-uci
- Implemented the Universal Chess Interface (UCI) protocol
- Enabled communication with GUIs and test harnesses

### 9. v1.8-null-move
- Added null move pruning
- Improved search speed in middlegames and endgames

### 10. v1.9-aspiration
- Added aspiration windows
- Improved iterative deepening stability

### 11. v2.0-transposition-table
- Added a transposition table
- Reduced repeated search work

### 12. v2.1-stable
- Focused on correctness and stability
- Prepared the engine for further optimization

### 13. v2.2-rfp-ordering
- Added reverse futility pruning (RFP)
- Improved pruning and move ordering logic

### 14. v2.3-optimized-board-state
- Optimized board state handling
- Reduced overhead in move application and undo operations

### 15. v2.4-evaluation
- Refined the evaluation function
- Improved positional understanding

### 16. v2.4.1-texel-tuning
- Introduced Texel-style tuning work
- Adjusted evaluation weights through testing

### 17. v2.5-tuned
- Applied broader tuning of search and evaluation parameters
- Improved overall engine strength

### 18. v2.6.0-countermove
- Added counter-move heuristics
- Improved move ordering in tactical lines

### 19. v2.6.1-history-malus
- Added history malus for quiet moves
- Reduced over-prioritization of poor quiet moves

### 20. v2.6.2-conthist-1ply
- Added continuation history with 1-ply depth
- Further improved move ordering quality

### 21. v2.6.3-search-flat-tables
- Applied search micro-optimizations
- Flattened hot search tables for better cache locality
- Reduced overhead in move ordering and PV handling

## Build and run

Each versioned folder contains its own C# project. You can build or run a project with the standard .NET CLI:

- dotnet build <project>.csproj
- dotnet run --project <project>.csproj

## Summary

This repository shows the full evolution of a chess engine from a basic prototype to a more advanced, competitive-style engine with modern search and evaluation techniques.
