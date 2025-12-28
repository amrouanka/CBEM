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

    private static int[,] killerMoves = new int[2,64];

    private static int[,] historyMoves = new int[12,64];

    private static int[,] pvTable = new int[64, 64];

    private static int[] pvLength = new int[64];


    // Main search routine using negamax with alpha-beta pruning
    public static void SearchPosition(int depth)
    {
        nodes = 0;

        // find best move within a given position
        int score = Negamax(-50000, 50000, depth);

        Console.Write($"info score cp {score} depth {depth} nodes {nodes} pv ");
        
        // loop over the moves within a PV line
        for (int count = 0; count < pvLength[0]; count++)
        {
            // print PV move
            Console.Write($"{GetMove(pvTable[0, count])} ");
        }
        
        // print new line
        Console.WriteLine();

        // best move placeholder
        Console.Write($"bestmove {GetMove(pvTable[0, 0])}");
        Console.WriteLine();
    }


    // negamax alpha beta search
    private static int Negamax(int alpha, int beta, int depth)
    {
        // init PV length
        pvLength[ply] = ply;

        // recursion escape condition
        if (depth == 0)
            // return evaluation
            return Quiescence(alpha, beta);

        // increment nodes count
        nodes++;

        bool inCheck = PieceAttacks.IsSquareAttacked((side == (int)Side.white) ? BitboardOperations.GetLs1bIndex(bitboards[K]) : BitboardOperations.GetLs1bIndex(bitboards[k]), side ^ 1);

        if (inCheck) depth++;

        int legalMoves = 0;

        // create move list instance
        MoveList moveList = new MoveList();

        // generate moves
        GenerateMoves(ref moveList);

        // score moves once
        for (int i = 0; i < moveList.count; i++)
            moveList.scores[i] = ScoreMove(moveList.moves[i]);

        // sort by cached scores (descending)
        Array.Sort(moveList.scores, moveList.moves, 0, moveList.count);
        Array.Reverse(moveList.moves, 0, moveList.count);
        Array.Reverse(moveList.scores, 0, moveList.count);

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

                for (int nextPly = ply + 1; nextPly < pvLength[ply + 1]; nextPly++)
                    pvTable[ply, nextPly] = pvTable[ply + 1, nextPly];

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

        // score moves once
        for (int i = 0; i < moveList.count; i++)
            moveList.scores[i] = ScoreMove(moveList.moves[i]);

        // sort by cached scores (descending)
        Array.Sort(moveList.scores, moveList.moves, 0, moveList.count);
        Array.Reverse(moveList.moves, 0, moveList.count);
        Array.Reverse(moveList.scores, 0, moveList.count);

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
            return mvv_lva[GetMovePiece(move), target_piece] + 10000;
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
}