using static Board;
using static PieceAttacks;
using static MoveGenerator;
using static BitboardOperations;

// =============================================================================
// Evaluation.cs
//
// Scores the current position using tapered evaluation:
//
//   finalScore = (middlegameScore * mgPhase + endgameScore * egPhase) / 24
//
//   mgPhase = number of non-pawn pieces still on the board (max 24)
//   egPhase = 24 - mgPhase
//
//   As pieces come off the board, the score smoothly shifts from
//   middlegame weights to endgame weights.
//
// Visual:
//
//   Opening          Middlegame         Endgame
//   phase=24         phase=12           phase=0
//   100% MG          50% MG + 50% EG    100% EG
//   ────────────────────────────────────────────▶
//
// Features scored:
//   ✓ Material + piece-square tables (tapered)
//   ✓ Bishop pair bonus
//   ✓ Passed pawns
//   ✓ Isolated pawns
//   ✓ Knight + bishop mobility
//   ✓ Rook on open / semi-open files
//   ✓ King open-file exposure penalty
//   ✓ Knight outposts
//
// Score conventions:
//   Evaluate()          → standard:  positive = White is better
//   EvaluateForSearch() → negamax:   positive = side to move is better
//
// Always use:
//   • EvaluateForSearch() inside alpha-beta and quiescence search
//   • Evaluate() for debugging, printing, tests
// =============================================================================
public static class Evaluation
{
    // =========================================================================
    // Dimensions
    // =========================================================================

    private const int PieceTypeCount = 6;   // P N B R Q K
    private const int PieceCount = 12;  // P N B R Q K p n b r q k
    private const int SquareCount = 64;

    // =========================================================================
    // Game Phase
    //
    //   Tracks how far we are into the endgame.
    //   Each piece type contributes a weight when it is on the board:
    //
    //     Pawn   = 0  (pawns don't change the game phase)
    //     Knight = 1
    //     Bishop = 1
    //     Rook   = 2
    //     Queen  = 4
    //     King   = 0  (kings are always present)
    //
    //   Max total = 2*(1+1+2+4) × 2 sides = 24
    //
    //   Visual:
    //     Start of game:  phase=24  → fully middlegame weights
    //     All queens off: phase=16
    //     Only pawns+kings: phase=0 → fully endgame weights
    // =========================================================================
    private const int TotalPhase = 24;

    private static readonly int[] PhaseWeights =
    {
        //  P  N  B  R  Q  K    p  n  b  r  q  k
            0, 1, 1, 2, 4, 0,  0, 1, 1, 2, 4, 0,
    };

    // =========================================================================
    // Material Values (centipawns)
    //
    //   Separate values for middlegame (Mg) and endgame (Eg).
    //   Example: a pawn is worth 82cp in the middlegame, 94cp in the endgame
    //   (slightly more valuable in the endgame because fewer pieces exist).
    //
    //   Index:  0=P  1=N  2=B  3=R  4=Q  5=K
    // =========================================================================
    private static readonly int[] MiddlegameMaterial = { 82, 337, 365, 477, 1025, 0 };
    private static readonly int[] EndgameMaterial = { 94, 281, 297, 512, 936, 0 };

    // =========================================================================
    // Positional Bonuses and Penalties
    // =========================================================================

    // --- Bishop pair ---
    //
    //   Two bishops together control both diagonal colors.
    //   This gives a structural advantage worth a small bonus.
    //
    //   Visual: having B on light + B on dark squares = bishop pair
    private const int BishopPairBonusMg = 15;
    private const int BishopPairBonusEg = 39; // more valuable in open endgames

    // --- Knight mobility ---
    //
    //   Bonus per square the knight can reach, relative to a baseline of 4.
    //
    //   mobility = (number of reachable squares) - baseline
    //   score   += mobility * weight
    //
    //   Example: knight with 6 reachable squares → mobility = 6 - 4 = +2
    //            knight with 2 reachable squares → mobility = 2 - 4 = -2 (penalty)
    private const int KnightMobilityBaseline = 4;
    private const int KnightMobilityMgWeight = 1;
    private const int KnightMobilityEgWeight = 0;

    // --- Bishop mobility ---
    //
    //   Same idea as knight mobility but bishops typically cover more squares.
    //   Baseline is higher because an unblocked bishop should have ~7-9 squares.
    private const int BishopMobilityBaseline = 6;
    private const int BishopMobilityMgWeight = 3;
    private const int BishopMobilityEgWeight = 1;

    // --- Rook file bonuses ---
    //
    //   A rook is stronger when its file has no pawns blocking it.
    //
    //   Semi-open file: no FRIENDLY pawns on this file (enemy pawn may exist)
    //   Open file:      no pawns at all on this file
    //
    //   Visual:
    //     . . . . . . . .    ← no pawns on d-file
    //     . . . . . . . .
    //     . . . . . . . .
    //     . . . . . . . .    open file → rook gets RookOpenFileBonusMg
    //     . . . . . . . .
    //     . . . . . . . .
    //     . . . p . . . .    ← enemy pawn blocks: semi-open for White rook
    //     . . . R . . . .    ← rook here
    private const int RookSemiOpenFileBonusMg = 13;
    private const int RookSemiOpenFileBonusEg = 7;
    private const int RookOpenFileBonusMg = 45;
    private const int RookOpenFileBonusEg = 2;

