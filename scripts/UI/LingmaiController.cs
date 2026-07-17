using Godot;
using System.Collections.Generic;

/// <summary>
/// 灵脉场景控制器。负责灵脉选项和升级选卡；地图由共享 overlay 呼出，不负责页面离场。
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
    private bool _healConsumed;
    private bool _upgradeConsumed;

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

        NodeMapEntry.Add(this, ToggleMapOverlay, IsMapOverlayOpen);
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
        bool canHeal = !_healConsumed && gm.PlayerHp < gm.PlayerMaxHp;
        var healButton = CreateButton(
            "休养生息",
            $"回复 {healAmount} 点生命（当前 {gm.PlayerHp}/{gm.PlayerMaxHp}）",
            canHeal,
            canHeal ? "" : _healConsumed ? "本次灵脉的休养生息已使用" : "气血已满，无需休养");
        healButton.Pressed += OnHealPressed;
        _optionContainer.AddChild(healButton);

        var upgradeableCards = gm.GetUpgradeableCards();
        bool canUpgrade = !_upgradeConsumed && upgradeableCards.Count > 0;
        var upgradeButton = CreateButton(
            "精进道行",
            canUpgrade ? $"选择 1 张卡牌升级（可精进 {upgradeableCards.Count} 张）" : "暂无可精进的卡牌",
            canUpgrade,
            canUpgrade ? "" : _upgradeConsumed ? "本次灵脉的精进道行已使用" : "暂无可精进的卡牌");
        upgradeButton.Pressed += () => ShowUpgradeChoices(upgradeableCards);
        _optionContainer.AddChild(upgradeButton);

        var companionButton = CreateButton("疗愈道友", "当前无人同行", false, "当前无人同行");
        _optionContainer.AddChild(companionButton);

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
        _healConsumed = true;
        string result = $"休养生息，生命 {oldHp}/{gm.PlayerMaxHp} → {gm.PlayerHp}/{gm.PlayerMaxHp}";
        GameManager.Instance.LastLingmaiResult = result;
        _resultLabel.Text = result;
        ShowOptions();
        GD.Print($"[灵脉] {result}");
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

        _upgradeConsumed = true;
        string result = $"精进道行，{oldName} → {upgradedInfo.Name}";
        GameManager.Instance.LastLingmaiResult = result;
        _resultLabel.Text = result;
        ShowOptions();
        GD.Print($"[灵脉] {result}");
    }

    private void ToggleMapOverlay()
    {
        if (_mapOverlay != null && GodotObject.IsInstanceValid(_mapOverlay))
        {
            _mapOverlay.Close();
            return;
        }

        _mapOverlay = MapOverlayController.Open(this, !_invalidEntry,
            _invalidEntry ? null : OnMapNodePressed, () => _mapOverlay = null);
        if (_mapOverlay == null)
            _resultLabel.Text = "地图 overlay 创建失败，请查看日志。";
    }

    private bool IsMapOverlayOpen() => _mapOverlay != null && GodotObject.IsInstanceValid(_mapOverlay);

    private void OnMapNodePressed(MapNodeDefinition info)
    {
        if (info == null)
            return;
        var gm = GameManager.Instance;
        if (!gm.TryTransitionAndRouteFromActiveServiceNode(info, out var transitionError))
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
