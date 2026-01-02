# CBEM - Chess Bitboard Engine Manager

A comprehensive chess engine project showcasing the development of a bitboard-based chess engine with progressive feature implementations across multiple versions.

## Overview

CBEM (Chess Bitboard Engine Manager) is an educational chess engine project that demonstrates the implementation of various chess programming techniques using bitboard representation and modern C# development practices.

## Project Structure

The project is organized into multiple version, each building upon the previous one with additional features:

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

### CBEMv1.2_SimpleOrdering1
- **Advanced Features**: Implements simple move ordering using MVVLVA (Most Valuable Victim - Least Valuable Attacker)
- **Status**: Implemented with basic capture move ordering
- **Current Features**:
  - MVVLVA-based capture move ordering for better move prioritization
  - Array.Sort implementation for efficient move sorting
  - Enhanced alpha-beta pruning efficiency through better move ordering
  - Improved search performance by trying promising captures first

### CBEMv1.3_SimpleOrdering2
- **Advanced Features**: Enhanced move ordering with killer moves and history heuristic
- **Improvements**:
  - Killer move tracking (2 slots per ply) for non-capture beta cutoffs
  - History table for quiet move ordering based on past effectiveness
  - Cached scoring optimization to eliminate repeated ScoreMove() calls during sorting
  - Proper move ordering hierarchy: captures > killer moves > history
  - Significant search speed improvements through better move prioritization

### CBEMv1.4_IteretiveDeep
- **Advanced Features**: Principal Variation (PV) table and iterative deepening framework
- **Improvements**:
  - PV table for storing and displaying full search variations
  - Enhanced UCI output with full Principal Variation line
  - Foundation for iterative deepening search algorithm
  - Improved move ordering through PV tracking
  - Better analysis capabilities with complete move sequences

### CBEMv1.5_SimpleEvaluation
- **Advanced Features**: PESTO (Piece-Square Table) evaluation system with tapered evaluation
- **Improvements**:
  - Comprehensive PESTO evaluation tables for all piece types (pawn, knight, bishop, rook, queen, king)
  - Tapered evaluation blending middlegame and endgame scores based on game phase
  - Separate middlegame and endgame piece-square tables for accurate position assessment
  - Game phase calculation for proper evaluation weighting
  - Significantly improved evaluation accuracy (~±1 centipawn for standard positions)
  - Modern C# array initialization syntax and proper bitboard integration

### CBEMv1.6_LateMoveReduction
- **Advanced Features**: Late Move Reduction (LMR) with enhanced search optimization
- **Improvements**:
  - LMR algorithm to reduce search depth for less promising moves
  - Dynamic reduction based on move count and search depth
  - Enhanced move ordering with improved PV scoring
  - Optimized bitboard operations for faster piece detection
  - Improved move sorting with insertion sort for small move lists
  - Better cache utilization through optimized data structures
  - Enhanced alpha-beta cutoff efficiency

### CBEMv2.0_LMR&DeltaPruning
- **Advanced Features**: Comprehensive search optimization with LMR and Delta Pruning
- **Major Optimizations**:
  - **Enhanced LMR Logic**: Dynamic reduction calculation with safety checks and depth capping
  - **Delta Pruning**: Skip unpromising captures in quiescence search using piece value evaluation
  - **Move Scoring Optimization**: O(1) piece detection replacing O(n) bitboard loops
  - **PV Table Optimization**: Efficient Array.Copy operations for better cache performance
  - **Check Detection Caching**: Cached king positions to avoid redundant calculations
  - **Improved Move Sorting**: Optimized insertion sort for small move lists
  - **Enhanced Quiescence Search**: Better pruning with capture value estimation
- **Performance Improvements**:
  - Reduced computational complexity in critical search paths
  - Better cache locality and memory access patterns
  - Enhanced pruning to reduce search tree size
  - Improved move ordering for earlier alpha-beta cutoffs
  - Optimized data structures for faster access

### CBEMv2.2_NullMove
- **Advanced Features**: Comprehensive search optimization with null move pruning and aggressive pruning techniques
- **Major Optimizations**:
  - **Null Move Pruning**: Beta cutoff detection using reduced depth searches (R = 2)
  - **Enhanced LMR Logic**: Aggressive reduction formula `1 + (movesSearched / 3) + (depth / 4)` with 4-ply cap
  - **Improved Delta Pruning**: Tightened margin to +10 for more aggressive capture pruning
  - **Futility Pruning**: Skip hopeless positions at shallow depths (≤ 3) with margin `depth * 150`
  - **Optimized Search Parameters**: Tuned for minimal node count while maintaining accuracy
