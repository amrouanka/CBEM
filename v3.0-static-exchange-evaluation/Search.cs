using static Board;
using static MoveEncoding;
using static MoveGenerator;

public static class Search
{
    // ─────────────────────────────────────────────
    //  Constants
    // ─────────────────────────────────────────────
    private const int MaxPly = 64;
    private const int Infinity = 50000;
    private const int MateScore = 49000;

    // LMR thresholds
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

    // ─────────────────────────────────────────────
    //  Search state
    // ─────────────────────────────────────────────
    private static int ply;
    private static long nodes;
    public static long LastNodeCount => nodes;
    public static int lastBestMove = 0;
    public static int lastDepthReached = 0;

    // ─────────────────────────────────────────────
    //  Move ordering tables
    // ─────────────────────────────────────────────
    private static readonly int[] killerMove1 = new int[MaxPly];
    private static readonly int[] killerMove2 = new int[MaxPly];
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
    //  followPv / scorePv removed — PV move is now
    //  passed directly as pvMove into SortMoves.
    // ─────────────────────────────────────────────
    private static readonly int[,] pvTable = new int[MaxPly, MaxPly];
    private static readonly int[] pvLength = new int[MaxPly];

    // ─────────────────────────────────────────────
    //  Repetition detection
    // ─────────────────────────────────────────────
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
        // No earlier reversible history to compare against
        int index = repetitionIndex;
        if (index < 3)
            return false;

        // Current position is at repetitionIndex - 1.
        // A repetition cannot go back beyond the last pawn move or capture,
        // so only search within the last halfmoveClock plies.
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

    // True if this score represents a forced mate rather than a normal cp eval
    private static bool IsMateScore(int score)
    {
        return Math.Abs(score) >= MateThreshold;
    }

    // Convert internal mate score to UCI "mate N"
    // Positive = side to move is mating
    // Negative = side to move is getting mated
    private static int ScoreToMate(int score)
    {
        if (score > 0)
            return (MateScore - score + 1) / 2;   // mate in N moves

        return -(MateScore + score) / 2;          // mated in N moves
    }

    // Format score exactly how UCI GUIs expect it
    private static string FormatUciScore(int score)
    {
        return IsMateScore(score)
            ? $"mate {ScoreToMate(score)}"
            : $"cp {score}";
    }


