using Godot;
using System.Collections.Generic;

/// <summary>
/// 场景C：战斗界面。对标尖塔2布局。
/// 左侧玩家立绘区 + 右侧敌人立绘区 + 底部手牌区（点击选中+目标箭头）+ 顶部 TopBar。
/// </summary>
public partial class BattleController : Control
{
    [Export] public PackedScene SettingsDialogScene { get; set; }

    // TopBar is the permanent global entry. Content modals begin below this fixed bar.
    private const float TopBarHeight = 44f;

    private TopBar _topBar;

    // 布局常量（1920×1080 基准）
    private const float PortraitWidth = 280f;
    private const float PortraitHeight = 420f;
    private const float BarWidth = 240f;
    private const float BarHeight = 22f;

    // ---- 玩家区域（左侧）----
    private ColorRect _playerPortrait;
    private ProgressBar _playerHpBar;
    private Label _playerHpLabel;
    private ProgressBar _playerHutiBar;
    private Label _playerHutiLabel;
    private Label _playerBuffsLabel;
    private Label _playerDoujinLabel;
    private Label _playerLingliLabel;
    private static readonly Color PlayerPortraitNormalColor = new(0.18f, 0.08f, 0.08f, 0.8f);
    private static readonly Color PlayerPortraitTargetColor = new(0.32f, 0.12f, 0.08f, 0.95f);

    // ---- 敌人区域（右侧）----
    private ColorRect _enemyPortrait;
    private ProgressBar _enemyHpBar;
    private Label _enemyHpLabel;
    private Label _enemyIntentLabel;
    private Label _enemyBuffsLabel;
    private static readonly Color EnemyPortraitNormalColor = new(0.12f, 0.08f, 0.06f, 0.8f);
    private static readonly Color EnemyPortraitTargetColor = new(0.25f, 0.12f, 0.08f, 0.95f);

    // ---- 手牌区域（底部）----
    private Control _handArea;
    private Button _endTurnButton;
    private Label _turnInfoLabel;
    private List<CardButton> _cardButtons = new();
    private List<HandCardLayout> _handLayouts = new();
    [Export] private HandLayoutProfile _handLayoutProfile = new();
    [Export] private bool _enableHandLayoutDebugHotkey = true;
    private HandLayoutDebugOverlay _handLayoutDebugOverlay;

    // ---- 选中卡牌状态 ----
    private CardButton _selectedCardBtn;
    private ResolvedCardExecution _selectedCardExecution;
    private bool _arrowVisible;
    private Vector2 _arrowTarget;
    private TargetArrowOverlay _targetArrowOverlay;

    // ---- 日志 ----
    private Panel _logPanel;
    private RichTextLabel _logLabel;
    private Button _logToggleButton;
    private bool _logExpanded;

    // ---- 战斗结算弹窗 ----
    private Panel _victoryPopup;
    private Panel _victoryModalLayer;
    private bool _popupShowing;
    private RewardContext _rewardContext;
    private readonly List<RewardPlan> _rewardPlans = new();
    private string _lingYunRewardId = "";
    private bool _rewardSettlementBlocked;
    private string _rewardSettlementError = "";
    private NodePageNavigationCoordinator _nodeNavigation;

    // ---- 玩家区域参数（供箭头使用）----
    private Vector2 PlayerPortraitCenter => new(220f, 370f);
    private Vector2 EnemyPortraitCenter => new(1780f, 370f);

    public override void _Ready()
    {
        _handLayoutProfile ??= new HandLayoutProfile();
#if DEBUG
        ValidateHandLayoutProfile(_handLayoutProfile);
#endif

        var gm = GameManager.Instance;
        if (!gm.TryBeginActiveBattle(out var battleError))
        {
            GD.PrintErr($"[战斗] 场景初始化被阻止：{battleError}");
            return;
        }

        BuildBackground();
        _nodeNavigation = new NodePageNavigationCoordinator(this, gm,
            error => AppendLog($"[color=red]地图导航失败：{error}[/color]"),
            ClearBattleSceneOverlays);
        BuildTopBar();
        BuildPlayerArea();
        BuildEnemyArea();
        BuildHandArea();
        BuildTargetArrowOverlay();
        BuildHandLayoutDebugOverlay();
        BuildLogPanel();
#if DEBUG
        BuildDebugTools();
#endif

        gm.StartPlayerTurn();
        UpdateAllUI();

    }

#if DEBUG
    private void BuildDebugTools()
    {
        if (!OS.IsDebugBuild())
            return;

        var scene = GD.Load<PackedScene>("res://scenes/Debug/BattleDebugTools.tscn");
        if (scene == null)
        {
            GD.PrintErr("[BattleDebug] 无法加载独立 Debug 工具场景。");
            return;
        }

        var tools = scene.Instantiate<BattleDebugToolsController>();
        tools.OnInstantVictory = TryDebugInstantVictory;
        AddChild(tools);
    }

    /// <summary>
    /// Debug 专用一键胜利入口。只把当前真实战斗的敌人生命置零，随后复用正式胜利检测、
    /// NodeResult、奖励暂存和回地图链路，不重置 RunState 或永久套牌。
    /// </summary>
    private void TryDebugInstantVictory()
    {
        if (!OS.IsDebugBuild())
            return;

        var gm = GameManager.Instance;
        if (gm == null || gm.ActiveBattle == null || gm.ActiveNode == null ||
            (gm.ActiveNode.NodeType != MapGraphNodeType.Battle && gm.ActiveNode.NodeType != MapGraphNodeType.Boss) ||
            gm.CurrentState != PlayerState.战斗中 || gm.BattleOver)
        {
            GD.PrintErr("[BattleDebug] 一键胜利拒绝：当前不是可操作的活动战斗节点。");
            return;
        }

        gm.EnemyHp = 0;
        CheckBattleResult();
    }
#endif

    public override void _Process(double delta)
    {
        if (IsVictoryModalActive)
        {
            if (_arrowVisible)
                HideTargetArrow();
            return;
        }

        if (_selectedCardBtn != null && _selectedCardBtn.Card.Info is CardInfo info && info.TargetMode == CardTargetMode.Enemy)
        {
            ShowTargetArrow(GetGlobalMousePosition());
        }
        else if (_selectedCardBtn != null && _selectedCardBtn.Card.Info is CardInfo heldInfo && IsAutoReleaseCard(heldInfo))
        {
            _selectedCardBtn.FollowMouse();
            if (_arrowVisible)
                HideTargetArrow();
        }
        else
        {
            if (_arrowVisible)
            {
                HideTargetArrow();
            }
        }

        UpdateHandLayoutDebugOverlay();
    }

