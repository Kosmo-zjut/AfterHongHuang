/// <summary>MapGraph 节点的运行时包装，供 B2 接入 Active/Completed 状态时复用。</summary>
public sealed class MapNodeRuntime
{
    public MapNodeDefinition Definition { get; init; }
    public NodeLifecycleState Lifecycle { get; set; } = NodeLifecycleState.Active;
}
