using static Board;
using static PieceAttacks;
using static MoveGenerator;

/// <summary>
/// Tapered evaluation with PeSTO piece-square tables and positional features.
///
/// Board square mapping:
///
///     a8=0  b8=1  c8=2  d8=3  e8=4  f8=5  g8=6  h8=7
///     a7=8  b7=9  ...                             h7=15
///     ...
///     a2=48 ...                                   h2=55
///     a1=56 b1=57 c1=58 d1=59 e1=60 f1=61 g1=62 h1=63
///
///     rank = square / 8    (0 = rank 8, 7 = rank 1)
///     file = square % 8    (0 = a-file, 7 = h-file)
///
/// Score convention:
///     Positive = White is better.
///     Negated at the end if Black is to move.
///
/// Proven features (kept):
///     ✓ Material + PST (tapered)
///     ✓ Bishop pair
///     ✓ Passed pawns
///     ✓ Isolated pawns
///     ✓ Knight + bishop mobility
///     ✓ Rook open / semi-open files
///     ✓ King open-file penalty
///     ✓ Knight outposts
///
/// Tested and rejected:
///     ✗ Doubled pawns
///     ✗ Rook on 7th rank
///     ✗ Bishop outposts
///     ✗ Connected passed pawns
///     ✗ Pawn shield king safety
/// </summary>
public static class Evaluation
{
    private const int PawnMgAdjust = 0;
    private const int PawnEgAdjust = 0;
    private const int KnightMgAdjust = 0;
    private const int KnightEgAdjust = 0;
    private const int BishopMgAdjust = 0;
    private const int BishopEgAdjust = 0;
    private const int RookMgAdjust = 0;
    private const int RookEgAdjust = 0;
    private const int QueenMgAdjust = 0;
    private const int QueenEgAdjust = 0;

    public static EvalWeights GetCurrentWeights()
    {
        return new EvalWeights
        {
            PawnMgAdjust = PawnMgAdjust,
            PawnEgAdjust = PawnEgAdjust,
            KnightMgAdjust = KnightMgAdjust,
            KnightEgAdjust = KnightEgAdjust,
            BishopMgAdjust = BishopMgAdjust,
            BishopEgAdjust = BishopEgAdjust,
            RookMgAdjust = RookMgAdjust,
            RookEgAdjust = RookEgAdjust,
            QueenMgAdjust = QueenMgAdjust,
            QueenEgAdjust = QueenEgAdjust,

            BishopPairMg = BishopPairMg,
            BishopPairEg = BishopPairEg,

            KnightMobMg = KnightMobMg,
            KnightMobEg = KnightMobEg,
            BishopMobMg = BishopMobMg,
            BishopMobEg = BishopMobEg,

            RookSemiOpenMg = RookSemiOpenMg,
            RookSemiOpenEg = RookSemiOpenEg,
            RookOpenMg = RookOpenMg,
            RookOpenEg = RookOpenEg,

            PassedMg = (int[])PassedMg.Clone(),
            PassedEg = (int[])PassedEg.Clone(),

            IsolatedMg = IsolatedMg,
            IsolatedEg = IsolatedEg,

            KingOwnOpenMg = KingOwnOpenMg,
            KingOwnSemiOpenMg = KingOwnSemiOpenMg,
            KingAdjacentOpenMg = KingAdjacentOpenMg,
            KingAdjacentSemiOpenMg = KingAdjacentSemiOpenMg,

            KnightOutpostMg = KnightOutpostMg
        };
    }

