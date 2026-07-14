using static Board;
using static MoveEncoding;
using static MoveGenerator;

public static class Search
{
    // ─────────────────────────────────────────────
    //  Constants
    // ─────────────────────────────────────────────
    private const int MaxPly        = 64;
    private const int Infinity      = 50000;
    private const int MateScore     = 49000;

    // LMR thresholds
    private const int FullDepthMoves = 2;
    private const int ReductionLimit = 3;

    // ─────────────────────────────────────────────
    //  Search state
    // ─────────────────────────────────────────────
    private static int  ply;
    private static long nodes;
    public  static long LastNodeCount => nodes;

    // ─────────────────────────────────────────────
    //  Move ordering tables
    // ─────────────────────────────────────────────
    private static readonly int[,] killerMoves  = new int[2, MaxPly];
    private static readonly int[,] historyMoves = new int[12, 64];

    // MVV-LVA [attacker][victim]
    private static readonly int[,] mvvLva = new int[,]
    {
        { 105, 205, 305, 405, 505, 605 },
        { 104, 204, 304, 404, 504, 604 },
        { 103, 203, 303, 403, 503, 603 },
        { 102, 202, 302, 402, 502, 602 },
        { 101, 201, 301, 401, 501, 601 },
        { 100, 200, 300, 400, 500, 600 },
    };

    // ─────────────────────────────────────────────
    //  Principal Variation
    // ─────────────────────────────────────────────
    private static readonly int[,] pvTable  = new int[MaxPly, MaxPly];
    private static readonly int[]  pvLength = new int[MaxPly];
    private static bool followPv;
    private static bool scorePv;

