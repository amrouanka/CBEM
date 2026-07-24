using static Board;
using static MoveEncoding;
using static MoveGenerator;

/// <summary>
/// Iterative deepening alpha-beta search with standard chess engine techniques.
/// Includes: aspiration windows, null move pruning, LMR, futility pruning,
/// reverse futility pruning, killer/history heuristics, and quiescence search.
/// </summary>
public static class Search
{
    #region Constants

    private const int MaxPly = 64;
    private const int Infinity = 50_000;
    private const int MateScore = 49_000;
    private const int MateThreshold = MateScore - MaxPly;

    // Late Move Reductions
    private const int LmrFullDepthMoves = 4;
    private const int LmrDepthLimit = 3;

    // Aspiration Windows
    private const int AspirationDepth = 4;
    private const int AspirationInitialDelta = 50;
    private const int AspirationMaxFails = 3;

    // Pruning margins
    private const int RfpMarginPerDepth = 150;   // Reverse futility pruning
    private const int FutilityMarginBase = 120;   // Futility pruning
    private const int FutilityMaxDepth = 3;
    private const int DeltaPruningMargin = 200;   // Delta pruning in QSearch

    // Move ordering scores
    private const int MoveScoreTT = 30_000;
    private const int MoveScoreQueenPromo = 29_000;
    private const int MoveScorePv = 20_000;
    private const int MoveScoreCapture = 10_000;
    private const int MoveScoreKiller1 = 9_000;
    private const int MoveScoreKiller2 = 8_000;
    private const int MoveScoreCastle = 7_500;
    private const int MoveScoreUnderPromo = 7_200;
    private const int MoveScoreHistoryCap = 7_000; // history scores capped here

    // Null Move Pruning
    private const int NmpMinDepth = 3;
    private const int NmpBaseReduction = 3;

    #endregion

    #region LMR Table

    private static readonly int[,] LmrTable = new int[MaxPly + 1, 64];

    static Search()
    {
        InitLmrTable();
    }

    private static void InitLmrTable()
    {
        for (int depth = 0; depth <= MaxPly; depth++)
        {
            for (int moveIndex = 0; moveIndex < 64; moveIndex++)
            {
                if (depth < 2 || moveIndex < 1)
                {
                    LmrTable[depth, moveIndex] = 1;
                    continue;
                }

                int reduction = (int)(1 + Math.Log(depth) * Math.Log(moveIndex) / 2);
                reduction = Math.Max(1, reduction);
                reduction = Math.Min(depth - 2, reduction);

                LmrTable[depth, moveIndex] = reduction;
            }
        }
    }

    #endregion

    #region Search State

    private static int _ply;
    private static long _nodes;

    public static long LastNodeCount => _nodes;
    public static int LastBestMove = 0;
    public static int LastDepthReached = 0;

    // Killer moves: two slots per ply
    private static readonly int[] _killerMove1 = new int[MaxPly];
    private static readonly int[] _killerMove2 = new int[MaxPly];

    // History heuristic indexed by [piece, targetSquare]
    private static readonly int[,] _historyMoves = new int[12, 64];

    // Triangular PV table
    private static readonly int[,] _pvTable = new int[MaxPly, MaxPly];
    private static readonly int[] _pvLength = new int[MaxPly];

    #endregion

    #region Move Ordering Tables

    /// <summary>
    /// MVV-LVA (Most Valuable Victim - Least Valuable Attacker) scores.
    /// Indexed by [attackerType % 6, victimType % 6].
    /// </summary>
    private static readonly int[,] MvvLva =
    {
        { 105, 205, 305, 405, 505, 605 },
        { 104, 204, 304, 404, 504, 604 },
        { 103, 203, 303, 403, 503, 603 },
        { 102, 202, 302, 402, 502, 602 },
        { 101, 201, 301, 401, 501, 601 },
        { 100, 200, 300, 400, 500, 600 },
    };

    #endregion

    #region Repetition Detection

    private static readonly ulong[] _repetitionTable = new ulong[1024];
    public static int RepetitionIndex = 0;

    public static void AddToRepetitionHistory(ulong hashKey)
    {
        if (RepetitionIndex < _repetitionTable.Length)
            _repetitionTable[RepetitionIndex++] = hashKey;
    }

    public static void RemoveFromRepetitionHistory()
    {
        if (RepetitionIndex > 0)
            RepetitionIndex--;
    }

