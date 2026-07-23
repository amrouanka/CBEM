using System.Globalization;
using System.Text;

public sealed class EvalWeights
{
    public int FixedMgScale = 100;
    public int FixedEgScale = 100;

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

    public int BishopPairMg = 30;
    public int BishopPairEg = 50;

    public int KnightMobMg = 4;
    public int KnightMobEg = 4;
    public int BishopMobMg = 5;
    public int BishopMobEg = 5;

    public int RookSemiOpenMg = 10;
    public int RookSemiOpenEg = 8;
    public int RookOpenMg = 20;
    public int RookOpenEg = 12;

    public int[] PassedMg = [0, 10, 10, 15, 25, 40, 70, 0];
    public int[] PassedEg = [0, 15, 20, 35, 55, 90, 140, 0];

    public int IsolatedMg = -8;
    public int IsolatedEg = -12;

    public int KingOwnOpenMg = 25;
    public int KingOwnSemiOpenMg = 10;
    public int KingAdjacentOpenMg = 10;
    public int KingAdjacentSemiOpenMg = 5;

    public int KnightOutpostMg = 15;

    public EvalWeights Clone()
    {
        return new EvalWeights
        {
            FixedMgScale = FixedMgScale,
            FixedEgScale = FixedEgScale,

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

            IsolatedMg = IsolatedMg,
            IsolatedEg = IsolatedEg,

            KingOwnOpenMg = KingOwnOpenMg,
            KingOwnSemiOpenMg = KingOwnSemiOpenMg,
            KingAdjacentOpenMg = KingAdjacentOpenMg,
            KingAdjacentSemiOpenMg = KingAdjacentSemiOpenMg,

            KnightOutpostMg = KnightOutpostMg
        };
    }

    public string ToCSharpConstants()
    {
        StringBuilder sb = new();

        int[] baseMg = [82, 337, 365, 477, 1025, 0];
        int[] baseEg = [94, 281, 297, 512, 936, 0];
        int[] adjMg = [PawnMgAdjust, KnightMgAdjust, BishopMgAdjust, RookMgAdjust, QueenMgAdjust, 0];
        int[] adjEg = [PawnEgAdjust, KnightEgAdjust, BishopEgAdjust, RookEgAdjust, QueenEgAdjust, 0];

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

        return sb.ToString();
    }
}