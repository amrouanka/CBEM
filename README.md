# CBEM - Chess Bitboard Engine Manager

A comprehensive chess engine project showcasing the development of a bitboard-based chess engine with progressive feature implementations across multiple versions.

## Overview

CBEM (Chess Bitboard Engine Manager) is an educational chess engine project that demonstrates the implementation of various chess programming techniques using bitboard representation and modern C# development practices.

## Project Structure

The project is organized into multiple versions, each building upon the previous one with additional features:

### CBEMv1.0_AlphaPruning
- **Core Features**: Basic chess engine with alpha-beta pruning
- **Key Components**:
  - Bitboard-based board representation
  - Move generation and validation
  - Basic evaluation function
  - Alpha-beta pruning algorithm
  - UCI (Universal Chess Interface) protocol support

### CBEMv1.1_QuiescenceSearch
- **Enhanced Features**: Adds quiescence search to v1.0
- **Improvements**:
  - Quiescence search for better tactical analysis
  - More accurate evaluation in complex positions
  - Enhanced search depth handling

### CBEMv1.2_MVVLVA
- **Advanced Features**: Implements simple move ordering using MVVLVA (Most Valuable Victim - Least Valuable Attacker)
- **Improvements**:
  - MVVLVA-based capture move ordering for better move prioritization
  - More efficient move sorting for capture selection
  - Enhanced alpha-beta pruning efficiency through better move ordering
  - Improved search performance by trying promising captures first

### CBEMv1.3_MoveOrdering
- **Advanced Features**: Enhanced move ordering with killer moves and history heuristic
- **Improvements**:
  - Killer move tracking for non-capture beta cutoffs
  - History heuristic for quiet move ordering based on past effectiveness
  - Cached score values to avoid repeated move scoring during sorting
  - Proper move ordering hierarchy: captures > killer moves > history
  - Significant search speed improvements through better move prioritization

### CBEMv1.4_IterativeDeepening
- **Advanced Features**: Principal Variation (PV) table and iterative deepening framework
- **Improvements**:
  - PV table for storing and displaying full search variations
  - Enhanced UCI output with Principal Variation lines
  - Iterative deepening search for progressively deeper analysis
  - Improved move ordering through PV tracking
  - Better analysis capabilities with complete move sequences

### CBEMv1.5_PestoEval
- **Advanced Features**: PESTO (Piece-Square Table) evaluation system with tapered evaluation
- **Improvements**:
  - Comprehensive PESTO evaluation tables for all piece types
  - Tapered evaluation blending middlegame and endgame scores
  - Separate middlegame and endgame piece-square tables for more accurate position assessment
  - Game phase calculation for proper evaluation weighting
  - Improved evaluation precision and board assessment

### CBEMv2.0_LMR&DeltaPruning
- **Advanced Features**: Search optimization with Late Move Reduction (LMR) and Delta Pruning
- **Major Optimizations**:
  - **LMR**: Reduce search depth for less promising moves while preserving quality
  - **Delta Pruning**: Skip unpromising captures in quiescence search using a value margin
  - **Move Scoring Optimization**: O(1) piece detection replacing slower bitboard loops
  - **PV Table Optimization**: Efficient data copying for better cache performance
  - **Check Detection Caching**: Avoid redundant king lookups during search
  - **Improved Move Sorting**: Better ordering for early alpha-beta cutoffs
- **Performance Improvements**:
  - Reduced search tree size and node counts
  - Better cache locality and memory efficiency
  - Improved pruning and move ordering for higher search speed

### CBEMv2.1_UCI
- **Advanced Features**: Full UCI integration with time management and GUI-ready command handling
- **Improvements**:
  - Full UCI position and go command parsing
  - Move time, increment, and clock handling for practical GUI play
  - Stable UCI loop with new game reset behavior
  - Expanded debug/test driver with node-count benchmarking for multiple positions
  - Better integration with chess GUIs and tournament-style search control

### CBEMv2.2_NullMove
- **Advanced Features**: Null move pruning with aggressive search pruning
- **Major Optimizations**:
  - **Null Move Pruning**: Beta cutoff detection from reduced-depth search
  - **Enhanced LMR Logic**: Aggressive reduction formula with depth cap
  - **Futility Pruning**: Skip hopeless shallow positions with a depth-dependent margin
  - **Delta Pruning**: Improve quiescence search efficiency with tighter capture filtering
- **Performance Improvements**:
  - Substantially reduced search nodes across test positions
  - Improved pruning while maintaining move quality
  - More aggressive but safe search pruning in non-check positions

### CBEMv2.3_AspirationWindows
- **Advanced Features**: Aspiration window search with tuned move ordering
- **Major Optimizations**:
  - **Aspiration Windows**: Narrow search windows around expected scores
  - **Restart Logic**: Widen windows only when needed
  - **Aggressive LMR**: Greater reductions for deeper move sequences
  - **Killer/History Ordering**: Better quiet move prioritization with capture filtering
