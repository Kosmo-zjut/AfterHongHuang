/// <summary>统一奖励写入接口的最小数据契约，后续可在此接入 RewardId 幂等表。</summary>
public enum RewardGrantType
{
    LingYun = 0,
    Card = 1,
}

public sealed class RewardGrant
{
    public string RewardId { get; init; }
    public RewardGrantType GrantType { get; init; }
    public int LingYunAmount { get; init; }
    public CardInfo Card { get; init; }
}
