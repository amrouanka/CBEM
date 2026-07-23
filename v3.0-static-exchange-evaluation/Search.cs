// =============================================================================
// Search.cs
// 
// Implements iterative-deepening alpha-beta search with:
//   • Aspiration windows
//   • Null-move pruning
//   • Reverse futility pruning (RFP)
//   • Late move reductions (LMR)
//   • Futility pruning
//   • Quiescence search
//   • Transposition table
//   • Move ordering (TT, PV, MVV-LVA, killers, history)
// =============================================================================

using static Board;
using static MoveEncoding;
using static MoveGenerator;

public static class Search
{
    // =========================================================================
    // Constants
    // =========================================================================

    /// <summary>Maximum search depth / ply stack size.</summary>
    private const int MaxPly = 64;

    /// <summary>A score larger than any real evaluation.</summary>
    private const int Infinity = 50_000;

    /// <summary>Base score for checkmate (adjusted by ply for faster-mate preference).</summary>
    private const int MateScore = 49_000;

    /// <summary>Any score at or above this is considered a mate score.</summary>
    private const int MateThreshold = MateScore - MaxPly;

    /// <summary>Minimum moves searched before LMR kicks in.</summary>
    private const int LmrMinMoves = 4;

    /// <summary>Minimum depth before LMR kicks in.</summary>
    private const int LmrMinDepth = 3;

    // =========================================================================
    // Late Move Reduction (LMR) table
    //
    //   lmrTable[depth, moveIndex] = how many plies to reduce
    //
    //   Visual:
    //     depth=1  → reduce by 1 (minimum)
    //     depth=4, move=8  → reduce by ~2
    //     depth=8, move=20 → reduce by ~3
    // =========================================================================
    private static readonly int[,] LmrTable = new int[MaxPly + 1, 64];

    static Search()
    {
        for (int depth = 0; depth <= MaxPly; depth++)
        {
            for (int moveIndex = 0; moveIndex < 64; moveIndex++)
            {
                // Shallow depths or first moves: no reduction
                if (depth < 2 || moveIndex < 1)
                {
                    LmrTable[depth, moveIndex] = 1;
                    continue;
                }

                // Formula: 1 + ln(depth) * ln(moveIndex) / 2
                int reduction = (int)(1 + Math.Log(depth) * Math.Log(moveIndex) / 2.0);
                reduction = Math.Max(1, reduction);
                reduction = Math.Min(reduction, depth - 2); // never reduce to 0 or below

                LmrTable[depth, moveIndex] = reduction;
            }
        }
    }

    // =========================================================================
    // Search State
    // =========================================================================

    /// <summary>Current distance from root (increments with each recursive call).</summary>
    private static int _ply;

    /// <summary>Total nodes visited this search.</summary>
    private static long _nodes;

    /// <summary>Publicly readable node count from the last search.</summary>
    public static long LastNodeCount => _nodes;

    /// <summary>Best move found in the last completed search.</summary>
    public static int LastBestMove;

    /// <summary>Deepest fully completed iteration.</summary>
    public static int LastDepthReached;

    // =========================================================================
    // Move Ordering Tables
    // =========================================================================

    // --- Killer moves ---
    // Two quiet moves per ply that caused a beta cutoff.
    // Stored so we try them early at the same ply next time.
    //
    //   ply 0: [killer1] [killer2]
    //   ply 1: [killer1] [killer2]
    //   ...
    private static readonly int[] _killer1 = new int[MaxPly]; // best killer
    private static readonly int[] _killer2 = new int[MaxPly]; // second killer

    // --- History heuristic ---
    // Records how often a quiet move [piece, toSquare] improved alpha.
    // Higher = try it sooner.
    //
    //   _history[piece, square] += depth * depth  (when move improves alpha)
    private static readonly int[,] _history = new int[12, 64];