    /// <summary>
    /// 在 GUI 子控件之前统一接收右键取消，避免 CardButton 消费事件后 Battle 根节点无法清理选中/拖拽状态。
    /// </summary>
    public override void _Input(InputEvent @event)
    {
        if (IsVictoryModalActive)
            return;

        if (@event is not InputEventMouseButton mouseButton ||
            mouseButton.ButtonIndex != MouseButton.Right ||
            !mouseButton.Pressed)
            return;

        bool hadInteraction = _selectedCardBtn != null || _arrowVisible;
        foreach (var cardButton in _cardButtons)
            hadInteraction |= cardButton.IsInteracting;

        CancelCardInteraction();
        if (hadInteraction)
        {
            AppendLog("[取消] 已取消当前卡牌交互");
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (IsVictoryModalActive)
            return;

        if (!_enableHandLayoutDebugHotkey) return;

        if (@event is InputEventKey keyEvent &&
            keyEvent.Pressed &&
            !keyEvent.Echo &&
            keyEvent.Keycode == Key.F9)
        {
            _handLayoutProfile.DebugOverlayEnabled = !_handLayoutProfile.DebugOverlayEnabled;
            AppendLog(_handLayoutProfile.DebugOverlayEnabled
                ? "[Debug] 手牌布局点位显示已开启"
                : "[Debug] 手牌布局点位显示已关闭");
            UpdateHandLayoutDebugOverlay();
        }
    }

    public override void _Draw()
    {
    }

    /// <summary>战斗页面只请求共享导航协调器；可达性和离场由核心端口决定。</summary>
    private void ShowMapOverlay()
    {
        if (_nodeNavigation == null)
        {
            AppendLog("[color=red]地图导航服务未初始化。[/color]");
            return;
        }
        if (!_nodeNavigation.TryToggleMap(out var error))
            AppendLog($"[color=red]地图 overlay 创建失败：{error}[/color]");
    }

    /// <summary>绘制从 start 到 end 的弧形箭头</summary>
    private void DrawCurvedArrow(Vector2 start, Vector2 end)
    {
        Vector2 dir = end - start;
        float dist = dir.Length();

        // 控制点：取中点向上偏移，制造弧线
        Vector2 mid = (start + end) / 2;
        Vector2 perp = dir.Orthogonal().Normalized();
        float arcHeight = Mathf.Min(dist * 0.35f, 180f);
        Vector2 control = mid - perp * arcHeight;

        // 沿二次贝塞尔曲线采样画线
        int segments = 30;
        var points = new Vector2[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            points[i] = QuadraticBezier(start, control, end, t);
        }

        // 主线（半透明白色）
        DrawPolyline(points, new Color(1f, 0.85f, 0.55f, 0.7f), 3f);

        // 箭头（在末端画三角形）
        Vector2 tip = points[segments];
        Vector2 dirNorm = (points[segments] - points[segments - 1]).Normalized();
        Vector2 arrowLeft = tip - dirNorm * 14f + dirNorm.Orthogonal() * 8f;
        Vector2 arrowRight = tip - dirNorm * 14f - dirNorm.Orthogonal() * 8f;
        DrawPolygon(new[] { tip, arrowLeft, arrowRight }, new[] { new Color(1f, 0.85f, 0.55f, 0.9f) });
    }

    private static Vector2 QuadraticBezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    private void BuildTargetArrowOverlay()
    {
        _targetArrowOverlay = new TargetArrowOverlay();
        _targetArrowOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
        _targetArrowOverlay.ZIndex = 45;
        _targetArrowOverlay.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_targetArrowOverlay);
    }

