using Godot;
using System.Collections.Generic;

/// <summary>
/// 灵脉场景控制器。负责灵脉选项和升级选卡；地图由共享 overlay 呼出，不负责页面离场。
/// </summary>
public partial class LingmaiController : Control
{
    [Export] public PackedScene SettingsDialogScene { get; set; }

    private Label _titleLabel;
    private Label _descriptionLabel;
    private Label _resultLabel;
    private VBoxContainer _optionContainer;
    private TopBar _topBar;
    private NodePageNavigationCoordinator _nodeNavigation;
    private LingmaiActionCommand _actions;
    private MapNodeDefinition _lingmaiNode;
    private bool _invalidEntry;

    public override void _Ready()
    {
        _titleLabel = GetNode<Label>("ContentPanel/TitleLabel");
        _descriptionLabel = GetNode<Label>("ContentPanel/DescriptionLabel");
        _resultLabel = GetNode<Label>("ContentPanel/ResultLabel");
        _optionContainer = GetNode<VBoxContainer>("ContentPanel/OptionScroll/OptionContainer");
        _topBar = GetNode<TopBar>("TopBar");
        _topBar.OnDeckPressed = () => DeckViewer.Show(this);
        _topBar.OnMapPressed = ToggleMapOverlay;
        _topBar.OnSettingsPressed = () => SettingsHelper.Show(this, SettingsDialogScene);
        _nodeNavigation = new NodePageNavigationCoordinator(this, GameManager.Instance,
            error => _resultLabel.Text = $"地图导航失败：{error}");

        _lingmaiNode = FindCurrentLingmaiNode();
        if (_lingmaiNode == null)
        {
            _invalidEntry = true;
            ShowInvalidNodeState();
            return;
        }

        _actions = new LingmaiActionCommand(GameManager.Instance);
        NodeMapEntry.Add(this, ToggleMapOverlay);
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
        _descriptionLabel.Text = "选择行动可获得对应收益；不选择也可直接通过地图继续前行。";
        ClearOptions();

        int healAmount = Mathf.Max(1, Mathf.FloorToInt(gm.PlayerMaxHp * 0.3f));
        bool canHeal = _actions?.CanRest == true;
        var healButton = CreateButton(
            "休养生息",
            $"回复 {healAmount} 点生命（当前 {gm.PlayerHp}/{gm.PlayerMaxHp}）",
            canHeal,
            canHeal ? "" : "本次灵脉的休养生息已使用");
        healButton.Pressed += OnHealPressed;
        _optionContainer.AddChild(healButton);

        var upgradeableCards = gm.GetUpgradeableCards();
        bool canUpgrade = _actions?.CanUpgrade == true && upgradeableCards.Count > 0;
        var upgradeButton = CreateButton(
            "精进道行",
            canUpgrade ? $"选择 1 张卡牌升级（可精进 {upgradeableCards.Count} 张）" : "暂无可精进的卡牌",
            canUpgrade,
            canUpgrade ? "" : _actions?.CanUpgrade == false ? "本次灵脉的精进道行已使用" : "暂无可精进的卡牌");
        upgradeButton.Pressed += () => ShowUpgradeChoices(upgradeableCards);
        _optionContainer.AddChild(upgradeButton);

        var eligibleCompanions = gm.GetEligibleLingmaiHealingTargets();
        foreach (var companion in eligibleCompanions)
        {
            bool canHealCompanion = _actions?.CanHealCompanion == true;
            var companionButton = CreateButton(
                $"疗愈道友：{companion.DisplayName}",
                $"回复 {Mathf.Max(1, Mathf.FloorToInt(companion.MaxHp * 0.3f))} 点生命（当前 {companion.CurrentHp}/{companion.MaxHp}）",
                canHealCompanion,
                "本次灵脉的疗愈道友已使用");
            string targetId = companion.MemberId;
            companionButton.Pressed += () => OnHealCompanionPressed(targetId);
            _optionContainer.AddChild(companionButton);
        }

    }

    private void ShowUpgradeChoices(List<CardRuntime> upgradeableCards)
    {
        _titleLabel.Text = "精进道行";
        _descriptionLabel.Text = "选择一张当前牌组中的卡牌，永久替换为升级版。";
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

    }

    private void OnHealPressed()
    {
        LingmaiActionCommandResult action = null;
        string error = "灵脉行动命令未初始化。";
        if (_actions == null || !_actions.TryRest(out action, out error))
        {
            ShowActionError(error);
            return;
        }
        PresentAction(action);
    }

    private void OnUpgradeCardPressed(CardRuntime card, CardInfo upgradedInfo)
    {
        LingmaiActionCommandResult action = null;
        string error = "灵脉行动命令未初始化。";
        if (_actions == null || !_actions.TryUpgrade(card, out action, out error))
        {
            ShowActionError(error);
            ShowOptions();
            return;
        }
        PresentAction(action);
    }

    private void OnHealCompanionPressed(string targetMemberId)
    {
        LingmaiActionCommandResult action = null;
        string error = "灵脉行动命令未初始化。";
        if (_actions == null || !_actions.TryHealCompanion(targetMemberId, out action, out error))
        {
            ShowActionError(error);
            return;
        }
        PresentAction(action);
    }

    private void PresentAction(LingmaiActionCommandResult action)
    {
        _resultLabel.Text = action.Feedback;
        ShowOptions();
        GD.Print($"[灵脉] {action.Feedback}");
    }

    private void ShowActionError(string error)
    {
        string message = string.IsNullOrWhiteSpace(error) ? "灵脉行动被拒绝。" : error;
        GD.PrintErr($"[灵脉] 行动失败：{message}");
        _resultLabel.Text = $"灵脉行动失败：{message}";
    }

    private void ToggleMapOverlay()
    {
        if (_nodeNavigation == null)
        {
            _resultLabel.Text = "地图导航服务未初始化，请查看日志。";
            return;
        }
        if (!_nodeNavigation.TryToggleMap(out var error) && !string.IsNullOrWhiteSpace(error))
            _resultLabel.Text = $"地图 overlay 创建失败：{error}";
    }

    public override void _ExitTree()
    {
        _nodeNavigation?.Dispose();
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
