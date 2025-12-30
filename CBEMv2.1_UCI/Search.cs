using System.Drawing;
using static Board;
using static MoveEncoding;
using static MoveGenerator;

public static class Search
{
    // half move counter
    private static int ply;
    // nodes counter
    private static long nodes;
    // MAX PLY to reach within the search - to prevent overflow
    private static readonly int maxPly = 64;

    static readonly int[,] killerMoves = new int[2, maxPly];
    // history moves [piece][SQUARE] (not ply)
    static readonly int[,] historyMoves = new int[12, 64];

    static readonly int[,] pvTable = new int[maxPly, maxPly];

    static readonly int[] pvLength = new int[maxPly];

    static bool followpv = false, scorepv = false;

    static readonly int fullDepthMoves = 2;
    static readonly int reductionLimit = 2;

    // MVV LVA [attacker][victim]
    private static readonly int[,] mvv_lva = new int[,]
    {
            {105, 205, 305, 405, 505, 605},
            {104, 204, 304, 404, 504, 604},
            {103, 203, 303, 403, 503, 603},
            {102, 202, 302, 402, 502, 602},
            {101, 201, 301, 401, 501, 601},
            {100, 200, 300, 400, 500, 600}
    };

    // Main search routine using negamax with alpha-beta pruning
    public static void SearchPosition(int depth)
    {
        nodes = 0;
        followpv = false;
        scorepv = false;

        // clear helper data structures
        Array.Clear(pvTable);
        Array.Clear(pvLength);
        Array.Clear(killerMoves);
        Array.Clear(historyMoves);

        // iteretive deepening
        for (int currentDepth = 1; currentDepth <= depth; currentDepth++)
        {
            followpv = true;

            // find best move within a given position
            int score = AlphaBeta(-50000, 50000, currentDepth);

            /*
            Console.Write($"info score cp {score} depth {currentDepth} nodes {nodes} pv ");

            // loop over the moves within a PV line
            for (int count = 0; count < pvLength[0]; count++)
            {
                // print PV move
                Console.Write($"{GetMove(pvTable[0, count])} ");
            }

            // print new line
            Console.WriteLine();
            */
        }

        // best move placeholder
        Console.Write($"bestmove {GetMove(pvTable[0, 0])}");
        if (Program.debug) Console.Write($" | depth {depth} nodes {nodes}");
        Console.WriteLine();
    }