    public static int EvaluateWhitePerspective(EvalFeatures f, EvalWeights w)
    {
        int mg = f.FixedMg;
        int eg = f.FixedEg;

        mg += f.PawnCountBalance * w.PawnMgAdjust;
        eg += f.PawnCountBalance * w.PawnEgAdjust;

        mg += f.KnightCountBalance * w.KnightMgAdjust;
        eg += f.KnightCountBalance * w.KnightEgAdjust;

        mg += f.BishopCountBalance * w.BishopMgAdjust;
        eg += f.BishopCountBalance * w.BishopEgAdjust;

        mg += f.RookCountBalance * w.RookMgAdjust;
        eg += f.RookCountBalance * w.RookEgAdjust;

        mg += f.QueenCountBalance * w.QueenMgAdjust;
        eg += f.QueenCountBalance * w.QueenEgAdjust;

        mg += f.BishopPairBalance * w.BishopPairMg;
        eg += f.BishopPairBalance * w.BishopPairEg;

        mg += f.KnightMobilityBalance * w.KnightMobMg;
        eg += f.KnightMobilityBalance * w.KnightMobEg;

        mg += f.BishopMobilityBalance * w.BishopMobMg;
        eg += f.BishopMobilityBalance * w.BishopMobEg;

        mg += f.RookSemiOpenBalance * w.RookSemiOpenMg;
        eg += f.RookSemiOpenBalance * w.RookSemiOpenEg;

        mg += f.RookOpenBalance * w.RookOpenMg;
        eg += f.RookOpenBalance * w.RookOpenEg;

        mg += f.IsolatedPawnBalance * w.IsolatedMg;
        eg += f.IsolatedPawnBalance * w.IsolatedEg;

        mg += f.KingOwnOpenBalance * w.KingOwnOpenMg;
        mg += f.KingOwnSemiOpenBalance * w.KingOwnSemiOpenMg;
        mg += f.KingAdjacentOpenBalance * w.KingAdjacentOpenMg;
        mg += f.KingAdjacentSemiOpenBalance * w.KingAdjacentSemiOpenMg;

        mg += f.KnightOutpostBalance * w.KnightOutpostMg;

        int mgPhase = Math.Min(f.Phase, TotalPhase);
        int egPhase = TotalPhase - mgPhase;

        return (mg * mgPhase + eg * egPhase) / TotalPhase;
    }

