using Godot;
using System.Collections.Generic;

/// <summary>
/// 灵脉场景控制器。负责灵脉选项、升级选卡和返回地图，不负责地图绘制。
/// </summary>
public partial class LingmaiController : Control
{
    private Label _titleLabel;
    private Label _descriptionLabel;
    private Label _resultLabel;
    private VBoxContainer _optionContainer;
    private TopBar _topBar;
    private MapOverlayController _mapOverlay;
    private MapNodeDefinition _lingmaiNode;
    private bool _invalidEntry;
    private bool _settled;

    public override void _Ready()
    {
        _titleLabel = GetNode<Label>("ContentPanel/TitleLabel");
        _descriptionLabel = GetNode<Label>("ContentPanel/DescriptionLabel");
        _resultLabel = GetNode<Label>("ContentPanel/ResultLabel");
        _optionContainer = GetNode<VBoxContainer>("ContentPanel/OptionScroll/OptionContainer");
        _topBar = GetNode<TopBar>("TopBar");
        _topBar.OnDeckPressed = () => DeckViewer.Show(this);
        _topBar.OnMapPressed = ToggleMapOverlay;
        _topBar.OnSettingsPressed = () => SettingsHelper.Show(this);

        _lingmaiNode = FindCurrentLingmaiNode();
        if (_lingmaiNode == null)
        {
            _invalidEntry = true;
            ShowInvalidNodeState();
            return;
        }

        ShowOptions();
    }

    private MapNodeDefinition FindCurrentLingmaiNode()
    {
        var gm = GameManager.Instance;
        var activeNode = gm.ActiveNode;
        if (activeNode == null)
        {
            GD.PrintErr("[灵脉] 场景进入失败：缺少 ActiveNode 上下文。");
            return null;
        }

        if (activeNode.NodeType != MapGraphNodeType.Lingmai)
        {
            GD.PrintErr($"[灵脉] 场景进入位置不是灵脉节点：{activeNode.NodeId} / {activeNode.NodeType}");
            return null;
        }

        var node = gm.MapGraph?.GetNode(activeNode.NodeId);
        if (node != null && node.NodeType == MapGraphNodeType.Lingmai)
            return node;

        GD.PrintErr($"[灵脉] ActiveNode 找不到地图节点定义：{activeNode.NodeId}");
        return null;
    }

    private void ShowInvalidNodeState()
    {
        _titleLabel.Text = "灵脉节点不可用";
        _descriptionLabel.Text = "当前地图位置无法定位灵脉节点，请返回地图。";
        _resultLabel.Text = "已记录无效进入状态，请从地图重新选择可达节点。";
        ClearOptions();

        var returnButton = CreateButton("返回地图", "不消耗灵脉", true);
        returnButton.Pressed += RecoverInvalidEntry;
        _optionContainer.AddChild(returnButton);
    }

    private void RecoverInvalidEntry()
    {
        if (!_invalidEntry)
        {
            GD.PrintErr("[灵脉] 非法恢复入口被正常流程调用，拒绝继续。");
            return;
        }

        if (!GameManager.Instance.RecoverFromInvalidNodeEntry(out var error))
        {
            GD.PrintErr($"[灵脉] 无效入口恢复失败：{error}");
            _resultLabel.Text = "当前入口无法恢复，请重新进入地图。";
            return;
        }

        ToggleMapOverlay();
    }