    // negamax alpha beta search
    private static int AlphaBeta(int alpha, int beta, int depth)
    {
        // init PV length
        pvLength[ply] = ply;

        // recursion escape condition
        if (depth == 0)
            // return evaluation
            return Quiescence(alpha, beta);

        // we are too deep, hence there's an overflow on arrays relying on maxPly constant
        if (ply > maxPly - 1)
            return Evaluation.Evaluate();

        // increment nodes count
        nodes++;

        // cache king position to avoid repeated calculations
        int kingSquare = (side == (int)Side.white) ?
            BitboardOperations.GetLs1bIndex(bitboards[K]) :
            BitboardOperations.GetLs1bIndex(bitboards[k]);
        bool inCheck = PieceAttacks.IsSquareAttacked(kingSquare, side ^ 1);

        int legalMoves = 0;

        // create move list instance
        MoveList moveList = new();

        // generate moves
        GenerateMoves(ref moveList);

        if (followpv) EnablepvScoring(moveList);

        SortMoves(moveList);

        // number of moves searched within a move list
        int movesSearched = 0;

        // loop over moves within a movelist
        for (int count = 0; count < moveList.count; count++)
        {
            // preserve board state
            BoardState state = CopyBoard();

            // make sure to make only legal moves
            if (MakeMove(moveList.moves[count], (int)MoveFlag.allMoves) == 0)
            {
                // skip to next move
                continue;
            }

            // increment ply
            ply++;

            legalMoves++;

            int score;

            if (movesSearched == 0)
            {
                // full search
                score = -AlphaBeta(-beta, -alpha, depth - 1);
            }

            // late move reduction LMR
            else
            {
                // improved LMR conditions
                bool isQuiet = GetMoveCapture(moveList.moves[count]) == 0 && GetMovePromoted(moveList.moves[count]) == 0;
                int reduction = 0;

                if (movesSearched >= fullDepthMoves &&
                    depth >= reductionLimit &&
                    !inCheck &&
                    isQuiet)
                {
                    // dynamic reduction based on move count and depth
                    reduction = 1 + (movesSearched / 6) + (depth / 8);
                    reduction = Math.Min(reduction, 3); // cap reduction at 3 plies

                    // ensure we don't go below depth 1
                    int reducedDepth = Math.Max(1, depth - reduction - 1);
                    score = -AlphaBeta(-alpha - 1, -alpha, reducedDepth);
                }
                else score = alpha + 1;

                // PVS
                if (score > alpha)
                {
                    /* Once you've found a move with a score that is between alpha and beta,
                    rest of the moves are searched with goal of proving that they are all bad.
                    It's possible to do this a bit faster than a search that worries that one
                    of the remaining moves might be good. */
                    score = -AlphaBeta(-alpha - 1, -alpha, depth - 1);

                    /* If the algorithm finds out that it was wrong, and that one of
                    subsequent moves was better than first PV move, it has to search again,
                    in the normal alpha-beta manner.  This happens sometimes, and it's a waste of time,
                    but generally not often enough to counteract the savings gained from doing the
                    "bad move proof" search referred to earlier. */
                    if (score > alpha && score < beta)
                    {
                        /* re-search the move that has failed to be proved to be bad
                        with normal alpha beta score bounds*/
                        score = -AlphaBeta(-beta, -alpha, depth - 1);
                    }
                }
            }
            // decrement ply
            ply--;
            // take move back
            TakeBack(state);
            movesSearched++;

            // fail-hard beta cutoff
            if (score >= beta)
            {
                // update killer moves
                killerMoves[1, ply] = killerMoves[0, ply];
                killerMoves[0, ply] = moveList.moves[count];

                // node (move) fails high
                return beta;
            }

            // found a better move
            if (score > alpha)
            {
                historyMoves[GetMovePiece(moveList.moves[count]), GetMoveTarget(moveList.moves[count])] += depth;

                // PV node (move)
                alpha = score;

                // write PV move
                pvTable[ply, ply] = moveList.moves[count];

                // copy remaining PV line from deeper ply
                int copyLength = pvLength[ply + 1] - (ply + 1);
                if (copyLength > 0)
                {
                    Array.Copy(pvTable, ply + 1 + (ply + 1) * maxPly,
                              pvTable, ply + 1 + ply * maxPly,
                              copyLength);
                }

                // adjust PV length to include the current move
                pvLength[ply] = pvLength[ply + 1];
            }
        }

        if (legalMoves == 0)
        {
            if (inCheck)
            {
                // return mating score (assuming closest distnce to mating position)
                return -49000 + ply;
            }
            else return 0;  // stalemate
        }

        // node (move) fails low
        return alpha;
    }

    public static int Quiescence(int alpha, int beta)
    {
        // increment nodes count
        nodes++;

        int evaluation = Evaluation.Evaluate();

        // fail-hard beta cutoff
        if (evaluation >= beta)
            return beta;

        // found a better move
        if (evaluation > alpha)
            alpha = evaluation;

        // cache king position to avoid repeated calculations
        int kingSquare = (side == (int)Side.white) ?
            BitboardOperations.GetLs1bIndex(bitboards[K]) :
            BitboardOperations.GetLs1bIndex(bitboards[k]);
        bool inCheck = PieceAttacks.IsSquareAttacked(kingSquare, side ^ 1);

        // create move list instance
        MoveList moveList = new();

        // generate moves - all moves if in check, only captures otherwise
        if (inCheck)
        {
            return AlphaBeta(alpha, beta, 1);
        }
        else
        {
            GenerateCaptureMoves(ref moveList);
        }

        SortMoves(moveList);

        // loop over moves within a movelist
        for (int count = 0; count < moveList.count; count++)
        {
            int move = moveList.moves[count];

            // delta pruning - skip captures that can't possibly raise alpha
            int targetSquare = GetMoveTarget(move);
            int capturedPiece = GetPieceAtSquare(targetSquare);
            int captureValue = GetPieceValue(capturedPiece);
            int promotionValue = (GetMovePromoted(move) != 0) ? GetPieceValue(GetMovePromoted(move)) - 100 : 0;

            if (evaluation + captureValue + promotionValue + 200 < alpha)
                continue;

            // preserve board state
            BoardState state = CopyBoard();

            // make sure to make only legal moves
            if (MakeMove(move, (int)MoveFlag.allMoves) == 0)
                continue;

            // increment ply
            ply++;

            // score current move
            int score = -Quiescence(-beta, -alpha);

            // decrement ply
            ply--;

            // take move back
            TakeBack(state);

            // fail-hard beta cutoff
            if (score >= beta)
                return beta;

            // found a better move
            if (score > alpha)
                alpha = score;
        }

        return alpha;
    }


