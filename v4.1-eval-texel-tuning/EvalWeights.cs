using System.Globalization;
using System.Text;

public sealed class EvalWeights
{
    public int BishopPairMg = 30;
    public int BishopPairEg = 50;

    public int KnightMobMg = 2;
    public int KnightMobEg = 2;
    public int BishopMobMg = 3;
    public int BishopMobEg = 3;

    public int RookSemiOpenMg = 8;
    public int RookSemiOpenEg = 6;
    public int RookOpenMg = 15;
    public int RookOpenEg = 10;

    public int[] PassedMg = [0, 15, 15, 17, 10, 6, 0, 0];
    public int[] PassedEg = [0, 97, 55, 36, 17, 12, 0, 0];

    public int IsolatedMg = -5;
    public int IsolatedEg = -15;

    public int KingOwnOpenMg = 20;
    public int KingOwnSemiOpenMg = 10;
    public int KingAdjacentOpenMg = 8;
    public int KingAdjacentSemiOpenMg = 4;

    public int KnightOutpostMg = 12;

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

        sb.AppendLine($"private static readonly int[] PassedMg = [{string.Join(", ", PassedMg)}];");
        sb.AppendLine($"private static readonly int[] PassedEg = [{string.Join(", ", PassedEg)}];");
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