    private void BuildHandLayoutDebugOverlay()
    {
        _handLayoutDebugOverlay = new HandLayoutDebugOverlay();
        _handLayoutDebugOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
        _handLayoutDebugOverlay.ZIndex = 70;
        _handLayoutDebugOverlay.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_handLayoutDebugOverlay);
    }

    private void ShowTargetArrow(Vector2 targetGlobalPosition)
    {
        if (_selectedCardBtn == null) return;

        _arrowTarget = targetGlobalPosition;
        _arrowVisible = true;
        _targetArrowOverlay.SetArrow(_selectedCardBtn.GetArrowOriginGlobal(), _arrowTarget, _handLayoutProfile);
        UpdateHandLayoutDebugOverlay();
    }

    private void HideTargetArrow()
    {
        _arrowVisible = false;
        _targetArrowOverlay?.HideArrow();
        UpdateHandLayoutDebugOverlay();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (_popupShowing) return;

        // 右键 / 点空白 → 取消选中
        if (@event is InputEventMouseButton mb &&
            mb.ButtonIndex == MouseButton.Right && mb.Pressed)
        {
            CancelCardInteraction();
            return;
        }

        if (@event is InputEventMouseButton mb2 &&
            mb2.ButtonIndex == MouseButton.Left && mb2.Pressed)
        {
            Vector2 pos = mb2.GlobalPosition;

            // 选中卡牌 + 点敌人区域 → 打出目标型卡牌
            if (_selectedCardBtn != null && _selectedCardBtn.Card.Info is CardInfo info && info.TargetMode == CardTargetMode.Enemy)
            {
                Rect2 enemyZone = new(EnemyPortraitCenter - new Vector2(140, 210), new Vector2(280, 420));
                if (enemyZone.HasPoint(pos))
                {
                    PlaySelectedCard();
                    return;
                }
            }

            if (_selectedCardBtn != null && _selectedCardBtn.Card.Info is CardInfo info2 && IsAutoReleaseCard(info2))
            {
                if (ShouldAutoPlayOnRelease(info2, pos))
                {
                    PlaySelectedCard();
                    return;
                }

                DeselectCard();
                return;
            }

            // 点击非目标区域 → 取消选中
            DeselectCard();
        }
    }

    // ==================== 背景 ====================

    private void BuildBackground()
    {
        var bg = new ColorRect();
        bg.Color = new Color(0.10f, 0.08f, 0.06f, 1);
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(bg);
    }

    // ==================== 顶部栏 ====================

    private void BuildTopBar()
    {
        _topBar = new TopBar();
        _topBar.SetPosition(new Vector2(0, 0));
        _topBar.Size = new Vector2(1920, 44);
        _topBar.OnDeckPressed = () => DeckViewer.Show(this);
        _topBar.OnMapPressed = () => ShowMapOverlay();
        _topBar.OnSettingsPressed = () => SettingsHelper.Show(this, SettingsDialogScene);
        AddChild(_topBar);
    }

    // ==================== 玩家区域（左侧立绘） ====================

    private void BuildPlayerArea()
    {
        float leftX = 80f;
        float topY = 160f;

        _playerPortrait = new ColorRect();
        _playerPortrait.Color = PlayerPortraitNormalColor;
        _playerPortrait.SetPosition(new Vector2(leftX, topY));
        _playerPortrait.Size = new Vector2(PortraitWidth, PortraitHeight);
        _playerPortrait.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_playerPortrait);

        var portraitLabel = new Label();
        var character = GameManager.Instance.SelectedCharacter;
        if (character == null || string.IsNullOrWhiteSpace(character.Name))
        {
            GD.PrintErr("[战斗] 玩家立绘绑定失败：RunState 缺少角色定义。");
            portraitLabel.Text = "角色数据缺失";
        }
        else
        {
            portraitLabel.Text = character.Name;
        }
        portraitLabel.AddThemeFontSizeOverride("font_size", 24);
        portraitLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.7f, 0.3f));
        portraitLabel.HorizontalAlignment = HorizontalAlignment.Center;
        portraitLabel.VerticalAlignment = VerticalAlignment.Center;
        portraitLabel.SetPosition(new Vector2(0, 0));
        portraitLabel.Size = new Vector2(PortraitWidth, PortraitHeight);
        portraitLabel.MouseFilter = MouseFilterEnum.Ignore;
        _playerPortrait.AddChild(portraitLabel);

        float barY = topY + PortraitHeight + 16f;
        _playerHpBar = new ProgressBar();
        _playerHpBar.SetPosition(new Vector2(leftX + 20, barY));
        _playerHpBar.Size = new Vector2(BarWidth, BarHeight);
        _playerHpBar.AddThemeColorOverride("font_color", new Color(0.9f, 0.2f, 0.2f));
        AddChild(_playerHpBar);

        _playerHpLabel = new Label();
        _playerHpLabel.SetPosition(new Vector2(leftX + 22, barY + 1));
        _playerHpLabel.Size = new Vector2(BarWidth - 4, BarHeight - 2);
        _playerHpLabel.AddThemeFontSizeOverride("font_size", 13);
        _playerHpLabel.AddThemeColorOverride("font_color", Colors.White);
        _playerHpLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _playerHpLabel.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_playerHpLabel);

        float hutiY = barY + BarHeight + 6f;
        _playerHutiBar = new ProgressBar();
        _playerHutiBar.SetPosition(new Vector2(leftX + 20, hutiY));
        _playerHutiBar.Size = new Vector2(BarWidth, BarHeight);
        _playerHutiBar.AddThemeColorOverride("font_color", new Color(0.3f, 0.6f, 0.9f));
        AddChild(_playerHutiBar);

        _playerHutiLabel = new Label();
        _playerHutiLabel.SetPosition(new Vector2(leftX + 22, hutiY + 1));
        _playerHutiLabel.Size = new Vector2(BarWidth - 4, BarHeight - 2);
        _playerHutiLabel.AddThemeFontSizeOverride("font_size", 13);
        _playerHutiLabel.AddThemeColorOverride("font_color", Colors.White);
        _playerHutiLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _playerHutiLabel.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_playerHutiLabel);

        float statY = hutiY + BarHeight + 8f;
        _playerLingliLabel = MakeStatLabel(leftX + 20, statY, "灵力", new Color(0.9f, 0.8f, 0.2f));
        AddChild(_playerLingliLabel);

        _playerDoujinLabel = MakeStatLabel(leftX + 140, statY, "斗劲", new Color(0.9f, 0.5f, 0.1f));
        AddChild(_playerDoujinLabel);

        float buffY = statY + 28f;
        _playerBuffsLabel = new Label();
        _playerBuffsLabel.SetPosition(new Vector2(leftX + 15, buffY));
        _playerBuffsLabel.Size = new Vector2(PortraitWidth - 10, 60);
        _playerBuffsLabel.AddThemeFontSizeOverride("font_size", 12);
        _playerBuffsLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        AddChild(_playerBuffsLabel);
    }

    // ==================== 敌人区域（右侧立绘） ====================

    private void BuildEnemyArea()
    {
        float rightX = 1920f - 80f - PortraitWidth;
        float topY = 160f;

        _enemyPortrait = new ColorRect();
        _enemyPortrait.Color = EnemyPortraitNormalColor;
        _enemyPortrait.SetPosition(new Vector2(rightX, topY));
        _enemyPortrait.Size = new Vector2(PortraitWidth, PortraitHeight);
        _enemyPortrait.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_enemyPortrait);

        var portraitLabel = new Label();
        var enemy = GameManager.Instance.ActiveEncounter?.EnemyInfo;
        if (enemy == null || string.IsNullOrWhiteSpace(enemy.Name))
        {
            GD.PrintErr("[战斗] 敌人立绘绑定失败：ActiveEncounter 缺少显示名称。");
            portraitLabel.Text = "敌方数据缺失";
        }
        else
        {
            portraitLabel.Text = enemy.Name;
        }
        portraitLabel.AddThemeFontSizeOverride("font_size", 22);
        portraitLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.35f, 0.3f));
        portraitLabel.HorizontalAlignment = HorizontalAlignment.Center;
        portraitLabel.VerticalAlignment = VerticalAlignment.Center;
        portraitLabel.SetPosition(new Vector2(0, 0));
        portraitLabel.Size = new Vector2(PortraitWidth, PortraitHeight);
        portraitLabel.MouseFilter = MouseFilterEnum.Ignore;
        _enemyPortrait.AddChild(portraitLabel);

        _enemyIntentLabel = new Label();
        _enemyIntentLabel.SetPosition(new Vector2(rightX + 20, topY - 32));
        _enemyIntentLabel.Size = new Vector2(BarWidth, 26);
        _enemyIntentLabel.AddThemeFontSizeOverride("font_size", 16);
        _enemyIntentLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.3f, 0.3f));
        _enemyIntentLabel.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(_enemyIntentLabel);

        float barY = topY + PortraitHeight + 16f;
        _enemyHpBar = new ProgressBar();
        _enemyHpBar.SetPosition(new Vector2(rightX + 20, barY));
        _enemyHpBar.Size = new Vector2(BarWidth, BarHeight);
        _enemyHpBar.AddThemeColorOverride("font_color", new Color(0.9f, 0.2f, 0.2f));
        AddChild(_enemyHpBar);

        _enemyHpLabel = new Label();
        _enemyHpLabel.SetPosition(new Vector2(rightX + 22, barY + 1));
        _enemyHpLabel.Size = new Vector2(BarWidth - 4, BarHeight - 2);
        _enemyHpLabel.AddThemeFontSizeOverride("font_size", 13);
        _enemyHpLabel.AddThemeColorOverride("font_color", Colors.White);
        _enemyHpLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _enemyHpLabel.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_enemyHpLabel);

        float buffY = barY + BarHeight + 8f;
        _enemyBuffsLabel = new Label();
        _enemyBuffsLabel.SetPosition(new Vector2(rightX + 10, buffY));
        _enemyBuffsLabel.Size = new Vector2(PortraitWidth - 10, 60);
        _enemyBuffsLabel.AddThemeFontSizeOverride("font_size", 12);
        _enemyBuffsLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        AddChild(_enemyBuffsLabel);
    }

    // ==================== 手牌区域（底部） ====================

    private void BuildHandArea()
    {
        _handArea = new Control();
        _handArea.SetPosition(new Vector2(60, 780));
        _handArea.Size = new Vector2(1800, 280);
        AddChild(_handArea);

        var handBg = new ColorRect();
        handBg.Color = new Color(0.05f, 0.04f, 0.03f, 0.6f);
        handBg.SetPosition(ToHandAreaLocal(_handLayoutProfile.HandAreaRect.Position));
        handBg.Size = _handLayoutProfile.HandAreaRect.Size;
        handBg.MouseFilter = MouseFilterEnum.Ignore;
        _handArea.AddChild(handBg);

        _endTurnButton = new Button();
        _endTurnButton.Text = "结束回合";
        _endTurnButton.AddThemeFontSizeOverride("font_size", 22);
        _endTurnButton.SetPosition(new Vector2(1560, 100));
        _endTurnButton.Size = new Vector2(180, 70);
        _endTurnButton.Pressed += OnEndTurnPressed;
        _handArea.AddChild(_endTurnButton);

        _turnInfoLabel = new Label();
        _turnInfoLabel.SetPosition(new Vector2(1580, 180));
        _turnInfoLabel.Size = new Vector2(140, 24);
        _turnInfoLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _turnInfoLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        _handArea.AddChild(_turnInfoLabel);
    }

    // ==================== 日志面板 ====================

    private void BuildLogPanel()
    {
        _logToggleButton = new Button();
        _logToggleButton.Text = "日志";
        _logToggleButton.SetPosition(new Vector2(1510, 52));
        _logToggleButton.Size = new Vector2(76, 30);
        _logToggleButton.AddThemeFontSizeOverride("font_size", 13);
        _logToggleButton.Pressed += ToggleLogPanel;
        AddChild(_logToggleButton);

        _logPanel = new Panel();
        _logPanel.SetPosition(new Vector2(420, 88));
        _logPanel.Size = new Vector2(1080, 40);
        _logPanel.Modulate = new Color(1, 1, 1, 0.42f);
        _logPanel.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_logPanel);

        _logLabel = new RichTextLabel();
        _logLabel.BbcodeEnabled = true;
        _logLabel.ScrollFollowing = true;
        _logLabel.SetPosition(new Vector2(6, 4));
        _logLabel.Size = new Vector2(1068, 32);
        _logLabel.FitContent = false;
        _logLabel.MouseFilter = MouseFilterEnum.Ignore;
        _logPanel.AddChild(_logLabel);
    }

    private void ToggleLogPanel()
    {
        _logExpanded = !_logExpanded;
        if (_logExpanded)
        {
            _logPanel.SetPosition(new Vector2(420, 88));
            _logPanel.Size = new Vector2(1080, 260);
            _logPanel.Modulate = new Color(1, 1, 1, 0.78f);
            _logPanel.MouseFilter = MouseFilterEnum.Stop;
            _logLabel.Size = new Vector2(1068, 252);
            _logLabel.MouseFilter = MouseFilterEnum.Stop;
            _logToggleButton.Text = "收起日志";
        }
        else
        {
            _logPanel.SetPosition(new Vector2(420, 88));
            _logPanel.Size = new Vector2(1080, 40);
            _logPanel.Modulate = new Color(1, 1, 1, 0.42f);
            _logPanel.MouseFilter = MouseFilterEnum.Ignore;
            _logLabel.Size = new Vector2(1068, 32);
            _logLabel.MouseFilter = MouseFilterEnum.Ignore;
            _logToggleButton.Text = "日志";
        }
    }

    // ==================== UI 刷新 ====================

    private void UpdateAllUI()
    {
        var gm = GameManager.Instance;
        var enemy = gm.ActiveEncounter?.EnemyInfo;
        if (enemy == null)
        {
            GD.PrintErr("[战斗] UI 刷新失败：ActiveEncounter 缺少敌人定义。");
            return;
        }

        _playerHpBar.MaxValue = gm.PlayerMaxHp;
        _playerHpBar.Value = gm.PlayerHp;
        _playerHpLabel.Text = $"生命 {gm.PlayerHp}/{gm.PlayerMaxHp}";

        _playerHutiBar.MaxValue = Mathf.Max(gm.PlayerHuti, 1);
        _playerHutiBar.Value = gm.PlayerHuti;
        _playerHutiLabel.Text = $"护体 {gm.PlayerHuti}";

        _playerLingliLabel.Text = $"灵力 {gm.PlayerLingli}/{gm.PlayerMaxLingli}";
        _playerDoujinLabel.Text = $"斗劲 {gm.PlayerDoujin}";

        string pBuffs = "状态：";
        if (gm.PlayerYongyan > 0) pBuffs += $"永炎×{gm.PlayerYongyan} ";
        if (gm.PlayerYirongCeng > 0) pBuffs += $"易损×{gm.PlayerYirongCeng} ";
        if (gm.PlayerYongyan == 0 && gm.PlayerYirongCeng == 0) pBuffs += "无";
        _playerBuffsLabel.Text = pBuffs;

        _enemyHpBar.MaxValue = gm.EnemyMaxHp;
        _enemyHpBar.Value = gm.EnemyHp;
        _enemyHpLabel.Text = $"HP {gm.EnemyHp}/{gm.EnemyMaxHp}";

        if (!gm.TryPrepareEnemyIntent(out var intent, out var intentError))
        {
            GD.PrintErr($"[战斗] 意图 UI 解析失败：{intentError}");
            _enemyIntentLabel.Text = "意图数据错误，已阻止敌方行动";
        }
        else
            _enemyIntentLabel.Text = FormatIntentSummary(intent);

        string eBuffs = "";
        if (gm.EnemyYongyan > 0) eBuffs += $"永炎×{gm.EnemyYongyan} ";
        if (gm.EnemyYirong > 0) eBuffs += $"易损×{gm.EnemyYirong} ";
        var mechanic = enemy.MechanicDefinition;
        if (gm.ActiveBattle?.EnemyMechanicActive == true && mechanic != null &&
            mechanic.CounterThreshold > 0 && !string.IsNullOrWhiteSpace(mechanic.CounterKey))
        {
            if (string.IsNullOrWhiteSpace(mechanic.DisplayName))
            {
                GD.PrintErr($"[战斗] 敌人机制缺少显示名称：{mechanic.Id}");
            }
            else
            {
                int counter = gm.ActiveBattle.EnemyMechanicCounters.GetValueOrDefault(mechanic.CounterKey);
                eBuffs += $"{mechanic.DisplayName}{counter}/{mechanic.CounterThreshold} ";
            }
        }
        if (gm.EnemyHuti > 0) eBuffs += $"护体{gm.EnemyHuti} ";
        if (string.IsNullOrEmpty(eBuffs)) eBuffs = "状态：无";
        else eBuffs = "状态：" + eBuffs;
        _enemyBuffsLabel.Text = eBuffs;

        _turnInfoLabel.Text = gm.IsPlayerTurn ? "你的回合" : "敌人回合...";
        _endTurnButton.Disabled = !gm.IsPlayerTurn;

        DeselectCard();
        RefreshHandUI();
    }

    /// <summary>从意图数据生成可读摘要，避免控制器按敌人或意图名称写分支。</summary>
    private static string FormatIntentSummary(ResolvedEnemyIntent intent)
    {
        return IntentPresentation.Format(intent);
    }

    private void RefreshHandUI()
    {
        foreach (var btn in _cardButtons)
        {
            btn.Visible = false;
            btn.QueueFree();
        }
        _cardButtons.Clear();
        _handLayouts.Clear();

        var hand = GameManager.Instance.Hand;
        var layouts = HandLayoutCalculator.CalculateRestLayouts(hand.Count, _handLayoutProfile);
        _handLayouts.AddRange(layouts);

        for (int i = 0; i < hand.Count; i++)
        {
            var btn = new CardButton(hand[i], _handLayoutProfile);
            btn.ApplyLayout(layouts[i], _handArea.GlobalPosition);
            btn.OnCardSelected += OnCardSelected;
            btn.OnCardDragStarted += OnCardDragStarted;
            btn.OnCardDragEnded += OnCardDragEnded;
            btn.OnCardCanceled += OnCardCanceled;
            _handArea.AddChild(btn);
            _cardButtons.Add(btn);
        }

        UpdateHandLayoutDebugOverlay();
        CheckBattleResult();
    }

    // ==================== 卡牌选中交互 ====================

    private void OnCardSelected(CardButton cardBtn)
    {
        if (_popupShowing) return;
        var gm = GameManager.Instance;

        if (!gm.IsPlayerTurn || gm.BattleOver) return;

        // 费用不足 → 不响应
        if (cardBtn.Card.Info.Cost > gm.PlayerLingli) return;

        // 点击已选中的卡 → 取消
        if (_selectedCardBtn == cardBtn)
        {
            if (IsAutoReleaseCard(cardBtn.Card.Info) &&
                ShouldAutoPlayOnRelease(cardBtn.Card.Info, cardBtn.GetGlobalMousePosition()))
            {
                PlaySelectedCard();
                return;
            }

            DeselectCard();
            return;
        }

        // 取消旧选中
        if (_selectedCardBtn != null)
            _selectedCardBtn.SetSelected(false);

        // 选中新卡
        _selectedCardBtn = cardBtn;
        cardBtn.SetSelected(true);
        if (!gm.TryPrepareCardExecution(cardBtn.Card, out _selectedCardExecution, out var prepareError))
        {
            AppendLog($"[取消] {prepareError}");
            _selectedCardBtn = null;
            cardBtn.ReturnToRest();
            return;
        }
        UpdateTargetHint(cardBtn.Card.Info);

        if (cardBtn.Card.Info.TargetMode == CardTargetMode.Self)
        {
            AppendLog($"[点选] {cardBtn.Card.Info.Name} → 移出手牌区点击或释放确认");
        }
        else if (cardBtn.Card.Info.TargetMode == CardTargetMode.None)
        {
            AppendLog($"[点选] {cardBtn.Card.Info.Name} → 移出手牌区点击或释放确认");
        }
    }

    private void OnCardDragStarted(CardButton cardBtn)
    {
        if (_popupShowing) return;
        var gm = GameManager.Instance;

        if (!gm.IsPlayerTurn || gm.BattleOver || cardBtn.Card.Info.Cost > gm.PlayerLingli)
            return;

        if (_selectedCardBtn != null && _selectedCardBtn != cardBtn)
            _selectedCardBtn.SetSelected(false);

        _selectedCardBtn = cardBtn;
        cardBtn.SetSelected(true);
        if (!gm.TryPrepareCardExecution(cardBtn.Card, out _selectedCardExecution, out var prepareError))
        {
            AppendLog($"[取消] {prepareError}");
            _selectedCardBtn = null;
            cardBtn.ReturnToRest();
            return;
        }
        UpdateTargetHint(cardBtn.Card.Info);
    }

    private void OnCardDragEnded(CardButton cardBtn, Vector2 globalPosition)
    {
        if (_popupShowing || _selectedCardBtn != cardBtn)
        {
            cardBtn.ReturnToRest();
            return;
        }

        if (ShouldAutoPlayOnRelease(cardBtn.Card.Info, globalPosition))
        {
            PlaySelectedCard();
            return;
        }

        if (IsLegalCardTarget(cardBtn.Card.Info, globalPosition))
        {
            PlaySelectedCard();
            return;
        }

        AppendLog($"[取消] {cardBtn.Card.Info.Name} 未放到合法目标");
        DeselectCard();
    }

    private void OnCardCanceled(CardButton cardBtn)
    {
        if (_selectedCardBtn == cardBtn)
        {
            AppendLog($"[取消] {cardBtn.Card.Info.Name}");
            DeselectCard();
        }
    }

    private void PlaySelectedCard()
    {
        if (_selectedCardBtn == null) return;
        var card = _selectedCardBtn.Card;
        var gm = GameManager.Instance;

        if (_selectedCardExecution == null)
        {
            AppendLog("[取消] 卡牌确认状态已失效，请重新选择");
            DeselectCard();
            return;
        }
        if (gm.PlayCard(card, _selectedCardExecution))
        {
            AppendLog($"打出 [{card.Info.Name}]：{gm.LastResolvedCardExecution?.Summary ?? card.Info.ExecutionSummary}");
            _selectedCardBtn.Visible = false;
            _selectedCardBtn = null;
            _selectedCardExecution = null;
            HideTargetArrow();
            ClearTargetHint();
            UpdateAllUI();
        }
    }

    private bool IsLegalCardTarget(CardInfo info, Vector2 globalPosition)
    {
        if (info.TargetMode == CardTargetMode.Enemy)
        {
            Rect2 enemyZone = new(EnemyPortraitCenter - new Vector2(140, 210), new Vector2(280, 420));
            return enemyZone.HasPoint(globalPosition);
        }

        return false;
    }

    private bool ShouldAutoPlayOnRelease(CardInfo info, Vector2 globalPosition)
    {
        if (globalPosition.Y >= _handLayoutProfile.HandExitThresholdY)
            return false;

        return IsAutoReleaseCard(info);
    }

    private static bool IsAutoReleaseCard(CardInfo info)
    {
        return info.TargetMode == CardTargetMode.Self || info.TargetMode == CardTargetMode.None;
    }

    private void UpdateTargetHint(CardInfo info)
    {
        if (info.TargetMode == CardTargetMode.Enemy)
        {
            _enemyPortrait.Color = EnemyPortraitTargetColor;
            _playerPortrait.Color = PlayerPortraitNormalColor;
        }
        else if (info.TargetMode == CardTargetMode.Self)
        {
            _playerPortrait.Color = PlayerPortraitTargetColor;
            _enemyPortrait.Color = EnemyPortraitNormalColor;
        }
        else
        {
            ClearTargetHint();
        }
    }

    private void ClearTargetHint()
    {
        _playerPortrait.Color = PlayerPortraitNormalColor;
        _enemyPortrait.Color = EnemyPortraitNormalColor;
    }

    private void DeselectCard()
    {
        if (_selectedCardBtn != null)
        {
            _selectedCardBtn.SetSelected(false);
            _selectedCardBtn = null;
        }
        _selectedCardExecution = null;
        HideTargetArrow();
        ClearTargetHint();
        QueueRedraw();
        UpdateHandLayoutDebugOverlay();
    }

    private void CancelCardInteraction()
    {
        foreach (var cardButton in _cardButtons)
            cardButton.ReturnToRest();

        DeselectCard();
    }

    private Vector2 ToHandAreaLocal(Vector2 globalPosition)
    {
        return globalPosition - _handArea.GlobalPosition;
    }

    private void UpdateHandLayoutDebugOverlay()
    {
        if (_handLayoutDebugOverlay == null || _handLayoutProfile == null) return;

        Vector2? hoverPoint = null;
        Vector2? heldPoint = null;
        foreach (var cardButton in _cardButtons)
        {
            if (cardButton.TryGetDebugHoverPoint(out Vector2 cardHoverPoint))
                hoverPoint = cardHoverPoint;
        }

        if (_selectedCardBtn != null)
            heldPoint = _selectedCardBtn.GetCenterGlobal();

        Vector2? arrowStart = null;
        Vector2? arrowControl = null;
        Vector2? arrowTarget = null;
        if (_arrowVisible && _selectedCardBtn != null)
        {
            arrowStart = _selectedCardBtn.GetArrowOriginGlobal();
            arrowTarget = _arrowTarget;
            arrowControl = HandLayoutCalculator.GetArrowControlPoint(arrowStart.Value, arrowTarget.Value, _handLayoutProfile);
        }

        _handLayoutDebugOverlay.SetSnapshot(
            _handLayoutProfile,
            _handLayouts,
            _handLayoutProfile.DebugOverlayEnabled,
            hoverPoint,
            heldPoint,
            arrowStart,
            arrowControl,
            arrowTarget);
    }

