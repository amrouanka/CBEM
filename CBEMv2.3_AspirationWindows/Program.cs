
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

    // Debug mode variable
    public static bool debug = false;

    static void Main()
    {
        PieceAttacks.InitAll();        // Initialize piece attacks

        if (debug)
        {
            // Parse FEN and print board
            Board.ParseFEN(StartPosition);
            Search.SearchPosition(12);  // 585997
            Board.ParseFEN(TrickyPosition);
            Search.SearchPosition(12);  // 1891123
            Board.ParseFEN(Position5);
            Search.SearchPosition(13);  // 344695
            Board.ParseFEN(KnightD5Repetition);
            Search.SearchPosition(12);  // 369704
            Board.ParseFEN(MirroredPosition);
            Search.SearchPosition(13);  // 459246
            Board.ParseFEN(ItalianPosition);
            Search.SearchPosition(13);  // 1117291
            Board.ParseFEN(LichessHardPuzzle);
            Search.SearchPosition(14);  // 1478089
            Board.ParseFEN(EndgameStudy);
            Search.SearchPosition(14);  // 442544


            Console.WriteLine("\n\n\n");
        }
        else
        {
            // Connect to the GUI
            Uci.UciLoop();
        }
    }

    public static int GetTimeMs() => Environment.TickCount;

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