    // --- MVV-LVA table (Most Valuable Victim – Least Valuable Attacker) ---
    //
    //   Attacker:  P   N   B   R   Q   K      (rows,    index 0-5)
    //   Victim  :  P   N   B   R   Q   K      (columns, index 0-5)
    //
    //   Example: Pawn takes Queen = mvvLva[0,4] = 505  (high score)
    //            Queen takes Pawn = mvvLva[4,0] = 101  (low score)
    //
    //   Visual grid (row = attacker%6, col = victim%6):
    //              vP   vN   vB   vR   vQ   vK
    //   aP (0)   105  205  305  405  505  605
    //   aN (1)   104  204  304  404  504  604
    //   aB (2)   103  203  303  403  503  603
    //   aR (3)   102  202  302  402  502  602
    //   aQ (4)   101  201  301  401  501  601
    //   aK (5)   100  200  300  400  500  600
    private static readonly int[,] MvvLva = new int[,]
    {
        // attacker = P(0)
        { 105, 205, 305, 405, 505, 605 },
        // attacker = N(1)
        { 104, 204, 304, 404, 504, 604 },
        // attacker = B(2)
        { 103, 203, 303, 403, 503, 603 },
        // attacker = R(3)
        { 102, 202, 302, 402, 502, 602 },
        // attacker = Q(4)
        { 101, 201, 301, 401, 501, 601 },
        // attacker = K(5)
        { 100, 200, 300, 400, 500, 600 },
    };

    // =========================================================================
    // Principal Variation (PV) Table
    //
    //   The PV is the sequence of "best moves" found so far.
    //   pvTable[ply, ply..pvLength[ply]] holds the moves at each depth level.
    //
    //   Visual layout:
    //     pvTable[0, 0] = root best move
    //     pvTable[0, 1] = opponent's expected reply
    //     pvTable[0, 2] = our reply to that, etc.
    //
    //     pvTable[1, 1] = best move at ply 1
    //     pvTable[1, 2] = continuation from ply 2, etc.
    //
    //   pvLength[ply] = how many moves are stored starting at pvTable[ply, ply]
    // =========================================================================
    private static readonly int[,] _pvTable = new int[MaxPly, MaxPly];
    private static readonly int[] _pvLength = new int[MaxPly];

    // =========================================================================
    // Repetition Detection
    //
    //   We keep a rolling array of Zobrist hash keys.
    //   If the current position's key appears earlier in the same game,
    //   it's a draw by repetition.
    // =========================================================================
    private static readonly ulong[] _repetitionTable = new ulong[1024];
    public static int RepetitionIndex = 0;

    /// <summary>Push the current position's hash key onto the repetition stack.</summary>
    public static void PushRepetition(ulong hashKey)
    {
        if (RepetitionIndex < _repetitionTable.Length)
            _repetitionTable[RepetitionIndex++] = hashKey;
    }

    /// <summary>Pop the last hash key from the repetition stack.</summary>
    public static void PopRepetition()
    {
        if (RepetitionIndex > 0)
            RepetitionIndex--;
    }

    /// <summary>
    /// Returns true if the current position has occurred before in this game.
    /// We only check positions reachable within the 50-move clock window,
    /// stepping by 2 plies (same side to move).
    /// </summary>
    private static bool IsRepetition()
    {
        // Need at least 3 positions to have a repetition (root, 2 plies back = same side)
        if (RepetitionIndex < 3) return false;

        ulong currentKey = Zobrist.hashKey;

        // Search backwards, 2 plies at a time (same side = same key format)
        // Stop at the start of the 50-move window
        int windowStart = Math.Max(0, RepetitionIndex - 1 - halfmoveClock);

        for (int i = RepetitionIndex - 3; i >= windowStart; i -= 2)
        {
            if (_repetitionTable[i] == currentKey)
                return true;
        }

        return false;
    }

    // =========================================================================
    // Piece Values (centipawns)
    //
    //   Used for:
    //     • MVV-LVA scoring
    //     • Delta pruning in quiescence
    //     • SEE
    // =========================================================================
    private static readonly int[] PieceValues =
    {
        //  P    N    B    R     Q      K
           88, 309, 331, 494,  981, 20000,  // White pieces (indices 0-5 match P..K)
           88, 309, 331, 494,  981, 20000,  // Black pieces (indices 6-11 match p..k)
    };

