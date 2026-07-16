using System.Collections.Generic;

/// <summary>MapGraphValidator 的结构约束报告。</summary>
public sealed class MapGraphValidationReport
{
    public int PathCount { get; set; }
    public int ReachableShopCount { get; set; }
    public int NoShopPathCount { get; set; }
    public int CrossingCount { get; set; }
    public List<string> Errors { get; } = new();
    public bool IsValid => Errors.Count == 0;
}