    private void ShowOptions()
    {
        var gm = GameManager.Instance;
        _titleLabel.Text = "灵脉节点";
        _descriptionLabel.Text = _settled
            ? "当前灵脉已结算；可通过顶部地图查看并选择下一节点。"
            : "可选择一项行动，也可返回地图继续前行；未选择不会消耗灵脉。";
        ClearOptions();

        if (_settled)
        {
            var settledButton = CreateButton("已结算", _resultLabel.Text, false, "当前灵脉已完成，不可重复操作");
            _optionContainer.AddChild(settledButton);
            var mapButton = CreateButton("返回地图", "打开地图卷轴并选择下一节点", true);
            mapButton.Pressed += OpenMapOverlayOnly;
            _optionContainer.AddChild(mapButton);
            return;
        }

        int healAmount = Mathf.Max(1, Mathf.FloorToInt(gm.PlayerMaxHp * 0.3f));
        bool canHeal = gm.PlayerHp < gm.PlayerMaxHp;
        var healButton = CreateButton(
            "休养生息",
            $"回复 {healAmount} 点生命（当前 {gm.PlayerHp}/{gm.PlayerMaxHp}）",
            canHeal,
            canHeal ? "" : "气血已满，无需休养");
        healButton.Pressed += OnHealPressed;
        _optionContainer.AddChild(healButton);

        var upgradeableCards = gm.GetUpgradeableCards();
        bool canUpgrade = upgradeableCards.Count > 0;
        var upgradeButton = CreateButton(
            "精进道行",
            canUpgrade ? $"选择 1 张卡牌升级（可精进 {upgradeableCards.Count} 张）" : "暂无可精进的卡牌",
            canUpgrade,
            canUpgrade ? "" : "暂无可精进的卡牌");
        upgradeButton.Pressed += () => ShowUpgradeChoices(upgradeableCards);
        _optionContainer.AddChild(upgradeButton);

        var companionButton = CreateButton("疗愈道友", "当前无人同行", false, "当前无人同行");
        _optionContainer.AddChild(companionButton);

        var returnButton = CreateButton("返回地图", "仅打开地图卷轴，不结算灵脉", true);
        returnButton.Pressed += OpenMapOverlayOnly;
        _optionContainer.AddChild(returnButton);

        var abandonButton = CreateButton("放弃灵脉并继续前行", "明确放弃本次灵脉行动，不获得收益", true);
        abandonButton.Pressed += AbandonLingmaiAndContinue;
        _optionContainer.AddChild(abandonButton);
    }

    private void ShowUpgradeChoices(List<CardRuntime> upgradeableCards)
    {
        _titleLabel.Text = "精进道行";
        _descriptionLabel.Text = "选择一张当前牌组中的卡牌，永久替换为升级版；返回不会消耗灵脉。";
        _resultLabel.Text = "";
        ClearOptions();

        foreach (var card in upgradeableCards)
        {
            if (!GameManager.Instance.TryGetUpgradeForCard(card.Info, out var upgradedInfo))
                continue;

            var cardButton = CreateButton(
                $"{card.Info.Name} → {upgradedInfo.Name}",
                string.Format(upgradedInfo.Description, upgradedInfo.Value),
                true);
            cardButton.Pressed += () => OnUpgradeCardPressed(card, upgradedInfo);
            _optionContainer.AddChild(cardButton);
        }

        var backButton = CreateButton("返回选项", "不消耗灵脉", true);
        backButton.Pressed += ShowOptions;
        _optionContainer.AddChild(backButton);

        var mapButton = CreateButton("返回地图", "仅打开地图卷轴，不结算灵脉", true);
        mapButton.Pressed += OpenMapOverlayOnly;
        _optionContainer.AddChild(mapButton);

        var abandonButton = CreateButton("放弃灵脉并继续前行", "明确放弃本次灵脉行动，不获得收益", true);
        abandonButton.Pressed += AbandonLingmaiAndContinue;
        _optionContainer.AddChild(abandonButton);
    }

    private void OnHealPressed()
    {
        var gm = GameManager.Instance;
        if (gm.PlayerHp >= gm.PlayerMaxHp)
        {
            GD.Print("[灵脉] 气血已满，休养生息未结算。");
            ShowOptions();
            return;
        }

        int oldHp = gm.PlayerHp;
        int healAmount = Mathf.Max(1, Mathf.FloorToInt(gm.PlayerMaxHp * 0.3f));
        gm.PlayerHp = Mathf.Min(gm.PlayerMaxHp, gm.PlayerHp + healAmount);
        CompleteLingmai($"休养生息，生命 {oldHp}/{gm.PlayerMaxHp} → {gm.PlayerHp}/{gm.PlayerMaxHp}");
    }