#if DEBUG
    private static void ValidateHandLayoutProfile(HandLayoutProfile profile)
    {
        foreach (int count in new[] { 1, 2, 3, 5, 7, 10 })
        {
            var layouts = HandLayoutCalculator.CalculateRestLayouts(count, profile);
            if (layouts.Count != count)
                throw new System.InvalidOperationException($"Hand layout self-check failed: count {count} produced {layouts.Count} layouts.");

            int maxRestZ = int.MinValue;
            for (int i = 0; i < layouts.Count; i++)
            {
                var center = layouts[i].Center;
                if (float.IsNaN(center.X) || float.IsNaN(center.Y))
                    throw new System.InvalidOperationException($"Hand layout self-check failed: count {count}, index {i} has NaN center.");

                if (i > 0 && layouts[i].ZIndex <= layouts[i - 1].ZIndex)
                    throw new System.InvalidOperationException($"Hand layout self-check failed: count {count} ZIndex is not increasing at index {i}.");

                maxRestZ = Mathf.Max(maxRestZ, layouts[i].ZIndex);
            }

            if (profile.HoverZIndex <= maxRestZ || profile.SelectedZIndex <= maxRestZ || profile.HeldZIndex <= maxRestZ)
                throw new System.InvalidOperationException($"Hand layout self-check failed: active ZIndex must be above rest ZIndex for count {count}.");
        }
    }