    // --- Passed pawn bonuses ---
    //
    //   A passed pawn has no enemy pawns on its file or adjacent files
    //   that can stop it from promoting.
    //
    //   Indexed by ENGINE rank (0 = rank 8, 7 = rank 1):
    //
    //   Engine rank:  0    1    2    3    4    5    6    7
    //   Chess rank:   8    7    6    5    4    3    2    1
    //
    //   Visual (White pawn advancing up the board):
    //
    //     rank 8 [ 0] ← promotion square (impossible to be here)
    //     rank 7 [ 1] ← very close → huge bonus (+97 eg)
    //     rank 6 [ 2] ← close      → large bonus (+55 eg)
    //     rank 5 [ 3] ← halfway    → medium bonus (+36 eg)
    //     rank 4 [ 4] ← early      → small bonus  (+17 eg)
    //     rank 3 [ 5] ← starting   → tiny bonus   (+12 eg)
    //     rank 2 [ 6] ← impossible for passed pawn
    //     rank 1 [ 7] ← impossible
    //
    //   Black passed pawns use mirrored rank: rank = 7 - (square / 8)
    private static readonly int[] PassedPawnBonusMg = { 0, 15, 15, 17, 10, 6, 0, 0 };
    private static readonly int[] PassedPawnBonusEg = { 0, 97, 55, 36, 17, 12, 0, 0 };

    // --- Isolated pawn penalty ---
    //
    //   A pawn is isolated if it has no friendly pawns on either adjacent file.
    //   It cannot be defended by another pawn and is a long-term weakness.
    //
    //   Visual:
    //     . . . . . . . .
    //     . . . . . . . .
    //     . P . . . P . .   ← these pawns support each other
    //     . . . . . . . .
    //     . . . P . . . .   ← this pawn is isolated (no neighbors on c or e file)
    //     . . . . . . . .
    private const int IsolatedPawnPenaltyMg = -11;
    private const int IsolatedPawnPenaltyEg = -3;

    // --- King file exposure penalties (middlegame only) ---
    //
    //   A king is safer behind a wall of pawns.
    //   If the king's file (or adjacent files) have no friendly pawns,
    //   the king is exposed to rook / queen attacks along that file.
    //
    //   We check 3 files: the king's own file + the two adjacent files.
    //
    //   Visual (White king on g1, no pawns on f/g/h files):
    //
    //     . . . . . . . .
    //     . . . . . p p .   ← enemy pawns advancing
    //     . . . . . . . .
    //     . . . . . . . .
    //     . . . . . . . .
    //     . . . . . . . .
    //     . . . . . P . .   ← only f-pawn, g and h are open
    //     . . . . R . K .   ← king exposed on g and h files
    //
    //   Penalty breakdown for this king:
    //     own file g (open):      -54
    //     adjacent file f (semi): -12  (f-pawn exists)
    //     adjacent file h (open): -24
    //     total:                  -90
    private const int KingOwnOpenFilePenaltyMg = 54;
    private const int KingOwnSemiOpenFilePenaltyMg = 14;
    private const int KingAdjacentOpenFilePenaltyMg = 24;
    private const int KingAdjacentSemiOpenFilePenaltyMg = 12;

    // --- Knight outpost bonus (middlegame only) ---
    //
    //   A knight on an outpost square is:
    //     1. On rank 4, 5, or 6 (engine ranks 2–4 for White, 3–5 for Black)
    //     2. Supported by a friendly pawn (pawn attacks the knight's square)
    //     3. Cannot be chased away by an enemy pawn
    //
    //   An outpost knight is extremely stable and hard to dislodge.
    //
    //   Visual (White knight on d5):
    //
    //     . . . . . . . .
    //     . . . . . . . .
    //     . . x . x . . .   ← no Black pawns on c or e files ahead
    //     . . . N . . . .   ← knight on d5 (engine rank 3)
    //     . . . P . . . .   ← White pawn on d4 supports the knight
    //     . . . . . . . .
    //     . . . . . . . .
    //     . . . . . . . .
    private const int KnightOutpostBonusMg = 43;

    // =========================================================================
    // Piece-Square Tables (PeSTO)
    //
    //   Each piece type has a table of 64 values (one per square).
    //   The value reflects how good it is for that piece to be on that square.
    //
    //   Layout matches engine square mapping:
    //     index 0  = a8 (top-left)
    //     index 63 = h1 (bottom-right)
    //
    //   White reads the table directly:  MgPst[piece][square]
    //   Black reads the mirrored table:  MgPst[piece][square ^ 56]
    //
    //     square ^ 56 flips the rank:
    //       square 0  (a8) ↔ square 56 (a1)
    //       square 7  (h8) ↔ square 63 (h1)
    //
    //   This means both sides read "good squares" from their own perspective.
    // =========================================================================