    private void OnUpgradeCardPressed(CardRuntime card, CardInfo upgradedInfo)
    {
        string oldName = card.Info.Name;
        if (!GameManager.Instance.TryUpgradeCard(card))
        {
            GD.PrintErr("[灵脉] 精进道行失败：目标卡牌已不可升级或不在当前牌组。");
            ShowOptions();
            return;
        }

        CompleteLingmai($"精进道行，{oldName} → {upgradedInfo.Name}");
    }

    private void CompleteLingmai(string result)
    {
        if (_lingmaiNode == null)
        {
            GD.PrintErr("[灵脉] 结算失败：当前灵脉节点为空，未修改访问状态。");
            return;
        }

        var gm = GameManager.Instance;
        var nodeResult = gm.CreateNodeResult(NodeResultType.Completed, result, out var createError);
        string submitError = "";
        bool submitted = nodeResult != null && gm.SubmitNodeResult(nodeResult, out submitError);
        if (!submitted)
        {
            GD.PrintErr($"[灵脉] 结算失败，未标记节点：{createError}{submitError}");
            _resultLabel.Text = "结算失败，请返回地图重新进入。";
            return;
        }

        gm.LastLingmaiResult = result;
        _settled = true;
        _resultLabel.Text = result;
        ShowOptions();
        GD.Print($"[灵脉] {result}");
    }

    /// <summary>Opens the shared overlay only. Navigation alone never creates an Exited node result.</summary>
    private void OpenMapOverlayOnly()
    {
        ToggleMapOverlay();
    }

    /// <summary>Explicit gameplay action for abandoning the current Lingmai and advancing its route.</summary>
    private void AbandonLingmaiAndContinue()
    {
        if (_settled)
            return;
        var gm = GameManager.Instance;
        var nodeResult = gm.CreateNodeResult(NodeResultType.Exited, "灵脉未选择行动", out var createError);
        string submitError = "";
        bool submitted = nodeResult != null && gm.SubmitNodeResult(nodeResult, out submitError);
        if (!submitted)
        {
            GD.PrintErr($"[灵脉] 放弃灵脉未能记录 Exited/Skipped 状态：{createError}{submitError}");
            return;
        }

        _settled = true;
        _resultLabel.Text = "已放弃灵脉行动，可通过地图选择下一节点。";
        ShowOptions();
        GD.Print("[灵脉] 已明确放弃行动，记录 Exited/Skipped 状态。" );
    }

    private void ToggleMapOverlay()
    {
        if (_mapOverlay != null && GodotObject.IsInstanceValid(_mapOverlay))
        {
            _mapOverlay.Close();
            return;
        }

        var gm = GameManager.Instance;
        bool interactive = _settled && gm.ActiveNodeResultSubmitted;
        _mapOverlay = MapOverlayController.Open(this, interactive,
            interactive ? OnMapNodePressed : null, () => _mapOverlay = null);
        if (_mapOverlay == null)
            _resultLabel.Text = "地图 overlay 创建失败，请查看日志。";
    }

    private void OnMapNodePressed(MapNodeDefinition info)
    {
        if (!_settled || info == null)
            return;
        var gm = GameManager.Instance;
        if (!gm.TryTransitionAndRouteFromCompletedNode(info, out var transitionError))
        {
            GD.PrintErr($"[灵脉] 目标节点事务迁移失败：{transitionError}");
            _resultLabel.Text = "进入下一节点失败，当前结果页已保留。";
            return;
        }
        _mapOverlay?.CloseImmediately();
        _mapOverlay = null;
    }

    private void ClearOptions()
    {
        foreach (Node child in _optionContainer.GetChildren())
            child.QueueFree();
    }

    private Button CreateButton(string title, string detail, bool enabled, string disabledReason = "")
    {
        var button = new Button();
        button.Text = $"{title}\n{detail}";
        button.CustomMinimumSize = new Vector2(560, 72);
        button.AddThemeFontSizeOverride("font_size", 17);
        button.Disabled = !enabled;
        button.TooltipText = enabled ? "" : disabledReason;
        if (!enabled)
            button.Modulate = new Color(0.45f, 0.45f, 0.45f, 1f);
        return button;
    }
}