#endif

    // ==================== 回合流程 ====================

    private async void OnEndTurnPressed()
    {
        var gm = GameManager.Instance;
        if (!gm.IsPlayerTurn || gm.BattleOver || _popupShowing) return;

        DeselectCard();
        if (!gm.TryCommitEnemyTurn(out var executedIntent, out var enemyTurnError))
        {
            UpdateAllUI();
            AppendLog($"[color=red]回合提交失败，状态已回滚：{enemyTurnError}[/color]");
            return;
        }
        UpdateAllUI();
        AppendLog("--- 玩家回合结束 ---");
        AppendLog($"--- {gm.ActiveEncounter.EnemyInfo.Name} 行动 [{executedIntent.ResolutionId}] ---");
        AppendLog(IntentPresentation.Format(executedIntent));
        AppendLog($"实际结算：造成{gm.ActiveBattle.LastEnemyActualDamage}点伤害，获得{gm.EnemyHuti}点护体");
        await ToSignal(GetTree().CreateTimer(0.8f), SceneTreeTimer.SignalName.Timeout);

        UpdateAllUI();
        AppendLog("--- 敌人回合结束 ---");

        if (CheckBattleResult()) return;

        await ToSignal(GetTree().CreateTimer(0.5f), SceneTreeTimer.SignalName.Timeout);
        gm.StartPlayerTurn();
        UpdateAllUI();
        AppendLog($"--- 第{gm.EnemyTurnIndex + 1}回合开始 ---");
    }

    private bool CheckBattleResult()
    {
        var gm = GameManager.Instance;
        if (gm.BattleOver) return true;
        var settlement = new BattleVictorySettlementCommand(gm).ResolveTerminalBattleState();
        switch (settlement.Status)
        {
            case BattleVictorySettlementStatus.NoOutcome:
                return false;
            case BattleVictorySettlementStatus.DefeatRegistered:
                AppendLog("[color=red]你被击败了！道心破碎……[/color]");
                ShowResult(false);
                return true;
            case BattleVictorySettlementStatus.VictoryCommitted:
            case BattleVictorySettlementStatus.VictoryAlreadyCommitted:
                BindVictoryPlan(settlement.VictoryPlan);
                AppendLog($"[color=green]击败了 {gm.ActiveEncounter.EnemyInfo.Name}！[/color]");
                ShowResult(true);
                return true;
            case BattleVictorySettlementStatus.VictorySettlementFailed:
                _rewardSettlementBlocked = true;
                _rewardSettlementError = settlement.Error;
                GD.PrintErr($"[战斗] 胜利计划提交被阻止：{settlement.Error}");
                AppendLog($"[color=red]奖励定义无效，胜利结算已阻止：{settlement.Error}[/color]");
                ShowResult(true);
                return true;
            default:
                _rewardSettlementBlocked = true;
                _rewardSettlementError = settlement.Error;
                GD.PrintErr($"[战斗] 战斗结算命令被拒绝：{settlement.Error}");
                AppendLog($"[color=red]战斗终止失败：{settlement.Error}[/color]");
                return true;
        }
    }

    /// <summary>Copies an already-committed plan into presentation state; this method never builds or submits rewards.</summary>
    private void BindVictoryPlan(BattleVictoryPlan victoryPlan)
    {
        if (victoryPlan == null)
        {
            _rewardSettlementBlocked = true;
            _rewardSettlementError = "胜利结算命令未返回已提交奖励计划。";
            GD.PrintErr($"[战斗] {_rewardSettlementError}");
            return;
        }

        _rewardContext = victoryPlan.RewardContext;
        _rewardPlans.Clear();
        _rewardPlans.AddRange(victoryPlan.CardRewards);
        _lingYunRewardId = victoryPlan.LingYunRewardId;
    }

    // ==================== 战斗结算弹窗 ====================

    private void ShowResult(bool won)
    {
        _endTurnButton.Disabled = true;
        DeselectCard();
        var gm = GameManager.Instance;

        if (!won)
        {
            GetTree().CreateTimer(1.5f).Timeout += () =>
            {
                if (!gm.ExitBattleToTitleAfterDefeat(out var exitError))
                {
                    GD.PrintErr($"[战斗] 失败离场未完成：{exitError}");
                    return;
                }

                gm.GoToTitle();
            };
            return;
        }

        // 胜利 → 弹出结算弹窗（在当前战斗页面上）。奖励解析失败时仍显示明确错误状态。
        if (_rewardSettlementBlocked)
            ShowVictorySettlementError(_rewardSettlementError);
        else
            ShowVictoryPopup();
    }

    private void ShowVictorySettlementError(string error)
    {
        _popupShowing = true;
        if (!TryCreateVictoryModal(new Vector2(480, 240), new Vector2(960, 520)))
            return;
        _victoryPopup.Size = new Vector2(960, 520);
        var background = new ColorRect();
        background.Color = new Color(0.08f, 0.06f, 0.04f, 0.97f);
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        _victoryPopup.AddChild(background);

        var title = new Label();
        title.Text = "— 胜 利 · 奖励结算错误 —";
        title.AddThemeFontSizeOverride("font_size", 30);
        title.AddThemeColorOverride("font_color", new Color(0.95f, 0.35f, 0.25f));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.SetPosition(new Vector2(30, 50));
        title.Size = new Vector2(900, 50);
        _victoryPopup.AddChild(title);

        var detail = new Label();
        detail.Text = string.IsNullOrWhiteSpace(error)
            ? "奖励定义不可用，已停止奖励结算。"
            : $"奖励定义不可用，已停止奖励结算。\n{error}";
        detail.AddThemeFontSizeOverride("font_size", 20);
        detail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        detail.HorizontalAlignment = HorizontalAlignment.Center;
        detail.VerticalAlignment = VerticalAlignment.Center;
        detail.SetPosition(new Vector2(70, 180));
        detail.Size = new Vector2(820, 180);
        _victoryPopup.AddChild(detail);

    }

    private void ShowVictoryPopup()
    {
        var gm = GameManager.Instance;
        var victoryPlan = gm.ActiveBattleVictoryPlan;
        if (victoryPlan == null)
        {
            GD.PrintErr("[战斗] 胜利弹窗缺少已提交的胜利计划，阻止显示伪造奖励。");
            AppendLog("[color=red]胜利计划缺失，无法打开胜利结算。[/color]");
            return;
        }

        _popupShowing = true;
        _rewardContext = victoryPlan.RewardContext;
        _rewardPlans.Clear();
        _rewardPlans.AddRange(victoryPlan.CardRewards);
        _lingYunRewardId = victoryPlan.LingYunRewardId;

        if (!TryCreateVictoryModal(new Vector2(480, 160), new Vector2(960, 760)))
            return;

        // 弹窗背景
        var popupBg = new ColorRect();
        popupBg.Color = new Color(0.08f, 0.06f, 0.04f, 0.96f);
        popupBg.SetAnchorsPreset(LayoutPreset.FullRect);
        _victoryPopup.AddChild(popupBg);

        // 标题
        var title = new Label();
        title.Text = "— 胜 利 —";
        title.AddThemeFontSizeOverride("font_size", 36);
        title.AddThemeColorOverride("font_color", new Color(0.9f, 0.7f, 0.3f));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.SetPosition(new Vector2(0, 30));
        title.Size = new Vector2(960, 50);
        _victoryPopup.AddChild(title);

        var rewardList = VictoryRewardList.AddTo(_victoryPopup);

        // 奖励1：灵韵（点击领取，领完消失）
        int lingYunAmount = gm.UnclaimedLingYun;
        var lingYunBtn = new Button();
        lingYunBtn.Text = $"灵韵 +{lingYunAmount}（点击领取）";
        lingYunBtn.AddThemeFontSizeOverride("font_size", 18);
        lingYunBtn.CustomMinimumSize = new Vector2(0, 56);
        lingYunBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        lingYunBtn.Pressed += () =>
        {
            if (!gm.ClaimUnclaimedLingYun(_lingYunRewardId, out var claimError))
            {
                GD.PrintErr($"[战斗] 灵韵奖励领取失败：{claimError}");
                return;
            }
            lingYunBtn.QueueFree();
        };
        rewardList.AddChild(lingYunBtn);

        // 卡牌奖励入口只绑定 RewardPlan；来源、卡池和候选数量已在解析层校验。
        foreach (var plan in _rewardPlans)
            AddCardRewardRow(rewardList, plan);

    }

    private void AddCardRewardRow(VBoxContainer rewardList, RewardPlan plan)
    {
        var cardBtn = new Button();
        cardBtn.Text = $"{plan.DisplayName}（{plan.Candidates.Count}选{plan.ChoiceCount}）";
        cardBtn.AddThemeFontSizeOverride("font_size", 18);
        cardBtn.CustomMinimumSize = new Vector2(0, 56);
        cardBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        cardBtn.Pressed += () =>
        {
            cardBtn.Disabled = true;
            if (!CardRewardHelper.Show(this, plan, () =>
            {
                cardBtn.QueueFree();
            }, () =>
            {
                // 跳过只关闭 CardReward overlay；奖励行和奖励槽必须保留并可再次打开。
                cardBtn.Disabled = false;
            }))
            {
                cardBtn.Disabled = false;
            }
        };

        rewardList.AddChild(cardBtn);
    }

    /// <summary>
    /// 仅在地图内选择合法下一节点并且路由成功后，清理战斗场景的旧 overlay，
    /// 避免旧节点在目标页面继续拦截输入。
    /// </summary>
    private void ClearBattleSceneOverlays()
    {
        _nodeNavigation?.Dispose();

        CardRewardHelper.CloseAllForParent(this);
        if (_victoryModalLayer != null && GodotObject.IsInstanceValid(_victoryModalLayer))
        {
            OverlayCoordinator.Unregister(_victoryModalLayer);
            _victoryModalLayer.QueueFree();
        }

        _victoryModalLayer = null;
        _victoryPopup = null;

        _popupShowing = false;
    }

    /// <summary>
    /// Creates a content-area input barrier. TopBar stays outside this rectangle as a
    /// permanent global entry; the visible popup remains a child so CardReward can stack above it.
    /// </summary>
    private bool TryCreateVictoryModal(Vector2 popupPosition, Vector2 popupSize)
    {
        CancelCardInteraction();
        _victoryModalLayer = new Panel
        {
            Name = "VictoryModalLayer",
            Position = new Vector2(0, TopBarHeight),
            Size = new Vector2(1920, 1080 - TopBarHeight),
            ZIndex = OverlayCoordinator.VictoryZIndex,
            MouseFilter = MouseFilterEnum.Stop,
        };
        if (!OverlayCoordinator.TryRegisterVictory(_victoryModalLayer, out var coordinatorError))
        {
            GD.PrintErr($"[战斗] 无法创建胜利模态层：{coordinatorError}");
            _victoryModalLayer = null;
            _popupShowing = false;
            return false;
        }

        AddChild(_victoryModalLayer);
        var dim = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.72f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        _victoryModalLayer.AddChild(dim);

        _victoryPopup = new Panel
        {
            // Callers use screen-space anchors; convert to the content-area coordinate space.
            Position = popupPosition - new Vector2(0, TopBarHeight),
            Size = popupSize,
            MouseFilter = MouseFilterEnum.Stop,
        };
        _victoryModalLayer.AddChild(_victoryPopup);
        NodeMapEntry.Add(_victoryModalLayer, ShowMapOverlay);
        return true;
    }

    private bool IsVictoryModalActive => _victoryModalLayer != null &&
        GodotObject.IsInstanceValid(_victoryModalLayer);

    // ==================== 日志 ====================

    private void AppendLog(string msg)
    {
        _logLabel.Text += msg + "\n";
    }

    // ==================== 工具 ====================

    private static Label MakeStatLabel(float x, float y, string label, Color color)
    {
        var l = new Label();
        l.Text = $"{label} 0";
        l.SetPosition(new Vector2(x, y));
        l.Size = new Vector2(110, 24);
        l.AddThemeFontSizeOverride("font_size", 14);
        l.AddThemeColorOverride("font_color", color);
        return l;
    }
}

