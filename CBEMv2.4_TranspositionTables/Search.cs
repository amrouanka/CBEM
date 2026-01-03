using System.Drawing;
using System.Runtime.InteropServices;
using static Board;
using static MoveEncoding;
using static MoveGenerator;

public static class Search
{
    // Current search depth (half-moves from root)
    private static int ply;
    // Total nodes searched in current iteration
    private static long nodes;
    // Public property to access the last node count
    public static long LastNodeCount => nodes;
    // Maximum search depth to prevent array overflow
    private static readonly int maxPly = 64;

    // Killer moves: [2 slots][ply] - stores non-capture moves that caused beta cutoffs
    static readonly int[,] killerMoves = new int[2, maxPly];
    // History moves: [piece type][square] - scores quiet moves based on past success
    static readonly int[,] historyMoves = new int[12, 64];

    // Principal Variation table: [ply][move_index] - stores best moves at each depth
    static readonly int[,] pvTable = new int[maxPly, maxPly];

    // PV length array: stores length of PV line from each ply
    static readonly int[] pvLength = new int[maxPly];

    // PV tracking flags
    static bool followpv = false, scorepv = false;

    // LMR parameters: first N moves searched full depth, minimum depth for reduction
    static readonly int fullDepthMoves = 2;
    static readonly int reductionLimit = 3;



    // MVV-LVA: [attacker][victim] - Most Valuable Victim, Least Valuable Attacker
    private static readonly int[,] mvv_lva = new int[,]
    {
        {105, 205, 305, 405, 505, 605},
        {104, 204, 304, 404, 504, 604},
        {103, 203, 303, 403, 503, 603},
        {102, 202, 302, 402, 502, 602},
        {101, 201, 301, 401, 501, 601},
        {100, 200, 300, 400, 500, 600}
    };

    // Main search entry point with iterative deepening and aspiration windows
    public static void SearchPosition(int depth)
    {
        nodes = 0;
        followpv = false;
        scorepv = false;
        TimeManagement.stopped = false;

        Array.Clear(pvTable);
        Array.Clear(killerMoves);
        Array.Clear(historyMoves);

        int alpha = -50000;
        int beta = 50000;
        int score = 0;
        int window = 50; // Initial window size (approx 0.5 pawn)

        for (int currentDepth = 1; currentDepth <= depth; currentDepth++)
        {
            followpv = true;

            // Perform the search with aspiration windows
            score = AlphaBeta(alpha, beta, currentDepth);

            // If the score falls outside our window, we must re-search
            while (score <= alpha || score >= beta)
            {
                // If we fail low (score <= alpha), the score is worse than expected. 
                // We lower alpha (expand search downwards).
                if (score <= alpha) alpha -= window;

                // If we fail high (score >= beta), the score is better than expected.
                // We raise beta (expand search upwards).
                if (score >= beta) beta += window;

                // Widen the window for the next attempt if we fail again
                window += (window / 2);

                // Re-search with the wider window at the SAME depth
                score = AlphaBeta(alpha, beta, currentDepth);

                // Safety break for time management
                if (TimeManagement.stopped) break;
            }

            // Narrow the window for the next depth iteration around the found score
            alpha = score - 50;
            beta = score + 50;
            window = 50; // Reset window expansion

            if (TimeManagement.stopped) break;

            // Output search info in UCI format
            if (!Program.debug)
            {
                Console.Write($"info score cp {score} depth {currentDepth} nodes {nodes} pv ");
                for (int count = 0; count < pvLength[0]; count++)
                {
                    Console.Write($"{GetMove(pvTable[0, count])} ");
                }
                Console.WriteLine();
            }
        }

        // Get best move from PV table or find first legal move
        int bestMove = pvTable[0, 0];
        if (bestMove == 0)
        {
            MoveList moveList = new();
            GenerateMoves(ref moveList);

            // Find first legal move
            for (int i = 0; i < moveList.count; i++)
            {
                BoardState state = CopyBoard();

                if (MakeMove(moveList.moves[i], (int)MoveFlag.allMoves) == 0)
                {
                    TakeBack(state);
                    continue;
                }

                TakeBack(state);
                bestMove = moveList.moves[i];
                break;
            }

            // No legal moves available
            if (bestMove == 0)
            {
                Console.WriteLine("bestmove 0000");
                return;
            }
        }

        // Output best move in UCI format
        Console.Write($"bestmove {GetMove(bestMove)}");
        if (Program.debug) Console.Write($" | depth {depth} nodes {nodes}");
        Console.WriteLine();
    }

