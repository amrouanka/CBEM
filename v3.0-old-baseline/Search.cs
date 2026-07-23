using static Board;
using static MoveEncoding;
using static MoveGenerator;

public static class Search
{
    private const int MaxPly = 64;
    private const int Infinity = 50000;
    private const int MateScore = 49000;
    private const int FullDepthMoves = 4;
    private const int ReductionLimit = 3;
    private static readonly int[,] lmrTable = new int[MaxPly + 1, 64];

    static Search()
    {
        for (int depth = 0; depth <= MaxPly; depth++)
        {
            for (int moves = 0; moves < 64; moves++)
            {
                if (depth < 2 || moves < 1)
                {
                    lmrTable[depth, moves] = 1;
                    continue;
                }

                int reduction = (int)(1 + Math.Log(depth) * Math.Log(moves) / 2);
                if (reduction < 1) reduction = 1;

                int maxReduction = depth - 2;
                if (reduction > maxReduction) reduction = maxReduction;

                lmrTable[depth, moves] = reduction;
            }
        }
    }

    private static int ply;
    private static long nodes;
    public static long LastNodeCount => nodes;
    public static int lastBestMove = 0;
    public static int lastDepthReached = 0;

    private static readonly int[] killerMove1 = new int[MaxPly];
    private static readonly int[] killerMove2 = new int[MaxPly];
    private static readonly int[,] historyMoves = new int[12, 64];

    private static readonly int[,] mvvLva = new int[,]
    {
        { 105, 205, 305, 405, 505, 605 },
        { 104, 204, 304, 404, 504, 604 },
        { 103, 203, 303, 403, 503, 603 },
        { 102, 202, 302, 402, 502, 602 },
        { 101, 201, 301, 401, 501, 601 },
        { 100, 200, 300, 400, 500, 600 },
    };

    private static readonly int[,] pvTable = new int[MaxPly, MaxPly];
    private static readonly int[] pvLength = new int[MaxPly];

    private static readonly ulong[] repetitionTable = new ulong[1024];
    public static int repetitionIndex = 0;

    public static void AddToRepetitionHistory(ulong hashKey)
    {
        if (repetitionIndex < repetitionTable.Length)
            repetitionTable[repetitionIndex++] = hashKey;
    }

    public static void RemoveFromRepetitionHistory()
    {
        if (repetitionIndex > 0)
            repetitionIndex--;
    }

    private static bool IsRepetition()
    {
        int index = repetitionIndex;
        if (index < 3)
            return false;

        int earliest = index - 1 - halfmoveClock;
        if (earliest < 0) earliest = 0;

        ulong key = Zobrist.hashKey;

        for (int i = index - 3; i >= earliest; i -= 2)
        {
            if (repetitionTable[i] == key)
                return true;
        }

        return false;
    }

    private const int MateThreshold = MateScore - MaxPly;

    private static bool IsMateScore(int score)
    {
        return Math.Abs(score) >= MateThreshold;
    }

    private static int ScoreToMate(int score)
    {
        if (score > 0)
            return (MateScore - score + 1) / 2;
        return -(MateScore + score) / 2;
    }

    private static string FormatUciScore(int score)
    {
        return IsMateScore(score)
            ? $"mate {ScoreToMate(score)}"
            : $"cp {score}";
    }