    private static readonly int[][] MiddlegamePst =
    [
        // Pawn MG
        // Pawns on rank 7 (about to promote) get big bonuses.
        // Central pawns get bonuses for controlling the center.
        [
              0,   0,   0,   0,   0,   0,   0,   0,   // rank 8 (impossible)
             98, 134,  61,  95,  68, 126,  34, -11,   // rank 7 (about to promote)
             -6,   7,  26,  31,  65,  56,  25, -20,   // rank 6
            -14,  13,   6,  21,  23,  12,  17, -23,   // rank 5
            -27,  -2,  -5,  12,  17,   6,  10, -25,   // rank 4
            -26,  -4,  -4, -10,   3,   3,  33, -12,   // rank 3
            -35,  -1, -20, -23, -15,  24,  38, -22,   // rank 2 (starting rank)
              0,   0,   0,   0,   0,   0,   0,   0,   // rank 1 (impossible)
        ],
        // Knight MG
        // Knights are terrible on the rim and strong in the center.
        [
            -167, -89, -34, -49,  61, -97, -15, -107,  // rank 8 (corner = terrible)
             -73, -41,  72,  36,  23,  62,   7,  -17,
             -47,  60,  37,  65,  84, 129,  73,   44,
              -9,  17,  19,  53,  37,  69,  18,   22,
             -13,   4,  16,  13,  28,  19,  21,   -8,
             -23,  -9,  12,  10,  19,  17,  25,  -16,
             -29, -53, -12,  -3,  -1,  18, -14,  -19,
            -105, -21, -58, -33, -17, -28, -19,  -23,  // rank 1 (corner = terrible)
        ],
        // Bishop MG
        // Bishops like long diagonals and open positions.
        [
            -29,   4, -82, -37, -25, -42,   7,  -8,
            -26,  16, -18, -13,  30,  59,  18, -47,
            -16,  37,  43,  40,  35,  50,  37,  -2,
             -4,   5,  19,  50,  37,  37,   7,  -2,
             -6,  13,  13,  26,  34,  12,  10,   4,
              0,  15,  15,  15,  14,  27,  18,  10,
              4,  15,  16,   0,   7,  21,  33,   1,
            -33,  -3, -14, -21, -13, -12, -39, -21,
        ],
        // Rook MG
        // Rooks like open files and the 7th rank.
        [
             32,  42,  32,  51,  63,   9,  31,  43,
             27,  32,  58,  62,  80,  67,  26,  44,
             -5,  19,  26,  36,  17,  45,  61,  16,
            -24, -11,   7,  26,  24,  35,  -8, -20,
            -36, -26, -12,  -1,   9,  -7,   6, -23,
            -45, -25, -16, -17,   3,   0,  -5, -33,
            -44, -16, -20,  -9,  -1,  11,  -6, -71,
            -19, -13,   1,  17,  16,   7, -37, -26,
        ],
        // Queen MG
        // Queens are flexible. Slightly penalized for early development.
        [
            -28,   0,  29,  12,  59,  44,  43,  45,
            -24, -39,  -5,   1, -16,  57,  28,  54,
            -13, -17,   7,   8,  29,  56,  47,  57,
            -27, -27, -16, -16,  -1,  17,  -2,   1,
             -9, -26,  -9, -10,  -2,  -4,   3,  -3,
            -14,   2, -11,  -2,  -5,   2,  14,   5,
            -35,  -8,  11,   2,   8,  15,  -3,   1,
             -1, -18,  -9,  10, -15, -25, -31, -50,
        ],
        // King MG
        // King wants to castle and stay safe behind pawns in the middlegame.
        [
            -65,  23,  16, -15, -56, -34,   2,  13,
             29,  -1, -20,  -7,  -8,  -4, -38, -29,
             -9,  24,   2, -16, -20,   6,  22, -22,
            -17, -20, -12, -27, -30, -25, -14, -36,
            -49,  -1, -27, -39, -46, -44, -33, -51,
            -14, -14, -22, -46, -44, -30, -15, -27,
              1,   7,  -8, -64, -43, -16,   9,   8,
            -15,  36,  12, -54,   8, -28,  24,  14,  // rank 1: castled positions get bonus
        ],
    ];

    private static readonly int[][] EndgamePst =
    [
        // Pawn EG
        // Passed pawns near promotion are extremely valuable in the endgame.
        [
              0,   0,   0,   0,   0,   0,   0,   0,   // rank 8
            178, 173, 158, 134, 147, 132, 165, 187,   // rank 7 ← massive bonus
             94, 100,  85,  67,  56,  53,  82,  84,   // rank 6
             32,  24,  13,   5,  -2,   4,  17,  17,   // rank 5
             13,   9,  -3,  -7,  -7,  -8,   3,  -1,   // rank 4
              4,   7,  -6,   1,   0,  -5,  -1,  -8,   // rank 3
             13,   8,   8,  10,  13,   0,   2,  -7,   // rank 2
              0,   0,   0,   0,   0,   0,   0,   0,   // rank 1
        ],
        // Knight EG
        // Knights prefer the center even in the endgame (but less extreme).
        [
             -58, -38, -13, -28, -31, -27, -63, -99,
             -25,  -8, -25,  -2,  -9, -25, -24, -52,
             -24, -20,  10,   9,  -1,  -9, -19, -41,
             -17,   3,  22,  22,  22,  11,   8, -18,
             -18,  -6,  16,  25,  16,  17,   4, -18,
             -23,  -3,  -1,  15,  10,  -3, -20, -22,
             -42, -20, -10,  -5,  -2, -20, -23, -44,
             -29, -51, -23, -15, -22, -18, -50, -64,
        ],
        // Bishop EG
        [
            -14, -21, -11,  -8,  -7,  -9, -17, -24,
             -8,  -4,   7, -12,  -3, -13,  -4, -14,
              2,  -8,   0,  -1,  -2,   6,   0,   4,
             -3,   9,  12,   9,  14,  10,   3,   2,
             -6,   3,  13,  19,   7,  10,  -3,  -9,
            -12,  -3,   8,  10,  13,   3,  -7, -15,
            -14, -18,  -7,  -1,   4,  -9, -15, -27,
            -23,  -9, -23,  -5,  -9, -16,  -5, -17,
        ],
        // Rook EG
        [
             13,  10,  18,  15,  12,  12,   8,   5,
             11,  13,  13,  11,  -3,   3,   8,   3,
              7,   7,   7,   5,   4,  -3,  -5,  -3,
              4,   3,  13,   1,   2,   1,  -1,   2,
              3,   5,   8,   4,  -5,  -6,  -8, -11,
             -4,   0,  -5,  -1,  -7, -12,  -8, -16,
             -6,  -6,   0,   2,  -9,  -9, -11,  -3,
             -9,   2,   3,  -1,  -5, -13,   4, -20,
        ],
        // Queen EG
        [
             -9,  22,  22,  27,  27,  19,  10,  20,
            -17,  20,  32,  41,  58,  25,  30,   0,
            -20,   6,   9,  49,  47,  35,  19,   9,
              3,  22,  24,  45,  57,  40,  57,  36,
            -18,  28,  19,  47,  31,  34,  39,  23,
            -16, -27,  15,   6,   9,  17,  10,   5,
            -22, -23, -30, -16, -16, -23, -36, -32,
            -33, -28, -22, -43,  -5, -32, -20, -41,
        ],
        // King EG
        // In the endgame the king becomes an active piece and moves to the center.
        [
            -74, -35, -18, -18, -11,  15,   4, -17,
            -12,  17,  14,  17,  17,  38,  23,  11,
             10,  17,  23,  15,  20,  45,  44,  13,
             -8,  22,  24,  27,  26,  33,  26,   3,
            -18,  -4,  21,  24,  27,  23,   9, -11,
            -19,  -3,  11,  21,  23,  16,   7,  -9,
            -27, -11,   4,  13,  14,   4,  -5, -17,
            -53, -34, -21, -11, -28, -14, -24, -43,
        ],
    ];

