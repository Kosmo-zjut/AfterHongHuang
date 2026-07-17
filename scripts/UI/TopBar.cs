using Godot;

/// <summary>
/// 通用顶部状态栏组件。
/// 左侧：角色信息（头像占位、HP、难度、灵韵）
/// 右侧：套牌/地图/设置按钮。
/// 通过 GameManager 读取状态自动刷新。
/// 宿主场景需将此节点置于顶部并设置 anchors 为左右拉伸。
/// </summary>
public partial class TopBar : Control
{
    // 左侧信息
    private TextureRect _avatarIcon;
    private Label _nameLabel;
    private Label _hpLabel;
    private Label _difficultyLabel;
    private Label _lingyunLabel;

    // 右侧按钮
    private Button _deckBtn;
    private Button _mapBtn;
    private Button _settingsBtn;

    // 回调（由宿主场景设置）
    public System.Action OnDeckPressed;
    public System.Action OnMapPressed;
    public System.Action OnSettingsPressed;

    public override void _Ready()
    {
        BuildUI();
    }

    public override void _Process(double delta)
    {
        RefreshState();
    }

    /// <summary>构建顶部栏 UI 节点树</summary>
    private void BuildUI()
    {
        // 自身尺寸
        CustomMinimumSize = new Vector2(1920, 44);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ShrinkBegin;

        // 背景色
        var bg = new ColorRect();
        bg.Color = new Color(0.08f, 0.06f, 0.04f, 0.92f);
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(bg);

        // 主布局容器（水平排列 3 个区域）
        var mainLayout = new HBoxContainer();
        mainLayout.SetAnchorsPreset(LayoutPreset.FullRect);
        mainLayout.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        mainLayout.AddThemeConstantOverride("separation", 0);
        mainLayout.SizeFlagsVertical = SizeFlags.ExpandFill;
        AddChild(mainLayout);

        // ==================== 左侧信息区 ====================
        var leftSection = new HBoxContainer();
        leftSection.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        leftSection.AddThemeConstantOverride("separation", 10);
        leftSection.SetPosition(new Vector2(8, 0));

        // 头像占位
        _avatarIcon = new TextureRect();
        _avatarIcon.CustomMinimumSize = new Vector2(34, 34);
        _avatarIcon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _avatarIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        leftSection.AddChild(_avatarIcon);

        // 角色名
        _nameLabel = CreateInfoLabel("", new Color(0.9f, 0.7f, 0.3f));
        leftSection.AddChild(_nameLabel);

        // 分隔符
        leftSection.AddChild(CreateSeparator());

        // HP
        _hpLabel = CreateInfoLabel("HP:80/80", new Color(0.9f, 0.25f, 0.25f));
        leftSection.AddChild(_hpLabel);

        // 难度
        _difficultyLabel = CreateInfoLabel("地仙", new Color(0.5f, 0.8f, 0.5f));
        leftSection.AddChild(_difficultyLabel);

        // 灵韵
        _lingyunLabel = CreateInfoLabel("灵韵:0", new Color(0.9f, 0.8f, 0.2f));
        leftSection.AddChild(_lingyunLabel);

        mainLayout.AddChild(leftSection);

        // ==================== 中间填充 ====================
        var spacer = new Control();
        spacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        mainLayout.AddChild(spacer);

        // ==================== 右侧按钮区 ====================
        var rightSection = new HBoxContainer();
        rightSection.AddThemeConstantOverride("separation", 4);
        rightSection.SizeFlagsVertical = SizeFlags.ExpandFill;

        _deckBtn = CreateTopButton("📋 套牌");
        _deckBtn.Pressed += () => OnDeckPressed?.Invoke();
        rightSection.AddChild(_deckBtn);

        _mapBtn = CreateTopButton("🗺 地图");
        _mapBtn.Pressed += () => OnMapPressed?.Invoke();
        rightSection.AddChild(_mapBtn);

        _settingsBtn = CreateTopButton("⚙");
        _settingsBtn.Pressed += () => OnSettingsPressed?.Invoke();
        rightSection.AddChild(_settingsBtn);

        mainLayout.AddChild(rightSection);
    }

    /// <summary>从 GameManager 刷新显示数据</summary>
    public void RefreshState()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.SelectedCharacter == null) return;

        _nameLabel.Text = gm.SelectedCharacter.Name;
        _hpLabel.Text = $"HP:{gm.PlayerHp}/{gm.PlayerMaxHp}";
        _difficultyLabel.Text = gm.Difficulty;
        _lingyunLabel.Text = $"灵韵:{gm.LingYun}";

        // HP 颜色随血量变化
        float hpRatio = (float)gm.PlayerHp / gm.PlayerMaxHp;
        if (hpRatio > 0.5f)
            _hpLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.25f, 0.25f));
        else if (hpRatio > 0.25f)
            _hpLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.5f, 0.1f));
        else
            _hpLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.15f, 0.15f));
    }

    /// <summary>设置地图按钮是否可用（在地图界面时禁用）</summary>
    public void SetMapButtonEnabled(bool enabled)
    {
        if (_mapBtn != null)
            _mapBtn.Disabled = !enabled;
    }

    /// <summary>手动设置套牌按钮回调</summary>
    public void SetDeckCallback(System.Action callback)
    {
        _deckBtn.Pressed += callback;
    }

    // ---- 工厂方法 ----

    private static Label CreateInfoLabel(string text, Color color)
    {
        var label = new Label();
        label.Text = text;
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeFontSizeOverride("font_size", 14);
        label.VerticalAlignment = VerticalAlignment.Center;
        return label;
    }

    private static Control CreateSeparator()
    {
        var sep = new ColorRect();
        sep.Color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        sep.CustomMinimumSize = new Vector2(1, 24);
        sep.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        return sep;
    }

    private static Button CreateTopButton(string text)
    {
        var btn = new Button();
        btn.Text = text;
        btn.CustomMinimumSize = new Vector2(70, 32);
        btn.AddThemeFontSizeOverride("font_size", 13);
        return btn;
    }
}