    public static EvalFeatures ExtractFeatures()
    {
        EvalFeatures f = new();

        // ------------------------------------------------
        // Fixed base: material + PST only
        // ------------------------------------------------
        ExtractFixedBase(P, K, +1, f);
        ExtractFixedBase(p, k, -1, f);

        f.PawnCountBalance =
            BitboardOperations.CountBits(bitboards[P]) -
            BitboardOperations.CountBits(bitboards[p]);

        f.KnightCountBalance =
            BitboardOperations.CountBits(bitboards[N]) -
            BitboardOperations.CountBits(bitboards[n]);

        f.BishopCountBalance =
            BitboardOperations.CountBits(bitboards[B]) -
            BitboardOperations.CountBits(bitboards[b]);

        f.RookCountBalance =
            BitboardOperations.CountBits(bitboards[R]) -
            BitboardOperations.CountBits(bitboards[r]);

        f.QueenCountBalance =
            BitboardOperations.CountBits(bitboards[Q]) -
            BitboardOperations.CountBits(bitboards[q]);


        // ------------------------------------------------
        // Bishop pair
        // ------------------------------------------------
        if (BitboardOperations.CountBits(bitboards[B]) >= 2) f.BishopPairBalance++;
        if (BitboardOperations.CountBits(bitboards[b]) >= 2) f.BishopPairBalance--;

        ulong wPawns = bitboards[P];
        ulong bPawns = bitboards[p];
        ulong wOcc = occupancies[White];
        ulong bOcc = occupancies[Black];
        ulong allOcc = occupancies[Both];
        ulong allPawns = wPawns | bPawns;

        // ------------------------------------------------
        // Isolated pawns
        // ------------------------------------------------
        for (ulong bb = wPawns; bb != 0;)
        {
            int sq = BitboardOperations.GetLs1bIndex(bb);
            if ((AdjacentFiles[sq % 8] & wPawns) == 0) f.IsolatedPawnBalance++;
            BitboardOperations.PopBit(ref bb, sq);
        }

        for (ulong bb = bPawns; bb != 0;)
        {
            int sq = BitboardOperations.GetLs1bIndex(bb);
            if ((AdjacentFiles[sq % 8] & bPawns) == 0) f.IsolatedPawnBalance--;
            BitboardOperations.PopBit(ref bb, sq);
        }

        // ------------------------------------------------
        // Mobility
        // ------------------------------------------------
        for (ulong bb = bitboards[N]; bb != 0;)
        {
            int sq = BitboardOperations.GetLs1bIndex(bb);
            f.KnightMobilityBalance += BitboardOperations.CountBits(knightAttacks[sq] & ~wOcc) - KnightMobBase;
            BitboardOperations.PopBit(ref bb, sq);
        }

        for (ulong bb = bitboards[n]; bb != 0;)
        {
            int sq = BitboardOperations.GetLs1bIndex(bb);
            f.KnightMobilityBalance -= BitboardOperations.CountBits(knightAttacks[sq] & ~bOcc) - KnightMobBase;
            BitboardOperations.PopBit(ref bb, sq);
        }

        for (ulong bb = bitboards[B]; bb != 0;)
        {
            int sq = BitboardOperations.GetLs1bIndex(bb);
            f.BishopMobilityBalance += BitboardOperations.CountBits(GetBishopAttacks(sq, allOcc) & ~wOcc) - BishopMobBase;
            BitboardOperations.PopBit(ref bb, sq);
        }

        for (ulong bb = bitboards[b]; bb != 0;)
        {
            int sq = BitboardOperations.GetLs1bIndex(bb);
            f.BishopMobilityBalance -= BitboardOperations.CountBits(GetBishopAttacks(sq, allOcc) & ~bOcc) - BishopMobBase;
            BitboardOperations.PopBit(ref bb, sq);
        }

        // ------------------------------------------------
        // Rook files
        // ------------------------------------------------
        for (ulong bb = bitboards[R]; bb != 0;)
        {
            int sq = BitboardOperations.GetLs1bIndex(bb);
            int file = sq % 8;
            ulong fileMask = FileMask[file];

            if ((wPawns & fileMask) == 0)
            {
                if ((allPawns & fileMask) == 0) f.RookOpenBalance++;
                else f.RookSemiOpenBalance++;
            }

            BitboardOperations.PopBit(ref bb, sq);
        }

        for (ulong bb = bitboards[r]; bb != 0;)
        {
            int sq = BitboardOperations.GetLs1bIndex(bb);
            int file = sq % 8;
            ulong fileMask = FileMask[file];

            if ((bPawns & fileMask) == 0)
            {
                if ((allPawns & fileMask) == 0) f.RookOpenBalance--;
                else f.RookSemiOpenBalance--;
            }

            BitboardOperations.PopBit(ref bb, sq);
        }

        // ------------------------------------------------
        // King exposure
        // ------------------------------------------------
        int wkFile = BitboardOperations.GetLs1bIndex(bitboards[K]) % 8;
        int bkFile = BitboardOperations.GetLs1bIndex(bitboards[k]) % 8;

        AddKingFileFeature(wkFile, wPawns, allPawns, -1, ref f.KingOwnOpenBalance, ref f.KingOwnSemiOpenBalance);
        AddKingFileFeature(bkFile, bPawns, allPawns, +1, ref f.KingOwnOpenBalance, ref f.KingOwnSemiOpenBalance);

        if (wkFile > 0) AddKingFileFeature(wkFile - 1, wPawns, allPawns, -1, ref f.KingAdjacentOpenBalance, ref f.KingAdjacentSemiOpenBalance);
        if (wkFile < 7) AddKingFileFeature(wkFile + 1, wPawns, allPawns, -1, ref f.KingAdjacentOpenBalance, ref f.KingAdjacentSemiOpenBalance);

        if (bkFile > 0) AddKingFileFeature(bkFile - 1, bPawns, allPawns, +1, ref f.KingAdjacentOpenBalance, ref f.KingAdjacentSemiOpenBalance);
        if (bkFile < 7) AddKingFileFeature(bkFile + 1, bPawns, allPawns, +1, ref f.KingAdjacentOpenBalance, ref f.KingAdjacentSemiOpenBalance);

        // ------------------------------------------------
        // Knight outposts
        // ------------------------------------------------
        for (ulong bb = bitboards[N]; bb != 0;)
        {
            int sq = BitboardOperations.GetLs1bIndex(bb);
            int rank = sq / 8;

            if (rank >= 2 && rank <= 4 &&
                (pawnAttacks[Black, sq] & wPawns) != 0 &&
                (WhiteOutpostMask[sq] & bPawns) == 0)
            {
                f.KnightOutpostBalance++;
            }

            BitboardOperations.PopBit(ref bb, sq);
        }

        for (ulong bb = bitboards[n]; bb != 0;)
        {
            int sq = BitboardOperations.GetLs1bIndex(bb);
            int rank = sq / 8;

            if (rank >= 3 && rank <= 5 &&
                (pawnAttacks[White, sq] & bPawns) != 0 &&
                (BlackOutpostMask[sq] & wPawns) == 0)
            {
                f.KnightOutpostBalance--;
            }

            BitboardOperations.PopBit(ref bb, sq);
        }

        return f;
    }

