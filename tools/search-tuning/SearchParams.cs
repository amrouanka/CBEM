using System;

public static class SearchParams
{
    public static int AspirationMinDepth = 4;

    public static int ReverseFutilityMaxDepth = 3;
    public static int FutilityMaxDepth = 3;

    public static int NullMoveMinDepth = 3;

    public static int AspirationWindow = 50;

    public static int FullDepthMoves = 4;
    public static int ReductionLimit = 3;

    public static int LmrBase = 1;
    public static int LmrDivisor = 2;

    public static int ReverseFutilityMarginPerDepth = 150;
    public static int FutilityMarginPerDepth = 120;

    public static int NullMoveBaseReduction = 3;
    public static int NullMoveDepthDivisor = 4;
    public static int NullMoveEvalDivisor = 200;
    public static int NullMoveEvalBonusCap = 3;

    public static int QsDeltaMargin = 200;

    public static void ResetDefaults()
    {
        AspirationMinDepth = 4;

        ReverseFutilityMaxDepth = 3;
        FutilityMaxDepth = 3;

        NullMoveMinDepth = 3;

        AspirationWindow = 50;

        FullDepthMoves = 4;
        ReductionLimit = 3;

        LmrBase = 1;
        LmrDivisor = 2;

        ReverseFutilityMarginPerDepth = 150;
        FutilityMarginPerDepth = 120;

        NullMoveBaseReduction = 3;
        NullMoveDepthDivisor = 4;
        NullMoveEvalDivisor = 200;
        NullMoveEvalBonusCap = 3;

        QsDeltaMargin = 200;
    }

    public static void ParseArgs(string[] args)
    {
        ResetDefaults();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (!arg.StartsWith("--"))
                continue;

            int eq = arg.IndexOf('=');
            if (eq <= 2 || eq == arg.Length - 1)
                continue;

            string key = arg.Substring(2, eq - 2).ToLowerInvariant();
            string valueText = arg.Substring(eq + 1);

            if (!int.TryParse(valueText, out int value))
                continue;

            switch (key)
            {
                case "awmindepth":
                case "aspirationmindepth":
                    AspirationMinDepth = value;
                    break;

                case "rfpdepth":
                case "reversefutilitymaxdepth":
                    ReverseFutilityMaxDepth = value;
                    break;

                case "fpdepth":
                case "futilitymaxdepth":
                    FutilityMaxDepth = value;
                    break;

                case "nmdepth":
                case "nullmovemindepth":
                    NullMoveMinDepth = value;
                    break;

                case "aw":
                case "aspirationwindow":
                    AspirationWindow = value;
                    break;

                case "fdm":
                case "fulldepthmoves":
                    FullDepthMoves = value;
                    break;

                case "rl":
                case "reductionlimit":
                    ReductionLimit = value;
                    break;

                case "lmrbase":
                    LmrBase = value;
                    break;

                case "lmrdiv":
                case "lmrdivisor":
                    LmrDivisor = value;
                    break;

                case "rfp":
                case "reversefutilitymargin":
                    ReverseFutilityMarginPerDepth = value;
                    break;

                case "fp":
                case "futilitymargin":
                    FutilityMarginPerDepth = value;
                    break;

                case "nmbase":
                case "nullmovebasereduction":
                    NullMoveBaseReduction = value;
                    break;

                case "nmdepthdiv":
                case "nullmovedepthdivisor":
                    NullMoveDepthDivisor = value;
                    break;

                case "nmevaldiv":
                case "nullmoveevaldivisor":
                    NullMoveEvalDivisor = value;
                    break;

                case "nmbonuscap":
                case "nullmoveevalbonuscap":
                    NullMoveEvalBonusCap = value;
                    break;

                case "qsdelta":
                case "qsdeltamargin":
                    QsDeltaMargin = value;
                    break;
            }
        }

        Sanitize();
    }

    private static void Sanitize()
    {
        if (AspirationMinDepth < 1) AspirationMinDepth = 1;

        if (ReverseFutilityMaxDepth < 0) ReverseFutilityMaxDepth = 0;
        if (FutilityMaxDepth < 0) FutilityMaxDepth = 0;

        if (NullMoveMinDepth < 1) NullMoveMinDepth = 1;

        if (AspirationWindow < 1) AspirationWindow = 1;

        if (FullDepthMoves < 1) FullDepthMoves = 1;
        if (ReductionLimit < 1) ReductionLimit = 1;

        if (LmrBase < 0) LmrBase = 0;
        if (LmrDivisor < 1) LmrDivisor = 1;

        if (ReverseFutilityMarginPerDepth < 0) ReverseFutilityMarginPerDepth = 0;
        if (FutilityMarginPerDepth < 0) FutilityMarginPerDepth = 0;

        if (NullMoveBaseReduction < 0) NullMoveBaseReduction = 0;
        if (NullMoveDepthDivisor < 1) NullMoveDepthDivisor = 1;
        if (NullMoveEvalDivisor < 1) NullMoveEvalDivisor = 1;
        if (NullMoveEvalBonusCap < 0) NullMoveEvalBonusCap = 0;

        if (QsDeltaMargin < 0) QsDeltaMargin = 0;
    }

    public static string ToArgumentString()
    {
        return
            $"--awmindepth={AspirationMinDepth} " +
            $"--rfpdepth={ReverseFutilityMaxDepth} " +
            $"--fpdepth={FutilityMaxDepth} " +
            $"--nmdepth={NullMoveMinDepth} " +
            $"--aw={AspirationWindow} " +
            $"--fdm={FullDepthMoves} " +
            $"--rl={ReductionLimit} " +
            $"--lmrbase={LmrBase} " +
            $"--lmrdiv={LmrDivisor} " +
            $"--rfp={ReverseFutilityMarginPerDepth} " +
            $"--fp={FutilityMarginPerDepth} " +
            $"--nmbase={NullMoveBaseReduction} " +
            $"--nmdepthdiv={NullMoveDepthDivisor} " +
            $"--nmevaldiv={NullMoveEvalDivisor} " +
            $"--nmbonuscap={NullMoveEvalBonusCap} " +
            $"--qsdelta={QsDeltaMargin}";
    }
}