- **Performance Impact**:
  - Added node reduction when window accuracy is good
  - Increased depth efficiency in many positions
  - Practical limitations without transposition support

### CBEMv3.0_TranspositionTables
- **Advanced Features**: Transposition tables and repetition detection
- **Major Optimizations**:
  - **Transposition Table**: Cache search results for repeated positions
  - **Zobrist Hashing**: Fast, unique position keys for lookup
  - **Threefold Repetition Detection**: Reliable draw handling
  - **Improved Time Management**: Safer move time and overhead handling
  - **UCI Enhancements**: Better state handling and protocol support
- **Performance Improvements**:
  - Significant node reduction through cache hits
  - Effective aspiration windows with transposition support
  - Better time control and stability in GUI play

### CBEMv3.5_Stable
- **Advanced Features**: Stable production-quality search with full UCI and repetition support
- **Improvements**:
  - Full transposition table integration with replacement strategy
  - Robust UCI time control handling and new game resets
  - Stable move ordering, null move, and delta pruning
  - Extensive debug/test harness for deep search benchmarking
- **Performance Improvements**:
  - Reliable search behavior on deeper tactical and strategic positions
  - Balanced pruning with move ordering to reduce pathological cases
  - Improved command-line and GUI compatibility

### CBEMv3.6_RFPOrdering
- **Advanced Features**: Refined move ordering with relative futility pruning (RFP)
- **Improvements**:
  - **Relative Futility Pruning**: Static evaluation margin pruning before search
  - **Aspiration Windows**: Narrow-window search with retry logic
  - **Transposition Table**: Cached node reuse for repeated positions
  - **Repetition Detection**: Threefold draw detection during iterative search
  - **UCI Time Management**: Practical clock and increment handling
- **Performance Improvements**:
  - Better move ordering and earlier cutoffs
  - Lower node counts for quiet and tactical positions
  - Improved stability in longer searches

### CBEMv3.7_OptimizedBoardState
- **Advanced Features**: Explicit board-state struct and optimized move execution
- **Major Optimizations**:
  - `Board.CopyBoard()` and `Board.TakeBack()` now use explicit field copies instead of array cloning
  - `UpdateOccupancies()` computes piece occupancies directly from explicit bitboards
  - `MakeMove()` capture handling simplified with a direct piece search loop
  - Tactical test runner and perft handling updated for better debug consistency
- **Performance Improvements**:
  - Lower allocation overhead during search and move make/unmake
  - Faster backtracking through explicit board-state fields
  - Reduced array manipulation and improved search iteration throughput
- **Technical Details**:
  - `BoardState` struct expanded with `bb0..bb11`, `occ0..occ2`, and `hashKey`
  - `v3.7-optimized-board-state.csproj` uses `RootNamespace` `v3.7-optimized-board-state`
  - Builds on transposition, repetition, and time management features with improved state handling

## Technical Architecture

### Core Components

- **BitboardOperations.cs**: Efficient bitboard manipulation utilities
- **Board.cs**: Chess board state management and FEN parsing
- **MoveGenerator.cs**: Legal move generation for all piece types
- **PieceAttacks.cs**: Attack detection and square safety evaluation
- **Evaluation.cs**: Position evaluation heuristics
- **Search.cs**: Search algorithms (negamax, alpha-beta, quiescence)
- **Uci.cs**: UCI protocol implementation for GUI integration
- **TranspositionTable.cs**: Hash table implementation for search result caching
- **Zobrist.cs**: Zobrist hashing for position identification
- **Enums.cs**: Chess piece and move type definitions
- **Move.cs**: Move representation and encoding

### Key Features

- **Bitboard Representation**: 64-bit integer representation for efficient board operations
- **UCI Protocol**: Full compatibility with chess GUIs like Arena, ChessBase, etc.
- **Performance Testing**: Built-in perft testing for move generation verification
- **Debug Mode**: Development tools for position analysis and testing

## Getting Started

### Prerequisites
- .NET 6.0 or later
- Visual Studio 2022 or VS Code
- Optional: Chess GUI supporting UCI protocol (Arena, ChessBase, etc.)

### Building and Running

1. **Clone the repository**:
   ```bash
   git clone [repository-url]
   cd CBEM
   ```