    // Negamax alpha-beta search with various pruning techniques
    private static int AlphaBeta(int alpha, int beta, int depth)
    {
        // Check time and input periodically
        if ((nodes & 2047) == 0)
            TimeManagement.Communicate();

        // Stop search if time is up
        if (TimeManagement.stopped)
            return 0;

        // Initialize PV length for current ply
        pvLength[ply] = ply;

        // Leaf node: switch to quiescence search
        if (depth == 0)
            return Quiescence(alpha, beta);

        // Prevent array overflow at maximum depth
        if (ply > maxPly - 1)
            return Evaluation.Evaluate();

        nodes++;

        // Check if current side is in check
        int kingSquare = (side == (int)Side.white) ?
            BitboardOperations.GetLs1bIndex(bitboards[K]) :
            BitboardOperations.GetLs1bIndex(bitboards[k]);
        bool inCheck = PieceAttacks.IsSquareAttacked(kingSquare, side ^ 1);

        int legalMoves = 0;

        // Null move pruning: try a null move to find easy beta cutoffs
        if (depth >= 3 && !inCheck && ply > 0)
        {
            BoardState state = CopyBoard();
            side ^= 1;
            enPassant = (int)Square.noSquare;

            int R = 2; // Null move reduction depth
            int score = -AlphaBeta(-beta, -beta + 1, depth - 1 - R);

            TakeBack(state);

            if (score >= beta)
                return beta;
        }

        // Futility pruning: skip nodes that can't improve alpha
        if (depth <= 3 && !inCheck)
        {
            int eval = Evaluation.Evaluate();
            int futilityMargin = depth * 150;
            if (eval + futilityMargin <= alpha)
                return alpha;
        }

        // Generate and sort moves
        MoveList moveList = new();
        GenerateMoves(ref moveList);

        if (followpv) EnablepvScoring(moveList);
        SortMoves(moveList);

        int movesSearched = 0;

        // Search all moves
        for (int count = 0; count < moveList.count; count++)
        {
            BoardState state = CopyBoard();

            // Skip illegal moves
            if (MakeMove(moveList.moves[count], (int)MoveFlag.allMoves) == 0)
                continue;

            ply++;
            legalMoves++;

            int score;

            // First move: full window search
            if (movesSearched == 0)
            {
                score = -AlphaBeta(-beta, -alpha, depth - 1);
            }
            // Later moves: try late move reduction
            else
            {
                bool isQuiet = GetMoveCapture(moveList.moves[count]) == 0 && GetMovePromoted(moveList.moves[count]) == 0;

                // Apply LMR to quiet moves
                if (movesSearched >= fullDepthMoves &&
                    depth >= reductionLimit &&
                    !inCheck &&
                    isQuiet)
                {
                    int reduction = 1 + (movesSearched / 2) + (depth / 3);
                    reduction = Math.Min(reduction, 6);
                    int reducedDepth = Math.Max(1, depth - reduction - 1);
                    score = -AlphaBeta(-alpha - 1, -alpha, reducedDepth);
                }
                else score = alpha + 1;

                // Principal Variation Search (PVS)
                if (score > alpha)
                {
                    // Research with narrow window
                    score = -AlphaBeta(-alpha - 1, -alpha, depth - 1);

                    // If move is better than alpha but less than beta, research with full window
                    if (score > alpha && score < beta)
                    {
                        score = -AlphaBeta(-beta, -alpha, depth - 1);
                    }
                }
            }

            ply--;
            TakeBack(state);

            if (TimeManagement.stopped == true)
                return 0;

            movesSearched++;

            // Beta cutoff: move is too good for opponent
            if (score >= beta)
            {
                // Store as killer move for future ordering
                if (GetMoveCapture(moveList.moves[count]) == 0)
                {
                    killerMoves[1, ply] = killerMoves[0, ply];
                    killerMoves[0, ply] = moveList.moves[count];
                }

                return beta;
            }

            // Alpha improvement: found better move
            if (score > alpha)
            {
                // Update history score for quiet moves
                if (GetMoveCapture(moveList.moves[count]) == 0)
                {
                    historyMoves[GetMovePiece(moveList.moves[count]), GetMoveTarget(moveList.moves[count])] += depth;
                }

                alpha = score;

                // Store move in PV table
                pvTable[ply, ply] = moveList.moves[count];

                // Copy PV line from deeper ply
                int copyLength = pvLength[ply + 1] - (ply + 1);
                if (copyLength > 0)
                {
                    Array.Copy(pvTable, ply + 1 + (ply + 1) * maxPly,
                              pvTable, ply + 1 + ply * maxPly,
                              copyLength);
                }

                // Update PV length
                pvLength[ply] = pvLength[ply + 1];
            }
        }

        // Check for checkmate or stalemate
        if (legalMoves == 0)
        {
            if (inCheck)
            {
                // Checkmate: negative score based on distance to mate
                return -49000 + ply;
            }
            else
            {
                // Stalemate: draw score
                return 0;
            }
        }

        // Return best score found
        return alpha;
    }

