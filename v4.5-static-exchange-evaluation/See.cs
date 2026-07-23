using static Board;
using static PieceAttacks;

/// <summary>
/// Static Exchange Evaluation (SEE)
///
/// Simulates a sequence of captures on a single square
/// WITHOUT making any moves on the board.
///
/// Visual example — rook takes pawn defended by queen:
///
///   White: Rxd5   gains  +100  (pawn)
///   Black: Qxd5   gains  +500  (rook)
///   Net =  +100 - 500 = -400   losing capture
///
/// SEE returns the estimated material gain/loss
/// for the side making the initial capture.
///
/// IsGoodCapture(move, threshold) returns true if
/// SEE(move) >= threshold.
///
/// Typical thresholds:
///   QSearch prune:  0   (skip if losing anything)
///   LMR extension: -100 (tolerate small losses)
/// </summary>
public static class See
{
    // ─────────────────────────────────────────────
    //  Piece values used ONLY inside SEE.
    //  These are intentionally simple round numbers.
    //  Exact tuned values don't matter here —
    //  what matters is the relative ordering.
    // ─────────────────────────────────────────────
    private static readonly int[] SeeValue =
    [
        100,    // Pawn   (P)
        300,    // Knight (N)
        300,    // Bishop (B)
        500,    // Rook   (R)
        900,    // Queen  (Q)
        20000,  // King   (K)
        100,    // pawn   (p)
        300,    // knight (n)
        300,    // bishop (b)
        500,    // rook   (r)
        900,    // queen  (q)
        20000,  // king   (k)
        0,      // none
    ];

    // ─────────────────────────────────────────────
    //  Public entry point
    //
    //  Returns true if making this capture is
    //  expected to gain >= threshold centipawns
    //  after the full exchange sequence settles.
    //
    //  threshold = 0   → "at least break even"
    //  threshold = -100 → "tolerate up to 100cp loss"
    // ─────────────────────────────────────────────
    public static bool IsGoodCapture(int move, int threshold)
    {
        return Evaluate(move) >= threshold;
    }

    // ─────────────────────────────────────────────
    //  SEE core
    //
    //  Returns the material gain for the side
    //  making the first capture (positive = good).
    // ─────────────────────────────────────────────
    public static int Evaluate(int move)
    {
        int from = MoveEncoding.GetMoveSource(move);
        int to = MoveEncoding.GetMoveTarget(move);
        int victim = MoveEncoding.GetMoveCapture(move);

        // Not a capture — SEE is meaningless here
        if (victim == 0)
            return 0;

        // ── What piece is doing the capturing? ───
        int attacker = MoveEncoding.GetMovePiece(move);

        // ── Gain array ───────────────────────────
        // gain[d] = net material gain if we stop after depth d captures.
        //
        // Visual:
        //   gain[0] = value of what we just captured
        //   gain[1] = value of our attacker - gain[0]
        //   gain[2] = value of next attacker - gain[1]
        //   ...
        //
        // We fill this forward, then walk backward
        // so each side can "stand pat" (stop capturing).
        int[] gain = new int[32];
        int depth = 0;

        gain[depth] = SeeValue[victim];

        // ── Build occupancy snapshot ──────────────
        // We simulate captures by removing pieces from
        // a local copy of occupancy — no board changes.
        ulong occ = Board.occupancies[Board.Both];

        // Remove the capturing piece from its source
        occ &= ~(1UL << from);

        // ── Track which side is capturing ─────────
        // Start: the side that made the first capture
        // Toggle each iteration
        int sideToMove = Board.side ^ 1; // opponent recaptures first

        // ─────────────────────────────────────────
        //  Exchange loop
        //
        //  Each iteration:
        //    1. Find the least valuable attacker of
        //       the target square for the side to move
        //    2. If none found, exchange is over
        //    3. Simulate the capture: record gain,
        //       remove attacker from occupancy,
        //       check for newly revealed X-ray attackers
        // ─────────────────────────────────────────
        while (true)
        {
            depth++;

            // gain[depth] = value of piece just captured (attacker from prev step)
            //               minus what the opponent can gain from here
            // We fill it as: value of attacker we're about to lose
            gain[depth] = SeeValue[attacker] - gain[depth - 1];

            // Find least valuable attacker of 'to' for 'sideToMove'
            int nextAttacker = GetLeastValuableAttacker(to, sideToMove, occ, out int nextFrom);

            // No more attackers — exchange ends
            if (nextAttacker < 0) break;

            // Remove the next attacker from occupancy
            // (simulate it moving to the target square)
            occ &= ~(1UL << nextFrom);

            // The piece that was just captured becomes the new attacker value
            attacker = nextAttacker;

            // Toggle side
            sideToMove ^= 1;
        }

        // ── Negamax minimax backward pass ─────────
        //
        // Each side will ONLY continue the exchange
        // if it improves their outcome.
        //
        // Walking backward from deepest capture:
        //   gain[d-1] = max(gain[d-1], -gain[d])
        //
        // This means: "I only capture if it gains me something"
        //
        // Visual (rook takes pawn defended by queen):
        //   gain[0] = +100  (pawn)
        //   gain[1] = +500 - 100 = +400  (rook)
        //   gain[2] = +900 - 400 = +500  (queen, but this is losing)
        //
        //   backward pass:
        //     gain[1] = max(+400, -500) = +400   queen would not recapture
        //     gain[0] = max(+100, -400) = +100   wait, rook takes was fine?
        //
        //   Actually for the Rxd5 (losing) example:
        //   gain[0] = +100
        //   gain[1] = +500 - 100 = +400   (black queen takes rook)
        //   backward:
        //     gain[0] = max(+100, -400) = +100   rook side thinks they gain 100
        //     but then queen recaptures for +400 net for black
        //
        //   Net from white's perspective: gain[0] after backward = actual gain
        //
        // The final gain[0] is the SEE result.
        while (depth > 0)
        {
            gain[depth - 1] = -Math.Max(-gain[depth - 1], gain[depth]);
            depth--;
        }

        return gain[0];
    }

