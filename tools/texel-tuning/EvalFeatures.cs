public sealed class EvalFeatures
{
    // public int FixedMg;
    // public int FixedEg;

    public int PawnCountBalance;
    public int KnightCountBalance;
    public int BishopCountBalance;
    public int RookCountBalance;
    public int QueenCountBalance;

    public int Phase;

    public int BishopPairBalance;

    public int KnightMobilityBalance;
    public int BishopMobilityBalance;

    public int RookSemiOpenBalance;
    public int RookOpenBalance;

    public int IsolatedPawnBalance;

    public int KingOwnOpenBalance;
    public int KingOwnSemiOpenBalance;
    public int KingAdjacentOpenBalance;
    public int KingAdjacentSemiOpenBalance;

    public int QueenlessKingCenterBalance;

    public int KnightOutpostBalance;

    public int[] PassedPawnBalance = new int[8];

    public int[,] PieceSqBalance = new int[6, 64];
}