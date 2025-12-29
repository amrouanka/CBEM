# CBEM - Chess Bitboard Engine Manager

A comprehensive chess engine project showcasing the development of a bitboard-based chess engine with progressive feature implementations across multiple versions.

## Overview

CBEM (Chess Bitboard Engine Manager) is an educational chess engine project that demonstrates the implementation of various chess programming techniques using bitboard representation and modern C# development practices.

## Project Structure

The project is organized into four main versions, each building upon the previous one with additional features:

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
   ```

3. **Run in debug mode** (for testing):
   ```bash
   dotnet run --project CBEMv1.5_SimpleEvaluation
   ```

4. **Run with UCI GUI**:
   ```bash
   dotnet run --project CBEMv1.5_SimpleEvaluation --no-debug
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
- **Quiescence Search**: Tactical position analysis
- **Move Ordering**: Search efficiency improvements
  - MVVLVA for capture moves
  - Killer moves for non-capture beta cutoffs
  - History heuristic for quiet move ordering
  - Cached scoring optimization for faster sorting
  - Principal Variation (PV) table for move sequence tracking
  - Enhanced UCI output with full PV display

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
- [ ] Transposition tables
- [ ] Null move pruning
- [ ] Late move reductions
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

**Note**: Each version of CBEM is designed to be a standalone implementation. Start with v1.0 to understand the basics, then progress through v1.1, v1.2, v1.3, v1.4, and v1.5. The v1.5_SimpleEvaluation version implements the sophisticated PESTO evaluation system with tapered evaluation, providing significantly more accurate position assessment and serving as the foundation for advanced chess engine evaluation techniques.