    /// <summary>Returns the centipawn value of a piece index (0..11).</summary>
    private static int GetPieceValue(int piece)
    {
        if (piece < 0 || piece > 11) return 0;
        return PieceValues[piece];
    }

    // =========================================================================
    // Public Entry Point
    // =========================================================================

    /// <summary>
    /// Searches the current position to the given depth using iterative deepening.
    ///
    /// Flow:
    ///   depth=1 → full window search
    ///   depth=2 → full window search
    ///   depth=3 → full window search
    ///   depth≥4 → aspiration windows (narrow window around previous score)
    ///             if result falls outside window → widen and retry
    ///
    /// After each iteration, prints UCI "info" line and checks time.
    /// </summary>
    public static void SearchPosition(int maxDepth)
    {
        // --- Reset state ---
        _nodes = 0;
        _ply = 0;
        TimeManagement.stopped = false;

        Array.Clear(_pvTable, 0, _pvTable.Length);
        Array.Clear(_pvLength, 0, _pvLength.Length);
        Array.Clear(_killer1, 0, _killer1.Length);
        Array.Clear(_killer2, 0, _killer2.Length);
        Array.Clear(_history, 0, _history.Length);

        int alpha = -Infinity;
        int beta = Infinity;
        int bestMove = 0;
        int completedDepth = 0;

        // --- Iterative deepening loop ---
        //
        //   We search depth=1, then depth=2, etc.
        //   Each iteration uses the previous iteration's move ordering (PV, history, killers).
        //   This makes deeper iterations faster overall.
        for (int depth = 1; depth <= maxDepth; depth++)
        {
            int score;

            // For early depths: full window (no aspiration)
            if (depth < 4)
            {
                score = AlphaBeta(-Infinity, Infinity, depth);
            }
            else
            {
                // Aspiration window: try a narrow band around last score
                //
                //   [score-50, score+50]
                //
                //   If search returns outside that band → "fail low" or "fail high"
                //   → widen the window and retry
                //
                //   Visual:
                //     alpha ─────────────────── beta
                //             score should land here
                //
                //     fail low: score ≤ alpha → widen left
                //     fail high: score ≥ beta → widen right
                score = AlphaBeta(alpha, beta, depth);

                int windowSize = 50;
                int failCount = 0;

                while ((score <= alpha || score >= beta) && !TimeManagement.stopped)
                {
                    failCount++;

                    if (failCount >= 3)
                    {
                        // Too many failures → full window
                        alpha = -Infinity;
                        beta = Infinity;
                    }
                    else
                    {
                        if (score <= alpha) alpha -= windowSize;
                        if (score >= beta) beta += windowSize;
                        windowSize += windowSize / 2; // grow window each retry
                    }

                    score = AlphaBeta(alpha, beta, depth);
                }
            }

            // If time ran out mid-search, don't use partial results
            if (TimeManagement.stopped) break;

            // Set aspiration window for next iteration centered on this score
            alpha = score - 50;
            beta = score + 50;

            // Save best move from PV
            completedDepth = depth;
            if (_pvTable[0, 0] != 0)
                bestMove = _pvTable[0, 0];

            // Print UCI info
            if (!Program.debug)
            {
                Console.Write($"info score {FormatScore(score)} depth {depth} nodes {_nodes} pv ");
                for (int i = 0; i < _pvLength[0]; i++)
                    Console.Write($"{GetMove(_pvTable[0, i])} ");
                Console.WriteLine();
            }

            // Check if we should stop (e.g. we've used more than half our time)
            if (TimeManagement.ShouldStopAfterIteration()) break;
        }

        // --- Fallback: if no move found (shouldn't happen), pick any legal move ---
        if (bestMove == 0)
        {
            MoveList moveList = new MoveList();
            GenerateMoves(ref moveList);

            for (int i = 0; i < moveList.count; i++)
            {
                BoardState state = CopyBoard();
                bool legal = MakeMove(moveList.moves[i], (int)MoveFlag.allMoves) != 0;
                TakeBack(state);

                if (legal)
                {
                    bestMove = moveList.moves[i];
                    break;
                }
            }
        }

        if (bestMove == 0)
        {
            Console.WriteLine("bestmove 0000");
            return;
        }

        // --- Output ---
        if (Program.debug)
        {
            LastBestMove = bestMove;
            LastDepthReached = completedDepth;
        }
        else
        {
            Console.WriteLine($"bestmove {GetMove(bestMove)}");
        }
    }