    // =========================================================================
    // Precomputed Lookup Tables
    //
    //   Built once at startup in the static constructor.
    //   Never modified after initialization.
    // =========================================================================

    // Combined material + PST score per piece per square.
    // MiddlegameScores[piece, square] = MiddlegameMaterial[type] + MiddlegamePst[type][square]
    // Covers all 12 piece indices (White 0–5, Black 6–11).
    private static readonly int[,] MiddlegameScores = new int[PieceCount, SquareCount];
    private static readonly int[,] EndgameScores = new int[PieceCount, SquareCount];

    // FileMasks[f] = bitboard with all squares on file f set.
    //
    //   FileMasks[0] = a-file = squares 0,8,16,24,32,40,48,56
    //   FileMasks[7] = h-file = squares 7,15,23,31,39,47,55,63
    private static readonly ulong[] FileMasks = new ulong[8];

    // AdjacentFileMasks[f] = bitboard of squares on the files immediately
    // left and right of file f.
    //
    //   AdjacentFileMasks[3] (d-file) = c-file | e-file
    //   Used to detect isolated pawns (no friendly pawn on adjacent files).
    private static readonly ulong[] AdjacentFileMasks = new ulong[8];

    // WhitePassedPawnMasks[sq] = squares on same + adjacent files, all ranks
    // ABOVE sq (closer to rank 8).
    //
    //   If (WhitePassedPawnMasks[sq] & blackPawns) == 0, the White pawn on sq
    //   is passed — no Black pawn can stop it.
    //
    //   Visual for White pawn on d4 (sq=35):
    //
    //     X X X . . . . .   rank 8  ← all these squares are in the mask
    //     . . X X X . . .
    //     . . X X X . . .
    //     . . X X X . . .   rank 5
    //     . . . P . . . .   rank 4  ← pawn here (sq=35)
    //     (ranks below not included)
    private static readonly ulong[] WhitePassedPawnMasks = new ulong[SquareCount];
    private static readonly ulong[] BlackPassedPawnMasks = new ulong[SquareCount];

    // WhiteOutpostMasks[sq] = squares on ADJACENT files only, all ranks above sq.
    //
    //   Used to check if an enemy pawn can still advance to challenge the outpost.
    //   (Same-file pawns can't attack diagonally, so we exclude the same file.)
    //
    //   Visual for White knight on d5 (sq=27):
    //
    //     . . X . X . . .   rank 8  ← adjacent file squares ahead
    //     . . X . X . . .   rank 7
    //     . . X . X . . .   rank 6
    //     . . . N . . . .   rank 5  ← knight here
    //     (ranks below not included)
    //
    //   If a Black pawn is anywhere in this mask, it could eventually
    //   advance and chase the knight off the outpost.
    private static readonly ulong[] WhiteOutpostMasks = new ulong[SquareCount];
    private static readonly ulong[] BlackOutpostMasks = new ulong[SquareCount];

    // =========================================================================
    // Static Constructor — runs once when the class is first used
    // =========================================================================
    static Evaluation()
    {
        InitializeFileMasks();
        InitializeAdjacentFileMasks();
        InitializePassedPawnMasks();
        InitializeOutpostMasks();
        InitializePieceSquareTables();
    }

    // =========================================================================
    // Public Evaluation Entry Points
    // =========================================================================

    /// <summary>
    /// Returns the position score from White's perspective.
    ///   Positive = White is better.
    ///   Negative = Black is better.
    ///
    /// Use this for debugging, logging, and test output.
    /// Do NOT use this inside the search (use EvaluateForSearch instead).
    /// </summary>
    public static int Evaluate()
    {
        int middlegameScore = 0;
        int endgameScore = 0;
        int phase = 0;

        ScoreMaterialAndPieceSquares(ref middlegameScore, ref endgameScore, ref phase);
        ScoreBishopPairs(ref middlegameScore, ref endgameScore);
        ScorePassedPawns(ref middlegameScore, ref endgameScore);
        ScoreIsolatedPawns(ref middlegameScore, ref endgameScore);
        ScoreMobility(ref middlegameScore, ref endgameScore);
        ScoreRookFiles(ref middlegameScore, ref endgameScore);
        ScoreKingExposure(ref middlegameScore);
        ScoreKnightOutposts(ref middlegameScore);

        // Taper between middlegame and endgame scores based on remaining material.
        //
        //   finalScore = (mgScore * mgPhase + egScore * egPhase) / 24
        //
        //   Early game (phase=24): result ≈ mgScore
        //   Late game  (phase= 0): result ≈ egScore
        int middlegamePhase = Math.Min(phase, TotalPhase);
        int endgamePhase = TotalPhase - middlegamePhase;

        return (middlegameScore * middlegamePhase + endgameScore * endgamePhase) / TotalPhase;
    }