    // ─────────────────────────────────────────────
    //  GetLeastValuableAttacker
    //
    //  Finds the weakest piece of 'side' that attacks
    //  square 'sq', given current occupancy 'occ'.
    //
    //  Returns the piece type (Board.P etc),
    //  and sets 'fromSquare' to where it sits.
    //  Returns -1 if no attacker found.
    //
    //  We search from weakest to strongest:
    //  Pawn → Knight → Bishop → Rook → Queen → King
    //
    //  Why weakest first?
    //  Because SEE models optimal play — each side
    //  recaptures with its cheapest available piece,
    //  risking the least material.
    // ─────────────────────────────────────────────
    private static int GetLeastValuableAttacker(int sq, int side, ulong occ, out int fromSquare)
    {
        fromSquare = -1;

        if (side == Board.White)
        {
            // ── White pawns ───────────────────────
            // A white pawn attacks sq from diagonally below.
            // pawnAttacks[Black, sq] gives squares a BLACK pawn
            // would attack sq FROM — which are exactly the squares
            // a WHITE pawn on those squares would attack sq.
            ulong pawns = pawnAttacks[Board.Black, sq] & Board.bitboards[Board.P] & occ;
            if (pawns != 0)
            {
                fromSquare = BitboardOperations.GetLs1bIndex(pawns);
                return Board.P;
            }

            // ── White knights ─────────────────────
            ulong knights = knightAttacks[sq] & Board.bitboards[Board.N] & occ;
            if (knights != 0)
            {
                fromSquare = BitboardOperations.GetLs1bIndex(knights);
                return Board.N;
            }

            // ── White bishops ─────────────────────
            // GetBishopAttacks gives all bishop rays from sq
            // intersected with occ. Any white bishop sitting
            // on those rays can attack sq.
            ulong bishops = GetBishopAttacks(sq, occ) & Board.bitboards[Board.B] & occ;
            if (bishops != 0)
            {
                fromSquare = BitboardOperations.GetLs1bIndex(bishops);
                return Board.B;
            }

            // ── White rooks ───────────────────────
            ulong rooks = GetRookAttacks(sq, occ) & Board.bitboards[Board.R] & occ;
            if (rooks != 0)
            {
                fromSquare = BitboardOperations.GetLs1bIndex(rooks);
                return Board.R;
            }

            // ── White queens ──────────────────────
            // Queens attack like bishop + rook combined
            ulong queens = (GetBishopAttacks(sq, occ) | GetRookAttacks(sq, occ))
                           & Board.bitboards[Board.Q] & occ;
            if (queens != 0)
            {
                fromSquare = BitboardOperations.GetLs1bIndex(queens);
                return Board.Q;
            }

            // ── White king ────────────────────────
            ulong king = kingAttacks[sq] & Board.bitboards[Board.K] & occ;
            if (king != 0)
            {
                fromSquare = BitboardOperations.GetLs1bIndex(king);
                return Board.K;
            }
        }
        else
        {
            // ── Black pawns ───────────────────────
            ulong pawns = pawnAttacks[Board.White, sq] & Board.bitboards[Board.p] & occ;
            if (pawns != 0)
            {
                fromSquare = BitboardOperations.GetLs1bIndex(pawns);
                return Board.p;
            }

            // ── Black knights ─────────────────────
            ulong knights = knightAttacks[sq] & Board.bitboards[Board.n] & occ;
            if (knights != 0)
            {
                fromSquare = BitboardOperations.GetLs1bIndex(knights);
                return Board.n;
            }

            // ── Black bishops ─────────────────────
            ulong bishops = GetBishopAttacks(sq, occ) & Board.bitboards[Board.b] & occ;
            if (bishops != 0)
            {
                fromSquare = BitboardOperations.GetLs1bIndex(bishops);
                return Board.b;
            }

            // ── Black rooks ───────────────────────
            ulong rooks = GetRookAttacks(sq, occ) & Board.bitboards[Board.r] & occ;
            if (rooks != 0)
            {
                fromSquare = BitboardOperations.GetLs1bIndex(rooks);
                return Board.r;
            }

            // ── Black queens ──────────────────────
            ulong queens = (GetBishopAttacks(sq, occ) | GetRookAttacks(sq, occ))
                           & Board.bitboards[Board.q] & occ;
            if (queens != 0)
            {
                fromSquare = BitboardOperations.GetLs1bIndex(queens);
                return Board.q;
            }

            // ── Black king ────────────────────────
            ulong king = kingAttacks[sq] & Board.bitboards[Board.k] & occ;
            if (king != 0)
            {
                fromSquare = BitboardOperations.GetLs1bIndex(king);
                return Board.k;
            }
        }

        return -1; // no attacker found
    }
}