    private static void ExtractFixedBase(int first, int last, int sign, EvalFeatures f)
    {
        for (int piece = first; piece <= last; piece++)
        {
            ulong bb = bitboards[piece];
            while (bb != 0)
            {
                int sq = BitboardOperations.GetLs1bIndex(bb);
                f.FixedMg += sign * MgTable[piece, sq];
                f.FixedEg += sign * EgTable[piece, sq];
                f.Phase += PhaseWeight[piece];
                BitboardOperations.PopBit(ref bb, sq);
            }
        }
    }

    private static void AddKingFileFeature(
        int file,
        ulong friendlyPawns,
        ulong allPawns,
        int sign,
        ref int openBalance,
        ref int semiOpenBalance)
    {
        ulong mask = FileMask[file];

        if ((friendlyPawns & mask) != 0)
            return;

        if ((allPawns & mask) == 0) openBalance += sign;
        else semiOpenBalance += sign;
    }


    // ================================================================
    //  Constants
    // ================================================================

    #region Material & Phase

    private static readonly int[] MgMaterial = [83, 359, 381, 482, 1069, 0];
    private static readonly int[] EgMaterial = [94, 267, 279, 503, 924, 0];

    //                                          P  N  B  R  Q  K  p  n  b  r  q  k
    private static readonly int[] PhaseWeight = [0, 1, 1, 2, 4, 0, 0, 1, 1, 2, 4, 0];
    private const int TotalPhase = 24;

    #endregion

    #region Positional Bonuses / Penalties

    // Bishop pair
    private const int BishopPairMg = 15;
    private const int BishopPairEg = 39;

    // Mobility: bonus per square above baseline, penalty per square below
    //
    //   score += (moves - baseline) * weight
    //
    //   Knight baseline ~4, Bishop baseline ~6
    private const int KnightMobMg = 1, KnightMobEg = 0, KnightMobBase = 4;
    private const int BishopMobMg = 3, BishopMobEg = 1, BishopMobBase = 6;

    // Rook on open / semi-open file
    //
    //   Semi-open = no friendly pawns on that file
    //   Open      = no pawns at all on that file
    private const int RookSemiOpenMg = 13, RookSemiOpenEg = 7;
    private const int RookOpenMg = 45, RookOpenEg = 2;

    // Passed pawns (indexed by engine rank, see table below)
    //
    //   Engine rank:    0    1    2    3    4    5    6    7
    //   Chess rank:     8    7    6    5    4    3    2    1
    //   White pawn:   (impossible)  ←── advancing ──→  (start)
    //   Black mirror: mirroredRank = 7 - rank
    private static readonly int[] PassedMg = [0, 15, 15, 17, 10, 6, 0, 0];
    private static readonly int[] PassedEg = [0, 97, 55, 36, 17, 12, 0, 0];

    // Isolated pawn (no friendly pawn on adjacent files)
    private const int IsolatedMg = -11;
    private const int IsolatedEg = -3;

    // King on open / semi-open file (middlegame only)
    //
    //   Penalizes kings whose file (and adjacent files) lack friendly pawns.
    //
    //   Example — White king on e1, no pawns on d/e/f files:
    //     Own file (e):    -54 (open)
    //     Adjacent (d):    -24 (open)
    //     Adjacent (f):    -24 (open)
    //     Total:          -102
    private const int KingOwnOpenMg = 54, KingOwnSemiOpenMg = 14;
    private const int KingAdjacentOpenMg = 24, KingAdjacentSemiOpenMg = 12;

    // Knight outpost (middlegame only)
    //
    //   Conditions:
    //     1) Knight on ranks 4–6 (engine rank 2–4 for White)
    //     2) Supported by a friendly pawn
    //     3) No enemy pawn on adjacent files can still advance to challenge it
    private const int KnightOutpostMg = 43;

    #endregion

    // ================================================================
    //  Piece-Square Tables (PeSTO)
    // ================================================================
    //
    //  Layout matches engine square mapping: index 0 = a8, index 63 = h1.
    //
    //  White reads PST[square] directly.
    //  Black reads PST[square ^ 56] (vertical mirror).

    #region PST Data