    /// <summary>
    /// Returns the position score from the side-to-move's perspective.
    ///   Positive = side to move is better.
    ///   Negative = side to move is worse.
    ///
    /// This is the negamax convention required by alpha-beta search.
    /// Use this inside AlphaBeta() and Quiescence().
    /// </summary>
    public static int EvaluateForSearch()
    {
        int whiteScore = Evaluate();
        return side == White ? whiteScore : -whiteScore;
    }

    /// <summary>
    /// Converts a search (negamax) score back to White perspective for display.
    ///
    /// Call this when printing UCI "info score" lines.
    ///
    /// Example:
    ///   rootSide = Black, score = +300 (Black is better by 3 pawns)
    ///   → ToWhitePerspective(300, Black) = -300 (White is worse by 3 pawns)
    /// </summary>
    public static int ToWhitePerspective(int searchScore, int rootSide)
    {
        return rootSide == White ? searchScore : -searchScore;
    }

    // =========================================================================
    // Material + Piece-Square Table Scoring
    // =========================================================================

    /// <summary>
    /// Iterates over all White and Black pieces.
    /// For each piece on each square, adds its combined material + PST score.
    /// Also accumulates the game phase counter.
    /// </summary>
    private static void ScoreMaterialAndPieceSquares(ref int middlegameScore, ref int endgameScore, ref int phase)
    {
        // White pieces contribute positively (+1), Black pieces negatively (-1)
        ScorePieceRange(P, K, +1, ref middlegameScore, ref endgameScore, ref phase);
        ScorePieceRange(p, k, -1, ref middlegameScore, ref endgameScore, ref phase);
    }

    /// <summary>
    /// Loops over a range of piece indices [firstPiece..lastPiece].
    /// For each piece on the board, adds its precomputed MG+EG score
    /// multiplied by sign (+1 for White, -1 for Black).
    /// </summary>
    private static void ScorePieceRange(int firstPiece, int lastPiece, int sign,
        ref int middlegameScore, ref int endgameScore, ref int phase)
    {
        for (int piece = firstPiece; piece <= lastPiece; piece++)
        {
            ulong pieces = bitboards[piece];

            while (pieces != 0)
            {
                int square = GetLs1bIndex(pieces);

                middlegameScore += sign * MiddlegameScores[piece, square];
                endgameScore += sign * EndgameScores[piece, square];
                phase += PhaseWeights[piece];

                PopBit(ref pieces, square);
            }
        }
    }

    // =========================================================================
    // Bishop Pair
    // =========================================================================

    /// <summary>
    /// Adds a bonus if a side has two bishops.
    /// The bonus is larger in the endgame where open positions favor bishops.
    /// </summary>
    private static void ScoreBishopPairs(ref int middlegameScore, ref int endgameScore)
    {
        if (CountBits(bitboards[B]) >= 2)
        {
            middlegameScore += BishopPairBonusMg;
            endgameScore += BishopPairBonusEg;
        }

        if (CountBits(bitboards[b]) >= 2)
        {
            middlegameScore -= BishopPairBonusMg;
            endgameScore -= BishopPairBonusEg;
        }
    }

    // =========================================================================
    // Passed Pawns
    // =========================================================================

    /// <summary>
    /// Adds a bonus for each passed pawn (no enemy pawn can stop it from promoting).
    /// Bonus increases the closer the pawn is to promotion.
    ///
    /// White passed pawn: checks WhitePassedPawnMasks[sq] against black pawns.
    /// Black passed pawn: checks BlackPassedPawnMasks[sq] against white pawns.
    ///                    Uses mirrored rank (7 - rank) so rank 1 = best for Black.
    /// </summary>
    private static void ScorePassedPawns(ref int middlegameScore, ref int endgameScore)
    {
        ulong whitePawns = bitboards[P];
        ulong blackPawns = bitboards[p];

        ulong whitePawnLoop = whitePawns;
        while (whitePawnLoop != 0)
        {
            int square = GetLs1bIndex(whitePawnLoop);

            if ((WhitePassedPawnMasks[square] & blackPawns) == 0)
            {
                // Engine rank 0 = rank 8, rank 1 = rank 7 (closest to promotion)
                int rank = square / 8;
                middlegameScore += PassedPawnBonusMg[rank];
                endgameScore += PassedPawnBonusEg[rank];
            }

            PopBit(ref whitePawnLoop, square);
        }

        ulong blackPawnLoop = blackPawns;
        while (blackPawnLoop != 0)
        {
            int square = GetLs1bIndex(blackPawnLoop);

            if ((BlackPassedPawnMasks[square] & whitePawns) == 0)
            {
                // Mirror the rank so Black's rank 1 (engine rank 7) maps to index 0
                int rank = 7 - (square / 8);
                middlegameScore -= PassedPawnBonusMg[rank];
                endgameScore -= PassedPawnBonusEg[rank];
            }

            PopBit(ref blackPawnLoop, square);
        }
    }

    // =========================================================================
    // Isolated Pawns
    // =========================================================================

    /// <summary>
    /// Applies a penalty for each isolated pawn (no friendly pawn on either adjacent file).
    /// Isolated pawns are permanent weaknesses that cannot be defended by other pawns.
    /// </summary>
    private static void ScoreIsolatedPawns(ref int middlegameScore, ref int endgameScore)
    {
        ulong whitePawns = bitboards[P];
        ulong blackPawns = bitboards[p];

        ulong whitePawnLoop = whitePawns;
        while (whitePawnLoop != 0)
        {
            int square = GetLs1bIndex(whitePawnLoop);

            // No White pawn on either adjacent file → isolated
            if ((AdjacentFileMasks[square % 8] & whitePawns) == 0)
            {
                middlegameScore += IsolatedPawnPenaltyMg; // negative constant
                endgameScore += IsolatedPawnPenaltyEg;
            }

            PopBit(ref whitePawnLoop, square);
        }

        ulong blackPawnLoop = blackPawns;
        while (blackPawnLoop != 0)
        {
            int square = GetLs1bIndex(blackPawnLoop);

            if ((AdjacentFileMasks[square % 8] & blackPawns) == 0)
            {
                middlegameScore -= IsolatedPawnPenaltyMg; // subtract negative = add penalty for Black
                endgameScore -= IsolatedPawnPenaltyEg;
            }

            PopBit(ref blackPawnLoop, square);
        }
    }

