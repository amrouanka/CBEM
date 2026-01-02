
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

    // Debug mode variable
    public static bool debug = false;

    static void Main()
    {
        PieceAttacks.InitAll();        // Initialize piece attacks

        if (debug)
        {
            TestNodes(15);    // 27,660,634 nodes (Best so far)

            Console.WriteLine("\n\n\n");
        }
        else
        {
            // Connect to the GUI
            Uci.UciLoop();
        }
    }

    public static int GetTimeMs() => Environment.TickCount;

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
