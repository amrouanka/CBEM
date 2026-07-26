# CBEM Chess Engine Evolution

This repository is a step-by-step implementation of a chess engine in C#. It starts from a simple alpha-beta search engine and gradually evolves into a more advanced engine with modern search heuristics, evaluation improvements, tuning, and UCI support.

## Project goal

The purpose of this project is to learn and demonstrate how a chess engine grows over time:

- basic board representation and move generation
- alpha-beta search
- quiescence search
- move ordering
- iterative deepening
- evaluation improvements
- pruning and reduction heuristics
- transposition table usage
- tuning and optimization
- UCI protocol integration

## Repository structure

The repository contains multiple versioned folders, each representing one stage of the engine development.

## Evolution history

### 1. v1.0-alpha-beta
- Initial chess engine foundation
- Basic board representation
- Legal move generation
- Alpha-beta search
- Simple evaluation

### 2. v1.1-quiescence
- Added quiescence search
- Reduced horizon effect in tactical positions
- Improved endgame/quiet-search behavior

### 3. v1.2-mvv-lva
- Introduced MVV-LVA move ordering
- Better tactical move prioritization
- Improved search efficiency

### 4. v1.3-move-ordering
- Expanded move ordering strategies
- Improved search pruning effectiveness
- Better handling of promising moves

### 5. v1.4-iterative-deepening
- Added iterative deepening
- Better time management support
- More practical search behavior for real-time play

### 6. v1.5-pesto-eval
- Replaced the basic evaluation with a PeSTO-style evaluation approach
- Added piece-square tables and positional awareness
- Stronger positional evaluation

### 7. v1.6-lmr
- Added late move reduction (LMR)
- Reduced search cost on less promising moves
- Improved overall performance

### 8. v1.7-uci
- Implemented the Universal Chess Interface (UCI) protocol
- Enabled standard chess engine communication
- Made the engine usable by GUI tools and test harnesses

### 9. v1.8-null-move
- Added null move pruning
- Improved search speed in middlegames and endgames
- Reduced node count for deeper searches

### 10. v1.9-aspiration
- Added aspiration windows
- Improved iterative deepening search stability
- Better handling of principal variation search flow

### 11. v2.0-transposition-table
- Added a transposition table
- Reduced repeated search work
- Improved efficiency across positions

### 12. v2.1-stable
- Focused on stability and correctness
- Fixed rough edges in search and engine behavior
- Prepared the engine for further optimization

### 13. v2.2-rfp-ordering
- Added reverse futility pruning (RFP)
- Improved move ordering and pruning logic
- Further reduced unnecessary search branches

### 14. v2.3-optimized-board-state
- Optimized the board representation and state handling
- Reduced overhead in move application and undo operations
- Improved engine performance

### 15. v2.4-evaluation
- Refined the evaluation function
- Improved positional understanding
- Added more nuanced evaluation features

### 16. v2.4.1-texel-tuning
- Introduced Texel-style tuning work
- Adjusted evaluation weights based on testing
- Improved strength through parameter tuning

### 17. v2.5-tuned
- Applied broader tuning of search and evaluation parameters
- Improved overall engine strength
- Made the engine more balanced and consistent

### 18. v2.6.0-countermove
- Added counter-move heuristic
- Improved move ordering in tactical lines
- Increased search efficiency in repeated patterns

### 19. v2.6.1-history-malus
- Added history malus for quiet moves
- Improved move ordering reliability
- Reduced over-prioritization of bad quiet moves

### 20. v2.6.2-conthist-1ply
- Added continuation history with 1-ply depth
- Further improved move ordering quality
- Made the engine more selective and efficient in search

## Notes on the tuning folder

The folder named search-tuning contains tuning-related scripts and utilities used to experiment with search and evaluation parameters. It complements the versioned engine folders and shows the engineering side of the project.


## Summary

This repository shows the full evolution of a chess engine from a basic search prototype to a more advanced, competitive-style engine with modern pruning and ordering techniques.
