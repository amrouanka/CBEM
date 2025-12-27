using System.Drawing;
using static Board;
using static MoveEncoding;
using static MoveGenerator;

public static class Search
{
    // half move counter
    private static int ply;
    // best move
    private static int bestMove;
    // nodes counter
    private static long nodes;

    // MVV LVA [attacker][victim]
    private static readonly int[,] mvv_lva = new int[,]
    {
        {105, 205, 305, 405, 505, 605,  105, 205, 305, 405, 505, 605},
        {104, 204, 304, 404, 504, 604,  104, 204, 304, 404, 504, 604},
        {103, 203, 303, 403, 503, 603,  103, 203, 303, 403, 503, 603},
        {102, 202, 302, 402, 502, 602,  102, 202, 302, 402, 502, 602},
        {101, 201, 301, 401, 501, 601,  101, 201, 301, 401, 501, 601},
        {100, 200, 300, 400, 500, 600,  100, 200, 300, 400, 500, 600},

        {105, 205, 305, 405, 505, 605,  105, 205, 305, 405, 505, 605},
        {104, 204, 304, 404, 504, 604,  104, 204, 304, 404, 504, 604},
        {103, 203, 303, 403, 503, 603,  103, 203, 303, 403, 503, 603},
        {102, 202, 302, 402, 502, 602,  102, 202, 302, 402, 502, 602},
        {101, 201, 301, 401, 501, 601,  101, 201, 301, 401, 501, 601},
        {100, 200, 300, 400, 500, 600,  100, 200, 300, 400, 500, 600}
    };


    // Main search routine using negamax with alpha-beta pruning
    public static void SearchPosition(int depth)
    {
        nodes = 0;

        // find best move within a given position
        int score = Negamax(-50000, 50000, depth);

        if (bestMove != 0)
        {
            // print search info
            Console.WriteLine($"info score cp {score} depth {depth} nodes {nodes}");

            // best move placeholder
            Console.WriteLine($"bestmove {GetMove(bestMove)}");
        }
    }


    // negamax alpha beta search
    private static int Negamax(int alpha, int beta, int depth)
    {
        // recursion escape condition
        if (depth == 0)
            // return evaluation
            return Quiescence(alpha, beta);

        // increment nodes count
        nodes++;

        bool inCheck = PieceAttacks.IsSquareAttacked((side == (int)Side.white) ? BitboardOperations.GetLs1bIndex(bitboards[K]) : BitboardOperations.GetLs1bIndex(bitboards[k]), side ^ 1);

        if (inCheck) depth++;

        int legalMoves = 0;

        // best move so far
        int best_sofar = 0;

        // old value of alpha
        int old_alpha = alpha;

        // create move list instance
        MoveList moveList = new MoveList();

        // generate moves
        GenerateMoves(ref moveList);

        // sort moves by MVV-LVA score (descending)
        Array.Sort(moveList.moves, 0, moveList.count, 
            Comparer<int>.Create((a, b) => ScoreMove(b).CompareTo(ScoreMove(a))));

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

            // score current move
            int score = -Negamax(-beta, -alpha, depth - 1);

            // decrement ply
            ply--;

            // take move back
            Board.TakeBack(state);

            // fail-hard beta cutoff
            if (score >= beta)
            {
                // node (move) fails high
                return beta;
            }

            // found a better move
            if (score > alpha)
            {
                // PV node (move)
                alpha = score;

                // if root move
                if (ply == 0)
                    // associate best move with the best score
                    best_sofar = moveList.moves[count];
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

        // found better move
        if (old_alpha != alpha)
            // init best move
            bestMove = best_sofar;

        // node (move) fails low
        return alpha;
    }

    public static int Quiescence(int alpha, int beta, int depth = 0)
    {
        // depth limit to prevent infinite recursion
        if (depth > 1)
            return Evaluation.Evaluate();

        // increment nodes count
        nodes++;

        int evaluation = Evaluation.Evaluate();

        // fail-hard beta cutoff
        if (evaluation >= beta)
            return beta;

        // found a better move
        if (evaluation > alpha)
            alpha = evaluation;

        // check for check evasion
        bool inCheck = PieceAttacks.IsSquareAttacked((side == (int)Side.white) ?
            BitboardOperations.GetLs1bIndex(bitboards[K]) :
            BitboardOperations.GetLs1bIndex(bitboards[k]), side ^ 1);

        // create move list instance
        MoveList moveList = new MoveList();

        // generate moves - all moves if in check, only captures otherwise
        if (inCheck)
        {
            GenerateMoves(ref moveList);
        }
        else
        {
            GenerateCaptureMoves(ref moveList);
        }

        // sort moves by MVV-LVA score (descending)
        Array.Sort(moveList.moves, 0, moveList.count, 
            Comparer<int>.Create((a, b) => ScoreMove(b).CompareTo(ScoreMove(a))));

        // loop over moves within a movelist
        for (int count = 0; count < moveList.count; count++)
        {
            int move = moveList.moves[count];

            // preserve board state
            BoardState state = CopyBoard();

            // make sure to make only legal moves
            if (MakeMove(move, (int)MoveFlag.allMoves) == 0)
                continue;

            // increment ply
            ply++;

            // score current move
            int score = -Quiescence(-beta, -alpha, depth + 1);

            // decrement ply
            ply--;

            // take move back
            Board.TakeBack(state);

            // fail-hard beta cutoff
            if (score >= beta)
                return beta;

            // found a better move
            if (score > alpha)
                alpha = score;
        }

        return alpha;
    }
    
    // score moves
    private static int ScoreMove(int move)
    {
        // score capture move
        if (GetMoveCapture(move) != 0)
        {
            // init target piece
            int target_piece = (int)Piece.P;
            
            // pick up bitboard piece index ranges depending on side
            int start_piece, end_piece;
            
            // pick up side to move
            if (side == (int)Side.white) 
            { 
                start_piece = (int)Piece.p; 
                end_piece = (int)Piece.k; 
            }
            else 
            { 
                start_piece = (int)Piece.P; 
                end_piece = (int)Piece.K; 
            }
            
            // loop over bitboards opposite to the current side to move
            for (int bb_piece = start_piece; bb_piece <= end_piece; bb_piece++)
            {
                // if there's a piece on the target square
                if (BitboardOperations.GetBit(bitboards[bb_piece], GetMoveTarget(move)))
                {
                    // remove it from corresponding bitboard
                    target_piece = bb_piece;
                    break;
                }
            }
                    
            // score move by MVV LVA lookup [source piece][target piece]
            return mvv_lva[GetMovePiece(move), target_piece];
        }
        
        // score quiet move
        else
        {
        
        }
        
        return 0;
    }
}