    private static void SortMoves(MoveList moveList)
    {
        // score moves once
        for (int i = 0; i < moveList.count; i++)
            moveList.scores[i] = ScoreMove(moveList.moves[i]);

        // simple insertion sort for move lists (usually small)
        for (int i = 1; i < moveList.count; i++)
        {
            int keyMove = moveList.moves[i];
            int keyScore = moveList.scores[i];
            int j = i - 1;

            while (j >= 0 && moveList.scores[j] < keyScore)
            {
                moveList.moves[j + 1] = moveList.moves[j];
                moveList.scores[j + 1] = moveList.scores[j];
                j--;
            }
            moveList.moves[j + 1] = keyMove;
            moveList.scores[j + 1] = keyScore;
        }
    }

    // score moves
    private static int ScoreMove(int move)
    {
        if (scorepv)
        {
            if (pvTable[0, ply] == move)
            {
                scorepv = false;
                // give pv the highest score to search it first
                return 20000;
            }
        }

        // score capture move
        if (GetMoveCapture(move) != 0)
        {
            int targetSquare = GetMoveTarget(move);
            int targetPiece = GetPieceAtSquare(targetSquare);

            // score move by MVV LVA lookup [source piece][target piece]
            return mvv_lva[GetMovePiece(move) % 6, targetPiece % 6] + 10000;
        }
        // score quiet move
        else
        {
            if (killerMoves[0, ply] == move)
                return 9000;
            else if (killerMoves[1, ply] == move)
                return 8000;
            else
                return historyMoves[GetMovePiece(move), GetMoveTarget(move)];
        }
    }

    // helper method to get piece values for delta pruning
    private static int GetPieceValue(int piece)
    {
        return piece switch
        {
            P or p => 100,
            N or n => 320,
            B or b => 330,
            R or r => 500,
            Q or q => 900,
            K or k => 20000,
            _ => 0
        };
    }

    // helper method to get piece at square without bitboard loop
    private static int GetPieceAtSquare(int square)
    {
        ulong mask = 1UL << square;

        // check white pieces
        if ((bitboards[P] & mask) != 0) return P;
        if ((bitboards[N] & mask) != 0) return N;
        if ((bitboards[B] & mask) != 0) return B;
        if ((bitboards[R] & mask) != 0) return R;
        if ((bitboards[Q] & mask) != 0) return Q;
        if ((bitboards[K] & mask) != 0) return K;

        // check black pieces
        if ((bitboards[p] & mask) != 0) return p;
        if ((bitboards[n] & mask) != 0) return n;
        if ((bitboards[b] & mask) != 0) return b;
        if ((bitboards[r] & mask) != 0) return r;
        if ((bitboards[q] & mask) != 0) return q;
        if ((bitboards[k] & mask) != 0) return k;

        return P; // default to pawn if not found
    }

    static void EnablepvScoring(MoveList moveList)
    {
        // disable following PV
        followpv = false;

        for (int count = 0; count < moveList.count; count++)
        {
            // make sure we hit pv move
            if (pvTable[0, ply] == moveList.moves[count])
            {
                scorepv = true;
                followpv = true;
            }
        }
    }
}