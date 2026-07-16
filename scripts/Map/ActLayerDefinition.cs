using System.Collections.Generic;

/// <summary>单层地图生成规则，属于 ActDefinition 数据而非生成器分支。</summary>
public sealed class ActLayerDefinition
{
    public int LayerIndex { get; init; }
    public int NodeCount { get; init; }
    public IReadOnlyList<string> WindowIds { get; init; }
    public IReadOnlyList<MapGraphNodeType> AllowedNodeTypes { get; init; }
    public IReadOnlyList<MapGraphNodeType> RequiredNodeTypes { get; init; }
    public EncounterTier BattleTier { get; init; }
    public IReadOnlyDictionary<MapGraphNodeType, int> NodeTypeWeights { get; init; }
    public string StableOrderPolicy { get; init; }
}