    public static void SearchPosition(int depth)
    {
        nodes = 0;
        ply = 0;
        TimeManagement.stopped = false;

        Array.Clear(pvTable);
        Array.Clear(killerMove1);
        Array.Clear(killerMove2);
        Array.Clear(historyMoves);

        int alpha = -Infinity;
        int beta = Infinity;
        int bestMove = 0;
        int completedDepth = 0;

        for (int currentDepth = 1; currentDepth <= depth; currentDepth++)
        {
            int score;
            if (currentDepth < 4)
            {
                score = AlphaBeta(-Infinity, Infinity, currentDepth);
            }
            else
            {
                score = AlphaBeta(alpha, beta, currentDepth);

                int window = 50;
                int failCount = 0;

                while ((score <= alpha || score >= beta) && !TimeManagement.stopped)
                {
                    failCount++;

                    if (failCount >= 3)
                    {
                        alpha = -Infinity;
                        beta = Infinity;
                    }
                    else
                    {
                        if (score <= alpha) alpha -= window;
                        if (score >= beta) beta += window;
                        window += window / 2;
                    }

                    score = AlphaBeta(alpha, beta, currentDepth);
                }
            }

            if (TimeManagement.stopped) break;

            alpha = score - 50;
            beta = score + 50;

            completedDepth = currentDepth;
            if (pvTable[0, 0] != 0) bestMove = pvTable[0, 0];

            if (!Program.debug)
            {
                Console.Write($"info score {FormatUciScore(score)} depth {currentDepth} nodes {nodes} pv ");
                for (int i = 0; i < pvLength[0]; i++)
                    Console.Write($"{GetMove(pvTable[0, i])} ");
                Console.WriteLine();
            }

            if (TimeManagement.ShouldStopAfterIteration()) break;
        }

        if (bestMove == 0)
        {
            MoveList moveList = new MoveList();
            GenerateMoves(ref moveList);

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

            if (bestMove == 0)
            {
                Console.WriteLine("bestmove 0000");
                return;
            }
        }

        if (Program.debug)
        {
            lastBestMove = bestMove;
            lastDepthReached = completedDepth;
        }
        else
        {
            Console.WriteLine($"bestmove {GetMove(bestMove)}");
        }
    }

    private static int AlphaBeta(int alpha, int beta, int depth, bool allowNullMove = true)
    {
        if ((nodes & 16383) == 0) TimeManagement.Communicate();
        if (TimeManagement.stopped) return 0;

        pvLength[ply] = ply;

        if (ply > 0 && IsRepetition()) return 0;
        if (halfmoveClock >= 100) return 0;

        bool inCheck = IsInCheck();

        if (inCheck && ply < MaxPly - 10) depth++;

        if (depth <= 0) return Quiescence(alpha, beta);
        if (ply >= MaxPly - 1) return Evaluation.Evaluate();

        nodes++;

        bool pvNode = (beta - alpha) > 1;
        int ttMove = 0;
        int ttScore = TranspositionTable.Probe(
                           Zobrist.hashKey, depth, alpha, beta, ply, out ttMove);

        if (ttScore != TranspositionTable.NoScore && !pvNode)
            return ttScore;

        int staticEval = inCheck ? -MateScore : Evaluation.Evaluate();

        if (depth <= 3 &&
            !pvNode &&
            !inCheck &&
            ply > 0 &&
            Math.Abs(staticEval) < MateThreshold)
        {
            int rfpMargin = 150 * depth;
            if (staticEval - rfpMargin >= beta)
                return beta;
        }

        if (depth >= 3 &&
            !pvNode &&
            !inCheck &&
            ply > 0 &&
            allowNullMove &&
            HasNonPawnMaterial(side) &&
            staticEval >= beta)
        {
            BoardState nmState = CopyBoard();

            Zobrist.hashKey ^= Zobrist.sideKey;
            side ^= 1;
            halfmoveClock++;

            int noSquare = (int)Square.noSquare;
            if (enPassant != noSquare)
            {
                Zobrist.hashKey ^= Zobrist.enpassantKeys[enPassant];
                enPassant = noSquare;
            }

            int evalBonus = Math.Min((staticEval - beta) / 200, 3);
            int R = 3 + depth / 4 + evalBonus;
            R = Math.Min(R, depth - 1);

            ply++;
            int nmScore = -AlphaBeta(-beta, -beta + 1, depth - 1 - R, false);
            ply--;
            TakeBack(nmState);

            if (TimeManagement.stopped) return 0;
            if (nmScore >= beta) return beta;
        }

        bool futilityOk = depth <= 3 && !inCheck && !pvNode;
        int futMargin = 120 * depth;
        bool canPrune = futilityOk && (staticEval + futMargin <= alpha);

        MoveList moveList = new MoveList();
        GenerateMoves(ref moveList);

        // FIX Bug 16/9: Pass PV move at current ply, not just at root
        int pvMove = (ply < pvLength[0]) ? pvTable[0, ply] : 0;
        SortMoves(moveList, ttMove, pvMove);

        int movesSearched = 0;
        int bestScore = -Infinity;
        int bestMove = 0;
        int originalAlpha = alpha;
        int legalMoves = 0;
        bool anyMovePruned = false;

        for (int i = 0; i < moveList.count; i++)
        {
            int move = moveList.moves[i];
            bool isCapture = GetMoveCapture(move) != 0;
            bool isPromo = GetMovePromoted(move) != 0;
            bool isQuiet = !isCapture && !isPromo;

            if (canPrune &&
                movesSearched > 0 &&
                isQuiet &&
                move != ttMove)
            {
                anyMovePruned = true;
                continue;
            }

            BoardState state = CopyBoard();
            if (MakeMove(move, (int)MoveFlag.allMoves) == 0)
            {
                TakeBack(state);
                continue;
            }

            AddToRepetitionHistory(Zobrist.hashKey);
            ply++;
            legalMoves++;

            int score;

            if (movesSearched == 0)
            {
                score = -AlphaBeta(-beta, -alpha, depth - 1);
            }
            else
            {
                bool isLosingCapture = isCapture && !See.IsGoodCapture(move, 0);

                if (movesSearched >= FullDepthMoves &&
                    depth >= ReductionLimit &&
                    !inCheck &&
                    (isQuiet || isLosingCapture))
                {
                    int moveIndex = movesSearched < 64 ? movesSearched : 63;
                    int reduction = lmrTable[depth, moveIndex];

                    if (pvNode && reduction > 1) reduction--;

                    score = -AlphaBeta(-alpha - 1, -alpha, depth - 1 - reduction);
                }
                else
                {
                    score = alpha + 1;
                }

                if (score > alpha)
                {
                    score = -AlphaBeta(-alpha - 1, -alpha, depth - 1);

                    if (score > alpha && score < beta)
                        score = -AlphaBeta(-beta, -alpha, depth - 1);
                }
            }

            ply--;
            RemoveFromRepetitionHistory();
            TakeBack(state);

            if (TimeManagement.stopped) return 0;

            movesSearched++;

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }

            if (score >= beta)
            {
                if (isQuiet)
                {
                    // FIX: Use current ply (already decremented) for killer storage
                    if (killerMove1[ply] != move)
                    {
                        killerMove2[ply] = killerMove1[ply];
                        killerMove1[ply] = move;
                    }
                }

                TranspositionTable.Store(
                    Zobrist.hashKey, depth, beta, bestMove, TTFlag.Beta, ply);

                return beta;
            }

            if (score > alpha)
            {
                if (isQuiet)
                    historyMoves[GetMovePiece(move), GetMoveTarget(move)] += depth * depth;

                alpha = score;

                pvTable[ply, ply] = move;
                for (int next = ply + 1; next < pvLength[ply + 1]; next++)
                    pvTable[ply, next] = pvTable[ply + 1, next];
                pvLength[ply] = pvLength[ply + 1];
            }
        }