    // =========================================================================
    // Mobility
    // =========================================================================

    /// <summary>
    /// Scores knight and bishop mobility.
    ///
    /// For each piece, counts the number of squares it can safely move to
    /// (not occupied by a friendly piece), then compares against a baseline.
    ///
    ///   mobility = reachableSquares - baseline
    ///   score   += mobility * weight
    ///
    /// A piece with many reachable squares gets a bonus.
    /// A piece with fewer reachable squares than the baseline gets a penalty.
    /// </summary>
    private static void ScoreMobility(ref int middlegameScore, ref int endgameScore)
    {
        ulong whiteOccupancy = occupancies[White];
        ulong blackOccupancy = occupancies[Black];
        ulong allOccupancy = occupancies[Both];

        // --- White knights ---
        ulong whiteKnights = bitboards[N];
        while (whiteKnights != 0)
        {
            int square = GetLs1bIndex(whiteKnights);
            int mobility = CountBits(knightAttacks[square] & ~whiteOccupancy) - KnightMobilityBaseline;

            middlegameScore += mobility * KnightMobilityMgWeight;
            endgameScore += mobility * KnightMobilityEgWeight;

            PopBit(ref whiteKnights, square);
        }

        // --- Black knights ---
        ulong blackKnights = bitboards[n];
        while (blackKnights != 0)
        {
            int square = GetLs1bIndex(blackKnights);
            int mobility = CountBits(knightAttacks[square] & ~blackOccupancy) - KnightMobilityBaseline;

            middlegameScore -= mobility * KnightMobilityMgWeight;
            endgameScore -= mobility * KnightMobilityEgWeight;

            PopBit(ref blackKnights, square);
        }

        // --- White bishops ---
        ulong whiteBishops = bitboards[B];
        while (whiteBishops != 0)
        {
            int square = GetLs1bIndex(whiteBishops);
            int mobility = CountBits(GetBishopAttacks(square, allOccupancy) & ~whiteOccupancy) - BishopMobilityBaseline;

            middlegameScore += mobility * BishopMobilityMgWeight;
            endgameScore += mobility * BishopMobilityEgWeight;

            PopBit(ref whiteBishops, square);
        }

        // --- Black bishops ---
        ulong blackBishops = bitboards[b];
        while (blackBishops != 0)
        {
            int square = GetLs1bIndex(blackBishops);
            int mobility = CountBits(GetBishopAttacks(square, allOccupancy) & ~blackOccupancy) - BishopMobilityBaseline;

            middlegameScore -= mobility * BishopMobilityMgWeight;
            endgameScore -= mobility * BishopMobilityEgWeight;

            PopBit(ref blackBishops, square);
        }
    }

    // =========================================================================
    // Rook File Bonuses
    // =========================================================================

    /// <summary>
    /// Gives rooks a bonus for standing on open or semi-open files.
    ///
    ///   Open file:      no pawns at all on the rook's file
    ///   Semi-open file: no FRIENDLY pawns (but enemy pawn may exist)
    ///
    /// If there is a friendly pawn on the file, no bonus is given.
    /// </summary>
    private static void ScoreRookFiles(ref int middlegameScore, ref int endgameScore)
    {
        ulong whitePawns = bitboards[P];
        ulong blackPawns = bitboards[p];
        ulong allPawns = whitePawns | blackPawns;

        ulong whiteRooks = bitboards[R];
        while (whiteRooks != 0)
        {
            int square = GetLs1bIndex(whiteRooks);
            AddRookFileBonus(square % 8, whitePawns, allPawns, +1, ref middlegameScore, ref endgameScore);
            PopBit(ref whiteRooks, square);
        }

        ulong blackRooks = bitboards[r];
        while (blackRooks != 0)
        {
            int square = GetLs1bIndex(blackRooks);
            AddRookFileBonus(square % 8, blackPawns, allPawns, -1, ref middlegameScore, ref endgameScore);
            PopBit(ref blackRooks, square);
        }
    }

    /// <summary>
    /// Applies the open or semi-open file bonus for a single rook on the given file.
    /// sign = +1 for White, -1 for Black.
    /// </summary>
    private static void AddRookFileBonus(int file, ulong friendlyPawns, ulong allPawns,
        int sign, ref int middlegameScore, ref int endgameScore)
    {
        ulong fileMask = FileMasks[file];

        // Friendly pawn on this file → rook is blocked, no bonus
        if ((friendlyPawns & fileMask) != 0)
            return;

        if ((allPawns & fileMask) == 0)
        {
            // No pawns at all → fully open file
            middlegameScore += sign * RookOpenFileBonusMg;
            endgameScore += sign * RookOpenFileBonusEg;
        }
        else
        {
            // Enemy pawn present but no friendly pawn → semi-open file
            middlegameScore += sign * RookSemiOpenFileBonusMg;
            endgameScore += sign * RookSemiOpenFileBonusEg;
        }
    }

    // =========================================================================
    // King Exposure (Middlegame Only)
    // =========================================================================