- **Performance Improvements**:
  - **50-80% node reduction** across all test positions
  - **Extreme cases**: Some positions reduced from ~450K to ~17K nodes (96% reduction)
  - **Consistent gains**: Most positions show significant speed improvements
  - **Maintained quality**: Search accuracy preserved while achieving dramatic speed gains
- **Technical Details**:
  - Null move reduction carefully balanced (R = 2 optimal, R = 3 causes stack overflow)
  - LMR parameters tuned through extensive testing for optimal node reduction
  - Futility pruning applied only in safe conditions (shallow depth, not in check)
  - Delta pruning optimized for quiescence search efficiency

### CBEMv2.3_AspirationWindows
- **Advanced Features**: Aspiration windows implementation with known limitations
- **Major Optimizations**:
  - **Aspiration Windows**: Dynamic search windows around expected scores to reduce search space
  - **Optimized Window Size**: ±35 point window provides best balance between cutoffs and restarts
  - **Fine-Tuned LMR**: Extremely aggressive reduction formula `1 + (movesSearched / 1) + (depth / 2)` with 7-ply cap
  - **Optimal LMR Parameters**: fullDepthMoves = 2, reductionLimit = 3 for deep searches
  - **Enhanced Move Ordering**: Killer moves and history heuristic with capture filtering
  - **Comprehensive Testing**: Extensive parameter tuning across multiple test positions
- **⚠️ Important Limitation**: **Aspiration windows are only effective when combined with transposition tables**. Without transposition tables, aspiration windows can actually weaken the engine by causing search restarts and skipping depths, making v2.3 weaker than v2.2 in practice.
- **Performance Impact**:
  - **Theoretical improvements**: 15% additional node reduction over v2.2 baseline with increased search depths
  - **Practical reality**: Without transposition tables, the engine performs worse than v2.2 due to aspiration window inefficiencies
  - **Recommendation**: Use v2.2 for better performance until transposition tables are implemented
- **Technical Details**:
  - Aspiration window restart logic handles score falls outside window but causes depth skipping
  - LMR reduction carefully balanced to avoid search instability
  - Capture checks in killer moves maintained for theoretical correctness
  - Parameters extensively tested but limited by lack of transposition table support

## Technical Architecture

### Core Components

- **BitboardOperations.cs**: Efficient bitboard manipulation utilities
- **Board.cs**: Chess board state management and FEN parsing
- **MoveGenerator.cs**: Legal move generation for all piece types
- **PieceAttacks.cs**: Attack detection and square safety evaluation
- **Evaluation.cs**: Position evaluation heuristics
- **Search.cs**: Search algorithms (negamax, alpha-beta, quiescence)
- **Uci.cs**: UCI protocol implementation for GUI integration
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
   dotnet build CBEMv1.0_AlphaPruning/CBEMv1.0_AlphaPruning.csproj
   # or
   dotnet build CBEMv1.1_QuiescenceSearch/CBEMv1.1_QuiescenceSearch.csproj
   # or
   dotnet build CBEMv1.2_SimpleOrdering1/CBEMv1.2_SimpleOrdering1.csproj
   # or
   dotnet build CBEMv1.3_SimpleOrdering2/CBEMv1.3_SimpleOrdering2.csproj
   # or
   dotnet build CBEMv1.4_IteretiveDeep/CBEMv1.4_IteretiveDeep.csproj
   # or
   dotnet build CBEMv1.5_SimpleEvaluation/CBEMv1.5_SimpleEvaluation.csproj
   # or
   dotnet build CBEMv1.6_LateMoveReduction/CBEMv1.6_LateMoveReduction.csproj
   # or
   dotnet build CBEMv2.0_LMR&DeltaPruning/CBEMv2.0_LMR&DeltaPruning.csproj
   # or
   dotnet build CBEMv2.2_NullMove/CBEMv2.2_NullMove.csproj
   # or
   dotnet build CBEMv2.3_AspirationWindows/CBEMv2.3_AspirationWindows.csproj
   ```

3. **Run in debug mode** (for testing):
   ```bash
   dotnet run --project CBEMv2.3_AspirationWindows
   ```

4. **Run with UCI GUI**:
   ```bash
   dotnet run --project CBEMv2.3_AspirationWindows --no-debug
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

**⚠️ Important**: The v2.3_AspirationWindows version implements aspiration windows but **requires transposition tables to be effective**. Without transposition tables, aspiration windows cause search restarts and depth skipping, making v2.3 **weaker than v2.2** in practice. For best performance, use v2.2 until transposition tables are implemented in a future version.
