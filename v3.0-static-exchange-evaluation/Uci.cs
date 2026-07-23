// =============================================================================
// Uci.cs
//
// Implements the Universal Chess Interface (UCI) protocol.
//
// UCI is a text-based protocol between the GUI and the engine:
//   • GUI sends commands on stdin
//   • Engine responds on stdout
//
// Key commands handled:
//   uci          → identify engine, list options
//   isready      → confirm engine is ready
//   ucinewgame   → reset for a new game
//   position     → set up the board (FEN + move list)
//   go           → start searching
//   quit         → exit
//
// Reference: http://wbec-ridderkerk.nl/html/UCIProtocol.html
// =============================================================================

using static Board;
using static MoveEncoding;
using static MoveGenerator;

public static class Uci
{
    // =========================================================================
    // Engine Identity
    // =========================================================================

    private const string EngineName = "Amrou";
    private const string EngineAuthor = "Amrou";

    // =========================================================================
    // UCI Loop
    //
    //   Reads lines from stdin and dispatches to handlers.
    //   Runs until "quit" is received or TimeManagement.quit is set.
    // =========================================================================

    /// <summary>
    /// Main UCI communication loop.
    /// Reads commands from stdin, writes responses to stdout.
    /// </summary>
    public static void UciLoop()
    {
        // Announce engine identity and capabilities
        SendIdentity();

        while (true)
        {
            if (TimeManagement.quit) break;

            string? input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) continue;

            input = input.Trim();

            // Dispatch command
            if (input == "uci") SendIdentity();
            else if (input == "isready") Console.WriteLine("readyok");
            else if (input == "ucinewgame") HandleNewGame();
            else if (input.StartsWith("position")) ParsePosition(input);
            else if (input.StartsWith("go")) ParseGo(input);
            else if (input == "quit") break;
            // Unknown commands are silently ignored (UCI spec allows this)
        }
    }

    // =========================================================================
    // Command Handlers
    // =========================================================================

    /// <summary>
    /// Sends engine identity and "uciok" to confirm UCI mode.
    /// Called on startup and when "uci" is received.
    /// </summary>
    private static void SendIdentity()
    {
        Console.WriteLine($"id name {EngineName}");
        Console.WriteLine($"id author {EngineAuthor}");
        Console.WriteLine("uciok");
    }

    /// <summary>
    /// Handles "ucinewgame": resets to start position and clears the
    /// transposition table so the new game starts with a clean slate.
    /// </summary>
    private static void HandleNewGame()
    {
        ParsePosition("position startpos");
        TranspositionTable.Clear();
    }

    // =========================================================================
    // ParsePosition
    //
    //   Handles: "position startpos [moves e2e4 e7e5 ...]"
    //            "position fen <fen> [moves e2e4 ...]"
    //
    //   Steps:
    //     1. Parse the FEN (start position or custom)
    //     2. Reset repetition history
    //     3. Apply each move in the move list
    //        (recording the hash key before each move for repetition detection)
    //     4. Record the final position's hash key
    // =========================================================================

    /// <summary>
    /// Parses a "position" UCI command and sets up the board accordingly.
    /// </summary>
    public static void ParsePosition(string command)
    {
        // Split into tokens: ["position", "startpos", "moves", "e2e4", ...]
        string[] parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0 || parts[0] != "position")
            return;

        int index = 1; // current token index

        // ── Step 1: Parse FEN ─────────────────────────────────────────────

        if (index < parts.Length && parts[index] == "startpos")
        {
            ParseFEN(Program.StartPosition);
            index++;
        }
        else if (index < parts.Length && parts[index] == "fen")
        {
            index++;

            // Collect FEN tokens until "moves" or end of line
            // A FEN string has 6 space-separated fields:
            //   "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"
            var fenTokens = new List<string>();
            while (index < parts.Length && parts[index] != "moves")
            {
                fenTokens.Add(parts[index]);
                index++;
            }

            string fen = fenTokens.Count > 0
                ? string.Join(" ", fenTokens)
                : Program.StartPosition; // fallback if FEN was missing

            ParseFEN(fen);
        }
        else
        {
            // Unknown format: default to start position
            ParseFEN(Program.StartPosition);
        }

        // ── Step 2: Reset repetition history ──────────────────────────────
        //
        //   We clear the table and record the starting position.
        //   Every move we make will also be recorded.
        Search.RepetitionIndex = 0;

        // ── Step 3: Apply moves ────────────────────────────────────────────
        //
        //   "moves e2e4 e7e5 g1f3 ..."
        //
        //   Before each move, record the current hash key so we can detect
        //   repetitions. This matches how we handle it during search.
        if (index < parts.Length && parts[index] == "moves")
        {
            index++;

            while (index < parts.Length)
            {
                int move = ParseMove(parts[index]);
                if (move == 0) break; // illegal or unknown move

                // Record position BEFORE the move
                Search.PushRepetition(Zobrist.hashKey);

                MakeMove(move, (int)MoveFlag.allMoves);
                index++;
            }
        }

        // ── Step 4: Record the final position ─────────────────────────────
        Search.PushRepetition(Zobrist.hashKey);
    }

    // =========================================================================
    // ParseMove
    //
    //   Converts a move string like "e2e4" or "a7a8q" into an encoded move integer.
    //
    //   Steps:
    //     1. Convert "e2" → source square index
    //     2. Convert "e4" → target square index
    //     3. Generate all legal moves
    //     4. Find a generated move with matching source/target/promotion
    //
    //   Square index layout (same as Board):
    //     a8=0, b8=1, ..., h8=7
    //     a7=8, b7=9, ..., h7=15
    //     ...
    //     a1=56, b1=57, ..., h1=63
    //
    //   So: col = 'e' - 'a' = 4
    //       row = (8 - '2') * 8 = 48   → e2 = 52
    // =========================================================================

    /// <summary>
    /// Parses a UCI move string (e.g. "e2e4", "a7a8q") and returns the
    /// encoded move integer, or 0 if no legal move matches.
    /// </summary>
    public static int ParseMove(string moveString)
    {
        // Minimum valid move: "e2e4" = 4 characters
        if (string.IsNullOrEmpty(moveString) || moveString.Length < 4)
            return 0;

        // Convert notation to square indices
        int sourceSquare = (moveString[0] - 'a') + (8 - (moveString[1] - '0')) * 8;
        int targetSquare = (moveString[2] - 'a') + (8 - (moveString[3] - '0')) * 8;

        // Generate all legal moves for the current position
        MoveList moveList = new MoveList();
        GenerateMoves(ref moveList);

        for (int i = 0; i < moveList.count; i++)
        {
            int move = moveList.moves[i];

            // Check source and target square match
            if (GetMoveSource(move) != sourceSquare) continue;
            if (GetMoveTarget(move) != targetSquare) continue;

            int promoted = GetMovePromoted(move);

            if (promoted != 0)
            {
                // Move requires a promotion character (5th character)
                if (moveString.Length < 5) continue;

                // Match promotion piece character to the encoded promotion
                char promoChar = char.ToLower(moveString[4]);

                bool matches = promoChar switch
                {
                    'q' => promoted == Q || promoted == q,
                    'r' => promoted == R || promoted == r,
                    'b' => promoted == B || promoted == b,
                    'n' => promoted == N || promoted == n,
                    _ => false,
                };

                if (!matches) continue;
            }

            // Found a matching legal move
            return move;
        }

        return 0; // no legal move matched
    }

    // =========================================================================
    // ParseGo
    //
    //   Handles the "go" command which starts the search.
    //
    //   Supported parameters:
    //     depth <n>       → search exactly n plies
    //     infinite        → search until "stop" (not implemented: just depth 64)
    //     movetime <ms>   → use exactly this many milliseconds
    //     wtime <ms>      → white's remaining clock time
    //     btime <ms>      → black's remaining clock time
    //     winc <ms>       → white's increment per move
    //     binc <ms>       → black's increment per move
    //     movestogo <n>   → moves until next time control
    //
    //   Time management priority:
    //     1. movetime   → fixed time per move
    //     2. wtime/btime → dynamic time allocation
    //     3. infinite   → no time limit
    //     4. (default)  → depth 64, no time limit
    // =========================================================================

    /// <summary>
    /// Parses a "go" UCI command, configures time management, and starts the search.
    /// </summary>
    public static void ParseGo(string command)
    {
        string[] parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        int depth = 64;   // default: search as deep as time allows
        bool infinite = false;

        TimeManagement.ResetForGo();

        // Parse all parameters
        for (int i = 0; i < parts.Length; i++)
        {
            switch (parts[i])
            {
                case "infinite":
                    infinite = true;
                    break;

                case "depth" when i + 1 < parts.Length:
                    int.TryParse(parts[i + 1], out depth);
                    break;

                case "movetime" when i + 1 < parts.Length:
                    int.TryParse(parts[i + 1], out TimeManagement.movetime);
                    break;

                // Only read time/increment for the side currently to move
                case "wtime" when i + 1 < parts.Length && side == White:
                    int.TryParse(parts[i + 1], out TimeManagement.time);
                    break;

                case "btime" when i + 1 < parts.Length && side == Black:
                    int.TryParse(parts[i + 1], out TimeManagement.time);
                    break;

                case "winc" when i + 1 < parts.Length && side == White:
                    int.TryParse(parts[i + 1], out TimeManagement.inc);
                    break;

                case "binc" when i + 1 < parts.Length && side == Black:
                    int.TryParse(parts[i + 1], out TimeManagement.inc);
                    break;

                case "movestogo" when i + 1 < parts.Length:
                    int.TryParse(parts[i + 1], out TimeManagement.movestogo);
                    break;
            }
        }

        // ── Configure time management ──────────────────────────────────────
        if (infinite)
        {
            TimeManagement.StartInfiniteSearch();
        }
        else if (TimeManagement.movetime >= 0)
        {
            TimeManagement.StartMoveTimeSearch(TimeManagement.movetime);
        }
        else if (TimeManagement.time >= 0)
        {
            TimeManagement.StartClockSearch(TimeManagement.time, TimeManagement.inc);
        }

        // ── Debug: print time management values ───────────────────────────
        if (Program.debug)
        {
            Console.WriteLine(
                $"info string " +
                $"start:{TimeManagement.starttime} " +
                $"soft:{TimeManagement.softStopTime} " +
                $"hard:{TimeManagement.stoptime} " +
                $"time:{TimeManagement.time} " +
                $"inc:{TimeManagement.inc} " +
                $"depth:{depth}");
        }

        // ── Start searching ────────────────────────────────────────────────
        Search.SearchPosition(maxDepth: depth);
    }
}