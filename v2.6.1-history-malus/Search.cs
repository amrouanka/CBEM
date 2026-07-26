using System.Runtime.CompilerServices;
using static Board;
using static MoveEncoding;
using static MoveGenerator;

public static class Search
{
    private const int MaxPly = 64;
    private const int Infinity = 50000;
    private const int MateScore = 49000;
    private const int MateThreshold = MateScore - MaxPly;

    // LMR
    private const int FullDepthMoves = 4;
    private const int ReductionLimit = 3;
    private const int LmrBase = 1;
    private const int LmrDivisor = 2;

    // Aspiration
    private const int AspirationWindow = 50;
    private const int AspirationMinDepth = 4;
    private const int AspirationRetryLimit = 3;

    // Reverse futility pruning
    private const int ReverseFutilityMaxDepth = 4;
    private const int ReverseFutilityMarginPerDepth = 110;

    // Futility pruning
    private const int FutilityMaxDepth = 4;
    private const int FutilityMarginPerDepth = 120;

    // Null move pruning
    private const int NullMoveMinDepth = 3;
    private const int NullMoveBaseReduction = 3;
    private const int NullMoveDepthDivisor = 4;
    private const int NullMoveEvalDivisor = 200;
    private const int NullMoveEvalBonusCap = 3;

    // Quiescence
    private const int QsDeltaMargin = 200;

    // Internal
    private const int TimeCheckMask = 16383;
    private const int RepetitionTableSize = 1024;
    private const int AllMoves = (int)MoveFlag.allMoves;
    private const int NoSquare = (int)Square.noSquare;

    private static readonly int[,] lmrTable = new int[MaxPly + 1, 64];

    private static int ply;
    private static long nodes;

    public static long LastNodeCount => nodes;
    public static int lastBestMove = 0;
    public static int lastDepthReached = 0;

    private static readonly int[] killerMove1 = new int[MaxPly];
    private static readonly int[] killerMove2 = new int[MaxPly];
    private static readonly int[,] counterMoves = new int[12, 64];
    private static readonly int[,] historyMoves = new int[12, 64];

    private static readonly int[,] mvvLva =
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

    private static readonly ulong[] repetitionTable = new ulong[RepetitionTableSize];
    public static int repetitionIndex = 0;

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

                int reduction = (int)(LmrBase + Math.Log(depth) * Math.Log(moves) / LmrDivisor);
                if (reduction < 1) reduction = 1;

                int maxReduction = depth - 2;
                if (reduction > maxReduction) reduction = maxReduction;

