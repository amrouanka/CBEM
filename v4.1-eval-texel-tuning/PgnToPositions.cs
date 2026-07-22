using System.Globalization;
using System.Text.RegularExpressions;

public static class PgnToPositions
{
    public static void Convert(
    string pgnPath,
    string outputPath,
    int skipPlies = 12,
    int sampleEvery = 8,
    int maxPositionsPerGame = 2,
    int maxCaptureMoves = 2)
    {
        Console.WriteLine($"Reading PGN from {pgnPath}...");

        HashSet<string> seen = new();
        List<string> outputLines = new();
        string[] lines = File.ReadAllLines(pgnPath);

        string currentFen = Program.StartPosition;
        string currentResult = "";
        List<string> moveTokens = new();


        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            if (line.StartsWith("[FEN "))
            {
                int firstQuote = line.IndexOf('"');
                int lastQuote = line.LastIndexOf('"');

                if (firstQuote >= 0 && lastQuote > firstQuote)
                    currentFen = line[(firstQuote + 1)..lastQuote];

                continue;
            }

            if (line.StartsWith("[Result "))
            {
                int firstQuote = line.IndexOf('"');
                int lastQuote = line.LastIndexOf('"');

                if (firstQuote >= 0 && lastQuote > firstQuote)
                    currentResult = line[(firstQuote + 1)..lastQuote];

                moveTokens.Clear();
                continue;
            }

            if (line.StartsWith("["))
                continue;

            if (line.Length == 0)
            {
                if (moveTokens.Count > 0 && currentResult.Length > 0)
                {
                    double result = currentResult switch
                    {
                        "1-0" => 1.0,
                        "0-1" => 0.0,
                        "1/2-1/2" => 0.5,
                        _ => -1.0
                    };

                    if (result >= 0.0)
                        ExtractPositions(currentFen, moveTokens, result, skipPlies, sampleEvery, maxPositionsPerGame, maxCaptureMoves, outputLines, seen);

                    moveTokens.Clear();
                    currentResult = "";
                    currentFen = Program.StartPosition;
                }

                continue;
            }

            // Remove {...} comments before splitting
            string cleaned = Regex.Replace(line, @"\{[^}]*\}", "");
            moveTokens.AddRange(cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        // Handle last game if file doesn't end with blank line
        if (moveTokens.Count > 0 && currentResult.Length > 0)
        {
            double result = currentResult switch
            {
                "1-0" => 1.0,
                "0-1" => 0.0,
                "1/2-1/2" => 0.5,
                _ => -1.0
            };

            if (result >= 0.0)
                ExtractPositions(currentFen, moveTokens, result, skipPlies, sampleEvery, maxPositionsPerGame, maxCaptureMoves, outputLines, seen);
        }

        File.WriteAllLines(outputPath, outputLines);
        Console.WriteLine($"Wrote {outputLines.Count} positions to {outputPath}");
    }

    private static void ExtractPositions(
    string startFen,
    List<string> moveTokens,
    double result,
    int skipPlies,
    int sampleEvery,
    int maxPositionsPerGame,
    int maxCaptureMoves,
    List<string> output,
    HashSet<string> seen)
    {
        Board.ParseFEN(startFen);

        int ply = 0;
        int saved = 0;

        foreach (string token in moveTokens)
        {
            if (saved >= maxPositionsPerGame)
                break;

            // Skip move numbers, results, and PGN comments like {book}
            if (token.Contains('.') ||
                token == "1-0" ||
                token == "0-1" ||
                token == "1/2-1/2" ||
                token == "*" ||
                token == "White" ||
                token == "Black" ||
                token == "Draw")
                continue;

            string moveStr = token.Replace("+", "").Replace("#", "").Replace("!", "").Replace("?", "");

            int move = ParseSanMove(moveStr);
            if (move == 0)
            {
                Console.WriteLine($"Failed to parse SAN '{moveStr}' at ply {ply} from start FEN:");
                Console.WriteLine(startFen);
                return;
            }

            BoardState state = Board.CopyBoard();
            if (MoveGenerator.MakeMove(move, (int)MoveFlag.allMoves) == 0)
            {
                Board.TakeBack(state);
                return;
            }

            ply++;

            if (ply <= skipPlies)
                continue;

            if (ply % sampleEvery != 0)
                continue;

            if (!IsQuietForTexel(maxCaptureMoves))
                continue;

            string fen = BoardToFen();

            if (seen.Add(fen))
            {
                output.Add($"{result.ToString("F1", CultureInfo.InvariantCulture)} | {fen}");
                saved++;
            }
        }
    }

    private static bool IsQuietForTexel(int maxCaptureMoves)
    {
        int stmKingSq = BitboardOperations.GetLs1bIndex(
            Board.side == MoveGenerator.White ? Board.bitboards[Board.K] : Board.bitboards[Board.k]);

        // Side to move is in check -> tactical / unstable
        if (PieceAttacks.IsSquareAttacked(stmKingSq, Board.side ^ 1))
            return false;

        MoveList captures = new();
        MoveGenerator.GenerateCaptureMoves(ref captures);

        // Too many captures usually means tactical noise
        if (captures.count > maxCaptureMoves)
            return false;

        return true;
    }

    private static int ParseSanMove(string san)
    {
        MoveList moveList = new();
        MoveGenerator.GenerateMoves(ref moveList);

        for (int i = 0; i < moveList.count; i++)
        {
            int move = moveList.moves[i];

            BoardState state = Board.CopyBoard();
            if (MoveGenerator.MakeMove(move, (int)MoveFlag.allMoves) == 0)
            {
                Board.TakeBack(state);
                continue;
            }

            Board.TakeBack(state);

            string moveSan = MoveToSan(move, ref moveList);
            if (moveSan == san)
                return move;
        }

        return 0;
    }

    private static string MoveToSan(int move, ref MoveList moveList)
    {
        int source = MoveEncoding.GetMoveSource(move);
        int target = MoveEncoding.GetMoveTarget(move);
        int piece = MoveEncoding.GetMovePiece(move);
        int promoted = MoveEncoding.GetMovePromoted(move);
        int capture = MoveEncoding.GetMoveCapture(move);
        int castling = MoveEncoding.GetMoveCastling(move);

        // Castling
        if (castling != 0)
        {
            int file = target % 8;
            return file == 6 ? "O-O" : "O-O-O";
        }

        // Pawn moves
        if (piece == Board.P || piece == Board.p)
        {
            string result = "";

            if (capture != 0)
                result += (char)('a' + source % 8) + "x";

            result += Board.squareToCoordinates[target];

            if (promoted != 0)
            {
                char promoChar = char.ToUpper(Board.asciiPieces[promoted]);
                result += "=" + promoChar;
            }

            return result;
        }

        // Piece moves
        char pieceChar = char.ToUpper(Board.asciiPieces[piece]);
        string disambiguation = GetDisambiguation(move, piece, source, target, ref moveList);

        string san = pieceChar.ToString() + disambiguation;

        if (capture != 0) san += "x";

        san += Board.squareToCoordinates[target];

        return san;
    }

    private static string GetDisambiguation(int move, int piece, int source, int target, ref MoveList moveList)
    {
        int ambiguities = 0;
        bool sameFile = false;
        bool sameRank = false;

        for (int i = 0; i < moveList.count; i++)
        {
            int other = moveList.moves[i];
            if (other == move) continue;

            int otherPiece = MoveEncoding.GetMovePiece(other);
            int otherTarget = MoveEncoding.GetMoveTarget(other);
            int otherSource = MoveEncoding.GetMoveSource(other);

            if (otherPiece != piece || otherTarget != target) continue;

            // Check if this move is legal
            BoardState state = Board.CopyBoard();
            if (MoveGenerator.MakeMove(other, (int)MoveFlag.allMoves) == 0)
            {
                Board.TakeBack(state);
                continue;
            }
            Board.TakeBack(state);

            ambiguities++;

            if (otherSource % 8 == source % 8) sameFile = true;
            if (otherSource / 8 == source / 8) sameRank = true;
        }

        if (ambiguities == 0) return "";
        if (!sameFile) return ((char)('a' + source % 8)).ToString();
        if (!sameRank) return (8 - source / 8).ToString();
        return ((char)('a' + source % 8)).ToString() + (8 - source / 8);
    }

    private static string BoardToFen()
    {
        char[] fen = new char[128];
        int idx = 0;

        for (int rank = 0; rank < 8; rank++)
        {
            int empty = 0;

            for (int file = 0; file < 8; file++)
            {
                int sq = rank * 8 + file;
                int foundPiece = -1;

                for (int p = Board.P; p <= Board.k; p++)
                {
                    if (BitboardOperations.GetBit(Board.bitboards[p], sq))
                    {
                        foundPiece = p;
                        break;
                    }
                }

                if (foundPiece == -1)
                {
                    empty++;
                }
                else
                {
                    if (empty > 0) { fen[idx++] = (char)('0' + empty); empty = 0; }
                    fen[idx++] = Board.asciiPieces[foundPiece];
                }
            }

            if (empty > 0) fen[idx++] = (char)('0' + empty);
            if (rank < 7) fen[idx++] = '/';
        }

        fen[idx++] = ' ';
        fen[idx++] = Board.side == MoveGenerator.White ? 'w' : 'b';
        fen[idx++] = ' ';

        // Castling
        bool anyCastle = false;
        if ((Board.castle & (int)CastlingRights.wk) != 0) { fen[idx++] = 'K'; anyCastle = true; }
        if ((Board.castle & (int)CastlingRights.wq) != 0) { fen[idx++] = 'Q'; anyCastle = true; }
        if ((Board.castle & (int)CastlingRights.bk) != 0) { fen[idx++] = 'k'; anyCastle = true; }
        if ((Board.castle & (int)CastlingRights.bq) != 0) { fen[idx++] = 'q'; anyCastle = true; }
        if (!anyCastle) fen[idx++] = '-';

        fen[idx++] = ' ';

        // En passant
        if (Board.enPassant != (int)Square.noSquare)
        {
            string epStr = Board.squareToCoordinates[Board.enPassant];
            fen[idx++] = epStr[0];
            fen[idx++] = epStr[1];
        }
        else
        {
            fen[idx++] = '-';
        }

        fen[idx++] = ' ';
        fen[idx++] = '0';
        fen[idx++] = ' ';
        fen[idx++] = '1';

        return new string(fen, 0, idx);
    }
}