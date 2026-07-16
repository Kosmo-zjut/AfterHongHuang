using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// 纯数据 MapGraph。节点按层和层内索引保存，边只允许连接相邻层。
/// </summary>
public sealed class MapGraph
{
    private readonly List<IReadOnlyList<MapNodeDefinition>> _layers;
    private readonly List<MapGraphEdge> _edges;

    public string ActId { get; }
    public int RuleVersion { get; }
    public IReadOnlyList<IReadOnlyList<MapNodeDefinition>> Layers => _layers;
    public IReadOnlyList<MapGraphEdge> Edges => _edges;
    public string Fingerprint { get; }
    public string GraphFingerprint => Fingerprint;

    public MapGraph(List<List<MapNodeDefinition>> layers, List<MapGraphEdge> edges,
        string actId = "", int ruleVersion = 0)
    {
        ActId = actId ?? "";
        RuleVersion = ruleVersion;
        _layers = layers
            .Select(layer => (IReadOnlyList<MapNodeDefinition>)layer.ToList())
            .ToList();
        _edges = edges
            .OrderBy(edge => edge.FromNodeId)
            .ThenBy(edge => edge.ToNodeId)
            .ToList();
        Fingerprint = BuildFingerprint();
    }

    public IEnumerable<MapNodeDefinition> AllNodes()
    {
        foreach (var layer in _layers)
        foreach (var node in layer)
            yield return node;
    }

    public IReadOnlyList<MapGraphEdge> GetOutgoing(string nodeId)
    {
        return _edges.Where(edge => edge.FromNodeId == nodeId).ToList();
    }

    public MapNodeDefinition GetNode(string nodeId)
    {
        return AllNodes().FirstOrDefault(node => node.NodeId == nodeId);
    }

    private string BuildFingerprint()
    {
        var builder = new StringBuilder();
        builder.Append(ActId).Append('|').Append(RuleVersion).Append('#');
        foreach (var layer in _layers)
        foreach (var node in layer)
        {
            builder.Append(node.NodeId).Append('|')
                .Append(node.LayerIndex).Append('|')
                .Append(node.IndexInLayer).Append('|')
                .Append(node.LayerOrder).Append('|')
                .Append(node.StableOrder).Append('|')
                .Append(node.NodeType).Append('|')
                .Append(node.Tier).Append('|')
                .Append(node.PoolId).Append('|')
                .Append(node.ContentId).Append('|')
                .Append(node.NodeSeed).Append(';');
        }

        builder.Append('#');
        foreach (var edge in _edges)
            builder.Append(edge.FromNodeId).Append('>').Append(edge.ToNodeId).Append(';');

        return StableHash.HashToUInt64(builder.ToString()).ToString("X16");
    }
}
