/// <summary>
/// 从一局主 seed 派生相互隔离的命名随机流和节点局部流。
/// 每次请求返回新的 StableRandom 实例，不共享可变 RNG 状态。
/// </summary>
public sealed class StableRandomStreams
{
    private readonly ulong _runSeed;
    private readonly string _actId;
    private readonly int _ruleVersion;

    public StableRandomStreams(ulong runSeed, string actId, int ruleVersion)
    {
        _runSeed = runSeed;
        _actId = actId ?? "";
        _ruleVersion = ruleVersion;
    }

    public StableRandom CreateStream(RandomStreamKey streamKey) =>
        CreateScopedStream(streamKey, "root", 0);

    public StableRandom CreateScopedStream(RandomStreamKey streamKey, string scope, int index)
    {
        return new StableRandom(DeriveSeed(streamKey, scope, index));
    }

    public StableRandom CreateNodeStream(RandomStreamKey streamKey, string nodeId, int localIndex)
    {
        return CreateScopedStream(streamKey, nodeId, localIndex);
    }

    public ulong DeriveSeed(RandomStreamKey streamKey, string scope, int index)
    {
        string material = $"{_runSeed:X16}|{_actId}|{_ruleVersion}|{streamKey}|{scope ?? ""}|{index}";
        return StableHash.HashToUInt64(material);
    }

    public StableRandom CreateMapStream(int generationAttempt) =>
        CreateScopedStream(RandomStreamKey.Map, "map_graph", generationAttempt);

    public StableRandom CreateEncounterStream(string nodeId, int localIndex) =>
        CreateNodeStream(RandomStreamKey.Encounter, nodeId, localIndex);

    public StableRandom CreateEventStream(string nodeId, int localIndex) =>
        CreateNodeStream(RandomStreamKey.Event, nodeId, localIndex);

    public StableRandom CreateShopStream(string nodeId, int localIndex) =>
        CreateNodeStream(RandomStreamKey.Shop, nodeId, localIndex);

    public StableRandom CreateRewardStream(string nodeId, int rewardIndex) =>
        CreateNodeStream(RandomStreamKey.Reward, nodeId, rewardIndex);

    public StableRandom CreateCombatStream(string nodeId, int shuffleIndex) =>
        CreateNodeStream(RandomStreamKey.Combat, nodeId, shuffleIndex);
}