    private static readonly int[][] MgPst =
    [
        // Pawn
        [
              0,   0,   0,   0,   0,   0,   0,   0,
             98, 134,  61,  95,  68, 126,  34, -11,
             -6,   7,  26,  31,  65,  56,  25, -20,
            -14,  13,   6,  21,  23,  12,  17, -23,
            -27,  -2,  -5,  12,  17,   6,  10, -25,
            -26,  -4,  -4, -10,   3,   3,  33, -12,
            -35,  -1, -20, -23, -15,  24,  38, -22,
              0,   0,   0,   0,   0,   0,   0,   0,
        ],
        // Knight
        [
            -167, -89, -34, -49,  61, -97, -15, -107,
             -73, -41,  72,  36,  23,  62,   7,  -17,
             -47,  60,  37,  65,  84, 129,  73,   44,
              -9,  17,  19,  53,  37,  69,  18,   22,
             -13,   4,  16,  13,  28,  19,  21,   -8,
             -23,  -9,  12,  10,  19,  17,  25,  -16,
             -29, -53, -12,  -3,  -1,  18, -14,  -19,
            -105, -21, -58, -33, -17, -28, -19,  -23,
        ],
        // Bishop
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
        // Rook
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
        // Queen
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
        // King
        [
            -65,  23,  16, -15, -56, -34,   2,  13,
             29,  -1, -20,  -7,  -8,  -4, -38, -29,
             -9,  24,   2, -16, -20,   6,  22, -22,
            -17, -20, -12, -27, -30, -25, -14, -36,
            -49,  -1, -27, -39, -46, -44, -33, -51,
            -14, -14, -22, -46, -44, -30, -15, -27,
              1,   7,  -8, -64, -43, -16,   9,   8,
            -15,  36,  12, -54,   8, -28,  24,  14,
        ],
    ];