    // ─────────────────────────────────────────────
    //  Repetition detection
    // ─────────────────────────────────────────────
    private static readonly ulong[] repetitionTable = new ulong[1024];
    public  static int repetitionIndex = 0;

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
        for (int i = repetitionIndex - 2; i >= 0; i--)
            if (repetitionTable[i] == Zobrist.hashKey)
                return true;
        return false;
    }

    // ═════════════════════════════════════════════
    //  SearchPosition  –  iterative deepening entry
    // ═════════════════════════════════════════════
    public static void SearchPosition(int depth)
    {
        // ── Reset search state ────────────────────
        nodes    = 0;
        ply      = 0;
        followPv = false;
        scorePv  = false;
        TimeManagement.stopped = false;

        Array.Clear(pvTable);
        Array.Clear(killerMoves);
        Array.Clear(historyMoves);

        int alpha          = -Infinity;
        int beta           =  Infinity;
        int bestMove       = 0;
        int bestScore      = 0;
        int completedDepth = 0;

        // ── Iterative deepening loop ──────────────
        for (int currentDepth = 1; currentDepth <= depth; currentDepth++)
        {
            if (TimeManagement.ShouldStopAfterIteration()) break;

            followPv = true;

            // ── Aspiration windows ────────────────
            // Search with a narrow window around the previous score.
            // On failure, widen only the side that failed and retry.
            // After 3 consecutive failures, fall back to a full window.
            int score     = AlphaBeta(alpha, beta, currentDepth);
            int window    = 50;
            int failCount = 0;

            while ((score <= alpha || score >= beta) && !TimeManagement.stopped)
            {
                failCount++;

                if (failCount >= 3)
                {
                    // Full window fallback — stop thrashing
                    alpha = -Infinity;
                    beta  =  Infinity;
                }
                else
                {
                    // Widen only the side that failed
                    if (score <= alpha) alpha -= window;
                    if (score >= beta)  beta  += window;
                    window += window / 2;
                }

                followPv = true;
                score    = AlphaBeta(alpha, beta, currentDepth);
            }

            if (TimeManagement.stopped) break;

            // Prepare window for next depth
            alpha = score - 50;
            beta  = score + 50;

            // ── Commit completed iteration ────────
            completedDepth = currentDepth;
            bestScore      = score;
            if (pvTable[0, 0] != 0) bestMove = pvTable[0, 0];

            // ── UCI info output ───────────────────
            if (!Program.debug)
            {
                Console.Write($"info score cp {score} depth {currentDepth} nodes {nodes} pv ");
                for (int i = 0; i < pvLength[0]; i++)
                    Console.Write($"{GetMove(pvTable[0, i])} ");
                Console.WriteLine();
            }

            if (TimeManagement.ShouldStopAfterIteration()) break;
        }

        // ── Fallback: pick first legal move ──────
        // Triggered only if no depth completed (e.g. instant time-out).
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

        // ── Output best move ──────────────────────
        Console.Write($"bestmove {GetMove(bestMove)}");
        if (Program.debug)
            Console.Write($" | depth {completedDepth} score {bestScore} nodes {nodes}");
        Console.WriteLine();
    }

    // ═════════════════════════════════════════════
    //  AlphaBeta
    // ═════════════════════════════════════════════
    private static int AlphaBeta(int alpha, int beta, int depth, bool allowNullMove = true)
    {
        // ── Time / node housekeeping ──────────────
        if ((nodes & 2047) == 0) TimeManagement.Communicate();
        if (TimeManagement.stopped) return 0;

        pvLength[ply] = ply;

        // ── Draw detection ────────────────────────
        if (ply > 0 && IsRepetition()) return 0;

        // ── In-check detection ────────────────────
        int kSq = (side == (int)Side.white)
                    ? BitboardOperations.GetLs1bIndex(bitboards[K])
                    : BitboardOperations.GetLs1bIndex(bitboards[k]);
        bool inCheck = PieceAttacks.IsSquareAttacked(kSq, side ^ 1);

        // ── Check extension ───────────────────────
        // Extend by 1 ply when in check. The ply guard prevents
        // explosions in long check sequences.
        if (inCheck && ply < MaxPly - 10) depth++;

        // ── Quiescence at horizon ─────────────────
        if (depth <= 0) return Quiescence(alpha, beta);

        // ── Depth safety ──────────────────────────
        if (ply >= MaxPly - 1) return Evaluation.Evaluate();

        nodes++;

        // ── Transposition table probe ─────────────
        bool pvNode  = (beta - alpha) > 1;
        int  ttMove  = 0;
        int  ttScore = TranspositionTable.Probe(
                           Zobrist.hashKey, depth, alpha, beta, ply, out ttMove);

        if (ttScore != TranspositionTable.NoScore && !pvNode)
            return ttScore;

        // ── Static evaluation ─────────────────────
        // Computed once and shared by null move and futility pruning.
        // Unreliable when in check, so we skip it in that case.
        int staticEval = inCheck ? 0 : Evaluation.Evaluate();

        // ── Null move pruning ─────────────────────
        // Skip a move and search at reduced depth. If the result still
        // exceeds beta, the position is so good we can prune the branch.
        // Requires non-pawn material to avoid zugzwang.
        // We intentionally skip AddToRepetitionHistory for the null move
        // because null moves are illegal — the side-key XOR guarantees
        // a unique hash unreachable from any real game position.
        if (depth >= 4        &&
            !pvNode           &&
            !inCheck          &&
            ply > 0           &&
            allowNullMove     &&
            HasNonPawnMaterial(side) &&
            staticEval >= beta)
        {
            BoardState nmState = CopyBoard();

            Zobrist.hashKey ^= Zobrist.sideKey;
            side ^= 1;
            if (enPassant != (int)Square.noSquare)
            {
                Zobrist.hashKey ^= Zobrist.enpassantKeys[enPassant];
                enPassant = (int)Square.noSquare;
            }

            int R = 2 + depth / 6;
            R = Math.Min(R, depth - 2);

            ply++;
            int nmScore = -AlphaBeta(-beta, -beta + 1, depth - 1 - R, false);
            ply--;
            TakeBack(nmState);

            // Score is not trusted if time ran out during the search above
            if (TimeManagement.stopped) return 0;

            if (nmScore >= beta) return beta;
        }

        // ── Futility pruning ──────────────────────
        // At shallow depths, if the static eval plus a margin still
        // can't reach alpha, skip quiet moves that are unlikely to help.
        bool futilityOk = depth <= 2 && !inCheck && !pvNode;
        int  futMargin  = depth == 1 ? 200 : 400;
        bool canPrune   = futilityOk && (staticEval + futMargin <= alpha);

        // ── Move generation & ordering ────────────
        MoveList moveList = new MoveList();
        GenerateMoves(ref moveList);

        if (followPv) EnablePvScoring(moveList);
        SortMoves(moveList, ttMove);

        int  movesSearched = 0;
        int  bestScore     = -Infinity;
        int  bestMove      = 0;
        int  originalAlpha = alpha;
        int  legalMoves    = 0;
        bool anyMovePruned = false;

        // ── Main move loop ────────────────────────
        for (int i = 0; i < moveList.count; i++)
        {
            int  move    = moveList.moves[i];
            bool isQuiet = GetMoveCapture(move) == 0 && GetMovePromoted(move) == 0;

            // ── Futility pruning ──────────────────
            // Never prune the TT move — it is our best known move.
            if (canPrune      &&
                movesSearched > 0 &&
                isQuiet       &&
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

            // ── First move: full window ───────────
            if (movesSearched == 0)
            {
                score = -AlphaBeta(-beta, -alpha, depth - 1);
            }
            // ── Later moves: LMR + PVS ────────────
            else
            {
                // Late Move Reduction — reduce quiet moves that are
                // unlikely to be best, then verify with PVS if needed.
                if (movesSearched >= FullDepthMoves &&
                    depth >= ReductionLimit         &&
                    !inCheck                        &&
                    isQuiet)
                {
                    int reduction = 1;
                    if (movesSearched >= 6)  reduction++;
                    if (movesSearched >= 12) reduction++;
                    if (movesSearched >= 18) reduction++;
                    if (depth >= 6)          reduction++;
                    if (depth >= 12)         reduction++;
                    if (depth >= 18)         reduction++;
                    reduction = Math.Clamp(reduction, 1, depth - 2);

                    score = -AlphaBeta(-alpha - 1, -alpha, depth - 1 - reduction);
                }
                else
                {
                    // Not reducing — force entry into the PVS narrow search below
                    score = alpha + 1;
                }

                // PVS narrow-window re-search
                if (score > alpha)
                {
                    score = -AlphaBeta(-alpha - 1, -alpha, depth - 1);

                    // Full window re-search if the narrow search raised alpha
                    if (score > alpha && score < beta)
                        score = -AlphaBeta(-beta, -alpha, depth - 1);
                }
            }

            ply--;
            RemoveFromRepetitionHistory();
            TakeBack(state);

            if (TimeManagement.stopped) return 0;

            movesSearched++;

            // ── Track best move ───────────────────
            if (score > bestScore)
            {
                bestScore = score;
                bestMove  = move;
            }

            // ── Beta cutoff ───────────────────────
            if (score >= beta)
            {
                if (isQuiet)
                {
                    killerMoves[1, ply] = killerMoves[0, ply];
                    killerMoves[0, ply] = move;
                }

                TranspositionTable.Store(
                    Zobrist.hashKey, depth, beta, bestMove, TTFlag.Beta, ply);

                return beta;
            }

            // ── Alpha improvement ─────────────────
            if (score > alpha)
            {
                if (isQuiet)
                    historyMoves[GetMovePiece(move), GetMoveTarget(move)] += depth;

                alpha = score;

                // Update PV table
                pvTable[ply, ply] = move;
                for (int next = ply + 1; next < pvLength[ply + 1]; next++)
                    pvTable[ply, next] = pvTable[ply + 1, next];
                pvLength[ply] = pvLength[ply + 1];
            }
        }

        // ── Checkmate / stalemate ─────────────────
        if (legalMoves == 0)
        {
            // If moves were pruned we cannot conclude mate or stalemate —
            // return staticEval as an honest lower bound.
            if (anyMovePruned) return staticEval;

            return inCheck ? -MateScore + ply : 0;
        }

        // ── Store result in TT ────────────────────
        TTFlag flag = alpha <= originalAlpha ? TTFlag.Alpha : TTFlag.Exact;
        TranspositionTable.Store(Zobrist.hashKey, depth, alpha, bestMove, flag, ply);

        return alpha;
    }

    // ═════════════════════════════════════════════
    //  Quiescence
    // ═════════════════════════════════════════════
    public static int Quiescence(int alpha, int beta)
    {
        // ── Time / node housekeeping ──────────────
        if ((nodes & 2047) == 0) TimeManagement.Communicate();
        if (TimeManagement.stopped) return 0;

        // ── Depth safety ──────────────────────────
        if (ply >= MaxPly - 1) return Evaluation.Evaluate();

        nodes++;

        // ── In-check detection ────────────────────
        int kSq = (side == (int)Side.white)
                    ? BitboardOperations.GetLs1bIndex(bitboards[K])
                    : BitboardOperations.GetLs1bIndex(bitboards[k]);
        bool inCheck = PieceAttacks.IsSquareAttacked(kSq, side ^ 1);

        // ── Stand-pat evaluation ──────────────────
        // When not in check, use the static eval as a lower bound.
        // If it already beats beta we can prune immediately.
        int eval = 0;
        if (!inCheck)
        {
            eval = Evaluation.Evaluate();
            if (eval >= beta) return beta;
            if (eval > alpha) alpha = eval;
        }

        // ── Move generation ───────────────────────
        // Generate all moves when in check (must escape), otherwise
        // only captures to keep the search focused.
        MoveList moveList = new MoveList();
        if (inCheck) GenerateMoves(ref moveList);
        else         GenerateCaptureMoves(ref moveList);

        SortMoves(moveList);

        int legalMoves = 0;

        // ── Capture loop ──────────────────────────
        for (int i = 0; i < moveList.count; i++)
        {
            int move = moveList.moves[i];

            // ── Delta pruning ─────────────────────
            // Skip captures where even the maximum material gain
            // plus a safety margin cannot raise alpha.
            // Only applied when not in check — in check we must search all moves.
            if (!inCheck)
            {
                int capVal   = GetPieceValue(GetPieceAtSquare(GetMoveTarget(move)));
                int promoVal = GetMovePromoted(move) != 0
                                ? GetPieceValue(GetMovePromoted(move)) - 100 : 0;
                if (eval + capVal + promoVal + 10 < alpha) continue;
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

        // ── Checkmate detection ───────────────────
        if (inCheck && legalMoves == 0)
            return -MateScore + ply;

        return alpha;
    }

    // ═════════════════════════════════════════════
    //  Move ordering
    // ═════════════════════════════════════════════

    // Score and sort all moves in descending order using insertion sort.
    private static void SortMoves(MoveList moveList, int ttMove = 0)
    {
        for (int i = 0; i < moveList.count; i++)
            moveList.scores[i] = ScoreMove(moveList.moves[i], ttMove);

        for (int i = 1; i < moveList.count; i++)
        {
            int mv = moveList.moves[i];
            int sc = moveList.scores[i];
            int j  = i - 1;
            while (j >= 0 && moveList.scores[j] < sc)
            {
                moveList.moves[j + 1]  = moveList.moves[j];
                moveList.scores[j + 1] = moveList.scores[j];
                j--;
            }
            moveList.moves[j + 1]  = mv;
            moveList.scores[j + 1] = sc;
        }
    }

    // Assign a priority score to a move for ordering purposes.
    // Priority: TT move > PV move > captures (MVV-LVA) > killers > history.
    private static int ScoreMove(int move, int ttMove = 0)
    {
        // ── Priority 1: TT move ───────────────────
        if (move == ttMove) return 30000;

        // ── Priority 2: PV move ───────────────────
        // scorePv is cleared here exactly once per sort cycle to prevent
        // the flag from leaking into sibling or child nodes.
        if (scorePv && pvTable[0, ply] == move)
        {
            scorePv = false;
            return 20000;
        }

        // ── Priority 3: Captures (MVV-LVA) ───────
        if (GetMoveCapture(move) != 0)
        {
            int victim = GetPieceAtSquare(GetMoveTarget(move));
            return mvvLva[GetMovePiece(move) % 6, victim % 6] + 10000;
        }

        // ── Priority 4: Killer moves ──────────────
        if (killerMoves[0, ply] == move) return 9000;
        if (killerMoves[1, ply] == move) return 8000;

        // ── Priority 5: History heuristic ─────────
        return historyMoves[GetMovePiece(move), GetMoveTarget(move)];
    }

    // Enable PV move scoring for the current node.
    // Sets scorePv so the PV move receives its bonus in ScoreMove.
    // If the PV move is not in the list, scorePv stays false — correct behaviour.
    private static void EnablePvScoring(MoveList moveList)
    {
        followPv = false;

        for (int i = 0; i < moveList.count; i++)
        {
            if (pvTable[0, ply] == moveList.moves[i])
            {
                scorePv  = true;
                followPv = true;
                return;
            }
        }
    }

    // ═════════════════════════════════════════════
    //  Helpers
    // ═════════════════════════════════════════════

    private static int GetPieceValue(int piece) => piece switch
    {
        P or p => 100,
        N or n => 320,
        B or b => 330,
        R or r => 500,
        Q or q => 900,
        K or k => 20000,
        _       => 0
    };

    // Pawns are checked first as they are the most common capture target.
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

    // Combine all non-pawn bitboards with bitwise OR for a single comparison.
    // Used to detect zugzwang-prone positions before null move pruning.
    private static bool HasNonPawnMaterial(int sideToCheck)
    {
        return sideToCheck == (int)Side.white
            ? (bitboards[N] | bitboards[B] | bitboards[R] | bitboards[Q]) != 0
            : (bitboards[n] | bitboards[b] | bitboards[r] | bitboards[q]) != 0;
    }
}