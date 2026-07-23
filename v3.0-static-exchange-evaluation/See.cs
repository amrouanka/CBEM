// =============================================================================
// SEE.cs  –  Static Exchange Evaluation
//
// SEE answers: "After all captures and recaptures on a square, do we gain
// material or lose it?"
//
// Example:
//   White Pawn captures Black Knight on e5.
//   Black Rook recaptures on e5.
//   White Bishop recaptures...
//   etc.
//
//   SEE computes whether the net result of this exchange chain is ≥ threshold.
//
// Algorithm:
//   1. Find the least-valuable attacker of the target square.
//   2. "Make" that capture (remove the piece from the board copy).
//   3. Alternate sides, repeating until no attackers remain.
//   4. Back-propagate gains: each side only captures if it gains material.
//
// Used in Search for:
//   • Move ordering: good captures first
//   • Pruning: skip losing captures in quiescence
//   • LMR: reduce losing captures like quiet moves
// =============================================================================

using static Board;
using static MoveEncoding;
using static MoveGenerator;

public static class SEE
{
    // =========================================================================
    // Piece values for SEE
    //
    //   These are simplified (not tuned eval values) – the important thing is
    //   relative ordering: Pawn < Knight ≈ Bishop < Rook < Queen.
    // =========================================================================
    private static readonly int[] SeeValues =
    {
        //  P    N    B     R     Q      K
           100, 300, 300,  500, 1000, 10000,  // White (indices 0-5)
           100, 300, 300,  500, 1000, 10000,  // Black (indices 6-11)
    };

    /// <summary>Returns the SEE value for a piece index (0..11).</summary>
    private static int Value(int piece)
    {
        if (piece < 0 || piece > 11) return 0;
        return SeeValues[piece];
    }

    // =========================================================================
    // Public Entry Point
    // =========================================================================

    /// <summary>
    /// Returns true if the capture encoded in <paramref name="move"/> wins at
    /// least <paramref name="threshold"/> centipawns after all recaptures.
    ///
    /// Fast path: obvious winning captures (e.g. Pawn takes Queen) skip SEE.
    /// </summary>
    public static bool IsGoodCapture(int move, int threshold)
    {
        int fromSq = GetMoveSource(move);
        int toSq = GetMoveTarget(move);

        int attacker = GetMovePiece(move);
        int attValue = Value(attacker);

        // --- Fast path: en passant ---
        // Always captures a pawn; rough gain = pawn value
        if (GetMoveEnpassant(move) != 0)
            return Value((int)Piece.P) - threshold >= 0;

        // --- Identify victim ---
        int victim = GetPieceAtSquare(toSq);
        int vicValue = Value(victim);

        // --- Fast path: obviously winning ---
        //
        //   If we gain enough just by capturing (before recaptures),
        //   no need to run full SEE.
        //   e.g. Pawn takes Queen: gain = 1000 - 100 = 900 (always good)
        if (vicValue - attValue >= threshold)
            return true;

        // --- Full SEE ---
        return RunSee(fromSq, toSq, attacker, victim) >= threshold;
    }

    // =========================================================================
    // SEE Implementation
    //
    //   We simulate the exchange on a local copy of the occupancy bitboards.
    //   No board state changes (no undo needed).
    //
    //   The "gain array" stores what each side can gain at each step.
    //   We then back-propagate: a side will only recapture if it gains.
    //
    //   Visual example (White Pawn takes Black Knight on e5):
    //
    //     Step 0: White Pawn takes Knight   gain[0] = +300
    //     Step 1: Black Rook takes Pawn     gain[1] = +100 (black gains pawn)
    //     Step 2: White Bishop takes Rook   gain[2] = +500 (white gains rook)
    //     Step 3: No more attackers         stop
    //
    //     Back-propagate:
    //       gain[1] = max(0, 300 - gain[1]) = max(0, 300-100) = 200  ← black gains 200
    //       gain[0] = max(0, gain[0] - gain[1]) = max(0, 300-200) = 100 ← white gains 100
    //
    //     Result ≥ 0 → good capture for White
    // =========================================================================
    private static int RunSee(int fromSq, int toSq, int movingPiece, int capturedPiece)
    {
        // Working copies of occupancy (we'll remove pieces as they're captured)
        ulong occupied = bitboards[0];  // start with full occupancy
        for (int i = 1; i < 12; i++) occupied |= bitboards[i];

        // Track which pieces are still on the board
        ulong[] bb = new ulong[12];
        for (int i = 0; i < 12; i++) bb[i] = bitboards[i];

        // Gain array: gain[depth] = material gained at this step
        int[] gain = new int[32];
        int depth = 0;

        int currentPiece = movingPiece;
        int currentValue = Value(capturedPiece); // what we gain by capturing

        gain[depth] = currentValue;

        // Remove the capturing piece from the board (it moved to toSq)
        bb[currentPiece] &= ~(1UL << fromSq);
        occupied &= ~(1UL << fromSq);

        // Alternate sides
        int sideToMove = side ^ 1; // opponent recaptures first

        while (true)
        {
            depth++;
            if (depth >= gain.Length) break;

            // What the capturing side would gain = value of piece just moved
            gain[depth] = Value(currentPiece) - gain[depth - 1];

            // The side would only capture if it gains (alpha-cutoff logic)
            if (Math.Max(0, gain[depth]) <= 0 && gain[depth - 1] >= 0)
                break; // this side won't capture

            // Find least-valuable attacker for sideToMove on toSq
            int lva = FindLeastValuableAttacker(toSq, sideToMove, bb, occupied, out int lvaFrom);

            if (lva < 0) break; // no more attackers

            // Remove LVA from board
            bb[lva] &= ~(1UL << lvaFrom);
            occupied &= ~(1UL << lvaFrom);

            // Handle sliding piece X-rays: after removing the piece,
            // a piece behind it may now attack the square
            // (This is handled by recomputing attackers each iteration)

            currentPiece = lva;
            sideToMove ^= 1;
        }

        // Back-propagate gains
        //
        //   Each side only captures if it profits:
        //     gain[d] = max(0, value_of_captured - gain[d+1])
        //
        //   We start from the deepest and work back to 0.
        for (int d = depth - 1; d > 0; d--)
            gain[d - 1] = Math.Max(gain[d - 1], Value(currentPiece) - gain[d]);

        // Actually back-propagate correctly:
        // Redo it properly from the end
        for (int d = depth - 1; d > 0; d--)
            gain[d - 1] -= Math.Max(0, -gain[d]);

        return gain[0];
    }