    // ═════════════════════════════════════════════
    //  SearchPosition  –  iterative deepening entry
    // ═════════════════════════════════════════════
    public static void SearchPosition(int depth)
    {
        // ── Reset search state ────────────────────
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

        // ── Iterative deepening loop ──────────────
        for (int currentDepth = 1; currentDepth <= depth; currentDepth++)
        {

            // ── Aspiration windows ────────────────
            // Search with a narrow window around the previous score.
            // On failure, widen only the failed side and retry.
            // After 3 consecutive failures, fall back to a full window.
            int score;
            if (currentDepth < 4)
            {
                // No aspiration at very shallow depths — not enough info yet
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

            // Prepare window for next depth
            alpha = score - 50;
            beta = score + 50;

            // ── Commit completed iteration ────────
            completedDepth = currentDepth;
            if (pvTable[0, 0] != 0) bestMove = pvTable[0, 0];

            // ── UCI info output ───────────────────
            if (!Program.debug)
            {
                Console.Write($"info score {FormatUciScore(score)} depth {currentDepth} nodes {nodes} pv ");
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

    // ═════════════════════════════════════════════
    //  AlphaBeta
    // ═════════════════════════════════════════════
    private static int AlphaBeta(int alpha, int beta, int depth, bool allowNullMove = true)
    {
        // ── Time / node housekeeping ──────────────
        if ((nodes & 16383) == 0) TimeManagement.Communicate();
        if (TimeManagement.stopped) return 0;

        pvLength[ply] = ply;

        // ── Draw detection ────────────────────────
        if (ply > 0 && IsRepetition()) return 0;

        // ── 50-move rule ──────────────────────────
        if (halfmoveClock >= 100) return 0;

        // ── In-check detection ────────────────────
        bool inCheck = IsInCheck();

        // ── Check extension ───────────────────────
        // Extend by 1 ply when in check, capped at depth 8 to avoid
        // node explosions in long check sequences.
        if (inCheck && ply < MaxPly - 10) depth++;

        // ── Quiescence at horizon ─────────────────
        if (depth <= 0) return Quiescence(alpha, beta);

        // ── Depth safety ──────────────────────────
        if (ply >= MaxPly - 1) return Evaluation.Evaluate();

        nodes++;

        // ── Transposition table probe ─────────────
        bool pvNode = (beta - alpha) > 1;
        int ttMove = 0;
        int ttScore = TranspositionTable.Probe(
                           Zobrist.hashKey, depth, alpha, beta, ply, out ttMove);

        if (ttScore != TranspositionTable.NoScore && !pvNode)
            return ttScore;

        // ── Static evaluation ─────────────────────
        // Computed once and shared by null move and futility pruning.
        // Unreliable when in check, so skipped in that case.
        int staticEval = inCheck ? -MateScore : Evaluation.Evaluate();

        // ── Reverse futility pruning ──────────────
        // If static eval is far above beta, the position is so good
        // that a full search is unlikely to change the result.
        // Only applied at shallow depths, not in check, not on PV nodes.
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

        // ── Null move pruning ─────────────────────
        // Skip a move and search at reduced depth. If the result still
        // exceeds beta, the position is good enough to prune the branch.
        // Requires non-pawn material to avoid zugzwang.
        // A margin of +100 is added to staticEval >= beta to avoid
        // pruning in dynamically unbalanced or sacrificial positions.
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

        // ── Futility pruning ──────────────────────
        // At very shallow depths, if static eval plus a margin still
        // cannot reach alpha, skip quiet moves unlikely to help.
        // Restricted to depth <= 2 and requires at least 2 moves searched
        // before pruning to avoid missing the first good quiet move.
        bool futilityOk = depth <= 3 && !inCheck && !pvNode;
        int futMargin = 120 * depth;
        bool canPrune = futilityOk && (staticEval + futMargin <= alpha);

        // ── Move generation & ordering ────────────
        MoveList moveList = new MoveList();
        GenerateMoves(ref moveList);

        // The PV move at this ply from the previous iteration is passed
        // directly into SortMoves. No global followPv/scorePv state needed.
        int pvMove = (ply == 0) ? pvTable[0, 0] : 0;
        SortMoves(moveList, ttMove, pvMove);

        int movesSearched = 0;
        int bestScore = -Infinity;
        int bestMove = 0;
        int originalAlpha = alpha;
        int legalMoves = 0;
        bool anyMovePruned = false;

        // ── Main move loop ────────────────────────
        for (int i = 0; i < moveList.count; i++)
        {
            int move = moveList.moves[i];
            bool isCapture = GetMoveCapture(move) != 0;
            bool isPromo = GetMovePromoted(move) != 0;
            bool isQuiet = !isCapture && !isPromo;

            // ── Futility pruning ──────────────────
            // Never prune the TT move — it is our best known move.
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

            // ── First move: full window ───────────
            if (movesSearched == 0)
            {
                score = -AlphaBeta(-beta, -alpha, depth - 1);
            }
            // ── Later moves: LMR + PVS ────────────
            else
            {
                // ── LMR condition ─────────────────────────
                // Reduce quiet moves AND losing captures.
                // A losing capture (SEE < 0) is unlikely to be best
                // and can be safely reduced like a quiet move.
                bool isLosingCapture = isCapture && !See.IsGoodCapture(move, 0);

                if (movesSearched >= FullDepthMoves &&
                    depth >= ReductionLimit &&
                    !inCheck &&
                    (isQuiet || isLosingCapture))
                {
                    int moveIndex = movesSearched < 64 ? movesSearched : 63;
                    int reduction = lmrTable[depth, moveIndex];

                    // Reduce less for PV nodes
                    if (pvNode && reduction > 1) reduction--;

                    score = -AlphaBeta(-alpha - 1, -alpha, depth - 1 - reduction);
                }
                else
                {
                    // Not reducing — force entry into PVS narrow search below
                    score = alpha + 1;
                }

                // PVS narrow-window re-search
                if (score > alpha)
                {
                    score = -AlphaBeta(-alpha - 1, -alpha, depth - 1);

                    // Full window re-search if narrow search raised alpha
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
                bestMove = move;
            }

            // ── Beta cutoff ───────────────────────
            if (score >= beta)
            {
                if (isQuiet)
                {
                    // Avoid duplicate killer entries
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

            // ── Alpha improvement ─────────────────
            if (score > alpha)
            {
                if (isQuiet)
                    historyMoves[GetMovePiece(move), GetMoveTarget(move)] += depth * depth;

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
        if ((nodes & 16383) == 0) TimeManagement.Communicate();
        if (TimeManagement.stopped) return 0;

        // ── Depth safety ──────────────────────────
        if (ply >= MaxPly - 1) return Evaluation.Evaluate();

        // ── 50-move rule ──────────────────────────
        if (halfmoveClock >= 100) return 0;

        nodes++;

        // ── In-check detection ────────────────────
        bool inCheck = IsInCheck();

        // ── Stand-pat evaluation ──────────────────
        // When not in check, use static eval as a lower bound.
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
        else GenerateCaptureMoves(ref moveList);

        SortMoves(moveList);

        int legalMoves = 0;

        // ── Capture loop ──────────────────────────
        for (int i = 0; i < moveList.count; i++)
        {
            int move = moveList.moves[i];

            if (!inCheck)
            {
                // ── SEE filter ────────────────────────────
                // Skip captures that lose material even after
                // the full exchange sequence settles.
                // Example: rook takes pawn defended by queen → skip
                if (!See.IsGoodCapture(move, 0))
                    continue;

                // ── Delta pruning ─────────────────────────
                // Even if SEE says break-even, if we still
                // can't reach alpha with a safety margin, skip.
                int capVal = GetPieceValue(GetPieceAtSquare(GetMoveTarget(move)));
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

        // ── Checkmate detection ───────────────────
        if (inCheck && legalMoves == 0)
            return -MateScore + ply;

        return alpha;
    }

    // ═════════════════════════════════════════════
    //  Move ordering
    // ═════════════════════════════════════════════

    // Score and sort all moves in descending order using insertion sort.
    private static void SortMoves(MoveList moveList, int ttMove = 0, int pvMove = 0)
    {
        // avoids full sort setup for 0 or 1 move nodes
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

    // Assign a priority score to a move for ordering purposes.
    // Priority: TT move > PV move > captures (MVV-LVA) > killers > history.
    // The PV move is the best move from the previous iteration at this ply.
    // It is passed in directly — no global state required.
    private static int ScoreMove(int move, int ttMove = 0, int pvMove = 0)
    {
        // ── Priority 1: TT move ───────────────────
        if (move == ttMove) return 30000;

        int promoted = GetMovePromoted(move);

        // ── Priority 2: Queen Promotions ──────────
        // A Queen promotion is basically a massive material win.
        // It must be searched before almost anything else.
        if (promoted == Q || promoted == q) return 29000;

        // ── Priority 3: PV move ───────────────────
        if (move == pvMove) return 20000;

        int piece = GetMovePiece(move);
        int target = GetMoveTarget(move);

        // ── Priority 4: Captures (MVV-LVA + SEE) ─
        //
        // Good captures (SEE >= 0) get scored high.
        // Bad captures (SEE < 0) get scored BELOW killers.
        //
        // Visual ordering:
        //   TT move       30000
        //   Queen promo   29000
        //   PV move       20000
        //   Good captures 10000 + MVV-LVA score   ← searched early
        //   Killers       8000-9000
        //   Castling      7500
        //   Quiet moves   history (0-7000)
        //   Bad captures  -1 to -10000            ← searched last
        if (GetMoveCapture(move) != 0)
        {
            int victim;

            // En passant: captured pawn is not on the target square
            if (GetMoveEnpassant(move) != 0)
                victim = (side == White) ? p : P;
            else
                victim = GetPieceAtSquare(target);

            int mvvScore = mvvLva[piece % 6, victim % 6];

            if (See.IsGoodCapture(move, 0))
                return mvvScore + 10000;   // good capture → search early
            else
                return mvvScore - 10000;   // bad capture → search after killers/quiets
        }

        // ── Priority 5: Killer moves ──────────────
        if (killerMove1[ply] == move) return 9000;
        if (killerMove2[ply] == move) return 8000;

        // ── Priority 6: Castling ──────────────────
        // Castling is statistically an excellent quiet move.
        if (GetMoveCastling(move) != 0) return 7500;

        // ── Priority 7: Underpromotions ───────────
        if (promoted != 0) return 7200;

        // ── Priority 8: History heuristic ─────────
        int history = historyMoves[piece, target];

        // Cap history so it NEVER overtakes killer moves or captures.
        return history > 7000 ? 7000 : history;
    }

    // ═════════════════════════════════════════════
    //  Helpers
    // ═════════════════════════════════════════════

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
        return sideToCheck == White
            ? (bitboards[N] | bitboards[B] | bitboards[R] | bitboards[Q]) != 0
            : (bitboards[n] | bitboards[b] | bitboards[r] | bitboards[q]) != 0;
    }
}