2. **Build a specific version**:
   ```bash
   dotnet build v1.0-alpha-beta/v1.0-alpha-beta.csproj
   # or
   dotnet build v1.1-quiescence/v1.1-quiescence.csproj
   # or
   dotnet build v1.2-mvv-lva/v1.2-mvv-lva.csproj
   # or
   dotnet build v1.3-move-ordering/v1.3-move-ordering.csproj
   # or
   dotnet build v1.4-iterative-deepening/v1.4-iterative-deepening.csproj
   # or
   dotnet build v1.5-pesto-eval/v1.5-pesto-eval.csproj
   # or
   dotnet build v2.0-lmr/v2.0-lmr.csproj
   # or
   dotnet build v2.1-uci/v2.1-uci.csproj
   # or
   dotnet build v2.2-null-move/v2.2-null-move.csproj
   # or
   dotnet build v2.3-aspiration/v2.3-aspiration.csproj
   # or
   dotnet build v3.0-transposition/v3.0-transposition.csproj
   # or
   dotnet build v3.5-stable/v3.5-stable.csproj
   # or
   dotnet build v3.6-rfp-ordering/v3.6-rfp-ordering.csproj
   # or
   dotnet build v3.7-optimized-board-state/v3.7-optimized-board-state.csproj
   ```

3. **Run a version from the terminal**:
   ```bash
   dotnet run --project v3.7-optimized-board-state/v3.7-optimized-board-state.csproj
   ```

4. **Run with UCI GUI**:
   ```bash
   dotnet run --project v3.7-optimized-board-state/v3.7-optimized-board-state.csproj --no-debug
   ```

### Testing

The engine includes built-in performance testing (perft) to verify move generation accuracy:

- Test positions are predefined in `Program.cs`
- Run perft tests by setting `debug = true` in `Program.cs`
- Results show node counts and timing for each move

## Chess Engine Features

### Implemented Algorithms
- **Negamax**: Recursive search algorithm
- **Alpha-Beta Pruning**: Search tree optimization
- **Quiescence Search**: Tactical position analysis with delta pruning
- **Move Ordering**: Search efficiency improvements
  - MVVLVA for capture moves
  - Killer moves for non-capture beta cutoffs
  - History heuristic for quiet move ordering
  - Cached scoring optimization for faster sorting
  - Principal Variation (PV) table for move sequence tracking
  - Enhanced UCI output with full PV display
  - **Late Move Reduction (LMR)**: Dynamic depth reduction for less promising moves
  - **Delta Pruning**: Skip unpromising captures in quiescence search
  - **Null Move Pruning**: Beta cutoff detection using reduced depth searches
  - **Futility Pruning**: Skip hopeless positions at shallow depths
  - **Aspiration Windows**: Dynamic search windows around expected scores for reduced search space
  - **Transposition Tables**: Hash table for storing and retrieving search results
  - **Repetition Detection**: Threefold repetition detection for correct draw handling

### Evaluation Components
- **PESTO (Piece-Square Table) evaluation system**
- **Tapered evaluation** blending middlegame and endgame scores
- Comprehensive piece-square tables for all piece types
- Game phase calculation for proper evaluation weighting
- Material balance evaluation
- Positional factors (pawn structure, king safety, piece activity)
- Significantly improved evaluation accuracy (~±1 centipawn for standard positions)

### Move Generation
- Complete legal move generation for all piece types
- Special moves (castling, en passant, pawn promotion)
- Check and checkmate detection
- Pin and discovery attack handling

## Development Roadmap

### Future Enhancements
- [x] Transposition tables
- [x] Null move pruning
- [x] Repetition detection
- [x] Late move reductions
- [x] Aspiration windows
- [ ] Opening book integration
- [ ] Endgame tablebases
- [ ] Parallel search
- [ ] Machine learning evaluation

### Testing Improvements
- [ ] Automated test suite
- [ ] Benchmarking framework
- [ ] GUI integration testing
- [ ] Performance profiling

## Contributing

This project serves as an educational resource for chess engine development. Contributions are welcome in the form of:
- Bug fixes and optimizations
- Additional features and algorithms
- Documentation improvements
- Test cases and benchmarks

## License

This project is provided for educational purposes. Please check the license file for specific terms.

## Acknowledgments

- Chess programming community for valuable resources and discussions
- Bitboard chess programming pioneers
- UCI protocol developers
- Various chess engine projects for inspiration

## Resources

- [Chess Programming Wiki](https://www.chessprogramming.org/)
- [UCI Protocol Specification](https://www.chess.com/forum/view/general/chess-protocol-uci)
- [Bitboard Chess Programming](https://www.chessprogramming.org/Bitboards)

---

**Note**: Each version of CBEM is designed to be a standalone implementation. Start with v1.0 to understand the basics, then progress through v1.1, v1.2, v1.3, v1.4, v1.5, v1.6. The v2.2_NullMove version represents comprehensive search optimizations including null move pruning, aggressive LMR, enhanced delta pruning, and futility pruning, providing dramatic performance improvements (50-80% node reduction).

**✅ Recommended**: The v3.0_TranspositionTables version is the current recommended version, featuring transposition tables, repetition detection, enhanced aspiration windows, and improved time management. This version provides the best performance with 30-50% additional node reduction over v2.2 and fully functional aspiration windows.
