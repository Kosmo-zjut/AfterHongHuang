using System.Collections.Generic;

/// <summary>沿实际地图边应用的节点相邻限制。</summary>
public sealed class NodeAdjacencyRule
{
    public string RuleId { get; init; }
    public IReadOnlyList<MapGraphNodeType> AppliesTo { get; init; }
}
