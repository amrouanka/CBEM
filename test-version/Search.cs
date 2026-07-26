using System.Runtime.CompilerServices;
using System.Text;
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

    // Flat-array stride shifts (all power-of-two so indexing is shift+or, never imul)
    private const int SquareShift = 6;   // 64 squares
    private const int SideShift = 12;    // 64 * 64 history block per side
    private const int PieceShift = 4;    // 16-wide mvv/lva rows (12 used, padded for shift indexing)
    private const int PvRowShift = 6;    // pvTable row stride == MaxPly == 64
    private const int LmrRowShift = 6;   // lmrTable row stride == 64
    private const int KillerShift = 1;   // 2 killers per ply, adjacent in one cache line

    // [depth][moveIndex] flattened
    private static readonly int[] lmrTable = new int[(MaxPly + 1) << LmrRowShift];

    private static int ply;
    private static long nodes;

    public static long LastNodeCount => nodes;
    public static int lastBestMove = 0;
    public static int lastDepthReached = 0;

    // Killer 1 and killer 2 for a ply are adjacent -> single cache line touch per node
    private static readonly int[] killerMoves = new int[MaxPly << KillerShift];

    // [piece][target] flattened
    private static readonly int[] counterMoves = new int[12 << SquareShift];

    // [side][source][target] flattened
    private static readonly int[] historyMoves = new int[2 << SideShift];

    private static readonly int[,] mvvLva =
    {
        { 105, 205, 305, 405, 505, 605 },
        { 104, 204, 304, 404, 504, 604 },
        { 103, 203, 303, 403, 503, 603 },
        { 102, 202, 302, 402, 502, 602 },
        { 101, 201, 301, 401, 501, 601 },
        { 100, 200, 300, 400, 500, 600 },
    };

    // mvvLva pre-expanded to full 12x12 piece indices so the hot path never executes '% 6'
    private static readonly int[] mvvLvaTable = new int[12 << PieceShift];

    // [ply][index] flattened
    private static readonly int[] pvTable = new int[MaxPly << PvRowShift];
    private static readonly int[] pvLength = new int[MaxPly];

    private static readonly ulong[] repetitionTable = new ulong[RepetitionTableSize];
    public static int repetitionIndex = 0;

    static Search()
    {
        for (int depth = 0; depth <= MaxPly; depth++)
        {
            int rowBase = depth << LmrRowShift;

            for (int moves = 0; moves < 64; moves++)
            {
                if (depth < 2 || moves < 1)
                {
                    lmrTable[rowBase + moves] = 1;
                    continue;
                }

                int reduction = (int)(LmrBase + Math.Log(depth) * Math.Log(moves) / LmrDivisor);
                if (reduction < 1) reduction = 1;

                int maxReduction = depth - 2;
                if (reduction > maxReduction) reduction = maxReduction;

                lmrTable[rowBase + moves] = reduction;
            }
        }

        for (int attacker = 0; attacker < 12; attacker++)
        {
            int rowBase = attacker << PieceShift;

            for (int victim = 0; victim < 12; victim++)
                mvvLvaTable[rowBase + victim] = mvvLva[attacker % 6, victim % 6];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HistoryIndex(int sideToMove, int source, int target)
    {
        return (sideToMove << SideShift) | (source << SquareShift) | target;
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
        if (earliest < 0)
            earliest = 0;

        ulong key = Zobrist.hashKey;
        ulong[] table = repetitionTable;

        for (int i = index - 3; i >= earliest; i -= 2)
        {
            if (table[i] == key)
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
        Array.Clear(killerMoves, 0, killerMoves.Length);
        Array.Clear(counterMoves, 0, counterMoves.Length);
        Array.Clear(historyMoves, 0, historyMoves.Length);

        int alpha = -Infinity;
        int beta = Infinity;
        int bestMove = 0;
        int completedDepth = 0;

        StringBuilder? infoBuilder = Program.debug ? null : new StringBuilder(256);

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

            int rootMove = pvTable[0];
            if (rootMove != 0)
                bestMove = rootMove;

            if (infoBuilder != null)
            {
                int rootPvLength = pvLength[0];

                infoBuilder.Clear();
                infoBuilder.Append("info score ").Append(FormatUciScore(score))
                           .Append(" depth ").Append(currentDepth)
                           .Append(" nodes ").Append(nodes)
                           .Append(" pv ");

                for (int i = 0; i < rootPvLength; i++)
                    infoBuilder.Append(GetMove(pvTable[i])).Append(' ');

                Console.WriteLine(infoBuilder.ToString());
            }

            if (TimeManagement.ShouldStopAfterIteration())
                break;
        }

        if (bestMove == 0)
        {
            MoveList moveList = new MoveList();
            GenerateMoves(ref moveList);

            int[] moves = moveList.moves;
            int moveCount = moveList.count;

            for (int i = 0; i < moveCount; i++)
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

    [SkipLocalsInit]
    private static int AlphaBeta(int alpha, int beta, int depth, bool allowNullMove = true, int prevMove = 0)
    {
        if ((nodes & TimeCheckMask) == 0)
            TimeManagement.Communicate();

        if (TimeManagement.stopped)
            return 0;

        int currentPly = ply;
        pvLength[currentPly] = currentPly;

        if (currentPly > 0 && IsRepetition())
            return 0;

        if (halfmoveClock >= 100)
            return 0;

        bool inCheck = IsInCheck();

        if (inCheck && currentPly < MaxPly - 10)
            depth++;

        if (depth <= 0)
            return Quiescence(alpha, beta);

        if (currentPly >= MaxPly - 1)
            return Evaluation.Evaluate();

        nodes++;

        bool pvNode = (beta - alpha) > 1;
        int sideToMove = side;
        ulong hashKey = Zobrist.hashKey;

        int ttMove = 0;
        int ttScore = TranspositionTable.Probe(
            hashKey,
            depth,
            alpha,
            beta,
            currentPly,
            out ttMove);

        if (ttScore != TranspositionTable.NoScore && !pvNode)
            return ttScore;

        int staticEval = inCheck ? -MateScore : Evaluation.Evaluate();

        // Single branch-free predicate shared by the three pruning gates below
        bool quietNode = !pvNode & !inCheck;
        bool prunableNode = quietNode & (currentPly > 0);

        // Reverse futility pruning
        if (prunableNode &&
            depth <= ReverseFutilityMaxDepth &&
            staticEval > -MateThreshold &&
            staticEval < MateThreshold)
        {
            if (staticEval - ReverseFutilityMarginPerDepth * depth >= beta)
                return beta;
        }

        // Null move pruning
        if (prunableNode &&
            depth >= NullMoveMinDepth &&
            allowNullMove &&
            staticEval >= beta &&
            HasNonPawnMaterial(sideToMove))
        {
            BoardState nullMoveState = CopyBoard();

            Zobrist.hashKey ^= Zobrist.sideKey;
            side ^= 1;
            halfmoveClock++;

            if (enPassant != NoSquare)
            {
                Zobrist.hashKey ^= Zobrist.enpassantKeys[enPassant];
                enPassant = NoSquare;
            }

            int evalBonus = (staticEval - beta) / NullMoveEvalDivisor;
            if (evalBonus > NullMoveEvalBonusCap)
                evalBonus = NullMoveEvalBonusCap;

            int reduction = NullMoveBaseReduction + depth / NullMoveDepthDivisor + evalBonus;
            if (reduction > depth - 1)
                reduction = depth - 1;

            ply++;
            int nullMoveScore = -AlphaBeta(-beta, -beta + 1, depth - 1 - reduction, false);
            ply--;
            TakeBack(nullMoveState);

            if (TimeManagement.stopped)
                return 0;

            if (nullMoveScore >= beta)
                return beta;
        }

        // Futility pruning
        bool canPrune = false;
        if (quietNode && depth <= FutilityMaxDepth)
            canPrune = staticEval + FutilityMarginPerDepth * depth <= alpha;

        MoveList moveList = new MoveList();
        GenerateMoves(ref moveList);

        bool hasPrevMove = prevMove != 0;
        int counterIndex = -1;
        int counterMove = 0;

        if (hasPrevMove)
        {
            counterIndex = (GetMovePiece(prevMove) << SquareShift) | GetMoveTarget(prevMove);
            counterMove = counterMoves[counterIndex];
        }

        int killerIndex = currentPly << KillerShift;
        int pvMove = currentPly == 0 ? pvTable[0] : 0;

        SortMoves(
            ref moveList,
            ttMove,
            pvMove,
            counterMove,
            killerMoves[killerIndex],
            killerMoves[killerIndex + 1],
            sideToMove);

        int[] moves = moveList.moves;
        int moveCount = moveList.count;

        // Loop invariants hoisted out of the move loop
        int newDepth = depth - 1;
        int depthSquared = depth * depth;
        int lmrRowBase = depth << LmrRowShift;
        int historyBase = sideToMove << SideShift;
        int pvRowBase = currentPly << PvRowShift;
        int nextPly = currentPly + 1;
        int childRowBase = nextPly << PvRowShift;

        int movesSearched = 0;
        int bestScore = -Infinity;
        int bestMove = 0;
        int originalAlpha = alpha;
        int legalMoves = 0;
        bool anyMovePruned = false;

        int quietMovesPlayedCount = 0;
        Span<int> quietMovesPlayed = stackalloc int[64];

        for (int i = 0; i < moveCount; i++)
        {
            int move = moves[i];
            int promoted = GetMovePromoted(move);
            bool isQuiet = (GetMoveCapture(move) == 0) & (promoted == 0);

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

            if (isQuiet && quietMovesPlayedCount < 64)
                quietMovesPlayed[quietMovesPlayedCount++] = move;

            int score;

            if (movesSearched == 0)
            {
                score = -AlphaBeta(-beta, -alpha, newDepth, true, move);
            }
            else
            {
                if (movesSearched >= FullDepthMoves &&
                    depth >= ReductionLimit &&
                    isQuiet &&
                    !inCheck)
                {
                    int moveIndex = movesSearched < 64 ? movesSearched : 63;
                    int reduction = lmrTable[lmrRowBase + moveIndex];

                    if (pvNode && reduction > 1)
                        reduction--;

                    score = -AlphaBeta(-alpha - 1, -alpha, newDepth - reduction, true, move);
                }
                else
                {
                    score = alpha + 1;
                }

                if (score > alpha)
                {
                    score = -AlphaBeta(-alpha - 1, -alpha, newDepth, true, move);

                    if (score > alpha && score < beta)
                        score = -AlphaBeta(-beta, -alpha, newDepth, true, move);
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
                    if (killerMoves[killerIndex] != move)
                    {
                        killerMoves[killerIndex + 1] = killerMoves[killerIndex];
                        killerMoves[killerIndex] = move;
                    }

                    if (hasPrevMove)
                        counterMoves[counterIndex] = move;

                    // historyMoves[historyBase | (GetMoveSource(move) << SquareShift) | GetMoveTarget(move)] += depthSquared;

                    // int malus = -depthSquared;
                    // for (int q = 0; q < quietMovesPlayedCount; q++)
                    // {
                    //     int failedMove = quietMovesPlayed[q];
                    //     if (failedMove == move)
                    //         continue;

                    //     historyMoves[historyBase | (GetMoveSource(failedMove) << SquareShift) | GetMoveTarget(failedMove)] += malus;
                    // }
                }

                TranspositionTable.Store(
                    hashKey,
                    depth,
                    beta,
                    bestMove,
                    TTFlag.Beta,
                    currentPly);

                return beta;
            }

            if (score > alpha)
            {
                if (isQuiet)
                    historyMoves[historyBase | (GetMoveSource(move) << SquareShift) | GetMoveTarget(move)] += depthSquared;

                alpha = score;

                pvTable[pvRowBase + currentPly] = move;

                int childPvLength = pvLength[nextPly];
                int copyCount = childPvLength - nextPly;

                if (copyCount > 0)
                    Array.Copy(pvTable, childRowBase + nextPly, pvTable, pvRowBase + nextPly, copyCount);

                pvLength[currentPly] = childPvLength;
            }
        }

        if (legalMoves == 0)
        {
            if (anyMovePruned)
                return staticEval;

            return inCheck ? -MateScore + currentPly : 0;
        }

        TTFlag flag = alpha <= originalAlpha ? TTFlag.Alpha : TTFlag.Exact;
        TranspositionTable.Store(hashKey, depth, alpha, bestMove, flag, currentPly);

        return alpha;
    }

    public static int Quiescence(int alpha, int beta)
    {
        if ((nodes & TimeCheckMask) == 0)
            TimeManagement.Communicate();

        if (TimeManagement.stopped)
            return 0;

        int currentPly = ply;

        if (currentPly >= MaxPly - 1)
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
        if (inCheck)
            GenerateMoves(ref moveList);
        else
            GenerateCaptureMoves(ref moveList);

        int killerIndex = currentPly << KillerShift;

        SortMoves(
            ref moveList,
            0,
            0,
            0,
            killerMoves[killerIndex],
            killerMoves[killerIndex + 1],
            side);

        int[] moves = moveList.moves;
        int moveCount = moveList.count;
        int legalMoves = 0;

        // eval and the delta margin are loop-invariant; only alpha moves
        int deltaBase = eval + QsDeltaMargin;

        for (int i = 0; i < moveCount; i++)
        {
            int move = moves[i];

            if (!inCheck)
            {
                int promoted = GetMovePromoted(move);
                int capturedValue = GetPieceValue(GetPieceAtSquare(GetMoveTarget(move)));
                int promotionValueDelta = promoted != 0 ? GetPieceValue(promoted) - 89 : 0;

                if (deltaBase + capturedValue + promotionValueDelta < alpha)
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
            return -MateScore + currentPly;

        return alpha;
    }

    private static void SortMoves(
        ref MoveList moveList,
        int ttMove,
        int pvMove,
        int counterMove,
        int killer1,
        int killer2,
        int sideToMove)
    {
        int count = moveList.count;
        if (count < 2)
        {
            if (count == 1)
                moveList.scores[0] = ScoreMove(moveList.moves[0], ttMove, pvMove, counterMove, killer1, killer2, sideToMove);
            return;
        }

        Span<int> moves = moveList.moves.AsSpan(0, count);
        Span<int> scores = moveList.scores.AsSpan(0, count);

        for (int i = 0; i < count; i++)
            scores[i] = ScoreMove(moves[i], ttMove, pvMove, counterMove, killer1, killer2, sideToMove);

        for (int i = 1; i < count; i++)
        {
            int score = scores[i];
            if (score <= scores[i - 1])
                continue;

            int move = moves[i];
            int j = i - 1;

            do
            {
                moves[j + 1] = moves[j];
                scores[j + 1] = scores[j];
                j--;
            }
            while (j >= 0 && scores[j] < score);

            moves[j + 1] = move;
            scores[j + 1] = score;
        }
    }

    private static int ScoreMove(
        int move,
        int ttMove,
        int pvMove,
        int counterMove,
        int killer1,
        int killer2,
        int sideToMove)
    {
        if (move == ttMove) return 30000;

        int promoted = GetMovePromoted(move);
        if (promoted == Q || promoted == q) return 29000;
        if (move == pvMove) return 20000;

        int target = GetMoveTarget(move);

        if (GetMoveCapture(move) != 0)
        {
            int victim = GetPieceAtSquare(target);
            return mvvLvaTable[(GetMovePiece(move) << PieceShift) | victim] + 10000;
        }

        if (move == killer1) return 9000;
        if (move == killer2) return 8000;
        if (move == counterMove) return 7600;
        if (GetMoveCastling(move) != 0) return 7500;
        if (promoted != 0) return 7200;

        int history = historyMoves[HistoryIndex(sideToMove, GetMoveSource(move), target)];

        if (history < -7000) return -7000;
        if (history > 7000) return 7000;
        return history;
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
        int sideToMove = side;

        int kSq = sideToMove == White
            ? BitboardOperations.GetLs1bIndex(bitboards[K])
            : BitboardOperations.GetLs1bIndex(bitboards[k]);

        return PieceAttacks.IsSquareAttacked(kSq, sideToMove ^ 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetPieceAtSquare(int square)
    {
        ulong mask = 1UL << square;
        ulong[] boards = bitboards;

        if ((boards[P] & mask) != 0) return P;
        if ((boards[p] & mask) != 0) return p;
        if ((boards[N] & mask) != 0) return N;
        if ((boards[n] & mask) != 0) return n;
        if ((boards[B] & mask) != 0) return B;
        if ((boards[b] & mask) != 0) return b;
        if ((boards[R] & mask) != 0) return R;
        if ((boards[r] & mask) != 0) return r;
        if ((boards[Q] & mask) != 0) return Q;
        if ((boards[q] & mask) != 0) return q;
        if ((boards[K] & mask) != 0) return K;
        if ((boards[k] & mask) != 0) return k;

        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasNonPawnMaterial(int sideToCheck)
    {
        ulong[] boards = bitboards;

        return sideToCheck == White
            ? (boards[N] | boards[B] | boards[R] | boards[Q]) != 0
            : (boards[n] | boards[b] | boards[r] | boards[q]) != 0;
    }
}