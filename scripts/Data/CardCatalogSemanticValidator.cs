using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>Catalog 候选数据的纯语义校验结果；可由运行时加载与编辑器提交共享。</summary>
public sealed class CardCatalogSemanticValidationResult
{
    public IReadOnlyList<CardCatalogSemanticIssue> Issues { get; init; } = Array.Empty<CardCatalogSemanticIssue>();
    public bool IsValid => Issues.Count == 0;
}

/// <summary>可定位的候选 Catalog 校验问题，不依赖 UI 或节点树。</summary>
public sealed class CardCatalogSemanticIssue
{
    public string Scope { get; init; }
    public string Path { get; init; }
    public string Message { get; init; }
}

/// <summary>
/// Resource、Catalog 与 manifest 的唯一候选语义门禁。运行时复用 Resource/Catalog/执行计划校验；
/// 编辑器额外传入 manifest 镜像，避免保存事务另写一套 GDScript 规则。
/// </summary>
public static class CardCatalogSemanticValidator
{
    public static CardCatalogSemanticValidationResult Validate(CardCatalogResource catalog,
        IReadOnlyList<CardDefinitionResource> cards, string manifestJson = null)
    {
        var issues = new List<CardCatalogSemanticIssue>();
        if (catalog == null)
        {
            Add(issues, "catalog", "catalog", "候选 Catalog 为空。");
            return Result(issues);
        }

        if (catalog.SchemaVersion != CardDefinitionValidator.CurrentSchemaVersion)
            Add(issues, "catalog", "catalog.schemaVersion", "Catalog SchemaVersion 不受支持。");

        var paths = catalog.CardResourcePaths?.ToList() ?? new List<string>();
        if (paths.Count == 0)
            Add(issues, "catalog", "catalog.cardResourcePaths", "候选 Catalog 缺少卡牌路径。");
        var seenPaths = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < paths.Count; index++)
        {
            string path = paths[index];
            if (string.IsNullOrWhiteSpace(path))
                Add(issues, "catalog", $"catalog.cardResourcePaths[{index}]", "路径不能为空。");
            else if (!seenPaths.Add(path))
                Add(issues, "catalog", $"catalog.cardResourcePaths[{index}]", "路径重复。");
        }

        if (cards == null || cards.Count != paths.Count)
            Add(issues, "catalog", "catalog.cardResourcePaths", "候选卡牌数量与路径列表不一致。");
        else if (!CardDefinitionValidator.TryValidateCatalog(cards, catalog.GetPools(), out var validationErrors))
            foreach (var error in validationErrors)
                Add(issues, "catalog", error, error);

        if (cards != null)
        {
            foreach (var card in cards.Where(card => card != null))
            {
                if (!CardDefinitionProjection.TryCreate(card, out _, out _, out var projectionError))
                    Add(issues, "runtimeProjection", $"card[{card.Id}]", projectionError);
            }
        }

        if (manifestJson != null)
            ValidateManifestMirror(manifestJson, paths, issues);
        return Result(issues);
    }

    private static void ValidateManifestMirror(string manifestJson, IReadOnlyList<string> expectedPaths,
        List<CardCatalogSemanticIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(manifestJson))
        {
            Add(issues, "manifest", "manifest", "manifest 内容不能为空。");
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(manifestJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schemaVersion", out var schemaVersion) ||
                schemaVersion.ValueKind != JsonValueKind.Number || schemaVersion.GetInt32() != 1)
            {
                Add(issues, "manifest", "manifest.schemaVersion", "必须为整数 1。");
                return;
            }
            if (!root.TryGetProperty("cardResourcePaths", out var paths) || paths.ValueKind != JsonValueKind.Array)
            {
                Add(issues, "manifest", "manifest.cardResourcePaths", "必须为数组。");
                return;
            }
            if (paths.GetArrayLength() != expectedPaths.Count)
            {
                Add(issues, "manifest", "manifest.cardResourcePaths", "数量与候选 Catalog 不一致。");
                return;
            }

            int index = 0;
            foreach (var path in paths.EnumerateArray())
            {
                if (path.ValueKind != JsonValueKind.String || !string.Equals(path.GetString(), expectedPaths[index], StringComparison.Ordinal))
                    Add(issues, "manifest", $"manifest.cardResourcePaths[{index}]", "与候选 Catalog 路径不一致。");
                index++;
            }
        }
        catch (JsonException exception)
        {
            Add(issues, "manifest", "manifest", $"JSON 解析失败：{exception.Message}");
        }
    }

    private static CardCatalogSemanticValidationResult Result(IReadOnlyList<CardCatalogSemanticIssue> issues) =>
        new() { Issues = issues };

    private static void Add(List<CardCatalogSemanticIssue> issues, string scope, string path, string message) =>
        issues.Add(new CardCatalogSemanticIssue { Scope = scope, Path = path, Message = message });
}