/// <summary>
/// 高层攻击箭头绘制层。独立于 BattleController 根节点绘制，避免箭头被手牌、背景或立绘层级盖住。
/// </summary>
public partial class TargetArrowOverlay : Control
{
    private bool _visible;
    private Vector2 _start;
    private Vector2 _end;
    private HandLayoutProfile _profile;

    public void SetArrow(Vector2 start, Vector2 end, HandLayoutProfile profile)
    {
        _visible = true;
        _start = start;
        _end = end;
        _profile = profile;
        QueueRedraw();
    }

    public void HideArrow()
    {
        if (!_visible) return;

        _visible = false;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_visible) return;

        DrawCurvedArrow(_start, _end);
    }

    private void DrawCurvedArrow(Vector2 start, Vector2 end)
    {
        Vector2 dir = end - start;
        float dist = dir.Length();
        if (dist < 8f) return;

        Vector2 mid = (start + end) / 2f;
        float arcHeight = _profile?.ArrowArcHeight ?? Mathf.Clamp(dist * 0.45f, 120f, 320f);
        Vector2 control = new(mid.X, Mathf.Min(start.Y, end.Y) - arcHeight);

        const int segments = 28;
        var points = new Vector2[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            points[i] = QuadraticBezier(start, control, end, t);
        }

        DrawPolyline(points, new Color(1f, 0.86f, 0.35f, 0.92f), 7f, true);
        DrawPolyline(points, new Color(0.35f, 0.08f, 0.02f, 0.65f), 3f, true);

        Vector2 tip = points[segments];
        Vector2 dirNorm = (points[segments] - points[segments - 1]).Normalized();
        Vector2 arrowLeft = tip - dirNorm * 28f + dirNorm.Orthogonal() * 16f;
        Vector2 arrowRight = tip - dirNorm * 28f - dirNorm.Orthogonal() * 16f;
        DrawPolygon(new[] { tip, arrowLeft, arrowRight }, new[] { new Color(1f, 0.86f, 0.35f, 0.95f) });
    }

    private static Vector2 QuadraticBezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }
}

