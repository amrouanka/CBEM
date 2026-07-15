using static MoveFlag;
using static MoveGenerator;

class Program
{
    public const string StartPosition = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    public const string TrickyPosition = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";
    public const string PinPosition = "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1";
    public const string Position5 = "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8";
    public const string KnightD5Repetition = "3rr3/p1kp1pb1/Bn4p1/8/1B1pn3/3N4/PPP2P1P/1K1RR3 b - - 5 12";
    public const string MirroredPosition = "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1";
    public const string ItalianPosition = "r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10";
    public const string LichessHardPuzzle = "r5Nk/2b2p2/2p1p1np/pbq2B2/4Q2P/5NP1/PP3P2/3RR1K1 b - - 0 1";
    public const string EndgameStudy = "5r2/8/1R6/ppk3p1/2N3P1/P4b2/1K6/5B2 w - - 0 1";
    public const string NajdorfComplex = "r2qkb1r/1p2ppp1/p1np1n2/4P3/2B3bP/2N2N2/PP2QPP1/R1B2RK1 b kq - 0 11";
    public const string KIDAttack = "r2q1rk1/pp2ppbp/1np3p1/3pP3/3P1P2/2NBB3/PPP2P1P/R2Q1RK1 w - - 1 12";
    public const string MarshallAttack = "r1bq1rk1/2p1bppp/p1np4/1p6/3PR3/5N2/PPB2PPP/RNBQ2K1 b - - 0 12";
    public const string FrenchWinawer = "r3k2r/pp1b1ppp/2n1p3/2ppP3/3P4/2P1BN2/PP3PPP/R3KB1R w KQkq - 1 12";
    public const string GrunfeldExchange = "rn1q1rk1/pp2ppbp/6p1/2pP4/2P1P1b1/2N5/PP2N1PP/R1BQKB1R w KQ - 1 9";

    // ─────────────────────────────────────────────
    //  Tactical test suite
    //  Each entry: (FEN, bestMove in coordinate notation, description)
    //  All positions verified for FEN validity.
    // ─────────────────────────────────────────────
    private static readonly (string fen, string bestMove, string name)[] TacticalTests = new[]
    {
        // 1. Mate in 1 — back rank
        ("6k1/5ppp/8/8/8/8/1r3PPP/3R2K1 w - - 0 1",
        "d1d8", "Back Rank Mate — Rd8#"),

        // 2. Mate in 1 — queen delivers
        ("5rk1/pb2npp1/1pq4p/5p2/1PP1rB2/2Q1P2P/P4PP1/3RR1K1 w - - 0 1",
        "c3g7", "Queen Mate — Qxg7#"),

        // 3. Fork winning material — knight fork
        ("r3k2r/ppp2ppp/2n5/3qp1B1/1b1P4/2N2Q2/PPP2PPP/R3KB1R w KQkq - 0 1",
        "d4e5", "Central Break — dxe5"),

        // 4. Winning pawn promotion
        ("8/5P1k/8/8/8/8/6K1/8 w - - 0 1",
        "f7f8q", "Promotion — f8=Q"),

        // 5. Queen trap — win the queen
        ("rnb1kbnr/ppppqppp/8/4p3/2B1P3/5Q2/PPPP1PPP/RNB1K1NR w KQkq - 2 3",
        "f3f7", "Scholar's Mate — Qxf7#"),

        // 6. Discovered attack
        ("r1bqk2r/pppp1ppp/2n2n2/2b1p1B1/2B1P3/3P1N2/PPP2PPP/RN1QK2R b KQkq - 0 5",
        "f6e4", "Win Bishop — Nxe4"),

        // 7. Skewer — rook wins material
        ("6k1/8/8/8/8/8/1R6/4K2r w - - 0 1",
        "b2b8", "Rook Skewer — Rb8+"),

        // 8. Pin exploitation
        ("r2qkb1r/ppp2ppp/2n1bn2/3pp3/4P1P1/3P1N1P/PPP1NP2/R1BQKB1R b KQkq - 0 5",
        "e6c4", "Pin Exploit — Bc4"),

        // 9. Sacrifice for mate — classic bishop sac
        ("rnbqk2r/pppp1ppp/4pn2/8/1bPP4/2N5/PP2PPPP/R1BQKBNR w KQkq - 2 4",
        "e2e3", "Develop — e3"),

        // 10. Rook endgame — cut off king
        ("8/8/8/4k3/8/8/1R6/4K3 w - - 0 1",
        "b2b5", "Cut Off King — Rb5+"),

        // 11. Zwischenzug
        ("r2qr1k1/ppp2ppp/2nb1n2/3p2B1/3P4/2N2N2/PPP1BPPP/R2Q1RK1 w - - 0 10",
        "g5f6", "Zwischenzug — Bxf6"),

        // 12. Trapped piece — win bishop
        ("rn1qkb1r/pbpppppp/1p3n2/6B1/2PP4/2N5/PP2PPPP/R2QKBNR b KQkq - 3 4",
        "f6e4", "Trap Bishop — Nxe4"),

        // 13. Clearance sacrifice
        ("r1bq1rk1/ppp2ppp/2np1n2/2b1p3/2B1P3/2NP1N2/PPP2PPP/R1BQ1RK1 w - - 0 7",
        "d3d4", "Central Push — d4"),

        // 14. King safety — castle
        ("r1bqk2r/pppp1ppp/2n2n2/2b1p3/2B1P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 4 4",
        "e1g1", "Castle Kingside — O-O"),

        // 15. Endgame — opposition
        ("8/8/4k3/8/8/4K3/4P3/8 w - - 0 1",
        "e3f4", "Opposition — Kf4"),
    };

    // Debug mode variable
    public static bool debug = false;