    /// <summary>
    /// Detects a repeated position by searching backwards through the
    /// repetition table, stepping two plies at a time (same side to move),
    /// within the bounds of the current halfmove clock.
    /// </summary>
    private static bool IsRepetition()
    {
        int index = RepetitionIndex;
        if (index < 3) return false;

        ulong key = Zobrist.hashKey;
        int earliest = Math.Max(0, index - 1 - halfmoveClock);

        for (int i = index - 3; i >= earliest; i -= 2)
        {
            if (_repetitionTable[i] == key)
                return true;
        }

        return false;
    }

    #endregion

    #region UCI Score Formatting

    private static bool IsMateScore(int score) => Math.Abs(score) >= MateThreshold;

    /// <summary>Returns the mate distance in moves (positive = we are mating).</summary>
    private static int ScoreToMate(int score)
    {
        return score > 0
            ? (MateScore - score + 1) / 2
            : -(MateScore + score) / 2;
    }

    private static string FormatUciScore(int score)
    {
        return IsMateScore(score)
            ? $"mate {ScoreToMate(score)}"
            : $"cp {score}";
    }

    #endregion

    #region Root Search (Iterative Deepening)

    /// <summary>
    /// Runs iterative deepening from depth 1 up to <paramref name="maxDepth"/>,
    /// using aspiration windows from depth <see cref="AspirationDepth"/> onwards.
    /// </summary>
    public static void SearchPosition(int maxDepth)
    {
        ResetSearchState();

        int bestMove = 0;
        int completedDepth = 0;
        int alpha = -Infinity;
        int beta = Infinity;

        for (int depth = 1; depth <= maxDepth; depth++)
        {
            int score = RunIterationWithAspiration(depth, ref alpha, ref beta);

            if (TimeManagement.stopped) break;

            // Update aspiration window around the returned score
            alpha = score - AspirationInitialDelta;
            beta = score + AspirationInitialDelta;

            completedDepth = depth;

            if (_pvTable[0, 0] != 0)
                bestMove = _pvTable[0, 0];

            ReportIterationInfo(score, depth);

            if (TimeManagement.ShouldStopAfterIteration()) break;
        }

        bestMove = ResolveNoMoveEdgeCase(bestMove);
        ReportBestMove(bestMove, completedDepth);
    }

    /// <summary>
    /// Runs one depth iteration. Uses full-window search for shallow depths,
    /// then aspiration windows with re-searches on failure.
    /// </summary>
    private static int RunIterationWithAspiration(int depth, ref int alpha, ref int beta)
    {
        // Use full window for first few depths to stabilise the score
        if (depth < AspirationDepth)
            return AlphaBeta(-Infinity, Infinity, depth);

        int score = AlphaBeta(alpha, beta, depth);
        int failCount = 0;
        int window = AspirationInitialDelta;

        while ((score <= alpha || score >= beta) && !TimeManagement.stopped)
        {
            failCount++;

            if (failCount >= AspirationMaxFails)
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

            score = AlphaBeta(alpha, beta, depth);
        }

        return score;
    }

    private static void ResetSearchState()
    {
        _nodes = 0;
        _ply = 0;
        TimeManagement.stopped = false;

        Array.Clear(_pvTable);
        Array.Clear(_killerMove1);
        Array.Clear(_killerMove2);
        Array.Clear(_historyMoves);
    }

    private static void ReportIterationInfo(int score, int depth)
    {
        if (Program.debug) return;

        Console.Write($"info score {FormatUciScore(score)} depth {depth} nodes {_nodes} pv ");

        for (int i = 0; i < _pvLength[0]; i++)
            Console.Write($"{GetMove(_pvTable[0, i])} ");

        Console.WriteLine();
    }

    private static void ReportBestMove(int bestMove, int completedDepth)
    {
        if (Program.debug)
        {
            LastBestMove = bestMove;
            LastDepthReached = completedDepth;
            return;
        }

        Console.WriteLine(bestMove != 0
            ? $"bestmove {GetMove(bestMove)}"
            : "bestmove 0000");
    }

    /// <summary>
    /// If no best move was found from search (e.g. extremely short time),
    /// return the first legal move as a fallback.
    /// </summary>
    private static int ResolveNoMoveEdgeCase(int bestMove)
    {
        if (bestMove != 0) return bestMove;

        var moveList = new MoveList();
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
            return moveList.moves[i];
        }

