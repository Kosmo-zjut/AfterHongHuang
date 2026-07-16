using Godot;

/// <summary>事件页面。只渲染 EventDefinition 选项，并把效果交给通用执行器。</summary>
public partial class EventController : Control
{
    private Label _titleLabel;
    private Label _descriptionLabel;
    private Label _resultLabel;
    private VBoxContainer _optionContainer;
    private TopBar _topBar;
    private MapOverlayController _mapOverlay;
    private MapNodeDefinition _eventNode;
    private EventDefinition _definition;
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

        var gm = GameManager.Instance;
        _eventNode = gm.ActiveNode == null ? null : gm.MapGraph?.GetNode(gm.ActiveNode.NodeId);
        if (gm.ActiveNode == null || gm.ActiveNode.NodeType != MapGraphNodeType.Event || _eventNode == null)
        {
            GD.PrintErr("[事件] 场景进入失败：缺少有效 Event NodeContext。");
            _invalidEntry = true;
            ShowInvalidState();
            return;
        }

        if (!EventDefinitionCatalog.TryGet(_eventNode.ContentId, out _definition, out var definitionError))
        {
            GD.PrintErr($"[事件] 事件定义绑定失败：{definitionError}");
            _invalidEntry = true;
            ShowInvalidState();
            return;
        }

        _titleLabel.Text = _definition.Title;
        _descriptionLabel.Text = _definition.Description;
        RenderOptions();
    }

    private void RenderOptions()
    {
        var gm = GameManager.Instance;
        string settledSummary = _resultLabel.Text;
        ClearOptions();
        if (_settled)
        {
            _resultLabel.Text = settledSummary;
            var settledButton = CreateOption("已结算", settledSummary, false, "当前事件已完成，不可重复执行");
            _optionContainer.AddChild(settledButton);
            return;
        }
        foreach (var option in _definition.Options)
        {
            bool enabled = option.Enabled && gm.PlayerHp >= option.MinimumHp &&
                gm.GetDeckSize() >= option.MinimumDeckSize;
            string reason = option.Enabled ? GetUnavailableReason(option) : option.DisabledReason;
            AddOption(option, enabled, reason);
        }
    }

    private string GetUnavailableReason(EventOptionDefinition option)
    {
        var gm = GameManager.Instance;
        if (gm.PlayerHp < option.MinimumHp)
            return $"当前生命不足{option.MinimumHp}点";
        if (gm.GetDeckSize() < option.MinimumDeckSize)
            return $"牌组至少需要{option.MinimumDeckSize}张牌";
        return "";
    }

    private void OnOptionPressed(EventOptionDefinition option)
    {
        if (_settled || option == null)
            return;
        if (option.RequiresCardChoice)
            ShowCardRemovalChoices(option);
        else
            ExecuteOption(option, null);
    }

    private void ShowCardRemovalChoices(EventOptionDefinition option)
    {
        _titleLabel.Text = option.Title;
        _descriptionLabel.Text = "选择一张牌永久移除；取消不会结算，也不会消耗事件。";
        ClearOptions();
        foreach (var card in GameManager.Instance.GetAllDeckCards())
        {
            var target = card;
            var button = CreateOption($"移除：{card.Info.Name}", "永久移出牌组", true, "");
            button.Pressed += () => ExecuteOption(option, target);
            _optionContainer.AddChild(button);
        }
        var backButton = CreateOption("返回事件选项", "不结算", true, "");
        backButton.Pressed += RenderOptions;
        _optionContainer.AddChild(backButton);
    }

    private void ExecuteOption(EventOptionDefinition option, CardRuntime selectedCard)
    {
        var gm = GameManager.Instance;
        string executionError = "";
        if (option == null || !gm.TryCommitEventOption(option.Id, selectedCard,
                out var transaction, out executionError))
        {
            GD.PrintErr($"[事件] 效果执行失败：{executionError}");
            _resultLabel.Text = "事件效果执行失败，状态已保持不变。";
            return;
        }
        if (transaction.RequiresCardChoice)
        {
            ShowCardRemovalChoices(option);
            return;
        }
        if (!transaction.RequestsExit)
        {
            GD.PrintErr("[事件] 选项执行完成但未声明 Exit 命令，拒绝伪造离场。");
            _resultLabel.Text = "事件定义缺少离场命令，未结算。";
            return;
        }

        _settled = true;
        GD.Print($"[事件] {transaction.Summary}");
        _resultLabel.Text = transaction.Summary;
        RenderOptions();
    }

    private void AddOption(EventOptionDefinition option, bool enabled, string reason)
    {
        var button = CreateOption(option.Title, option.Detail, enabled, reason);
        button.Pressed += () => OnOptionPressed(option);
        _optionContainer.AddChild(button);
    }

    private Button CreateOption(string title, string detail, bool enabled, string reason)
    {
        var button = new Button
        {
            Text = $"{title}\n{detail}",
            CustomMinimumSize = new Vector2(560, 72),
            Disabled = !enabled,
            TooltipText = enabled ? "" : reason,
        };
        button.AddThemeFontSizeOverride("font_size", 17);
        if (!enabled)
            button.Modulate = new Color(0.45f, 0.45f, 0.45f, 1f);
        return button;
    }

    private void ClearOptions()
    {
        foreach (Node child in _optionContainer.GetChildren())
            child.QueueFree();
        _resultLabel.Text = "";
    }

    private void ShowInvalidState()
    {
        _titleLabel.Text = "事件节点不可用";
        _descriptionLabel.Text = "当前入口无有效事件定义，不能伪造事件结果。";
        ClearOptions();
        _resultLabel.Text = "请返回地图重新选择可达节点。";
        var recoverButton = CreateOption("返回地图", "不结算当前无效入口", true, "");
        recoverButton.Pressed += RecoverInvalidEntry;
        _optionContainer.AddChild(recoverButton);
    }

    private void RecoverInvalidEntry()
    {
        if (!_invalidEntry) return;
        if (!GameManager.Instance.RecoverFromInvalidNodeEntry(out var error))
        {
            GD.PrintErr($"[事件] 无效入口恢复失败：{error}");
            return;
        }
        ToggleMapOverlay();
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
            GD.PrintErr($"[事件] 目标节点事务迁移失败：{transitionError}");
            _resultLabel.Text = "进入下一节点失败，当前结果页已保留。";
            return;
        }
        _mapOverlay?.CloseImmediately();
        _mapOverlay = null;
    }
}