    static void Main()
    {
        if (debug)
        {
            // RunTacticalTest();
            
            TestNodes(10); // 2.91m
            TranspositionTable.Clear();
            TestNodes(11); // 5.98m
            TranspositionTable.Clear();
            TestNodes(12); // 10.57m nodes
        }
        else
        {
            // Connect to the GUI
            Uci.UciLoop();
        }
    }

    public static int GetTimeMs() => Environment.TickCount;

    public static void RunTacticalTest()
    {
        int passed = 0;
        int total  = TacticalTests.Length;

        Console.WriteLine("═══════════════════════════════════════════════");
        Console.WriteLine("  Tactical Test Suite — 1 second per position");
        Console.WriteLine("═══════════════════════════════════════════════");
        Console.WriteLine();

        for (int t = 0; t < total; t++)
        {
            var (fen, expectedMove, name) = TacticalTests[t];

            // ── Setup position ────────────────────
            Board.ParseFEN(fen);
            TranspositionTable.Clear();
            Search.repetitionIndex = 0;
            Search.AddToRepetitionHistory(Zobrist.hashKey);

            // ── Setup time: 1 second ──────────────
            // Use the proper method — it sets BOTH softStopTime and stoptime
            TimeManagement.ResetForGo();
            TimeManagement.StartMoveTimeSearch(1000);

            // ── Run search ────────────────────────
            Search.SearchPosition(64);

            // ── Compare result ────────────────────
            string engineMove = MoveEncoding.GetMove(Search.lastBestMove);
            bool   correct    = engineMove == expectedMove;
            if (correct) passed++;

            string status = correct ? "✓ PASS" : "✗ FAIL";

            Console.WriteLine($"  [{t + 1,2}/{total}] {status}  {name}");
            Console.WriteLine($"         Expected: {expectedMove}  Got: {engineMove}  Nodes: {Search.LastNodeCount:N0}");
            Console.WriteLine();
        }

        Console.WriteLine("═══════════════════════════════════════════════");
        Console.WriteLine($"  Result: {passed}/{total} positions solved");

        double pct = (double)passed / total * 100.0;
        Console.WriteLine($"  Accuracy: {pct:F1}%");
        Console.WriteLine("═══════════════════════════════════════════════");
    }
    
    static void TestNodes(int depth)
    {
        // Parse FEN and print board
        long totalNodes = 0;

        Board.ParseFEN(StartPosition);
        Search.SearchPosition(depth);
        totalNodes += Search.LastNodeCount;

        Board.ParseFEN(TrickyPosition);
        Search.SearchPosition(depth);
        totalNodes += Search.LastNodeCount;

        Board.ParseFEN(Position5);
        Search.SearchPosition(depth);
        totalNodes += Search.LastNodeCount;

        Board.ParseFEN(KnightD5Repetition);
        Search.SearchPosition(depth);
        totalNodes += Search.LastNodeCount;

        Board.ParseFEN(MirroredPosition);
        Search.SearchPosition(depth);
        totalNodes += Search.LastNodeCount;

        Board.ParseFEN(ItalianPosition);
        Search.SearchPosition(depth);
        totalNodes += Search.LastNodeCount;

        Board.ParseFEN(LichessHardPuzzle);
        Search.SearchPosition(depth);
        totalNodes += Search.LastNodeCount;

        Board.ParseFEN(EndgameStudy);
        Search.SearchPosition(depth);
        totalNodes += Search.LastNodeCount;

        // Test "Common Structure" Positions
        Board.ParseFEN(NajdorfComplex);
        Search.SearchPosition(depth);
        totalNodes += Search.LastNodeCount;

        Board.ParseFEN(KIDAttack);
        Search.SearchPosition(depth);
        totalNodes += Search.LastNodeCount;

        Board.ParseFEN(MarshallAttack);
        Search.SearchPosition(depth);
        totalNodes += Search.LastNodeCount;

        Board.ParseFEN(FrenchWinawer);
        Search.SearchPosition(depth);
        totalNodes += Search.LastNodeCount;

        Board.ParseFEN(GrunfeldExchange);
        Search.SearchPosition(depth);
        totalNodes += Search.LastNodeCount;

        Console.WriteLine($"\nTotal nodes for all tests: {totalNodes:N0}");
    }

    static long PerftDriver(int depth)
    {
        long nodes = 0;
        if (depth == 0)
        {
            return 1;
        }

        MoveList moveList = new();
        GenerateMoves(ref moveList);

        for (int i = 0; i < moveList.count; i++)
        {
            BoardState state = Board.CopyBoard();

            if (MakeMove(moveList.moves[i], (int)allMoves) == 0)
            {
                continue;
            }

            nodes += PerftDriver(depth - 1);

            Board.TakeBack(state);
        }
        return nodes;
    }

    static void PerftTest(int depth)
    {
        Console.WriteLine("\n     Performance test\n");

        MoveList moveList = new();
        GenerateMoves(ref moveList);

        long totalNodes = 0;
        int start = GetTimeMs();

        for (int i = 0; i < moveList.count; i++)
        {
            BoardState state = Board.CopyBoard();

            if (MakeMove(moveList.moves[i], (int)allMoves) == 0)
                continue;

            long nodesForMove = PerftDriver(depth - 1);
            totalNodes += nodesForMove;

            Board.TakeBack(state);

            Console.WriteLine($"     move: {MoveEncoding.GetMove(moveList.moves[i])}  nodes: {nodesForMove}");
        }

        Console.WriteLine($"\n    Depth: {depth}");
        Console.WriteLine($"    Nodes: {totalNodes}");
        Console.WriteLine($"     Time: {GetTimeMs() - start}ms\n");
    }
}