    /// <summary>
    /// Penalizes kings that lack pawn shelter in the middlegame.
    ///
    /// Checks 3 files: the king's own file + the two adjacent files.
    /// For each file without a friendly pawn, applies a penalty based on
    /// whether the file is fully open (no pawns) or semi-open (enemy pawn only).
    ///
    /// A bad-shelter king for White subtracts from the score (hurts White).
    /// A bad-shelter king for Black adds to the score (hurts Black = helps White).
    /// </summary>
    private static void ScoreKingExposure(ref int middlegameScore)
    {
        ulong whitePawns = bitboards[P];
        ulong blackPawns = bitboards[p];
        ulong allPawns = whitePawns | blackPawns;

        int whiteKingFile = GetLs1bIndex(bitboards[K]) % 8;
        int blackKingFile = GetLs1bIndex(bitboards[k]) % 8;

        // --- White king shelter ---
        // sign = -1: penalty hurts White (subtracts from score)
        AddKingFilePenalty(whiteKingFile, whitePawns, allPawns, -1,
            KingOwnOpenFilePenaltyMg, KingOwnSemiOpenFilePenaltyMg, ref middlegameScore);

        if (whiteKingFile > 0)
            AddKingFilePenalty(whiteKingFile - 1, whitePawns, allPawns, -1,
                KingAdjacentOpenFilePenaltyMg, KingAdjacentSemiOpenFilePenaltyMg, ref middlegameScore);

        if (whiteKingFile < 7)
            AddKingFilePenalty(whiteKingFile + 1, whitePawns, allPawns, -1,
                KingAdjacentOpenFilePenaltyMg, KingAdjacentSemiOpenFilePenaltyMg, ref middlegameScore);

        // --- Black king shelter ---
        // sign = +1: Black's penalty helps White (adds to score)
        AddKingFilePenalty(blackKingFile, blackPawns, allPawns, +1,
            KingOwnOpenFilePenaltyMg, KingOwnSemiOpenFilePenaltyMg, ref middlegameScore);

        if (blackKingFile > 0)
            AddKingFilePenalty(blackKingFile - 1, blackPawns, allPawns, +1,
                KingAdjacentOpenFilePenaltyMg, KingAdjacentSemiOpenFilePenaltyMg, ref middlegameScore);

        if (blackKingFile < 7)
            AddKingFilePenalty(blackKingFile + 1, blackPawns, allPawns, +1,
                KingAdjacentOpenFilePenaltyMg, KingAdjacentSemiOpenFilePenaltyMg, ref middlegameScore);
    }

    /// <summary>
    /// Applies an open-file or semi-open-file penalty to the king on the given file.
    /// Only applies if there is no friendly pawn on that file.
    /// sign = -1 for White king (penalty hurts White), +1 for Black king (helps White).
    /// </summary>
    private static void AddKingFilePenalty(int file, ulong friendlyPawns, ulong allPawns,
        int sign, int openPenalty, int semiOpenPenalty, ref int middlegameScore)
    {
        ulong fileMask = FileMasks[file];

        // Friendly pawn present → king has shelter on this file, no penalty
        if ((friendlyPawns & fileMask) != 0)
            return;

        // Open = no pawns at all; semi-open = enemy pawn only
        middlegameScore += sign * (((allPawns & fileMask) == 0) ? openPenalty : semiOpenPenalty);
    }

    // =========================================================================
    // Knight Outposts (Middlegame Only)
    // =========================================================================

    /// <summary>
    /// Gives a bonus for knights placed on outpost squares.
    ///
    /// An outpost knight must be:
    ///   1. On engine ranks 2–4 for White (chess ranks 4–6), ranks 3–5 for Black
    ///   2. Supported by a friendly pawn (the pawn attacks that square)
    ///   3. Safe from being chased by an enemy pawn
    ///      (no enemy pawn exists that could advance to challenge the outpost)
    ///
    /// Supporting pawn check:
    ///   pawnAttacks[Black, sq] gives squares a Black pawn on sq would attack.
    ///   Those are also the squares where a White pawn would need to be to
    ///   attack sq from below — i.e., to support a White piece on sq.
    ///
    ///   So: (pawnAttacks[Black, sq] & whitePawns) != 0
    ///   means a White pawn is diagonally behind the knight → it supports it.
    ///
    /// Chasing check:
    ///   WhiteOutpostMasks[sq] = squares on adjacent files ahead of sq.
    ///   If a Black pawn is on any of those squares, it could advance and
    ///   attack the outpost. If none exist → outpost is safe.
    /// </summary>
    private static void ScoreKnightOutposts(ref int middlegameScore)
    {
        ulong whitePawns = bitboards[P];
        ulong blackPawns = bitboards[p];

        // --- White knights ---
        ulong whiteKnights = bitboards[N];
        while (whiteKnights != 0)
        {
            int square = GetLs1bIndex(whiteKnights);
            int rank = square / 8;

            // Engine ranks 2, 3, 4 = chess ranks 6, 5, 4
            bool onOutpostRank = rank >= 2 && rank <= 4;
            bool supportedByPawn = (pawnAttacks[Black, square] & whitePawns) != 0;
            bool cannotBeChasedByPawn = (WhiteOutpostMasks[square] & blackPawns) == 0;

            if (onOutpostRank && supportedByPawn && cannotBeChasedByPawn)
                middlegameScore += KnightOutpostBonusMg;

            PopBit(ref whiteKnights, square);
        }

        // --- Black knights ---
        ulong blackKnights = bitboards[n];
        while (blackKnights != 0)
        {
            int square = GetLs1bIndex(blackKnights);
            int rank = square / 8;

            // Engine ranks 3, 4, 5 = chess ranks 5, 4, 3
            bool onOutpostRank = rank >= 3 && rank <= 5;
            bool supportedByPawn = (pawnAttacks[White, square] & blackPawns) != 0;
            bool cannotBeChasedByPawn = (BlackOutpostMasks[square] & whitePawns) == 0;

            if (onOutpostRank && supportedByPawn && cannotBeChasedByPawn)
                middlegameScore -= KnightOutpostBonusMg;

            PopBit(ref blackKnights, square);
        }
    }

