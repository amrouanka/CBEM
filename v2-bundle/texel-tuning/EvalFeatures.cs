public sealed class EvalFeatures
{
    public int PawnCountBalance;
    public int KnightCountBalance;
    public int BishopCountBalance;
    public int RookCountBalance;
    public int QueenCountBalance;

    // Fixed base score from material + PST only
    public int FixedMg;
    public int FixedEg;
    public int Phase;

    // Scalar feature balances from White's perspective
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

    public int KnightOutpostBalance;
}