        if (legalMoves == 0)
        {
            if (anyMovePruned) return staticEval;
            return inCheck ? -MateScore + ply : 0;
        }

        TTFlag flag = alpha <= originalAlpha ? TTFlag.Alpha : TTFlag.Exact;
        TranspositionTable.Store(Zobrist.hashKey, depth, alpha, bestMove, flag, ply);

        return alpha;
    }

    public static int Quiescence(int alpha, int beta)
    {
        if ((nodes & 16383) == 0) TimeManagement.Communicate();
        if (TimeManagement.stopped) return 0;

        if (ply >= MaxPly - 1) return Evaluation.Evaluate();
        if (halfmoveClock >= 100) return 0;

        nodes++;

        bool inCheck = IsInCheck();

        int eval = 0;
        if (!inCheck)
        {
            eval = Evaluation.Evaluate();
            if (eval >= beta) return beta;
            if (eval > alpha) alpha = eval;
        }

        MoveList moveList = new MoveList();
        if (inCheck) GenerateMoves(ref moveList);
        else GenerateCaptureMoves(ref moveList);

        SortMoves(moveList);

        int legalMoves = 0;

        for (int i = 0; i < moveList.count; i++)
        {
            int move = moveList.moves[i];

            if (!inCheck)
            {
                if (!See.IsGoodCapture(move, 0))
                    continue;

                // FIX Bug 18: Handle en passant in delta pruning
                int capVal;
                if (GetMoveEnpassant(move) != 0)
                {
                    capVal = GetPieceValue(side == White ? p : P);
                }
                else
                {
                    int capturedPiece = GetPieceAtSquare(GetMoveTarget(move));
                    capVal = capturedPiece >= 0 ? GetPieceValue(capturedPiece) : 0;
                }

                int promoVal = GetMovePromoted(move) != 0
                                ? GetPieceValue(GetMovePromoted(move)) - 88 : 0;
                if (eval + capVal + promoVal + 200 < alpha) continue;
            }

            BoardState state = CopyBoard();
            if (MakeMove(move, (int)MoveFlag.allMoves) == 0)
            {
                TakeBack(state);
                continue;
            }

            legalMoves++;
            ply++;
            int score = -Quiescence(-beta, -alpha);
            ply--;
            TakeBack(state);

            if (TimeManagement.stopped) return 0;

            if (score >= beta) return beta;
            if (score > alpha) alpha = score;
        }

        if (inCheck && legalMoves == 0)
            return -MateScore + ply;

        return alpha;
    }

    private static void SortMoves(MoveList moveList, int ttMove = 0, int pvMove = 0)
    {
        if (moveList.count < 2)
        {
            if (moveList.count == 1)
                moveList.scores[0] = ScoreMove(moveList.moves[0], ttMove, pvMove);
            return;
        }

        for (int i = 0; i < moveList.count; i++)
            moveList.scores[i] = ScoreMove(moveList.moves[i], ttMove, pvMove);

        for (int i = 1; i < moveList.count; i++)
        {
            int mv = moveList.moves[i];
            int sc = moveList.scores[i];
            int j = i - 1;
            while (j >= 0 && moveList.scores[j] < sc)
            {
                moveList.moves[j + 1] = moveList.moves[j];
                moveList.scores[j + 1] = moveList.scores[j];
                j--;
            }
            moveList.moves[j + 1] = mv;
            moveList.scores[j + 1] = sc;
        }
    }

    private static int ScoreMove(int move, int ttMove = 0, int pvMove = 0)
    {
        if (move == ttMove) return 30000;

        int promoted = GetMovePromoted(move);

        if (promoted == Q || promoted == q) return 29000;

        if (move == pvMove) return 20000;

        int piece = GetMovePiece(move);
        int target = GetMoveTarget(move);

        if (GetMoveCapture(move) != 0)
        {
            int victim;

            if (GetMoveEnpassant(move) != 0)
                victim = (side == White) ? p : P;
            else
                victim = GetPieceAtSquare(target);

            // FIX:  Guard against GetPieceAtSquare returning -1
            if (victim < 0) victim = p; // fallback to pawn value

            int mvvScore = mvvLva[piece % 6, victim % 6];

            if (See.IsGoodCapture(move, 0))
                return mvvScore + 10000;
            else
                return mvvScore - 10000;
        }

        if (killerMove1[ply] == move) return 9000;
        if (killerMove2[ply] == move) return 8000;

        if (GetMoveCastling(move) != 0) return 7500;

        if (promoted != 0) return 7200;

        int history = historyMoves[piece, target];
        return history > 7000 ? 7000 : history;
    }

    private static int GetPieceValue(int piece) => piece switch
    {
        P or p => 88,
        N or n => 309,
        B or b => 331,
        R or r => 494,
        Q or q => 981,
        K or k => 20000,
        _ => 0
    };

    private static bool IsInCheck()
    {
        int kSq = side == White
            ? BitboardOperations.GetLs1bIndex(bitboards[K])
            : BitboardOperations.GetLs1bIndex(bitboards[k]);

        return PieceAttacks.IsSquareAttacked(kSq, side ^ 1);
    }

    // FIX Bug 8/18: Returns -1 when no piece found (same fix as in SEE)
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
        return -1; // FIX: was returning 0 (= P piece index) — now returns sentinel
    }

    private static bool HasNonPawnMaterial(int sideToCheck)
    {
        return sideToCheck == White
            ? (bitboards[N] | bitboards[B] | bitboards[R] | bitboards[Q]) != 0
            : (bitboards[n] | bitboards[b] | bitboards[r] | bitboards[q]) != 0;
    }
}