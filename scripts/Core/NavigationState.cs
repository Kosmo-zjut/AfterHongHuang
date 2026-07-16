/// <summary>
/// 场景切换期间的兼容导航状态。
/// TODO: Phase B 迁移到统一的 NodeResult/场景路由，不把导航标记当作 RunState 事实。
/// </summary>
public sealed class NavigationState
{
    public bool OpenMapOnEnter { get; set; }
    /// <summary>
    /// MapScene 的显式进入目标。它描述场景初始化时应展示的页面，不属于 RunState 事实。
    /// </summary>
    public MapEntryMode MapEntryMode { get; set; }
    public string LastLingmaiResult { get; set; } = "";
}

/// <summary>MapScene 初始化时的页面目标。</summary>
public enum MapEntryMode
{
    None = 0,
    OpenInteractiveMap = 1,
}
