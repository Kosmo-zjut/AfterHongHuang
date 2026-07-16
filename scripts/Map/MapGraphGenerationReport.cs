using System.Collections.Generic;

/// <summary>MapGraph 生成和验证统计，不参与地图运行态。</summary>
public sealed class MapGraphGenerationReport
{
    public int Attempts { get; init; }
    public int PathCount { get; init; }
    public int ReachableShopCount { get; init; }
    public string Fingerprint { get; init; }
    public IReadOnlyList<string> ValidationErrors { get; init; }
}