    // =========================================================================
    // Alpha-Beta Search
    //
    //   Returns the best score achievable from the current position.
    //
    //   Parameters:
    //     alpha        = lower bound (we can guarantee at least this)
    //     beta         = upper bound (opponent can guarantee at most this)
    //     depth        = remaining plies to search
    //     allowNullMove = false after a null move (avoid double null move)
    //
    //   Pruning techniques applied (in order):
    //     1. Repetition / 50-move draw detection
    //     2. Check extension
    //     3. Transposition table lookup
    //     4. Reverse futility pruning (RFP)
    //     5. Null move pruning
    //     6. Futility pruning (per-move)
    //     7. LMR (late move reductions)
    // =========================================================================
    private static int AlphaBeta(int alpha, int beta, int depth, bool allowNullMove = true)
    {
        // --- Periodic time check ---
        if ((_nodes & 16_383) == 0) TimeManagement.Communicate();
        if (TimeManagement.stopped) return 0;

        // --- Initialize PV for this ply ---
        _pvLength[_ply] = _ply;

        // --- Draw detection ---
        if (_ply > 0 && IsRepetition()) return 0;
        if (halfmoveClock >= 100) return 0;

        // --- Check extension ---
        // If the side to move is in check, search one ply deeper.
        // This avoids missing mating sequences.
        bool inCheck = IsInCheck();
        if (inCheck && _ply < MaxPly - 10)
            depth++;

        // --- Drop into quiescence at depth 0 ---
        if (depth <= 0) return Quiescence(alpha, beta);

        // --- Ply limit guard ---
        if (_ply >= MaxPly - 1) return Evaluation.Evaluate();

        _nodes++;

        // --- Transposition Table (TT) lookup ---
        //
        //   The TT stores results from previous searches.
        //   If we've already searched this position at sufficient depth,
        //   we can reuse the score directly.
        bool pvNode = (beta - alpha) > 1; // PV nodes have a full window
        int ttMove = 0;
        int ttScore = TranspositionTable.Probe(
                           Zobrist.hashKey, depth, alpha, beta, _ply, out ttMove);

        if (ttScore != TranspositionTable.NoScore && !pvNode)
            return ttScore;

        // --- Static evaluation ---
        // Used by pruning heuristics below.
        // If in check, we don't trust the static eval (board is volatile).
        int staticEval = inCheck ? -MateScore : Evaluation.Evaluate();

        // -----------------------------------------------------------------------
        // REVERSE FUTILITY PRUNING (RFP)
        //
        //   If our static eval is already so far above beta that even if we
        //   "lose" some margin it stays above beta, just return beta.
        //
        //   Condition:
        //     • Shallow depth (≤ 3)
        //     • Not a PV node
        //     • Not in check
        //     • Not at root (ply > 0)
        //     • Not a mate score
        //     • staticEval - margin ≥ beta
        //
        //   Visual:
        //     beta  ──────────────────────────
        //                ← margin (150*depth)
        //     staticEval ─────────────────────  ← "we're already well above beta"
        // -----------------------------------------------------------------------
        if (depth <= 3 &&
            !pvNode &&
            !inCheck &&
            _ply > 0 &&
            Math.Abs(staticEval) < MateThreshold)
        {
            int rfpMargin = 150 * depth;
            if (staticEval - rfpMargin >= beta)
                return beta;
        }

        // -----------------------------------------------------------------------
        // NULL MOVE PRUNING
        //
        //   Skip our turn ("null move") and search with reduced depth.
        //   If the opponent can't beat beta even with a free move, we can
        //   prune this branch.
        //
        //   We must NOT do this:
        //     • In check (we must move)
        //     • On PV nodes
        //     • In a row (allowNullMove = false after a null move)
        //     • When we only have pawns (zugzwang risk)
        //
        //   R = depth reduction:
        //     Base R = 3 + depth/4 + eval bonus
        //     Capped at depth-1
        // -----------------------------------------------------------------------
        if (depth >= 3 &&
            !pvNode &&
            !inCheck &&
            _ply > 0 &&
            allowNullMove &&
            HasNonPawnMaterial(side) &&
            staticEval >= beta)
        {
            BoardState savedState = CopyBoard();

            // Switch side without making a move
            Zobrist.hashKey ^= Zobrist.sideKey;
            side ^= 1;
            halfmoveClock++;

            // Clear en passant square
            if (enPassant != (int)Square.noSquare)
            {
                Zobrist.hashKey ^= Zobrist.enpassantKeys[enPassant];
                enPassant = (int)Square.noSquare;
            }

            // Dynamic R: bigger eval advantage → bigger reduction
            int evalBonus = Math.Min((staticEval - beta) / 200, 3);
            int R = Math.Min(3 + depth / 4 + evalBonus, depth - 1);

            _ply++;
            int nullScore = -AlphaBeta(-beta, -beta + 1, depth - 1 - R, allowNullMove: false);
            _ply--;
            TakeBack(savedState);

            if (TimeManagement.stopped) return 0;

            // Null move held above beta → prune
            if (nullScore >= beta) return beta;
        }

        // --- Futility pruning setup ---
        //
        //   For quiet moves near the leaves, if staticEval + margin ≤ alpha,
        //   the move is unlikely to improve alpha → skip it.
        //
        //   Visual:
        //     alpha  ─────────────────────────
        //                ← futility margin (120*depth)
        //     staticEval ─────────────────────  ← "too far below alpha to matter"
        bool futilityPruning =
            depth <= 3 &&
            !inCheck &&
            !pvNode &&
            (staticEval + 120 * depth) <= alpha;

        // --- Generate and sort moves ---
        MoveList moveList = new MoveList();
        GenerateMoves(ref moveList);

        // Get PV move at this ply (from previous iteration's PV line)
        int pvMove = (_ply < _pvLength[0]) ? _pvTable[0, _ply] : 0;
        SortMoves(ref moveList, ttMove, pvMove);

        // --- Move loop ---
        int movesSearched = 0;  // legal moves tried so far
        int legalMoves = 0;  // total legal moves found
        int bestScore = -Infinity;
        int bestMove = 0;
        int originalAlpha = alpha;
        bool anyMoveFutilityPruned = false;

        for (int i = 0; i < moveList.count; i++)
        {
            int move = moveList.moves[i];

            bool isCapture = GetMoveCapture(move) != 0;
            bool isPromo = GetMovePromoted(move) != 0;
            bool isQuiet = !isCapture && !isPromo;

            // --- Futility pruning (skip quiet moves too far below alpha) ---
            if (futilityPruning &&
                movesSearched > 0 &&
                isQuiet &&
                move != ttMove)
            {
                anyMoveFutilityPruned = true;
                continue;
            }

            // --- Make move ---
            BoardState savedState = CopyBoard();
            bool legal = MakeMove(move, (int)MoveFlag.allMoves) != 0;
            if (!legal)
            {
                TakeBack(savedState);
                continue;
            }

            PushRepetition(Zobrist.hashKey);
            _ply++;
            legalMoves++;

            int score;

            // ---------------------------------------------------------------
            // SEARCH THIS MOVE
            //
            //   Move 0: full window  [-beta, -alpha]
            //   Move 1+:
            //     A) Try LMR (reduced depth, null window)
            //     B) If LMR score > alpha → re-search full depth, null window
            //     C) If still > alpha and < beta → re-search full window
            // ---------------------------------------------------------------
            if (movesSearched == 0)
            {
                // First move: full search
                score = -AlphaBeta(-beta, -alpha, depth - 1);
            }
            else
            {
                bool isLosingCapture = isCapture && !SEE.IsGoodCapture(move, threshold: 0);

                // --- Late Move Reduction (LMR) ---
                //
                //   Moves searched late are likely to be bad.
                //   Search them with reduced depth first.
                //   If they surprise us (score > alpha), re-search at full depth.
                bool tryLmr =
                    movesSearched >= LmrMinMoves &&
                    depth >= LmrMinDepth &&
                    !inCheck &&
                    (isQuiet || isLosingCapture);

                if (tryLmr)
                {
                    int moveIndex = Math.Min(movesSearched, 63);
                    int reduction = LmrTable[depth, moveIndex];

                    // PV nodes: reduce less aggressively
                    if (pvNode && reduction > 1) reduction--;

                    // LMR: null window + reduced depth
                    score = -AlphaBeta(-alpha - 1, -alpha, depth - 1 - reduction);
                }
                else
                {
                    // Force re-search below (score = alpha + 1 triggers it)
                    score = alpha + 1;
                }

                // If the reduced search looked good, re-search at full depth
                if (score > alpha)
                {
                    // Full depth, null window
                    score = -AlphaBeta(-alpha - 1, -alpha, depth - 1);

                    // If still above alpha and within beta, re-search full window
                    if (score > alpha && score < beta)
                        score = -AlphaBeta(-beta, -alpha, depth - 1);
                }
            }

            _ply--;
            PopRepetition();
            TakeBack(savedState);

            if (TimeManagement.stopped) return 0;

            movesSearched++;

            // --- Update best score ---
            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }

            // --- Beta cutoff (fail high) ---
            //
            //   Our score is so good the opponent won't allow this position.
            //   Stop searching this branch.
            if (score >= beta)
            {
                // Update killers for quiet moves
                if (isQuiet && _killer1[_ply] != move)
                {
                    _killer2[_ply] = _killer1[_ply];
                    _killer1[_ply] = move;
                }

                TranspositionTable.Store(
                    Zobrist.hashKey, depth, beta, bestMove, TTFlag.Beta, _ply);

                return beta;
            }

            // --- Alpha improvement ---
            if (score > alpha)
            {
                // Update history for quiet moves
                if (isQuiet)
                    _history[GetMovePiece(move), GetMoveTarget(move)] += depth * depth;

                alpha = score;

                // Update PV table
                //
                //   pvTable[ply] = [this move] + [continuation from ply+1]
                _pvTable[_ply, _ply] = move;
                for (int next = _ply + 1; next < _pvLength[_ply + 1]; next++)
                    _pvTable[_ply, next] = _pvTable[_ply + 1, next];
                _pvLength[_ply] = _pvLength[_ply + 1];
            }
        }

