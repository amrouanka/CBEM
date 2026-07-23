using static Board;
using static MoveEncoding;
using static MoveGenerator;
using static PieceAttacks;

/// <summary>
/// Static Exchange Evaluation (SEE)
///
/// Simulates a sequence of captures on a single square
/// WITHOUT making any moves on the board.
/// </summary>
public static class See
{
    // SEE piece values (by piece index P=0 up to k=11)
    private static readonly int[] SeeValue =
    [
        100,    // P (0)
        300,    // N (1)
        300,    // B (2)
        500,    // R (3)
        900,    // Q (4)
        20000,  // K (5)
        100,    // p (6)
        300,    // n (7)
        300,    // b (8)
        500,    // r (9)
        900,    // q (10)
        20000,  // k (11)
    ];

    public static bool IsGoodCapture(int move, int threshold)
    {
        return Evaluate(move) >= threshold;
    }

    public static int Evaluate(int move)
    {
        int from = GetMoveSource(move);
        int to = GetMoveTarget(move);
        bool isCapture = GetMoveCapture(move) != 0;
        bool isEnPassant = GetMoveEnpassant(move) != 0;

        if (!isCapture)
            return 0;

        // Get victim piece and square
        int victimSq;
        int victimPiece;

        if (isEnPassant)
        {
            // En passant: captured pawn is on square behind target
            victimSq = (side == White) ? to + 8 : to - 8;
            victimPiece = (side == White) ? p : P;
        }
        else
        {
            victimSq = to;
            victimPiece = GetPieceAtSquare(victimSq);
        }

        // Gain[0] = value of captured piece
        int[] gain = new int[32];
        int depth = 0;
        gain[depth] = SeeValue[victimPiece];

        // Occupancy snapshot (Both = index 2)
        ulong occ = occupancies[2];
        // Remove attacker from source square
        occ &= ~(1UL << from);
        // For en passant, remove captured pawn from board
        if (isEnPassant)
            occ &= ~(1UL << victimSq);

        int attackerPiece = GetMovePiece(move);
        int sideToMove = side ^ 1;

        while (true)
        {
            depth++;

            // Value of piece we're about to lose minus previous gain
            gain[depth] = SeeValue[attackerPiece] - gain[depth - 1];

            // Find least valuable attacker
            int nextFromSq;
            int nextAttacker = GetLeastValuableAttacker(to, sideToMove, occ, out nextFromSq);

            if (nextAttacker < 0)
                break;

            // Remove next attacker
            occ &= ~(1UL << nextFromSq);
            attackerPiece = nextAttacker;
            sideToMove ^= 1;
        }

        // Backward minimax
        while (depth > 0)
        {
            gain[depth - 1] = -Math.Max(-gain[depth - 1], gain[depth]);
            depth--;
        }

        return gain[0];
    }

    private static int GetLeastValuableAttacker(int sq, int side, ulong occ, out int fromSquare)
    {
        fromSquare = -1;

        if (side == White)
        {
            // Pawns
            ulong pawns = pawnAttacks[Black, sq] & bitboards[P] & occ;
            if (pawns != 0)
            {
                fromSquare = BitboardOperations.GetLs1bIndex(pawns);
                return P;
            }

            // Knights
            ulong knights = knightAttacks[sq] & bitboards[N] & occ;
            if (knights != 0)
            {
                fromSquare = BitboardOperations.GetLs1bIndex(knights);
                return N;
            }

            // Bishops
            ulong bishops = GetBishopAttacks(sq, occ) & bitboards[B] & occ;
            if (bishops != 0)
            {
                fromSquare = BitboardOperations.GetLs1bIndex(bishops);
                return B;
            }

            // Rooks
            ulong rooks = GetRookAttacks(sq, occ) & bitboards[R] & occ;
            if (rooks != 0)
            {
                fromSquare = BitboardOperations.GetLs1bIndex(rooks);
                return R;
            }

            // Queens
            ulong queens = (GetBishopAttacks(sq, occ) | GetRookAttacks(sq, occ)) & bitboards[Q] & occ;
            if (queens != 0)
            {
                fromSquare = BitboardOperations.GetLs1bIndex(queens);
                return Q;
            }

            // King
            ulong king = kingAttacks[sq] & bitboards[K] & occ;
            if (king != 0)
            {
                fromSquare = BitboardOperations.GetLs1bIndex(king);
                return K;
            }
        }
        else
        {
            // Black
            ulong pawns = pawnAttacks[White, sq] & bitboards[p] & occ;
            if (pawns != 0)
            {
                fromSquare = BitboardOperations.GetLs1bIndex(pawns);
                return p;
            }

            ulong knights = knightAttacks[sq] & bitboards[n] & occ;
            if (knights != 0)
            {
                fromSquare = BitboardOperations.GetLs1bIndex(knights);
                return n;
            }

            ulong bishops = GetBishopAttacks(sq, occ) & bitboards[b] & occ;
            if (bishops != 0)
            {
                fromSquare = BitboardOperations.GetLs1bIndex(bishops);
                return b;
            }

            ulong rooks = GetRookAttacks(sq, occ) & bitboards[r] & occ;
            if (rooks != 0)
            {
                fromSquare = BitboardOperations.GetLs1bIndex(rooks);
                return r;
            }

            ulong queens = (GetBishopAttacks(sq, occ) | GetRookAttacks(sq, occ)) & bitboards[q] & occ;
            if (queens != 0)
            {
                fromSquare = BitboardOperations.GetLs1bIndex(queens);
                return q;
            }

            ulong king = kingAttacks[sq] & bitboards[k] & occ;
            if (king != 0)
            {
                fromSquare = BitboardOperations.GetLs1bIndex(king);
                return k;
            }
        }

        return -1;
    }

    // Helper to get piece at square (needed)
    private static int GetPieceAtSquare(int square)
    {
        ulong mask = 1UL << square;
        if ((bitboards[P] & mask) != 0) return P;
        if ((bitboards[p] & mask) != 0) return p;
        if ((bitboards[N] & mask) != 0) return N;
        if ((bitboards[n] & mask) != 0) return n;
        if ((bitboards[B] & mask) != 0) return B;
        if ((bitboards[b] & mask) != 0) return b;
        if ((bitboards[R] & mask) != 0) return R;
        if ((bitboards[r] & mask) != 0) return r;
        if ((bitboards[Q] & mask) != 0) return Q;
        if ((bitboards[q] & mask) != 0) return q;
        if ((bitboards[K] & mask) != 0) return K;
        if ((bitboards[k] & mask) != 0) return k;
        return 0; // empty
    }
}