        return 0; // truly no legal move (checkmate or stalemate)
    }

    #endregion

    #region Alpha-Beta

    /// <summary>
    /// Principal variation search with:
    ///   - Transposition table probe/store
    ///   - Reverse futility pruning (static eval pruning)
    ///   - Null move pruning
    ///   - Futility pruning (move-loop)
    ///   - Late move reductions (LMR)
    ///   - PV-search (zero-window + re-search)
    ///   - Killer and history move heuristics
    /// </summary>
    private static int AlphaBeta(int alpha, int beta, int depth, bool allowNullMove = true)
    {
        // Periodically check time and update UCI status
        if ((_nodes & 16_383) == 0)
            TimeManagement.Communicate();

        if (TimeManagement.stopped) return 0;

        // Initialise PV line for this ply
        _pvLength[_ply] = _ply;

        // --- Draw detection ---
        if (_ply > 0 && IsRepetition()) return 0;
        if (halfmoveClock >= 100) return 0;

        bool inCheck = IsInCheck();
        bool pvNode = (beta - alpha) > 1;

        // Check extension: avoid horizon effect when in check
        if (inCheck && _ply < MaxPly - 10)
            depth++;

        // Drop into quiescence at the leaves
        if (depth <= 0)
            return Quiescence(alpha, beta);

        // Avoid array overflow
        if (_ply >= MaxPly - 1)
            return Evaluation.Evaluate();

        _nodes++;

        // --- Transposition Table probe ---
        int ttScore = TranspositionTable.Probe(
            Zobrist.hashKey, depth, alpha, beta, _ply, out int ttMove);

        if (ttScore != TranspositionTable.NoScore && !pvNode)
            return ttScore;

        // Static evaluation (not computed while in check to avoid instability)
        int staticEval = inCheck ? -MateScore : Evaluation.Evaluate();

        // --- Reverse Futility Pruning (static eval pruning) ---
        // If static eval beats beta by a depth-scaled margin, prune the node.
        if (depth <= 3 &&
            !pvNode &&
            !inCheck &&
            _ply > 0 &&
            Math.Abs(staticEval) < MateThreshold)
        {
            int margin = RfpMarginPerDepth * depth;

            if (staticEval - margin >= beta)
                return beta;
        }

        // --- Null Move Pruning ---
        // Skip a move and see if the opponent can improve. If they cannot beat
        // beta with a free tempo, the position is probably too good for us.
        if (depth >= NmpMinDepth &&
            !pvNode &&
            !inCheck &&
            _ply > 0 &&
            allowNullMove &&
            HasNonPawnMaterial(side) &&
            staticEval >= beta)
        {
            DoNullMoveSearch(depth, beta, staticEval, out int nmScore);

            if (TimeManagement.stopped) return 0;
            if (nmScore >= beta) return beta;
        }

        // Futility pruning eligibility (evaluated once per node)
        bool canFutilityPrune = depth <= FutilityMaxDepth
                             && !inCheck
                             && !pvNode
                             && (staticEval + FutilityMarginBase * depth) <= alpha;

        // --- Generate and order moves ---
        var moveList = new MoveList();
        GenerateMoves(ref moveList);

        int pvMove = (_ply == 0) ? _pvTable[0, 0] : 0;
        SortMoves(moveList, ttMove, pvMove);

        int bestScore = -Infinity;
        int bestMove = 0;
        int originalAlpha = alpha;
        int movesSearched = 0;
        int legalMoves = 0;
        bool anyMovePruned = false;

        for (int i = 0; i < moveList.count; i++)
        {
            int move = moveList.moves[i];
            bool isCapture = GetMoveCapture(move) != 0;
            bool isPromo = GetMovePromoted(move) != 0;
            bool isQuiet = !isCapture && !isPromo;

            // --- Futility pruning (move-loop) ---
            if (canFutilityPrune &&
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
            _ply++;
            legalMoves++;

            int score = SearchMove(
                move, movesSearched, depth, alpha, beta,
                inCheck, pvNode, isQuiet);

            _ply--;
            RemoveFromRepetitionHistory();
            TakeBack(state);

            if (TimeManagement.stopped) return 0;

            movesSearched++;

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }

            // --- Beta cutoff ---
            if (score >= beta)
            {
                if (isQuiet)
                    UpdateKillers(move);

                TranspositionTable.Store(
                    Zobrist.hashKey, depth, beta, bestMove, TTFlag.Beta, _ply);

                return beta;
            }

            // --- Improve alpha ---
            if (score > alpha)
            {
                if (isQuiet)
                    _historyMoves[GetMovePiece(move), GetMoveTarget(move)] += depth * depth;

                alpha = score;
                UpdatePvTable(move);
            }
        }

        // --- No legal moves: checkmate or stalemate ---
        if (legalMoves == 0)
        {
            // If we pruned moves, return static eval rather than claiming checkmate
            if (anyMovePruned)
                return staticEval;

            return inCheck ? -MateScore + _ply : 0;
        }

        TTFlag ttFlag = alpha <= originalAlpha ? TTFlag.Alpha : TTFlag.Exact;
        TranspositionTable.Store(Zobrist.hashKey, depth, alpha, bestMove, ttFlag, _ply);

        return alpha;
    }

    /// <summary>
    /// Determines the score for a move using PVS / LMR logic.
    /// The first move is searched with the full window.
    /// Later moves are searched with a reduced window (LMR), then re-searched
    /// if they beat alpha.
    /// </summary>
    private static int SearchMove(
        int move,
        int movesSearched,
        int depth,
        int alpha,
        int beta,
        bool inCheck,
        bool pvNode,
        bool isQuiet)
    {
        // First move: full window search
        if (movesSearched == 0)
            return -AlphaBeta(-beta, -alpha, depth - 1);

        int score;

        // Late Move Reduction for quiet moves beyond the first few
        bool tryLmr = movesSearched >= LmrFullDepthMoves
                   && depth >= LmrDepthLimit
                   && !inCheck
                   && isQuiet;

        if (tryLmr)
        {
            int moveIndex = Math.Min(movesSearched, 63);
            int reduction = LmrTable[depth, moveIndex];

            // Reduce less in PV nodes
            if (pvNode && reduction > 1)
                reduction--;

            score = -AlphaBeta(-alpha - 1, -alpha, depth - 1 - reduction);
        }
        else
        {
            // Force a re-search below (set score above alpha artificially)
            score = alpha + 1;
        }

        // Zero-window re-search if LMR beat alpha
        if (score > alpha)
        {
            score = -AlphaBeta(-alpha - 1, -alpha, depth - 1);

            // Full re-search if the zero-window result is inside the window
            if (score > alpha && score < beta)
                score = -AlphaBeta(-beta, -alpha, depth - 1);
        }

        return score;
    }

    /// <summary>
    /// Performs the null move and searches with a reduced depth.
    /// Restores the board state afterwards.
    /// </summary>
    private static void DoNullMoveSearch(
        int depth, int beta, int staticEval, out int nmScore)
    {
        BoardState nmState = CopyBoard();

        // Flip side to move without making a move
        Zobrist.hashKey ^= Zobrist.sideKey;
        side ^= 1;
        halfmoveClock++;

        // Remove en-passant square (it no longer applies)
        int noSquare = (int)Square.noSquare;
        if (enPassant != noSquare)
        {
            Zobrist.hashKey ^= Zobrist.enpassantKeys[enPassant];
            enPassant = noSquare;
        }

        // Adaptive reduction: deeper reduction when we are further ahead
        int evalBonus = Math.Min((staticEval - beta) / 200, 3);
        int R = Math.Min(NmpBaseReduction + depth / 4 + evalBonus, depth - 1);

        _ply++;
        nmScore = -AlphaBeta(-beta, -beta + 1, depth - 1 - R, false);
        _ply--;

        TakeBack(nmState);
    }

    #endregion

    #region Quiescence Search

    /// <summary>
    /// Searches captures (and evasions when in check) until the position is quiet,
    /// preventing the horizon effect.
    /// </summary>
    public static int Quiescence(int alpha, int beta)
    {
        if ((_nodes & 16_383) == 0)
            TimeManagement.Communicate();

        if (TimeManagement.stopped) return 0;
        if (_ply >= MaxPly - 1) return Evaluation.Evaluate();
        if (halfmoveClock >= 100) return 0;

        _nodes++;

        bool inCheck = IsInCheck();
        int eval = 0;

        // Stand-pat evaluation when not in check
        if (!inCheck)
        {
            eval = Evaluation.Evaluate();

            if (eval >= beta) return beta;
            if (eval > alpha) alpha = eval;
        }

        var moveList = new MoveList();

        if (inCheck) GenerateMoves(ref moveList);
        else GenerateCaptureMoves(ref moveList);

        SortMoves(moveList);

        int legalMoves = 0;

        for (int i = 0; i < moveList.count; i++)
        {
            int move = moveList.moves[i];

            // Delta pruning: skip captures that cannot raise alpha even optimistically
            if (!inCheck && !CanRaiseAlpha(move, eval, alpha))
                continue;

            BoardState state = CopyBoard();

            if (MakeMove(move, (int)MoveFlag.allMoves) == 0)
            {
                TakeBack(state);
                continue;
            }

            legalMoves++;
            _ply++;
            int score = -Quiescence(-beta, -alpha);
            _ply--;
            TakeBack(state);

            if (TimeManagement.stopped) return 0;

            if (score >= beta) return beta;
            if (score > alpha) alpha = score;
        }

        // Checkmate detection: no legal moves while in check
        if (inCheck && legalMoves == 0)
            return -MateScore + _ply;

        return alpha;
    }

    /// <summary>
    /// Returns true if this capture has any chance of raising alpha
    /// (delta / futility pruning for QSearch).
    /// </summary>
    private static bool CanRaiseAlpha(int move, int eval, int alpha)
    {
        int captureValue = GetPieceValue(GetPieceAtSquare(GetMoveTarget(move)));
        int promoBonus = GetMovePromoted(move) != 0
                         ? GetPieceValue(GetMovePromoted(move)) - GetPieceValue(P)
                         : 0;

        return eval + captureValue + promoBonus + DeltaPruningMargin > alpha;
    }

    #endregion

    #region Move Ordering

    /// <summary>
    /// Sorts moves in descending score order using insertion sort.
    /// Insertion sort is efficient for the small move lists typical in chess.
    /// </summary>
    private static void SortMoves(MoveList moveList, int ttMove = 0, int pvMove = 0)
    {
        for (int i = 0; i < moveList.count; i++)
            moveList.scores[i] = ScoreMove(moveList.moves[i], ttMove, pvMove);

        // Insertion sort (descending)
        for (int i = 1; i < moveList.count; i++)
        {
            int move = moveList.moves[i];
            int score = moveList.scores[i];
            int j = i - 1;

            while (j >= 0 && moveList.scores[j] < score)
            {
                moveList.moves[j + 1] = moveList.moves[j];
                moveList.scores[j + 1] = moveList.scores[j];
                j--;
            }

            moveList.moves[j + 1] = move;
            moveList.scores[j + 1] = score;
        }
    }

    /// <summary>
    /// Assigns a priority score to a move for ordering purposes.
    /// Priority (high to low):
    ///   TT move > queen promotions > PV move > captures (MVV-LVA) >
    ///   killer 1 > killer 2 > castling > under-promotions > history
    /// </summary>
    private static int ScoreMove(int move, int ttMove, int pvMove)
    {
        if (move == ttMove) return MoveScoreTT;

        int promoted = GetMovePromoted(move);

        // Queen promotions are extremely valuable
        if (promoted == Q || promoted == q)
            return MoveScoreQueenPromo;

        if (move == pvMove) return MoveScorePv;

        // Captures ordered by MVV-LVA
        if (GetMoveCapture(move) != 0)
        {
            int piece = GetMovePiece(move);
            int victim = GetPieceAtSquare(GetMoveTarget(move));
            return MvvLva[piece % 6, victim % 6] + MoveScoreCapture;
        }

        // Quiet move ordering
        if (_killerMove1[_ply] == move) return MoveScoreKiller1;
        if (_killerMove2[_ply] == move) return MoveScoreKiller2;

        if (GetMoveCastling(move) != 0) return MoveScoreCastle;
        if (promoted != 0) return MoveScoreUnderPromo;

        // History score, capped to avoid interfering with killer scores
        int history = _historyMoves[GetMovePiece(move), GetMoveTarget(move)];
        return Math.Min(history, MoveScoreHistoryCap);
    }

    #endregion

    #region Heuristic Updates

    private static void UpdateKillers(int move)
    {
        if (_killerMove1[_ply] == move) return;

        _killerMove2[_ply] = _killerMove1[_ply];
        _killerMove1[_ply] = move;
    }

    private static void UpdatePvTable(int move)
    {
        _pvTable[_ply, _ply] = move;

        for (int next = _ply + 1; next < _pvLength[_ply + 1]; next++)
            _pvTable[_ply, next] = _pvTable[_ply + 1, next];

        _pvLength[_ply] = _pvLength[_ply + 1];
    }

    #endregion

    #region Board Helpers

    private static bool IsInCheck()
    {
        int kingSq = side == White
            ? BitboardOperations.GetLs1bIndex(bitboards[K])
            : BitboardOperations.GetLs1bIndex(bitboards[k]);

        return PieceAttacks.IsSquareAttacked(kingSq, side ^ 1);
    }

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

    private static bool HasNonPawnMaterial(int sideToCheck)
    {
        return sideToCheck == White
            ? (bitboards[N] | bitboards[B] | bitboards[R] | bitboards[Q]) != 0
            : (bitboards[n] | bitboards[b] | bitboards[r] | bitboards[q]) != 0;
    }

    private static int GetPieceValue(int piece) => piece switch
    {
        P or p => 88,
        N or n => 309,
        B or b => 331,
        R or r => 494,
        Q or q => 981,
        K or k => 20_000,
        _ => 0
    };

    #endregion
}