    // =========================================================================
    // Initialization Methods
    // =========================================================================

    /// <summary>
    /// Fills FileMasks[f] with a bitboard covering all 8 squares on file f.
    /// File 0 = a-file, file 7 = h-file.
    /// </summary>
    private static void InitializeFileMasks()
    {
        for (int file = 0; file < 8; file++)
            for (int rank = 0; rank < 8; rank++)
                FileMasks[file] |= 1UL << (rank * 8 + file);
    }

    /// <summary>
    /// Fills AdjacentFileMasks[f] with a bitboard covering the files
    /// immediately left and right of file f.
    ///
    ///   f=0 (a): only b-file
    ///   f=3 (d): c-file + e-file
    ///   f=7 (h): only g-file
    /// </summary>
    private static void InitializeAdjacentFileMasks()
    {
        for (int file = 0; file < 8; file++)
        {
            if (file > 0) AdjacentFileMasks[file] |= FileMasks[file - 1];
            if (file < 7) AdjacentFileMasks[file] |= FileMasks[file + 1];
        }
    }

    /// <summary>
    /// Builds passed pawn masks for every square.
    ///
    ///   WhitePassedPawnMasks[sq] = (same file | left file | right file) ∩ ranks above sq
    ///   BlackPassedPawnMasks[sq] = (same file | left file | right file) ∩ ranks below sq
    ///
    ///   "Ranks above" = smaller rank indices (closer to rank 8 in engine layout).
    ///   "Ranks below" = larger rank indices (closer to rank 1).
    /// </summary>
    private static void InitializePassedPawnMasks()
    {
        for (int square = 0; square < 64; square++)
        {
            int file = square % 8;
            int rank = square / 8;

            ulong relevantFiles = FileMasks[file] | AdjacentFileMasks[file];

            // Build "ranks above" mask: all squares with rank index < current rank
            ulong whiteAheadMask = 0;
            for (int r = 0; r < rank; r++)
                for (int f = 0; f < 8; f++)
                    whiteAheadMask |= 1UL << (r * 8 + f);

            // Build "ranks below" mask: all squares with rank index > current rank
            ulong blackAheadMask = 0;
            for (int r = rank + 1; r < 8; r++)
                for (int f = 0; f < 8; f++)
                    blackAheadMask |= 1UL << (r * 8 + f);

            WhitePassedPawnMasks[square] = relevantFiles & whiteAheadMask;
            BlackPassedPawnMasks[square] = relevantFiles & blackAheadMask;
        }
    }

    /// <summary>
    /// Builds outpost masks for every square.
    ///
    ///   WhiteOutpostMasks[sq] = ADJACENT files only ∩ ranks above sq
    ///   BlackOutpostMasks[sq] = ADJACENT files only ∩ ranks below sq
    ///
    ///   Excludes the same file because pawns attack diagonally, not straight ahead.
    ///   An enemy pawn on the same file cannot capture a piece on sq.
    /// </summary>
    private static void InitializeOutpostMasks()
    {
        for (int square = 0; square < 64; square++)
        {
            int file = square % 8;
            int rank = square / 8;

            ulong whiteMask = 0;
            ulong blackMask = 0;

            // Left adjacent file
            if (file > 0)
            {
                for (int r = 0; r < rank; r++)
                    whiteMask |= 1UL << (r * 8 + file - 1);

                for (int r = rank + 1; r < 8; r++)
                    blackMask |= 1UL << (r * 8 + file - 1);
            }

            // Right adjacent file
            if (file < 7)
            {
                for (int r = 0; r < rank; r++)
                    whiteMask |= 1UL << (r * 8 + file + 1);

                for (int r = rank + 1; r < 8; r++)
                    blackMask |= 1UL << (r * 8 + file + 1);
            }

            WhiteOutpostMasks[square] = whiteMask;
            BlackOutpostMasks[square] = blackMask;
        }
    }

    /// <summary>
    /// Combines material values and PST values into single lookup arrays.
    ///
    ///   MiddlegameScores[piece, square] = MgMaterial[type] + MgPst[type][square]
    ///   EndgameScores[piece, square]    = EgMaterial[type] + EgPst[type][square]
    ///
    /// White pieces (0–5) read the PST directly.
    /// Black pieces (6–11) read the PST mirrored vertically (square ^ 56).
    ///
    ///   square ^ 56 flips rank:  a8(0) ↔ a1(56),  h8(7) ↔ h1(63)
    ///   This means both sides see their "good squares" at their own end.
    /// </summary>
    private static void InitializePieceSquareTables()
    {
        for (int pieceType = 0; pieceType < PieceTypeCount; pieceType++)
        {
            for (int square = 0; square < SquareCount; square++)
            {
                int mirroredSquare = square ^ 56;

                // White pieces read PST normally
                MiddlegameScores[pieceType, square] =
                    MiddlegameMaterial[pieceType] + MiddlegamePst[pieceType][square];
                EndgameScores[pieceType, square] =
                    EndgameMaterial[pieceType] + EndgamePst[pieceType][square];

                // Black pieces read the PST mirrored (so their rank 1 maps to PST rank 1)
                MiddlegameScores[pieceType + 6, square] =
                    MiddlegameMaterial[pieceType] + MiddlegamePst[pieceType][mirroredSquare];
                EndgameScores[pieceType + 6, square] =
                    EndgameMaterial[pieceType] + EndgamePst[pieceType][mirroredSquare];
            }
        }
    }
}