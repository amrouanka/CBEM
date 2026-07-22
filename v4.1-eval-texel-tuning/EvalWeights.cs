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

        sb.AppendLine($"private const int BishopPairMg = {BishopPairMg};");
        sb.AppendLine($"private const int BishopPairEg = {BishopPairEg};");
        sb.AppendLine();

        sb.AppendLine($"private const int KnightMobMg = {KnightMobMg}, KnightMobEg = {KnightMobEg}, KnightMobBase = 4;");
        sb.AppendLine($"private const int BishopMobMg = {BishopMobMg}, BishopMobEg = {BishopMobEg}, BishopMobBase = 6;");
        sb.AppendLine();

        sb.AppendLine($"private const int RookSemiOpenMg = {RookSemiOpenMg}, RookSemiOpenEg = {RookSemiOpenEg};");
        sb.AppendLine($"private const int RookOpenMg = {RookOpenMg}, RookOpenEg = {RookOpenEg};");
        sb.AppendLine();

        sb.AppendLine($"private const int IsolatedMg = {IsolatedMg};");
        sb.AppendLine($"private const int IsolatedEg = {IsolatedEg};");
        sb.AppendLine();

        sb.AppendLine($"private const int KingOwnOpenMg = {KingOwnOpenMg}, KingOwnSemiOpenMg = {KingOwnSemiOpenMg};");
        sb.AppendLine($"private const int KingAdjacentOpenMg = {KingAdjacentOpenMg}, KingAdjacentSemiOpenMg = {KingAdjacentSemiOpenMg};");
        sb.AppendLine();

        sb.AppendLine($"private const int KnightOutpostMg = {KnightOutpostMg};");

        return sb.ToString();
    }
}