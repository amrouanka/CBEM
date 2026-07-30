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

    public static int[][] GetMgPst() => MgPst;
    public static int[][] GetEgPst() => EgPst;

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

            PstMgAdjust = new int[6, 64],
            PstEgAdjust = new int[6, 64],

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
        int mg = 0;
        int eg = 0;

        // PST base score from MgTable/EgTable + tuned adjustments
        for (int piece = 0; piece < 6; piece++)
        {
            for (int sq = 0; sq < 64; sq++)
            {
                int bal = f.PieceSqBalance[piece, sq];
                if (bal != 0)
                {
                    mg += bal * (MgTable[piece, sq] + w.PstMgAdjust[piece, sq]);
                    eg += bal * (EgTable[piece, sq] + w.PstEgAdjust[piece, sq]);
                }
            }
        }

        // Material adjustments
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

        // Positional
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

        // Passed pawns
        for (int rank = 1; rank <= 6; rank++)
        {
            mg += f.PassedPawnBalance[rank] * w.PassedMg[rank];
            eg += f.PassedPawnBalance[rank] * w.PassedEg[rank];
        }

        int mgPhase = Math.Min(f.Phase, TotalPhase);
        int egPhase = TotalPhase - mgPhase;

        return (mg * mgPhase + eg * egPhase) / TotalPhase;
    }
    public static EvalFeatures ExtractFeatures()
    {
        EvalFeatures f = new();

        // Phase only (no PST score — PST is handled via PieceSqBalance)
        ExtractPhaseOnly(f);

        // Material count balances
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

        // Bishop pair
        if (BitboardOperations.CountBits(bitboards[B]) >= 2) f.BishopPairBalance++;
        if (BitboardOperations.CountBits(bitboards[b]) >= 2) f.BishopPairBalance--;

        ulong wPawns = bitboards[P];
        ulong bPawns = bitboards[p];
        ulong wOcc = occupancies[White];
        ulong bOcc = occupancies[Black];
        ulong allOcc = occupancies[Both];
        ulong allPawns = wPawns | bPawns;

        // Isolated pawns
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

        // Mobility
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

        // Rook files
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

        // King safety
        int wkFile = BitboardOperations.GetLs1bIndex(bitboards[K]) % 8;
        int bkFile = BitboardOperations.GetLs1bIndex(bitboards[k]) % 8;

        AddKingFileFeature(wkFile, wPawns, allPawns, -1, ref f.KingOwnOpenBalance, ref f.KingOwnSemiOpenBalance);
        AddKingFileFeature(bkFile, bPawns, allPawns, +1, ref f.KingOwnOpenBalance, ref f.KingOwnSemiOpenBalance);

        if (wkFile > 0) AddKingFileFeature(wkFile - 1, wPawns, allPawns, -1, ref f.KingAdjacentOpenBalance, ref f.KingAdjacentSemiOpenBalance);
        if (wkFile < 7) AddKingFileFeature(wkFile + 1, wPawns, allPawns, -1, ref f.KingAdjacentOpenBalance, ref f.KingAdjacentSemiOpenBalance);

        if (bkFile > 0) AddKingFileFeature(bkFile - 1, bPawns, allPawns, +1, ref f.KingAdjacentOpenBalance, ref f.KingAdjacentSemiOpenBalance);
        if (bkFile < 7) AddKingFileFeature(bkFile + 1, bPawns, allPawns, +1, ref f.KingAdjacentOpenBalance, ref f.KingAdjacentSemiOpenBalance);

        // Knight outposts
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

        // Passed pawns
        for (ulong bb = wPawns; bb != 0;)
        {
            int sq = BitboardOperations.GetLs1bIndex(bb);
            int rank = sq / 8;

            if ((WhitePassedMask[sq] & bPawns) == 0)
            {
                f.PassedPawnBalance[rank]++;
            }

            BitboardOperations.PopBit(ref bb, sq);
        }

        for (ulong bb = bPawns; bb != 0;)
        {
            int sq = BitboardOperations.GetLs1bIndex(bb);
            int rank = 7 - (sq / 8);

            if ((BlackPassedMask[sq] & wPawns) == 0)
            {
                f.PassedPawnBalance[rank]--;
            }

            BitboardOperations.PopBit(ref bb, sq);
        }

        // Piece-square balance (for PST tuning)
        for (int piece = 0; piece < 6; piece++)
        {
            // White pieces
            for (ulong bb = bitboards[piece]; bb != 0;)
            {
                int sq = BitboardOperations.GetLs1bIndex(bb);
                f.PieceSqBalance[piece, sq]++;
                BitboardOperations.PopBit(ref bb, sq);
            }

            // Black pieces (mirrored)
            for (ulong bb = bitboards[piece + 6]; bb != 0;)
            {
                int sq = BitboardOperations.GetLs1bIndex(bb);
                int mirroredSq = sq ^ 56;
                f.PieceSqBalance[piece, mirroredSq]--;
                BitboardOperations.PopBit(ref bb, sq);
            }
        }

        return f;
    }

    private static void ExtractPhaseOnly(EvalFeatures f)
    {
        for (int piece = 0; piece < 12; piece++)
        {
            ulong bb = bitboards[piece];
            while (bb != 0)
            {
                int sq = BitboardOperations.GetLs1bIndex(bb);
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

    private static readonly int[] MgMaterial = [82, 337, 365, 477, 1025, 0];
    private static readonly int[] EgMaterial = [94, 281, 297, 512, 936, 0];

    //                                          P  N  B  R  Q  K  p  n  b  r  q  k
    private static readonly int[] PhaseWeight = [0, 1, 1, 2, 4, 0, 0, 1, 1, 2, 4, 0];
    private const int TotalPhase = 24;

    #endregion

    #region Positional Bonuses / Penalties

    // Bishop pair
    private const int BishopPairMg = 25;
    private const int BishopPairEg = 49;

    // Mobility: bonus per square above baseline, penalty per square below
    //
    //   score += (moves - baseline) * weight
    //
    //   Knight baseline ~4, Bishop baseline ~6
    private const int KnightMobMg = 1, KnightMobEg = 0, KnightMobBase = 4;
    private const int BishopMobMg = 4, BishopMobEg = 4, BishopMobBase = 6;

    // Rook on open / semi-open file
    //
    //   Semi-open = no friendly pawns on that file
    //   Open      = no pawns at all on that file
    private const int RookSemiOpenMg = 21, RookSemiOpenEg = 15;
    private const int RookOpenMg = 54, RookOpenEg = 8;

    // Passed pawns (indexed by engine rank, see table below)
    //
    //   Engine rank:    0    1    2    3    4    5    6    7
    //   Chess rank:     8    7    6    5    4    3    2    1
    //   White pawn:   (impossible)  ←── advancing ──→  (start)
    //   Black mirror: mirroredRank = 7 - rank
    private static readonly int[] PassedMg = [0, 0, 42, 14, 0, 11, 10, 0];
    private static readonly int[] PassedEg = [0, 48, 67, 48, 25, 3, 0, 0];

    // Isolated pawn (no friendly pawn on adjacent files)
    private const int IsolatedMg = -19;
    private const int IsolatedEg = -8;

    // King on open / semi-open file (middlegame only)
    //
    //   Penalizes kings whose file (and adjacent files) lack friendly pawns.
    //
    //   Example — White king on e1, no pawns on d/e/f files:
    //     Own file (e):    -54 (open)
    //     Adjacent (d):    -24 (open)
    //     Adjacent (f):    -24 (open)
    //     Total:          -102
    private const int KingOwnOpenMg = 67, KingOwnSemiOpenMg = 16;
    private const int KingAdjacentOpenMg = 33, KingAdjacentSemiOpenMg = 13;

    // Knight outpost (middlegame only)
    //
    //   Conditions:
    //     1) Knight on ranks 4–6 (engine rank 2–4 for White)
    //     2) Supported by a friendly pawn
    //     3) No enemy pawn on adjacent files can still advance to challenge it
    private const int KnightOutpostMg = 46;

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
    0, 0, 0, 0, 0, 0, 0, 0,
    112, 125, 83, 116, 83, 96, 4, -17,
    -2, -15, 5, 1, 35, 46, -5, -15,
    -4, 5, 9, 24, 26, 18, 5, -18,
    -21, -16, -1, 12, 16, 10, -9, -24,
    -19, -21, -4, -9, 1, 4, 10, -11,
    -31, -15, -23, -21, -16, 21, 18, -21,
    0, 0, 0, 0, 0, 0, 0, 0
    ],
    // Knight
    [
    -195, -82, -48, -32, 89,-112, -10,-130,
    -70, -46, 43, 45, -4, 64, 12, -5,
    -45, 42, 38, 51, 93, 110, 68, 47,
    -6, 18, 7, 49, 38, 71, 26, 33,
    -10, 7, 18, 14, 34, 27, 32, -2,
    -16, -1, 18, 24, 33, 28, 34, -7,
    -23, -40, -3, 10, 15, 25, 3, -3,
    -106, -15, -42, -29, -1, -16, -9, -13
    ],
    // Bishop
    [
    -44, -8,-112, -65, -48, -56, -14, -28,
    -39, -11, -32, -43, 18, 29, 1, -60,
    -27, 18, 13, 16, 10, 50, 17, -6,
    -11, -2, -3, 32, 24, 17, -1, -5,
    -11, 1, -1, 20, 24, -3, -1, 2,
    -6, 11, 6, 2, 5, 23, 10, 4,
    -5, 15, 8, -4, 5, 15, 33, 1,
    -37, -8, -12, -24, -11, -11, -44, -26
    ],
    // Rook
    [
    21, 32, 11, 50, 47, -4, 22, 13,
    15, 8, 52, 66, 86, 71, -4, 34,
    -17, 1, 2, 14, -9, 41, 65, -2,
    -37, -24, -5, 8, 6, 29, -10, -34,
    -41, -30, -26, -9, -5, -3, 14, -27,
    -42, -25, -24, -25, -9, 6, -2, -25,
    -39, -14, -28, -20, -5, 15, -6, -63,
    -11, -13, -7, 1, 4, 13, -30, -14
    ],
    // Queen
    [
    -40, -12, 11, 2, 89, 74, 73, 24,
    -26, -50, -17, -4, -28, 47, -2, 42,
    -9, -13, -12, -5, 11, 68, 44, 53,
    -31, -29, -22, -20, -6, 8, -4, -6,
    -10, -30, -10, -11, -3, -1, -1, -5,
    -18, 2, -11, 2, -1, 2, 10, 2,
    -37, -8, 11, 6, 15, 23, 4, 8,
    1, -16, -9, 10, -16, -18, -35, -58
    ],
    // King
    [
    -90, 53, 46, 14, -84, -58, 31, 16,
    59, 29, 10, 23, 21, 17, -23, -59,
    21, 54, 32, 13, 9, 34, 52, -25,
    2, -1, 18, -22, -9, -16, -9, -66,
    -65, 28, -18, -49, -70, -44, -63, -80,
    -18, -5, -15, -56, -52, -34, -19, -43,
    -3, 9, -16, -60, -47, -24, 13, 12,
    -16, 40, 20, -54, 8, -36, 24, 16
    ],
    ];

    private static readonly int[][] EgPst =
    [
    // Pawn
    [
    0, 0, 0, 0, 0, 0, 0, 0,
    156, 143, 128, 104, 117, 103, 148, 179,
    66, 70, 55, 37, 26, 23, 52, 54,
    21, 8, -4, -22, -18, -13, 1, 6,
    11, 5, -8, -15, -14, -14, -6, -5,
    1, 0, -10, -8, -6, -10, -14, -13,
    13, -2, 5, -2, 4, -9, -13, -11,
    0, 0, 0, 0, 0, 0, 0, 0
    ],
    // Knight
    [
    -31, -30, -1, -24, -36, -15, -59, -78,
    -19, 2, -16, 2, 2, -27, -20, -49,
    -20, -12, 12, 13, -8, -10, -20, -45,
    -12, 5, 28, 24, 26, 10, 9, -17,
    -12, 1, 22, 30, 20, 20, 9, -13,
    -16, 6, 8, 19, 14, 4, -15, -17,
    -30, -13, -1, 2, 3, -12, -19, -42,
    -9, -41, -13, -5, -16, -9, -45, -63
    ],
    // Bishop
    [
    -7, -16, 2, -1, 3, -2, -6, -16,
    1, -5, 3, -10, -10, -15, -8, -7,
    4, -10, -8, -11, -12, -14, -4, 4,
    -2, 3, 4, -7, -6, -2, -5, 2,
    -5, -1, 3, 1, -11, -1, -7, -6,
    -6, -6, 2, -1, 5, -11, -3, -11,
    -6, -18, -9, -3, -2, -9, -16, -22,
    -9, -3, -13, -1, -2, -7, 3, -9
    ],
    // Rook
    [
    19, 14, 23, 11, 13, 24, 17, 19,
    16, 21, 9, 3, -13, 3, 21, 10,
    17, 17, 13, 11, 12, -1, -5, 7,
    18, 14, 19, 5, 6, 5, 7, 18,
    15, 15, 18, 6, 3, 2, -4, 3,
    8, 10, 3, 5, -3, -8, -3, -6,
    8, 0, 8, 10, -5, -9, -7, 12,
    7, 10, 11, 7, 3, -3, 12, -8
    ],
    // Queen
    [
    21, 52, 52, 57, 18, 13, 0, 50,
    3, 49, 62, 71, 88, 33, 46, 20,
    -3, 27, 39, 79, 77, 42, 37, 23,
    33, 52, 54, 71, 86, 70, 87, 62,
    2, 58, 49, 77, 60, 60, 69, 53,
    14, -11, 43, 32, 39, 47, 40, 35,
    8, 7, -6, 6, 3, -9, -20, -19,
    -17, -2, 6, -27, 25, -12, 8, -11
    ],
    // King
    [
    -77, -44, -29, -21, 2, 25, -5, -24,
    -28, 8, 11, 15, 13, 31, 24, 15,
    -6, 8, 17, 15, 17, 39, 34, 5,
    -19, 19, 23, 34, 28, 33, 26, 3,
    -15, -8, 25, 36, 41, 27, 18, -5,
    -21, -3, 16, 33, 35, 20, 7, -9,
    -35, -11, 12, 25, 26, 12, -9, -29,
    -65, -50, -25, 1, -24, -6, -36, -61
    ],
    ];

    #endregion

    // ================================================================
    //  Precomputed Lookup Tables
    // ================================================================

    // Material + PST combined: MgTable[piece, square], EgTable[piece, square]
    //   White pieces 0..5, Black pieces 6..11
    internal static readonly int[,] MgTable = new int[12, 64];
    internal static readonly int[,] EgTable = new int[12, 64];

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