        // --- No legal moves ---
        if (legalMoves == 0)
        {
            // If we only pruned futility moves, return static eval (not mate)
            if (anyMoveFutilityPruned) return staticEval;

            // Checkmate or stalemate
            return inCheck ? (-MateScore + _ply) : 0;
        }

        // --- Store result in TT ---
        TTFlag ttFlag = (alpha <= originalAlpha) ? TTFlag.Alpha : TTFlag.Exact;
        TranspositionTable.Store(Zobrist.hashKey, depth, alpha, bestMove, ttFlag, _ply);

        return alpha;
    }

    // =========================================================================
    // Quiescence Search
    //
    //   Called at depth=0 to resolve tactical positions (captures, promotions).
    //   Only searches captures (and all moves when in check).
    //
    //   Without this, the engine would have a "horizon effect":
    //   seeing a "free" piece at depth limit without seeing the recapture.
    //
    //   Visual:
    //     depth 0 → quiescence:
    //       capture capture capture ... quiet position → static eval
    // =========================================================================
    public static int Quiescence(int alpha, int beta)
    {
        if ((_nodes & 16_383) == 0) TimeManagement.Communicate();
        if (TimeManagement.stopped) return 0;

        if (_ply >= MaxPly - 1) return Evaluation.Evaluate();
        if (halfmoveClock >= 100) return 0;

        _nodes++;

        bool inCheck = IsInCheck();

        // --- Stand-pat ---
        // If we're not in check, we can "stand pat" (do nothing) if static eval
        // is already above beta or improves alpha.
        int standPatScore = 0;
        if (!inCheck)
        {
            standPatScore = Evaluation.Evaluate();
            if (standPatScore >= beta) return beta;
            if (standPatScore > alpha) alpha = standPatScore;
        }

        // Generate only captures (or all moves if in check)
        MoveList moveList = new MoveList();
        if (inCheck) GenerateMoves(ref moveList);
        else GenerateCaptureMoves(ref moveList);

        SortMoves(ref moveList);

        int legalMoves = 0;

        for (int i = 0; i < moveList.count; i++)
        {
            int move = moveList.moves[i];

            if (!inCheck)
            {
                // --- SEE filter: skip losing captures ---
                if (!SEE.IsGoodCapture(move, threshold: 0))
                    continue;

                // --- Delta pruning ---
                //
                //   If even the best case (capturing + promo bonus + margin)
                //   can't raise alpha, skip this move.
                //
                //   captureValue + promoBonus + 200 < alpha → prune
                int capturedValue;
                if (GetMoveEnpassant(move) != 0)
                {
                    // En passant always captures a pawn
                    capturedValue = GetPieceValue(side == White ? (int)Piece.p : (int)Piece.P);
                }
                else
                {
                    int victim = GetPieceAtSquare(GetMoveTarget(move));
                    capturedValue = GetPieceValue(victim);
                }

                int promoBonus = GetMovePromoted(move) != 0
                    ? GetPieceValue(GetMovePromoted(move)) - GetPieceValue((int)Piece.P)
                    : 0;

                if (standPatScore + capturedValue + promoBonus + 200 < alpha)
                    continue;
            }

            BoardState savedState = CopyBoard();
            bool legal = MakeMove(move, (int)MoveFlag.allMoves) != 0;
            if (!legal)
            {
                TakeBack(savedState);
                continue;
            }

            legalMoves++;
            _ply++;
            int score = -Quiescence(-beta, -alpha);
            _ply--;
            TakeBack(savedState);

            if (TimeManagement.stopped) return 0;

            if (score >= beta) return beta;
            if (score > alpha) alpha = score;
        }

        // Checkmate in quiescence (when in check with no legal moves)
        if (inCheck && legalMoves == 0)
            return -MateScore + _ply;

        return alpha;
    }

    // =========================================================================
    // Move Sorting
    //
    //   Scores each move then insertion-sorts descending.
    //   Best moves first → more alpha-beta pruning.
    // =========================================================================

    /// <summary>
    /// Scores and sorts all moves in descending order.
    /// </summary>
    private static void SortMoves(ref MoveList moveList, int ttMove = 0, int pvMove = 0)
    {
        // Score all moves
        for (int i = 0; i < moveList.count; i++)
            moveList.scores[i] = ScoreMove(moveList.moves[i], ttMove, pvMove);

        // Insertion sort (fast for small lists, stable)
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

    // =========================================================================
    // Move Scoring
    //
    //   Priority (highest first):
    //
    //   30000  TT move         (from hash table – best known move)
    //   29000  Queen promotion
    //   20000  PV move         (best move from previous iteration)
    //   10000+ Good capture    (MVV-LVA score, SEE ≥ 0)
    //    9000  Killer 1        (quiet move that caused cutoff at this ply)
    //    8000  Killer 2
    //    7500  Castling
    //    7200  Other promotion
    //    0-7000 History        (quiet moves weighted by how often they improved alpha)
    //  -10000+ Bad capture     (MVV-LVA score, SEE < 0)
    // =========================================================================
    private static int ScoreMove(int move, int ttMove = 0, int pvMove = 0)
    {
        if (move == ttMove) return 30_000;

        int promoted = GetMovePromoted(move);
        if (promoted == (int)Piece.Q || promoted == (int)Piece.q) return 29_000;

        if (move == pvMove) return 20_000;

        // --- Captures ---
        if (GetMoveCapture(move) != 0)
        {
            int attacker = GetMovePiece(move);

            int victim;
            if (GetMoveEnpassant(move) != 0)
            {
                // En passant: captures a pawn
                victim = (side == White) ? (int)Piece.p : (int)Piece.P;
            }
            else
            {
                victim = GetPieceAtSquare(GetMoveTarget(move));
                if (victim < 0) victim = (int)Piece.p; // safety fallback
            }

            int mvvLvaScore = MvvLva[attacker % 6, victim % 6];

            // Good capture: bonus; bad capture: penalty
            return SEE.IsGoodCapture(move, threshold: 0)
                ? mvvLvaScore + 10_000
                : mvvLvaScore - 10_000;
        }

        // --- Quiet move ordering ---
        if (_killer1[_ply] == move) return 9_000;
        if (_killer2[_ply] == move) return 8_000;
        if (GetMoveCastling(move) != 0) return 7_500;
        if (promoted != 0) return 7_200;

        // History score, capped at 7000 to stay below killers
        return Math.Min(_history[GetMovePiece(move), GetMoveTarget(move)], 7_000);
    }

    // =========================================================================
    // Helper Methods
    // =========================================================================

    /// <summary>
    /// Returns true if the current side's king is attacked by the opponent.
    /// </summary>
    private static bool IsInCheck()
    {
        int kingSq = side == White
            ? BitboardOperations.GetLs1bIndex(bitboards[(int)Piece.K])
            : BitboardOperations.GetLs1bIndex(bitboards[(int)Piece.k]);

        return PieceAttacks.IsSquareAttacked(kingSq, side ^ 1);
    }

    /// <summary>
    /// Returns the piece index (0..11) on the given square, or -1 if empty.
    /// </summary>
    private static int GetPieceAtSquare(int square)
    {
        ulong bit = 1UL << square;
        for (int piece = 0; piece <= 11; piece++)
        {
            if ((bitboards[piece] & bit) != 0)
                return piece;
        }
        return -1; // empty square
    }

    /// <summary>
    /// Returns true if the given side has any pieces other than pawns and king.
    /// Used to avoid null-move pruning in (near-)zugzwang positions.
    /// </summary>
    private static bool HasNonPawnMaterial(int sideToCheck)
    {
        return sideToCheck == White
            ? (bitboards[(int)Piece.N] | bitboards[(int)Piece.B] |
               bitboards[(int)Piece.R] | bitboards[(int)Piece.Q]) != 0
            : (bitboards[(int)Piece.n] | bitboards[(int)Piece.b] |
               bitboards[(int)Piece.r] | bitboards[(int)Piece.q]) != 0;
    }

    /// <summary>Returns true if the score is a forced-mate score.</summary>
    private static bool IsMateScore(int score) => Math.Abs(score) >= MateThreshold;

    /// <summary>
    /// Converts an internal mate score to "mate in N moves" for UCI output.
    /// Positive = we're mating; negative = we're being mated.
    /// </summary>
    private static int MateInMoves(int score)
    {
        return score > 0
            ? (MateScore - score + 1) / 2
            : -(MateScore + score) / 2;
    }

    /// <summary>Formats score for UCI output: "cp 42" or "mate 3".</summary>
    private static string FormatScore(int score)
    {
        return IsMateScore(score)
            ? $"mate {MateInMoves(score)}"
            : $"cp {score}";
    }
}