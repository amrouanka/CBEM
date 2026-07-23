using static Board;
using static MoveEncoding;
using static MoveGenerator;
using static PieceAttacks;

public static class See
{
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

        int victimPiece;
        int victimSq;

        if (isEnPassant)
        {
            // White moves up (decreasing index): captured pawn is one rank below target
            // Black moves down (increasing index): captured pawn is one rank above target
            victimSq = (side == White) ? to + 8 : to - 8;
            victimPiece = (side == White) ? p : P;
        }
        else
        {
            victimSq = to;
            victimPiece = GetPieceAtSquare(victimSq);
            // FIX Bug 8: if no piece found, not a real capture
            if (victimPiece < 0) return 0;
        }

        int[] gain = new int[32];
        int d = 0;

        gain[0] = SeeValue[victimPiece];

        ulong occ = occupancies[2];

        // Remove the first attacker
        occ &= ~(1UL << from);

        // For en passant, remove the captured pawn
        if (isEnPassant)
            occ &= ~(1UL << victimSq);

        int currentAttacker = GetMovePiece(move);
        int currentSide = side ^ 1; // opponent recaptures next

        // FIX Bug (Main): Check for recapturer BEFORE computing gain[d]
        // so we don't record a capture that can't be made
        while (true)
        {
            // Find the least valuable attacker for currentSide FIRST
            int nextAttacker = GetLeastValuableAttacker(to, currentSide, occ, out int nextFrom);

            // No recapture available — exchange is over
            if (nextAttacker < 0)
                break;

            d++;

            // Record what currentSide gains by capturing currentAttacker
            gain[d] = SeeValue[currentAttacker] - gain[d - 1];

            // Simulate: remove the recapturer from the board
            occ &= ~(1UL << nextFrom);

            // Update state for next iteration
            currentAttacker = nextAttacker;
            currentSide ^= 1;
        }

        // Backward minimax pass
        while (d > 0)
        {
            gain[d - 1] = -Math.Max(-gain[d - 1], gain[d]);
            d--;
        }

        return gain[0];
    }

    private static int GetLeastValuableAttacker(int sq, int side, ulong occ, out int fromSquare)
    {
        fromSquare = -1;

        if (side == White)
        {
            ulong pawns = pawnAttacks[Black, sq] & bitboards[P] & occ;
            if (pawns != 0) { fromSquare = BitboardOperations.GetLs1bIndex(pawns); return P; }

            ulong knights = knightAttacks[sq] & bitboards[N] & occ;
            if (knights != 0) { fromSquare = BitboardOperations.GetLs1bIndex(knights); return N; }

            ulong bishops = GetBishopAttacks(sq, occ) & bitboards[B] & occ;
            if (bishops != 0) { fromSquare = BitboardOperations.GetLs1bIndex(bishops); return B; }

            ulong rooks = GetRookAttacks(sq, occ) & bitboards[R] & occ;
            if (rooks != 0) { fromSquare = BitboardOperations.GetLs1bIndex(rooks); return R; }

            ulong queens = (GetBishopAttacks(sq, occ) | GetRookAttacks(sq, occ)) & bitboards[Q] & occ;
            if (queens != 0) { fromSquare = BitboardOperations.GetLs1bIndex(queens); return Q; }

            ulong king = kingAttacks[sq] & bitboards[K] & occ;
            if (king != 0) { fromSquare = BitboardOperations.GetLs1bIndex(king); return K; }
        }
        else
        {
            ulong pawns = pawnAttacks[White, sq] & bitboards[p] & occ;
            if (pawns != 0) { fromSquare = BitboardOperations.GetLs1bIndex(pawns); return p; }

            ulong knights = knightAttacks[sq] & bitboards[n] & occ;
            if (knights != 0) { fromSquare = BitboardOperations.GetLs1bIndex(knights); return n; }

            ulong bishops = GetBishopAttacks(sq, occ) & bitboards[b] & occ;
            if (bishops != 0) { fromSquare = BitboardOperations.GetLs1bIndex(bishops); return b; }

            ulong rooks = GetRookAttacks(sq, occ) & bitboards[r] & occ;
            if (rooks != 0) { fromSquare = BitboardOperations.GetLs1bIndex(rooks); return r; }

            ulong queens = (GetBishopAttacks(sq, occ) | GetRookAttacks(sq, occ)) & bitboards[q] & occ;
            if (queens != 0) { fromSquare = BitboardOperations.GetLs1bIndex(queens); return q; }

            ulong king = kingAttacks[sq] & bitboards[k] & occ;
            if (king != 0) { fromSquare = BitboardOperations.GetLs1bIndex(king); return k; }
        }

        return -1;
    }

    // FIX Bug 8: Returns -1 when no piece is on the square
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
        return -1; // no piece found
    }
}