/// <summary>
/// 手牌卡牌按钮。支持点击选中和长按拖拽，目标结算由 BattleController 完成。
/// </summary>
public partial class CardButton : Control
{
    public CardRuntime Card { get; private set; }
    public System.Action<CardButton> OnCardSelected;
    public System.Action<CardButton> OnCardDragStarted;
    public System.Action<CardButton, Vector2> OnCardDragEnded;
    public System.Action<CardButton> OnCardCanceled;
    public bool IsInteracting => _selected || _pressing || _dragging;

    private bool _selected;
    private bool _hovered;
    private bool _pressing;
    private bool _dragging;
    private ColorRect _bg;
    private Label _label;
    private HandLayoutProfile _layoutProfile;
    private HandCardLayout _restLayout;
    private Vector2 _handAreaGlobalOrigin;
    private Vector2 _restPos;
    private float _restRotation;
    private int _restZIndex;
    private Vector2 _pressGlobalPosition;

    public CardButton(CardRuntime card, HandLayoutProfile layoutProfile)
    {
        Card = card;
        _layoutProfile = layoutProfile;
        MouseFilter = MouseFilterEnum.Stop;
    }

    public void ApplyLayout(HandCardLayout layout, Vector2 handAreaGlobalOrigin)
    {
        _restLayout = layout;
        _handAreaGlobalOrigin = handAreaGlobalOrigin;
        Size = layout.Size;
        PivotOffset = Size * 0.5f;
        Position = layout.Position - _handAreaGlobalOrigin;
        RotationDegrees = layout.RotationDegrees;
        Scale = layout.Scale;
        ZIndex = layout.ZIndex;
        _restPos = Position;
        _restRotation = RotationDegrees;
        _restZIndex = ZIndex;
    }

