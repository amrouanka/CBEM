using System.Globalization;
using System.Text;

public sealed class EvalWeights
{
    public int PawnMgAdjust = 0;
    public int PawnEgAdjust = 0;
    public int KnightMgAdjust = 0;
    public int KnightEgAdjust = 0;
    public int BishopMgAdjust = 0;
    public int BishopEgAdjust = 0;
    public int RookMgAdjust = 0;
    public int RookEgAdjust = 0;
    public int QueenMgAdjust = 0;
    public int QueenEgAdjust = 0;

    public int BishopPairMg = 25;
    public int BishopPairEg = 49;

    public int KnightMobMg = 1;
    public int KnightMobEg = 0;
    public int BishopMobMg = 4;
    public int BishopMobEg = 4;

    public int RookSemiOpenMg = 21;
    public int RookSemiOpenEg = 15;
    public int RookOpenMg = 54;
    public int RookOpenEg = 8;

    public int[] PassedMg = [0, 0, 34, 14, 11, 11, 10, 0];
    public int[] PassedEg = [0, 48, 95, 52, 26, 7, 4, 0];

    public int[,] PstMgAdjust = new int[6, 64];
    public int[,] PstEgAdjust = new int[6, 64];

    public int IsolatedMg = -19;
    public int IsolatedEg = -8;

    public int KingOwnOpenMg = 71;
    public int KingOwnSemiOpenMg = 18;
    public int KingAdjacentOpenMg = 37;
    public int KingAdjacentSemiOpenMg = 13;

    public int QueenlessKingCenterMg = 20;

    public int KnightOutpostMg = 46;

    public EvalWeights Clone()
    {
        return new EvalWeights
        {
            PawnMgAdjust = PawnMgAdjust,
            PawnEgAdjust = PawnEgAdjust,
            KnightMgAdjust = KnightMgAdjust,
            KnightEgAdjust = KnightEgAdjust,
            BishopMgAdjust = BishopMgAdjust,
            BishopEgAdjust = BishopEgAdjust,
            RookMgAdjust = RookMgAdjust,
            RookEgAdjust = RookEgAdjust,
            QueenMgAdjust = QueenMgAdjust,
            QueenEgAdjust = QueenEgAdjust,

            BishopPairMg = BishopPairMg,
            BishopPairEg = BishopPairEg,

            KnightMobMg = KnightMobMg,
            KnightMobEg = KnightMobEg,
            BishopMobMg = BishopMobMg,
            BishopMobEg = BishopMobEg,

            RookSemiOpenMg = RookSemiOpenMg,
            RookSemiOpenEg = RookSemiOpenEg,
            RookOpenMg = RookOpenMg,
            RookOpenEg = RookOpenEg,

            PassedMg = (int[])PassedMg.Clone(),
            PassedEg = (int[])PassedEg.Clone(),

            PstMgAdjust = (int[,])PstMgAdjust.Clone(),
            PstEgAdjust = (int[,])PstEgAdjust.Clone(),

            IsolatedMg = IsolatedMg,
            IsolatedEg = IsolatedEg,

            KingOwnOpenMg = KingOwnOpenMg,
            KingOwnSemiOpenMg = KingOwnSemiOpenMg,
            KingAdjacentOpenMg = KingAdjacentOpenMg,
            KingAdjacentSemiOpenMg = KingAdjacentSemiOpenMg,

            QueenlessKingCenterMg = QueenlessKingCenterMg,

            KnightOutpostMg = KnightOutpostMg
        };
    }