                lmrTable[depth, moves] = reduction;
            }
        }
    }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsMateScore(int score)
    {
        return score >= MateThreshold || score <= -MateThreshold;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        Array.Clear(pvTable, 0, pvTable.Length);
        Array.Clear(pvLength, 0, pvLength.Length);
        Array.Clear(killerMove1, 0, killerMove1.Length);
        Array.Clear(killerMove2, 0, killerMove2.Length);
        Array.Clear(counterMoves, 0, counterMoves.Length);
        Array.Clear(historyMoves, 0, historyMoves.Length);

        int alpha = -Infinity;
        int beta = Infinity;
        int bestMove = 0;
        int completedDepth = 0;

        for (int currentDepth = 1; currentDepth <= depth; currentDepth++)
        {
            int score;

            if (currentDepth < AspirationMinDepth)
            {
                score = AlphaBeta(-Infinity, Infinity, currentDepth);
            }
            else
            {
                score = AlphaBeta(alpha, beta, currentDepth);

                int window = AspirationWindow;
                int failCount = 0;

                while ((score <= alpha || score >= beta) && !TimeManagement.stopped)
                {
                    failCount++;

                    if (failCount >= AspirationRetryLimit)
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

            if (TimeManagement.stopped)
                break;

            alpha = score - AspirationWindow;
            beta = score + AspirationWindow;

            completedDepth = currentDepth;

            if (pvTable[0, 0] != 0)
                bestMove = pvTable[0, 0];

            if (!Program.debug)
            {
                Console.Write($"info score {FormatUciScore(score)} depth {currentDepth} nodes {nodes} pv ");
                for (int i = 0; i < pvLength[0]; i++)
                    Console.Write($"{GetMove(pvTable[0, i])} ");
                Console.WriteLine();
            }

            if (TimeManagement.ShouldStopAfterIteration())
                break;
        }

        if (bestMove == 0)
        {
            MoveList moveList = new MoveList();
            GenerateMoves(ref moveList);

            int[] moves = moveList.moves;
            for (int i = 0; i < moveList.count; i++)
            {
                int move = moves[i];
                BoardState state = CopyBoard();

                if (MakeMove(move, AllMoves) == 0)
                {
                    TakeBack(state);
                    continue;
                }

                TakeBack(state);
                bestMove = move;
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

    private static int AlphaBeta(int alpha, int beta, int depth, bool allowNullMove = true, int prevMove = 0)
    {
        if ((nodes & TimeCheckMask) == 0)
            TimeManagement.Communicate();

        if (TimeManagement.stopped)
            return 0;

        pvLength[ply] = ply;

        if (ply > 0 && IsRepetition())
            return 0;

        if (halfmoveClock >= 100)
            return 0;

        bool inCheck = IsInCheck();

        if (inCheck && ply < MaxPly - 10)
            depth++;

        if (depth <= 0)
            return Quiescence(alpha, beta);

        if (ply >= MaxPly - 1)
            return Evaluation.Evaluate();

        nodes++;

        bool pvNode = (beta - alpha) > 1;

        int ttMove = 0;
        int ttScore = TranspositionTable.Probe(
            Zobrist.hashKey,
            depth,
            alpha,
            beta,
            ply,
            out ttMove);

        if (ttScore != TranspositionTable.NoScore && !pvNode)
            return ttScore;

        int staticEval = inCheck ? -MateScore : Evaluation.Evaluate();

        // Reverse futility pruning
        if (depth <= ReverseFutilityMaxDepth &&
            !pvNode &&
            !inCheck &&
            ply > 0 &&
            staticEval > -MateThreshold &&
            staticEval < MateThreshold)
        {
            int rfpMargin = ReverseFutilityMarginPerDepth * depth;
            if (staticEval - rfpMargin >= beta)
                return beta;
        }

        // Null move pruning
        if (depth >= NullMoveMinDepth &&
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

            if (enPassant != NoSquare)
            {
                Zobrist.hashKey ^= Zobrist.enpassantKeys[enPassant];
                enPassant = NoSquare;
            }

            int evalBonus = (staticEval - beta) / NullMoveEvalDivisor;
            if (evalBonus > NullMoveEvalBonusCap) evalBonus = NullMoveEvalBonusCap;

            int reduction = NullMoveBaseReduction + depth / NullMoveDepthDivisor + evalBonus;
            if (reduction > depth - 1) reduction = depth - 1;

            ply++;
            int nmScore = -AlphaBeta(-beta, -beta + 1, depth - 1 - reduction, false);
            ply--;
            TakeBack(nmState);

            if (TimeManagement.stopped)
                return 0;

            if (nmScore >= beta)
                return beta;
        }

        // Futility pruning
        bool canPrune = false;
        if (depth <= FutilityMaxDepth && !inCheck && !pvNode)
        {
            int futMargin = FutilityMarginPerDepth * depth;
            canPrune = staticEval + futMargin <= alpha;
        }

        MoveList moveList = new MoveList();
        GenerateMoves(ref moveList);

        // Resolve counter-move for the move the opponent just played
        int counterMove = 0;
        if (prevMove != 0)
        {
            int prevPiece = GetMovePiece(prevMove);
            int prevTarget = GetMoveTarget(prevMove);
            counterMove = counterMoves[prevPiece, prevTarget];
        }

        int pvMove = ply == 0 ? pvTable[0, 0] : 0;
        SortMoves(ref moveList, ttMove, pvMove, counterMove);

        int[] moves = moveList.moves;
        int moveCount = moveList.count;

        int movesSearched = 0;
        int bestScore = -Infinity;
        int bestMove = 0;
        int originalAlpha = alpha;
        int legalMoves = 0;
        bool anyMovePruned = false;

        // Buffer to track quiet moves searched (for history malus)
        int quietMovesPlayedCount = 0;
        int[] quietMovesPlayed = new int[64];

        for (int i = 0; i < moveCount; i++)
        {
            int move = moves[i];
            int promoted = GetMovePromoted(move);
            bool isCapture = GetMoveCapture(move) != 0;
            bool isQuiet = !isCapture && promoted == 0;

            if (canPrune &&
                movesSearched > 0 &&
                isQuiet &&
                move != ttMove)
            {
                anyMovePruned = true;
                continue;
            }

            BoardState state = CopyBoard();
            if (MakeMove(move, AllMoves) == 0)
            {
                TakeBack(state);
                continue;
            }

            repetitionTable[repetitionIndex++] = Zobrist.hashKey;
            ply++;
            legalMoves++;

            // Track quiet moves for history malus
            if (isQuiet && quietMovesPlayedCount < 64)
                quietMovesPlayed[quietMovesPlayedCount++] = move;

            int score;

            if (movesSearched == 0)
            {
                score = -AlphaBeta(-beta, -alpha, depth - 1, true, move);
            }
            else
            {
                if (movesSearched >= FullDepthMoves &&
                    depth >= ReductionLimit &&
                    !inCheck &&
                    isQuiet)
                {
                    int moveIndex = movesSearched < 64 ? movesSearched : 63;
                    int reduction = lmrTable[depth, moveIndex];

                    if (pvNode && reduction > 1)
                        reduction--;

                    score = -AlphaBeta(-alpha - 1, -alpha, depth - 1 - reduction, true, move);
                }
                else
                {
                    score = alpha + 1;
                }

                if (score > alpha)
                {
                    score = -AlphaBeta(-alpha - 1, -alpha, depth - 1, true, move);

                    if (score > alpha && score < beta)
                        score = -AlphaBeta(-beta, -alpha, depth - 1, true, move);
                }
            }

            ply--;
            repetitionIndex--;
            TakeBack(state);

            if (TimeManagement.stopped)
                return 0;

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
                    if (killerMove1[ply] != move)
                    {
                        killerMove2[ply] = killerMove1[ply];
                        killerMove1[ply] = move;
                    }

                    // store counter-move keyed on what the opponent just played
                    if (prevMove != 0)
                    {
                        int prevPiece = GetMovePiece(prevMove);
                        int prevTarget = GetMoveTarget(prevMove);
                        counterMoves[prevPiece, prevTarget] = move;
                    }

                    // History bonus for the move that caused the cutoff
                    int bonus = depth * depth;
                    historyMoves[GetMovePiece(move), GetMoveTarget(move)] += bonus;

                    // History malus for all quiet moves that failed before this cutoff
                    int malus = -(depth * depth);
                    for (int q = 0; q < quietMovesPlayedCount; q++)
                    {
                        int failedMove = quietMovesPlayed[q];
                        if (failedMove == move) continue;
                        historyMoves[GetMovePiece(failedMove), GetMoveTarget(failedMove)] += malus;
                    }
                }

                TranspositionTable.Store(
                    Zobrist.hashKey,
                    depth,
                    beta,
                    bestMove,
                    TTFlag.Beta,
                    ply);

                return beta;
            }

            if (score > alpha)
            {
                alpha = score;

                pvTable[ply, ply] = move;
                int nextPly = ply + 1;
                for (int next = nextPly; next < pvLength[nextPly]; next++)
                    pvTable[ply, next] = pvTable[nextPly, next];
                pvLength[ply] = pvLength[nextPly];
            }
        }

        if (legalMoves == 0)
        {
            if (anyMovePruned)
                return staticEval;

            return inCheck ? -MateScore + ply : 0;
        }

        TTFlag flag = alpha <= originalAlpha ? TTFlag.Alpha : TTFlag.Exact;
        TranspositionTable.Store(Zobrist.hashKey, depth, alpha, bestMove, flag, ply);

        return alpha;
    }

    public static int Quiescence(int alpha, int beta)
    {
        if ((nodes & TimeCheckMask) == 0)
            TimeManagement.Communicate();

        if (TimeManagement.stopped)
            return 0;

        if (ply >= MaxPly - 1)
            return Evaluation.Evaluate();

        if (halfmoveClock >= 100)
            return 0;

        nodes++;

        bool inCheck = IsInCheck();

        int eval = 0;
        if (!inCheck)
        {
            eval = Evaluation.Evaluate();

            if (eval >= beta)
                return beta;

            if (eval > alpha)
                alpha = eval;
        }

        MoveList moveList = new MoveList();
        if (inCheck) GenerateMoves(ref moveList);
        else GenerateCaptureMoves(ref moveList);

        SortMoves(ref moveList);

        int[] moves = moveList.moves;
        int moveCount = moveList.count;
        int legalMoves = 0;

        for (int i = 0; i < moveCount; i++)
        {
            int move = moves[i];

            if (!inCheck)
            {
                int target = GetMoveTarget(move);
                int promoted = GetMovePromoted(move);
                int capVal = GetPieceValue(GetPieceAtSquare(target));
                int promoVal = promoted != 0 ? GetPieceValue(promoted) - 89 : 0;

                if (eval + capVal + promoVal + QsDeltaMargin < alpha)
                    continue;
            }

            BoardState state = CopyBoard();
            if (MakeMove(move, AllMoves) == 0)
            {
                TakeBack(state);
                continue;
            }

            legalMoves++;
            ply++;
            int score = -Quiescence(-beta, -alpha);
            ply--;
            TakeBack(state);

            if (TimeManagement.stopped)
                return 0;

            if (score >= beta)
                return beta;

            if (score > alpha)
                alpha = score;
        }

        if (inCheck && legalMoves == 0)
            return -MateScore + ply;

        return alpha;
    }

    private static void SortMoves(ref MoveList moveList, int ttMove = 0, int pvMove = 0, int counterMove = 0)
    {
        int count = moveList.count;
        if (count < 2)
        {
            if (count == 1)
                moveList.scores[0] = ScoreMove(moveList.moves[0], ttMove, pvMove, counterMove);
            return;
        }

        int[] moves = moveList.moves;
        int[] scores = moveList.scores;

        for (int i = 0; i < count; i++)
            scores[i] = ScoreMove(moves[i], ttMove, pvMove, counterMove);

        for (int i = 1; i < count; i++)
        {
            int mv = moves[i];
            int sc = scores[i];
            int j = i - 1;

            while (j >= 0 && scores[j] < sc)
            {
                moves[j + 1] = moves[j];
                scores[j + 1] = scores[j];
                j--;
            }

            moves[j + 1] = mv;
            scores[j + 1] = sc;
        }
    }

    private static int ScoreMove(int move, int ttMove = 0, int pvMove = 0, int counterMove = 0)
    {
        if (move == ttMove) return 30000;

        int promoted = GetMovePromoted(move);
        if (promoted == Q || promoted == q) return 29000;
        if (move == pvMove) return 20000;

        int piece = GetMovePiece(move);
        int target = GetMoveTarget(move);

        if (GetMoveCapture(move) != 0)
        {
            int victim = GetPieceAtSquare(target);
            return mvvLva[piece % 6, victim % 6] + 10000;
        }

        if (killerMove1[ply] == move) return 9000;
        if (killerMove2[ply] == move) return 8000;
        if (move == counterMove) return 7600;
        if (GetMoveCastling(move) != 0) return 7500;
        if (promoted != 0) return 7200;

        int history = historyMoves[piece, target];
        return Math.Clamp(history, -7000, 7000);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsInCheck()
    {
        int kSq = side == White
            ? BitboardOperations.GetLs1bIndex(bitboards[K])
            : BitboardOperations.GetLs1bIndex(bitboards[k]);

        return PieceAttacks.IsSquareAttacked(kSq, side ^ 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasNonPawnMaterial(int sideToCheck)
    {
        return sideToCheck == White
            ? (bitboards[N] | bitboards[B] | bitboards[R] | bitboards[Q]) != 0
            : (bitboards[n] | bitboards[b] | bitboards[r] | bitboards[q]) != 0;
    }
}