    // Quiescence search: resolves captures at leaf nodes to avoid horizon effect
    public static int Quiescence(int alpha, int beta)
    {
        // Check time and input periodically
        if ((nodes & 2047) == 0)
            TimeManagement.Communicate();

        // Stop search if time is up
        if (TimeManagement.stopped)
            return 0;

        nodes++;

        int evaluation = Evaluation.Evaluate();

        // Beta cutoff
        if (evaluation >= beta)
            return beta;

        // Alpha improvement
        if (evaluation > alpha)
            alpha = evaluation;

        // Check if in check (need all moves, not just captures)
        int kingSquare = (side == (int)Side.white) ?
            BitboardOperations.GetLs1bIndex(bitboards[K]) :
            BitboardOperations.GetLs1bIndex(bitboards[k]);
        bool inCheck = PieceAttacks.IsSquareAttacked(kingSquare, side ^ 1);

        // Generate moves: all if in check, only captures otherwise
        MoveList moveList = new();
        if (inCheck)
        {
            GenerateMoves(ref moveList);
        }
        else
        {
            GenerateCaptureMoves(ref moveList);
        }

        SortMoves(moveList);

        // Search all captures
        for (int count = 0; count < moveList.count; count++)
        {
            int move = moveList.moves[count];

            // Delta pruning: skip captures that can't improve alpha
            int targetSquare = GetMoveTarget(move);
            int capturedPiece = GetPieceAtSquare(targetSquare);
            int captureValue = GetPieceValue(capturedPiece);
            int promotionValue = (GetMovePromoted(move) != 0) ? GetPieceValue(GetMovePromoted(move)) - 100 : 0;

            if (evaluation + captureValue + promotionValue + 10 < alpha)
                continue;

            BoardState state = CopyBoard();

            // Skip illegal moves
            if (MakeMove(move, (int)MoveFlag.allMoves) == 0)
                continue;

            ply++;
            int score = -Quiescence(-beta, -alpha);
            ply--;

            TakeBack(state);

            if (TimeManagement.stopped == true)
                return 0;

            // Beta cutoff
            if (score >= beta)
                return beta;

            // Alpha improvement
            if (score > alpha)
                alpha = score;
        }

        return alpha;
    }

    // Sort moves by score using insertion sort (efficient for small lists)
    private static void SortMoves(MoveList moveList)
    {
        // Score all moves first
        for (int i = 0; i < moveList.count; i++)
            moveList.scores[i] = ScoreMove(moveList.moves[i]);

        // Insertion sort by score (descending)
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

    // Score moves for ordering: PV moves first, then captures, then quiet moves
    private static int ScoreMove(int move)
    {
        // PV move gets highest priority
        if (scorepv)
        {
            if (pvTable[0, ply] == move)
            {
                scorepv = false;
                return 20000; // Highest score for PV move
            }
        }

        // Capture moves: score by MVV-LVA
        if (GetMoveCapture(move) != 0)
        {
            int targetSquare = GetMoveTarget(move);
            int targetPiece = GetPieceAtSquare(targetSquare);
            return mvv_lva[GetMovePiece(move) % 6, targetPiece % 6] + 10000;
        }
        // Quiet moves: score by killer moves and history
        else
        {
            if (killerMoves[0, ply] == move)
                return 9000; // First killer move
            else if (killerMoves[1, ply] == move)
                return 8000; // Second killer move
            else
                return historyMoves[GetMovePiece(move), GetMoveTarget(move)]; // History score
        }
    }

    // Get piece value for delta pruning and move scoring
    private static int GetPieceValue(int piece)
    {
        return piece switch
        {
            P or p => 100,  // Pawn
            N or n => 320,  // Knight
            B or b => 330,  // Bishop
            R or r => 500,  // Rook
            Q or q => 900,  // Queen
            K or k => 20000, // King
            _ => 0
        };
    }

    // Get piece at specific square without bitboard iteration
    private static int GetPieceAtSquare(int square)
    {
        ulong mask = 1UL << square;

        // Check white pieces
        if ((bitboards[P] & mask) != 0) return P;
        if ((bitboards[N] & mask) != 0) return N;
        if ((bitboards[B] & mask) != 0) return B;
        if ((bitboards[R] & mask) != 0) return R;
        if ((bitboards[Q] & mask) != 0) return Q;
        if ((bitboards[K] & mask) != 0) return K;

        // Check black pieces
        if ((bitboards[p] & mask) != 0) return p;
        if ((bitboards[n] & mask) != 0) return n;
        if ((bitboards[b] & mask) != 0) return b;
        if ((bitboards[r] & mask) != 0) return r;
        if ((bitboards[q] & mask) != 0) return q;
        if ((bitboards[k] & mask) != 0) return k;

        return 0; // Empty square
    }

    // Enable PV move scoring for current ply
    static void EnablepvScoring(MoveList moveList)
    {
        followpv = false;

        // Find PV move in move list and enable scoring
        for (int count = 0; count < moveList.count; count++)
        {
            if (pvTable[0, ply] == moveList.moves[count])
            {
                scorepv = true;
                followpv = true;
            }
        }
    }
}
