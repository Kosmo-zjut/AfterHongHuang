/// <summary>MapGraph 中的不可变节点定义。</summary>
public sealed class MapNodeDefinition
{
    public string NodeId { get; init; }
    public int LayerIndex { get; init; }
    public int IndexInLayer { get; init; }
    /// <summary>节点在所属层的持久化顺序；地图坐标和无交叉校验均消费此字段。</summary>
    public int LayerOrder { get; init; }
    /// <summary>稳定排序键；同层比较边是否反转，不由 UI 坐标推导。</summary>
    public int StableOrder { get; init; }
    public MapGraphNodeType NodeType { get; init; }
    public EncounterTier Tier { get; init; }
    public string PoolId { get; init; }
    public ulong NodeSeed { get; init; }
    public string ContentId { get; init; }
    public string DisplayName { get; init; }
}