    private static readonly int[][] EgPst =
    [
        // Pawn
        [
              0,   0,   0,   0,   0,   0,   0,   0,
            178, 173, 158, 134, 147, 132, 165, 187,
             94, 100,  85,  67,  56,  53,  82,  84,
             32,  24,  13,   5,  -2,   4,  17,  17,
             13,   9,  -3,  -7,  -7,  -8,   3,  -1,
              4,   7,  -6,   1,   0,  -5,  -1,  -8,
             13,   8,   8,  10,  13,   0,   2,  -7,
              0,   0,   0,   0,   0,   0,   0,   0,
        ],
        // Knight
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
        // Bishop
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
        // Rook
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
        // Queen
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
        // King
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

    #endregion

    // ================================================================
    //  Precomputed Lookup Tables
    // ================================================================

    // Material + PST combined: MgTable[piece, square], EgTable[piece, square]
    //   White pieces 0..5, Black pieces 6..11
    private static readonly int[,] MgTable = new int[12, 64];
    private static readonly int[,] EgTable = new int[12, 64];

    //  FileMask[f]          = all 8 squares on file f
    //  AdjacentFiles[f]     = all squares on files f-1 and f+1
    //  WhitePassedMask[sq]  = same + adjacent files, ranks ahead for White
    //  BlackPassedMask[sq]  = same + adjacent files, ranks ahead for Black
    //  WhiteOutpostMask[sq] = adjacent files only, ranks ahead for White
    //  BlackOutpostMask[sq] = adjacent files only, ranks ahead for Black
    private static readonly ulong[] FileMask = new ulong[8];
    private static readonly ulong[] AdjacentFiles = new ulong[8];
    private static readonly ulong[] WhitePassedMask = new ulong[64];
    private static readonly ulong[] BlackPassedMask = new ulong[64];
    private static readonly ulong[] WhiteOutpostMask = new ulong[64];
    private static readonly ulong[] BlackOutpostMask = new ulong[64];

    // ================================================================
    //  Initialization
    // ================================================================

    static Evaluation()
    {
        InitFileMasks();
        InitAdjacentFileMasks();
        InitPassedPawnMasks();
        InitOutpostMasks();
        InitMaterialPstTables();
    }

    private static void InitFileMasks()
    {
        for (int f = 0; f < 8; f++)
            for (int r = 0; r < 8; r++)
                FileMask[f] |= 1UL << (r * 8 + f);
    }

    private static void InitAdjacentFileMasks()
    {
        for (int f = 0; f < 8; f++)
        {
            if (f > 0) AdjacentFiles[f] |= FileMask[f - 1];
            if (f < 7) AdjacentFiles[f] |= FileMask[f + 1];
        }
    }

    private static void InitPassedPawnMasks()
    {
        //  For a White pawn on square s:
        //    "passed" means no Black pawn exists on [same + adjacent files]
        //    on any rank closer to Black's side (lower rank index).
        //
        //  Visual — White pawn on d4 (square 35, rank=4, file=3):
        //
        //      . . X X X . . .    rank 8  (index 0)
        //      . . X X X . . .    rank 7  (index 1)
        //      . . X X X . . .    rank 6  (index 2)
        //      . . X X X . . .    rank 5  (index 3)
        //      . . . P . . . .    rank 4  (index 4)  ← pawn here
        //      . . . . . . . .    rank 3  (index 5)
        //      . . . . . . . .    rank 2  (index 6)
        //      . . . . . . . .    rank 1  (index 7)
        //
        //  X = squares in WhitePassedMask[35]

        for (int sq = 0; sq < 64; sq++)
        {
            int file = sq % 8;
            int rank = sq / 8;

            ulong relevantFiles = FileMask[file] | AdjacentFiles[file];

            ulong aheadWhite = 0UL;
            for (int r = 0; r < rank; r++)
                for (int f = 0; f < 8; f++)
                    aheadWhite |= 1UL << (r * 8 + f);

            ulong aheadBlack = 0UL;
            for (int r = rank + 1; r < 8; r++)
                for (int f = 0; f < 8; f++)
                    aheadBlack |= 1UL << (r * 8 + f);

            WhitePassedMask[sq] = relevantFiles & aheadWhite;
            BlackPassedMask[sq] = relevantFiles & aheadBlack;
        }
    }

    private static void InitOutpostMasks()
    {
        //  Similar to passed-pawn masks but ONLY adjacent files (not same file),
        //  because pawns attack diagonally, not straight ahead.
        //
        //  Visual — White knight on d5 (square 27, rank=3, file=3):
        //
        //      . . X . X . . .    rank 8  (index 0)
        //      . . X . X . . .    rank 7  (index 1)
        //      . . X . X . . .    rank 6  (index 2)
        //      . . . N . . . .    rank 5  (index 3)  ← knight here
        //      . . . . . . . .
        //      . . . . . . . .
        //      . . . . . . . .
        //      . . . . . . . .
        //
        //  X = squares in WhiteOutpostMask[27]
        //  If a Black pawn is on any X, it can still advance and challenge d5.

        for (int sq = 0; sq < 64; sq++)
        {
            int file = sq % 8;
            int rank = sq / 8;

            ulong wMask = 0UL, bMask = 0UL;

            if (file > 0)
            {
                for (int r = 0; r < rank; r++)
                    wMask |= 1UL << (r * 8 + file - 1);

                for (int r = rank + 1; r < 8; r++)
                    bMask |= 1UL << (r * 8 + file - 1);
            }

            if (file < 7)
            {
                for (int r = 0; r < rank; r++)
                    wMask |= 1UL << (r * 8 + file + 1);

                for (int r = rank + 1; r < 8; r++)
                    bMask |= 1UL << (r * 8 + file + 1);
            }

            WhiteOutpostMask[sq] = wMask;
            BlackOutpostMask[sq] = bMask;
        }
    }

    private static void InitMaterialPstTables()
    {
        for (int piece = 0; piece < 6; piece++)
        {
            for (int sq = 0; sq < 64; sq++)
            {
                MgTable[piece, sq] = MgMaterial[piece] + MgPst[piece][sq];
                EgTable[piece, sq] = EgMaterial[piece] + EgPst[piece][sq];
                MgTable[piece + 6, sq] = MgMaterial[piece] + MgPst[piece][sq ^ 56];
                EgTable[piece + 6, sq] = EgMaterial[piece] + EgPst[piece][sq ^ 56];
            }
        }
    }

    // ================================================================
    //  Main Entry Point
    // ================================================================

    public static int Evaluate()
    {
        int mg = 0, eg = 0, phase = 0;

        // ---- Material + PST ----
        ScorePieces(P, K, +1, ref mg, ref eg, ref phase);
        ScorePieces(p, k, -1, ref mg, ref eg, ref phase);

        // ---- Positional features ----
        ScoreBishopPair(ref mg, ref eg);
        ScorePassedPawns(ref mg, ref eg);   // kept, but NOT tuned
        ScoreIsolatedPawns(ref mg, ref eg);
        ScoreMobility(ref mg, ref eg);
        ScoreRookFiles(ref mg, ref eg);
        ScoreKingExposure(ref mg);
        ScoreKnightOutposts(ref mg);

        // ---- Taper and return ----
        int mgPhase = Math.Min(phase, TotalPhase);
        int egPhase = TotalPhase - mgPhase;
        int score = (mg * mgPhase + eg * egPhase) / TotalPhase;

        return side == White ? score : -score;
    }


    // ================================================================
    //  Evaluation Helpers
    // ================================================================

    private static void ScorePieces(int first, int last, int sign,
        ref int mg, ref int eg, ref int phase)
    {
        for (int piece = first; piece <= last; piece++)
        {
            ulong bb = bitboards[piece];
            while (bb != 0)
            {
                int sq = BitboardOperations.GetLs1bIndex(bb);
                mg += sign * MgTable[piece, sq];
                eg += sign * EgTable[piece, sq];
                phase += PhaseWeight[piece];
                BitboardOperations.PopBit(ref bb, sq);
            }
        }
    }

    private static void ScoreBishopPair(ref int mg, ref int eg)
    {
        if (BitboardOperations.CountBits(bitboards[B]) >= 2) { mg += BishopPairMg; eg += BishopPairEg; }
        if (BitboardOperations.CountBits(bitboards[b]) >= 2) { mg -= BishopPairMg; eg -= BishopPairEg; }
    }

    private static void ScorePassedPawns(ref int mg, ref int eg)
    {
        ulong wPawns = bitboards[P], bPawns = bitboards[p];

        for (ulong bb = wPawns; bb != 0;)
        {
            int sq = BitboardOperations.GetLs1bIndex(bb);
            if ((WhitePassedMask[sq] & bPawns) == 0)
            {
                int rank = sq / 8;
                mg += PassedMg[rank];
                eg += PassedEg[rank];
            }
            BitboardOperations.PopBit(ref bb, sq);
        }

        for (ulong bb = bPawns; bb != 0;)
        {
            int sq = BitboardOperations.GetLs1bIndex(bb);
            if ((BlackPassedMask[sq] & wPawns) == 0)
            {
                int rank = 7 - sq / 8;
                mg -= PassedMg[rank];
                eg -= PassedEg[rank];
            }
            BitboardOperations.PopBit(ref bb, sq);
        }
    }

    private static void ScoreIsolatedPawns(ref int mg, ref int eg)
    {
        ulong wPawns = bitboards[P], bPawns = bitboards[p];

        for (ulong bb = wPawns; bb != 0;)
        {
            int sq = BitboardOperations.GetLs1bIndex(bb);
            if ((AdjacentFiles[sq % 8] & wPawns) == 0) { mg += IsolatedMg; eg += IsolatedEg; }
            BitboardOperations.PopBit(ref bb, sq);
        }

        for (ulong bb = bPawns; bb != 0;)
        {
            int sq = BitboardOperations.GetLs1bIndex(bb);
            if ((AdjacentFiles[sq % 8] & bPawns) == 0) { mg -= IsolatedMg; eg -= IsolatedEg; }
            BitboardOperations.PopBit(ref bb, sq);
        }
    }

    private static void ScoreMobility(ref int mg, ref int eg)
    {
        ulong wOcc = occupancies[White];
        ulong bOcc = occupancies[Black];
        ulong all = occupancies[Both];

        // Knights
        ScorePieceMobility(bitboards[N], sq => knightAttacks[sq] & ~wOcc,
            KnightMobBase, KnightMobMg, KnightMobEg, +1, ref mg, ref eg);
        ScorePieceMobility(bitboards[n], sq => knightAttacks[sq] & ~bOcc,
            KnightMobBase, KnightMobMg, KnightMobEg, -1, ref mg, ref eg);

        // Bishops
        ScorePieceMobility(bitboards[B], sq => GetBishopAttacks(sq, all) & ~wOcc,
            BishopMobBase, BishopMobMg, BishopMobEg, +1, ref mg, ref eg);
        ScorePieceMobility(bitboards[b], sq => GetBishopAttacks(sq, all) & ~bOcc,
            BishopMobBase, BishopMobMg, BishopMobEg, -1, ref mg, ref eg);
    }

    private static void ScorePieceMobility(ulong bb, Func<int, ulong> getAttacks,
        int baseline, int mgWeight, int egWeight, int sign, ref int mg, ref int eg)
    {
        while (bb != 0)
        {
            int sq = BitboardOperations.GetLs1bIndex(bb);
            int mobility = BitboardOperations.CountBits(getAttacks(sq)) - baseline;
            mg += sign * mobility * mgWeight;
            eg += sign * mobility * egWeight;
            BitboardOperations.PopBit(ref bb, sq);
        }
    }

    private static void ScoreRookFiles(ref int mg, ref int eg)
    {
        ulong wPawns = bitboards[P], bPawns = bitboards[p];
        ulong allPawns = wPawns | bPawns;

        for (ulong bb = bitboards[R]; bb != 0;)
        {
            int sq = BitboardOperations.GetLs1bIndex(bb);
            ScoreFileBonus(sq % 8, wPawns, allPawns, +1, ref mg, ref eg);
            BitboardOperations.PopBit(ref bb, sq);
        }

        for (ulong bb = bitboards[r]; bb != 0;)
        {
            int sq = BitboardOperations.GetLs1bIndex(bb);
            ScoreFileBonus(sq % 8, bPawns, allPawns, -1, ref mg, ref eg);
            BitboardOperations.PopBit(ref bb, sq);
        }
    }

    private static void ScoreFileBonus(int file, ulong friendlyPawns, ulong allPawns,
        int sign, ref int mg, ref int eg)
    {
        ulong mask = FileMask[file];

        if ((friendlyPawns & mask) != 0) return;

        if ((allPawns & mask) == 0) { mg += sign * RookOpenMg; eg += sign * RookOpenEg; }
        else { mg += sign * RookSemiOpenMg; eg += sign * RookSemiOpenEg; }
    }

    private static void ScoreKingExposure(ref int mg)
    {
        ulong wPawns = bitboards[P], bPawns = bitboards[p];
        ulong allPawns = wPawns | bPawns;

        int wkFile = BitboardOperations.GetLs1bIndex(bitboards[K]) % 8;
        int bkFile = BitboardOperations.GetLs1bIndex(bitboards[k]) % 8;

        // White king
        ScoreKingFile(wkFile, wPawns, allPawns, -1, KingOwnOpenMg, KingOwnSemiOpenMg, ref mg);
        if (wkFile > 0) ScoreKingFile(wkFile - 1, wPawns, allPawns, -1, KingAdjacentOpenMg, KingAdjacentSemiOpenMg, ref mg);
        if (wkFile < 7) ScoreKingFile(wkFile + 1, wPawns, allPawns, -1, KingAdjacentOpenMg, KingAdjacentSemiOpenMg, ref mg);

        // Black king
        ScoreKingFile(bkFile, bPawns, allPawns, +1, KingOwnOpenMg, KingOwnSemiOpenMg, ref mg);
        if (bkFile > 0) ScoreKingFile(bkFile - 1, bPawns, allPawns, +1, KingAdjacentOpenMg, KingAdjacentSemiOpenMg, ref mg);
        if (bkFile < 7) ScoreKingFile(bkFile + 1, bPawns, allPawns, +1, KingAdjacentOpenMg, KingAdjacentSemiOpenMg, ref mg);
    }

    private static void ScoreKingFile(int file, ulong friendlyPawns, ulong allPawns,
        int sign, int openPenalty, int semiOpenPenalty, ref int mg)
    {
        ulong mask = FileMask[file];
        if ((friendlyPawns & mask) != 0) return;
        mg += sign * (((allPawns & mask) == 0) ? openPenalty : semiOpenPenalty);
    }

    private static void ScoreKnightOutposts(ref int mg)
    {
        ulong wPawns = bitboards[P], bPawns = bitboards[p];

        //  White knight outposts — engine ranks 2..4 (chess ranks 6..4)
        //
        //  pawnAttacks[Black, sq] gives the squares from which a BLACK pawn
        //  would attack sq. Those same squares are where a WHITE pawn must be
        //  to support sq. So:
        //
        //    supportedByWhitePawn = (pawnAttacks[Black, sq] & whitePawns) != 0
        for (ulong bb = bitboards[N]; bb != 0;)
        {
            int sq = BitboardOperations.GetLs1bIndex(bb);
            int rank = sq / 8;

            if (rank >= 2 && rank <= 4
                && (pawnAttacks[Black, sq] & wPawns) != 0
                && (WhiteOutpostMask[sq] & bPawns) == 0)
            {
                mg += KnightOutpostMg;
            }

            BitboardOperations.PopBit(ref bb, sq);
        }

        //  Black knight outposts — engine ranks 3..5 (chess ranks 5..3)
        for (ulong bb = bitboards[n]; bb != 0;)
        {
            int sq = BitboardOperations.GetLs1bIndex(bb);
            int rank = sq / 8;

            if (rank >= 3 && rank <= 5
                && (pawnAttacks[White, sq] & bPawns) != 0
                && (BlackOutpostMask[sq] & wPawns) == 0)
            {
                mg -= KnightOutpostMg;
            }

            BitboardOperations.PopBit(ref bb, sq);
        }
    }
}