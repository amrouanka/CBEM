using System.Globalization;

public static class EpdToPositions
{
    public static void Convert(string epdPath, string outputPath)
    {
        Console.WriteLine($"Reading EPD from {epdPath}...");

        HashSet<string> seen = new();
        List<string> outputLines = new();
        int total = 0;
        int skipped = 0;

        foreach (string rawLine in File.ReadLines(epdPath))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            // EPD format: <FEN without move counters> <operations>
            // e.g: rnbq1rk1/ppp2pbp/3p1np1/4p3/2PPP3/2N1BN2/PP3PPP/R2QKB1R w KQ - c9 "1/2-1/2";
            // OR with move counters already included (some sets):
            // rnbq1rk1/ppp2pbp/3p1np1/4p3/2PPP3/2N1BN2/PP3PPP/R2QKB1R w KQ - 0 1 c9 "1/2-1/2";

            // Find the result string
            int quoteStart = line.IndexOf('"');
            int quoteEnd = line.LastIndexOf('"');
            if (quoteStart < 0 || quoteEnd <= quoteStart)
            {
                skipped++;
                continue;
            }

            string resultStr = line[(quoteStart + 1)..quoteEnd];
            double result = resultStr switch
            {
                "1-0" => 1.0,
                "0-1" => 0.0,
                "1/2-1/2" => 0.5,
                _ => -1.0
            };

            if (result < 0.0)
            {
                skipped++;
                continue;
            }

            // Everything before the first operation tag is the FEN
            // Strip the c9 "..." part to get clean FEN
            string fenPart = line[..quoteStart].Trim();

            // Remove trailing operation name (e.g. "c9" or "ce" or "acd" etc)
            // The last token before the quote is the operation name
            string[] fenTokens = fenPart.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // A full FEN has 6 tokens: pieces side castling ep halfmove fullmove
            // EPD without move counters has 4 tokens: pieces side castling ep
            // Either way, strip any trailing non-FEN tokens (operation names)
            string fen;
            if (fenTokens.Length >= 6)
            {
                // Has move counters — take first 6
                fen = string.Join(' ', fenTokens[..6]);
            }
            else if (fenTokens.Length == 5)
            {
                // Missing fullmove — add it
                fen = string.Join(' ', fenTokens[..4]) + " 0 1";
            }
            else if (fenTokens.Length == 4)
            {
                // Pure EPD — add dummy move counters
                fen = string.Join(' ', fenTokens) + " 0 1";
            }
            else
            {
                skipped++;
                continue;
            }

            // Validate by parsing — skip if board state is broken
            try
            {
                Board.ParseFEN(fen);
            }
            catch
            {
                skipped++;
                continue;
            }

            // Skip if in check — not quiet
            int kingSq = BitboardOperations.GetLs1bIndex(
                Board.side == MoveGenerator.White
                    ? Board.bitboards[Board.K]
                    : Board.bitboards[Board.k]);

            if (PieceAttacks.IsSquareAttacked(kingSq, Board.side ^ 1))
            {
                skipped++;
                continue;
            }

            // Deduplicate
            if (!seen.Add(fen))
            {
                skipped++;
                continue;
            }

            outputLines.Add($"{result.ToString("F1", CultureInfo.InvariantCulture)} | {fen}");
            total++;

            if (total % 100_000 == 0)
                Console.WriteLine($"  Converted {total} positions...");
        }

        File.WriteAllLines(outputPath, outputLines);
        Console.WriteLine($"Done. Wrote {total} positions, skipped {skipped}.");
    }
}