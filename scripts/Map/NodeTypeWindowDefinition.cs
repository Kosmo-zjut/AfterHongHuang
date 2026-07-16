using System.Collections.Generic;

/// <summary>跨层复用的节点类型窗口、层内下限和图级上限。</summary>
public sealed class NodeTypeWindowDefinition
{
    public string WindowId { get; init; }
    public int MinLayer { get; init; }
    public int MaxLayer { get; init; }
    public IReadOnlyList<MapGraphNodeType> AllowedNodeTypes { get; init; }
    public int MinDistinctTypesPerLayer { get; init; }
    public int MinCombatNodesPerLayer { get; init; }
    public IReadOnlyDictionary<MapGraphNodeType, int> PerLayerCaps { get; init; } =
        new Dictionary<MapGraphNodeType, int>();
    public IReadOnlyDictionary<MapGraphNodeType, int> GraphCaps { get; init; } =
        new Dictionary<MapGraphNodeType, int>();
}