    public string ToCSharpConstants()
    {
        StringBuilder sb = new();
        string[] pieceNames = ["Pawn", "Knight", "Bishop", "Rook", "Queen", "King"];

        int[] baseMg = Evaluation.GetMgMaterial();
        int[] baseEg = Evaluation.GetEgMaterial();
        int[] adjMg = [PawnMgAdjust, KnightMgAdjust, BishopMgAdjust, RookMgAdjust, QueenMgAdjust, 0];
        int[] adjEg = [PawnEgAdjust, KnightEgAdjust, BishopEgAdjust, RookEgAdjust, QueenEgAdjust, 0];

        // MG PST
        int[][] mgPst = Evaluation.GetMgPst();
        int[][] egPst = Evaluation.GetEgPst();

        sb.AppendLine("private static readonly int[][] MgPst =");
        sb.AppendLine("[");
        for (int piece = 0; piece < 6; piece++)
        {
            sb.AppendLine($"    // {pieceNames[piece]}");
            sb.AppendLine("    [");
            for (int sq = 0; sq < 64; sq++)
            {
                int adjusted = mgPst[piece][sq] + PstMgAdjust[piece, sq];
                if (sq % 8 == 0) sb.Append("        ");
                sb.Append($"{adjusted,4}");
                if (sq < 63) sb.Append(",");
                if (sq % 8 == 7) sb.AppendLine();
            }
            sb.AppendLine("    ],");
        }
        sb.AppendLine("];");
        sb.AppendLine();

        // EG PST
        sb.AppendLine("private static readonly int[][] EgPst =");
        sb.AppendLine("[");
        for (int piece = 0; piece < 6; piece++)
        {
            sb.AppendLine($"    // {pieceNames[piece]}");
            sb.AppendLine("    [");
            for (int sq = 0; sq < 64; sq++)
            {
                int adjusted = egPst[piece][sq] + PstEgAdjust[piece, sq];
                if (sq % 8 == 0) sb.Append("        ");
                sb.Append($"{adjusted,4}");
                if (sq < 63) sb.Append(",");
                if (sq % 8 == 7) sb.AppendLine();
            }
            sb.AppendLine("    ],");
        }
        sb.AppendLine("];");
        sb.AppendLine();

        // Material
        sb.Append("private static readonly int[] MgMaterial = [");
        for (int i = 0; i < 6; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(baseMg[i] + adjMg[i]);
        }
        sb.AppendLine("];");

        sb.Append("private static readonly int[] EgMaterial = [");
        for (int i = 0; i < 6; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(baseEg[i] + adjEg[i]);
        }
        sb.AppendLine("];");
        sb.AppendLine();


        // Positional
        sb.AppendLine($"private const int BishopPairMg = {BishopPairMg};");
        sb.AppendLine($"private const int BishopPairEg = {BishopPairEg};");
        sb.AppendLine();
        sb.AppendLine($"private const int KnightMobMg = {KnightMobMg}, KnightMobEg = {KnightMobEg};");
        sb.AppendLine($"private const int BishopMobMg = {BishopMobMg}, BishopMobEg = {BishopMobEg};");
        sb.AppendLine();
        sb.AppendLine($"private const int RookSemiOpenMg = {RookSemiOpenMg}, RookSemiOpenEg = {RookSemiOpenEg};");
        sb.AppendLine($"private const int RookOpenMg = {RookOpenMg}, RookOpenEg = {RookOpenEg};");
        sb.AppendLine();
        sb.Append("private static readonly int[] PassedMg = [");
        sb.Append(string.Join(", ", PassedMg));
        sb.AppendLine("];");
        sb.Append("private static readonly int[] PassedEg = [");
        sb.Append(string.Join(", ", PassedEg));
        sb.AppendLine("];");
        sb.AppendLine();
        sb.AppendLine($"private const int IsolatedMg = {IsolatedMg};");
        sb.AppendLine($"private const int IsolatedEg = {IsolatedEg};");
        sb.AppendLine();
        sb.AppendLine($"private const int KingOwnOpenMg = {KingOwnOpenMg}, KingOwnSemiOpenMg = {KingOwnSemiOpenMg};");
        sb.AppendLine($"private const int KingAdjacentOpenMg = {KingAdjacentOpenMg}, KingAdjacentSemiOpenMg = {KingAdjacentSemiOpenMg};");
        sb.AppendLine();
        sb.AppendLine($"private const int QueenlessKingCenterMg = {QueenlessKingCenterMg};");
        sb.AppendLine();
        sb.AppendLine($"private const int KnightOutpostMg = {KnightOutpostMg};");

        return sb.ToString();
    }
}