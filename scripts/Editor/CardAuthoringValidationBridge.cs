using Godot;
using System.Collections.Generic;

/// <summary>
/// 仅随卡牌编辑 Dock 存在的 Tool 节点。GDScript 只传 staging 路径与 manifest 文本；
/// 此处在 C# 内加载强类型 Resource，避免跨语言参数被降级为基类 Resource。
/// </summary>
[Tool]
public partial class CardAuthoringValidationBridge : Node
{
    /// <summary>校验 staging 三件套，返回可直接呈现的结构化问题列表，不修改 Resource 或 Catalog。</summary>
    public Godot.Collections.Dictionary ValidateCandidateStaging(string stagedCardPath, string stagedCatalogPath,
        string manifestJson, string targetResourcePath)
    {
        var issues = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        if (string.IsNullOrWhiteSpace(stagedCardPath) || string.IsNullOrWhiteSpace(stagedCatalogPath) ||
            string.IsNullOrWhiteSpace(targetResourcePath))
        {
            AddIssue(issues, "resource", "staging", "staging 路径或目标资源路径不能为空。");
            return ToResult(issues);
        }

        var stagedCard = ResourceLoader.Load<CardDefinitionResource>(stagedCardPath);
        if (stagedCard == null)
            AddIssue(issues, "resource", stagedCardPath, "无法加载 staging 卡牌 Resource。");
        var catalog = ResourceLoader.Load<CardCatalogResource>(stagedCatalogPath);
        if (catalog == null)
            AddIssue(issues, "catalog", stagedCatalogPath, "无法加载 staging Catalog Resource。");
        if (issues.Count > 0)
            return ToResult(issues);

        var cards = new List<CardDefinitionResource>();
        int targetCount = 0;
        foreach (var path in catalog.CardResourcePaths)
        {
            if (path == targetResourcePath)
            {
                cards.Add(stagedCard);
                targetCount++;
                continue;
            }

            var card = ResourceLoader.Load<CardDefinitionResource>(path);
            if (card == null)
                AddIssue(issues, "resource", path, "候选 Catalog 引用的卡牌无法加载。");
            cards.Add(card);
        }
        if (targetCount != 1)
            AddIssue(issues, "catalog", "catalog.cardResourcePaths", "候选目标资源必须恰好出现一次。");
        if (issues.Count > 0)
            return ToResult(issues);

        var validation = CardCatalogSemanticValidator.Validate(catalog, cards, manifestJson);
        foreach (var issue in validation.Issues)
            AddIssue(issues, issue.Scope, issue.Path, issue.Message);
        return ToResult(issues);
    }

    private static Godot.Collections.Dictionary ToResult(Godot.Collections.Array<Godot.Collections.Dictionary> issues) =>
        new() { ["ok"] = issues.Count == 0, ["issues"] = issues };

    private static void AddIssue(Godot.Collections.Array<Godot.Collections.Dictionary> issues,
        string scope, string path, string message) =>
        issues.Add(new Godot.Collections.Dictionary { ["scope"] = scope, ["path"] = path, ["message"] = message });
}