    // =========================================================================
    // FindLeastValuableAttacker
    //
    //   Finds the least-valuable piece that attacks <targetSq> for <sideToMove>.
    //   Returns the piece index and sets <fromSq> to its square.
    //   Returns -1 if no attacker found.
    //
    //   We check piece types from cheapest to most expensive:
    //     Pawn → Knight → Bishop → Rook → Queen → King
    //
    //   This ensures we always capture with our cheapest piece first (optimal SEE).
    // =========================================================================
    private static int FindLeastValuableAttacker(
        int targetSq,
        int attackingSide,
        ulong[] bb,
        ulong occupied,
        out int fromSq)
    {
        fromSq = -1;

        // Piece index offsets: White = 0..5, Black = 6..11
        int offset = attackingSide == White ? 0 : 6;

        // Check each piece type from cheapest to most expensive
        for (int type = 0; type < 6; type++)
        {
            int pieceIndex = offset + type;
            ulong attackers = GetAttackers(targetSq, pieceIndex, bb, occupied);

            if (attackers != 0)
            {
                fromSq = BitboardOperations.GetLs1bIndex(attackers);
                return pieceIndex;
            }
        }

        return -1; // no attacker
    }

    // =========================================================================
    // GetAttackers
    //
    //   Returns a bitboard of squares where <piece> attacks <targetSq>.
    //   Uses attack tables for pawns, knights, kings.
    //   Uses sliding piece logic (rays through occupancy) for bishops, rooks, queens.
    // =========================================================================
    private static ulong GetAttackers(int targetSq, int piece, ulong[] bb, ulong occupied)
    {
        ulong pieceBb = bb[piece];
        if (pieceBb == 0) return 0;

        // Which side owns this piece?
        bool isWhite = piece < 6;

        switch (piece % 6)
        {
            // --- Pawn ---
            // A white pawn on sq attacks targetSq if pawn's attack table covers targetSq.
            // We reverse: which squares could a pawn of this color stand on to attack targetSq?
            case 0: // Pawn
                {
                    // Reverse pawn attacks: if a black pawn on targetSq would attack sq,
                    // then a white pawn on sq attacks targetSq.
                    ulong reverseAttacks = isWhite
                        ? PieceAttacks.pawnAttacks[Black, targetSq]
                        : PieceAttacks.pawnAttacks[White, targetSq];
                    return reverseAttacks & pieceBb;
                }

            case 1: // Knight
                return PieceAttacks.knightAttacks[targetSq] & pieceBb;

            case 2: // Bishop
                return PieceAttacks.GetBishopAttacks(targetSq, occupied) & pieceBb;

            case 3: // Rook
                return PieceAttacks.GetRookAttacks(targetSq, occupied) & pieceBb;

            case 4: // Queen
                return (PieceAttacks.GetBishopAttacks(targetSq, occupied) |
                        PieceAttacks.GetRookAttacks(targetSq, occupied)) & pieceBb;

            case 5: // King
                return PieceAttacks.kingAttacks[targetSq] & pieceBb;

            default:
                return 0;
        }
    }

    // =========================================================================
    // Helper: GetPieceAtSquare
    //
    //   Returns the piece index (0..11) at a given square, or -1 if empty.
    //   Used to identify the victim piece in IsGoodCapture.
    // =========================================================================
    private static int GetPieceAtSquare(int square)
    {
        ulong bit = 1UL << square;
        for (int piece = 0; piece <= 11; piece++)
        {
            if ((bitboards[piece] & bit) != 0)
                return piece;
        }
        return -1;
    }
}