    public override void _Ready()
    {
        _restPos = Position;
        _restRotation = RotationDegrees;
        _restZIndex = ZIndex;
        PivotOffset = Size / 2f;

        _bg = new ColorRect();
        _bg.Color = Card.Info.Type == CardType.斗击
            ? new Color(0.25f, 0.12f, 0.1f, 0.95f)
            : new Color(0.1f, 0.15f, 0.25f, 0.95f);
        _bg.Size = Size;
        _bg.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_bg);

        _label = new Label();
        string effectSummary = string.IsNullOrWhiteSpace(Card.Info.ExecutionSummary)
            ? Card.Info.Description.Replace("{0}", Card.Info.Value.ToString())
            : Card.Info.ExecutionSummary;
        _label.Text = $"[{Card.Info.Cost}费] {Card.Info.Name}\n\n{effectSummary}";
        _label.SetPosition(new Vector2(8, 10));
        _label.Size = new Vector2(Mathf.Max(80f, Size.X - 16f), Mathf.Max(120f, Size.Y - 30f));
        _label.AddThemeFontSizeOverride("font_size", 13);
        _label.AddThemeColorOverride("font_color", Colors.White);
        _label.MouseFilter = MouseFilterEnum.Ignore;
        _label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        AddChild(_label);

        if (Card.Info.Cost > GameManager.Instance.PlayerLingli)
            Modulate = new Color(0.4f, 0.4f, 0.4f, 1.0f);

        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
    }

    public void SetSelected(bool selected)
    {
        _selected = selected;
        if (selected)
        {
            _hovered = false;
            ApplySelectedVisual();
        }
        else
        {
            ReturnToRest();
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (Card.Info.Cost > GameManager.Instance.PlayerLingli) return;

        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed)
            {
                _pressing = true;
                _dragging = false;
                _pressGlobalPosition = GetGlobalMousePosition();
                AcceptEvent();
                return;
            }

            if (_dragging)
            {
                _pressing = false;
                _dragging = false;
                OnCardDragEnded?.Invoke(this, GetGlobalMousePosition());
                AcceptEvent();
                return;
            }

            _pressing = false;
            OnCardSelected?.Invoke(this);
            AcceptEvent();
            return;
        }

        if (@event is InputEventMouseButton mbRight &&
            mbRight.ButtonIndex == MouseButton.Right &&
            mbRight.Pressed &&
            (_pressing || _dragging))
        {
            _pressing = false;
            _dragging = false;
            ReturnToRest();
            OnCardCanceled?.Invoke(this);
            AcceptEvent();
            return;
        }

        if (@event is InputEventMouseMotion)
        {
            if (!_pressing && !_dragging) return;

            Vector2 mouseGlobal = GetGlobalMousePosition();
            if (!_dragging && mouseGlobal.DistanceTo(_pressGlobalPosition) >= _layoutProfile.DragThreshold)
            {
                _dragging = true;
                _hovered = false;
                _selected = true;
                ApplyDragVisual();
                OnCardDragStarted?.Invoke(this);
            }

            if (_dragging)
            {
                if (Card.Info.TargetMode == CardTargetMode.Enemy)
                {
                    ApplyEnemyTargetVisual();
                }
                else
                {
                    FollowMouse();
                }
                AcceptEvent();
            }
        }
    }

    public void ReturnToRest()
    {
        _pressing = false;
        _dragging = false;
        _selected = false;
        Position = _restPos;
        RotationDegrees = _hovered && Card.Info.Cost <= GameManager.Instance.PlayerLingli
            ? 0f
            : _restRotation;
        Scale = _hovered && Card.Info.Cost <= GameManager.Instance.PlayerLingli
            ? Vector2.One * _layoutProfile.HoverScale
            : Vector2.One;
        ZIndex = _hovered && Card.Info.Cost <= GameManager.Instance.PlayerLingli
            ? _layoutProfile.HoverZIndex
            : _restZIndex;
        if (_hovered && Card.Info.Cost <= GameManager.Instance.PlayerLingli)
            Position = _restPos + new Vector2(0f, _layoutProfile.HoverLiftY);
        _bg.Color = GetBaseColor();
    }

    public Vector2 GetArrowOriginGlobal()
    {
        return GetCenterGlobal() + _layoutProfile.ArrowStartOffset;
    }

    public Vector2 GetCenterGlobal()
    {
        Rect2 rect = GetGlobalRect();
        return rect.Position + rect.Size * 0.5f;
    }

    public bool TryGetDebugHoverPoint(out Vector2 point)
    {
        point = GetCenterGlobal();
        return _hovered;
    }

    public void FollowMouse()
    {
        GlobalPosition = GetGlobalMousePosition() + _layoutProfile.HeldFollowOffset - Size / 2f;
        RotationDegrees = 0f;
        Scale = Vector2.One * _layoutProfile.SelectedScale;
        ZIndex = _layoutProfile.HeldZIndex;
    }

    private void ApplySelectedVisual()
    {
        if (Card.Info.TargetMode == CardTargetMode.Enemy)
            ApplyEnemyTargetVisual();
        else
            ApplySelfHeldVisual();
        _bg.Color = GetActiveColor();
    }

    private void ApplyDragVisual()
    {
        if (Card.Info.TargetMode == CardTargetMode.Enemy)
            ApplyEnemyTargetVisual();
        else
            ApplySelfHeldVisual();

        _bg.Color = GetActiveColor();
    }

    private void ApplyEnemyTargetVisual()
    {
        Position = _restPos + new Vector2(0f, _layoutProfile.HoverLiftY * 0.25f);
        RotationDegrees = 0f;
        Scale = Vector2.One * Mathf.Min(_layoutProfile.SelectedScale, 1.08f);
        ZIndex = _layoutProfile.SelectedZIndex;
    }

    private void ApplySelfHeldVisual()
    {
        Position = _restPos + new Vector2(0f, _layoutProfile.SelectedLiftY);
        RotationDegrees = 0f;
        Scale = Vector2.One * _layoutProfile.SelectedScale;
        ZIndex = _layoutProfile.SelectedZIndex;
    }

    private Color GetBaseColor()
    {
        return Card.Info.Type == CardType.斗击
            ? new Color(0.25f, 0.12f, 0.1f, 0.95f)
            : new Color(0.1f, 0.15f, 0.25f, 0.95f);
    }

    private Color GetActiveColor()
    {
        return Card.Info.Type == CardType.斗击
            ? new Color(0.45f, 0.22f, 0.12f, 1f)
            : new Color(0.16f, 0.28f, 0.45f, 1f);
    }

    private void OnMouseEntered()
    {
        if (_selected || _dragging || Card.Info.Cost > GameManager.Instance.PlayerLingli) return;
        _hovered = true;
        ReturnToRest();
    }

    private void OnMouseExited()
    {
        if (_selected || _dragging) return;
        _hovered = false;
        ReturnToRest();
    }
}
