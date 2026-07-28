using Godot;
using System.Collections.Generic;

/// <summary>
/// 场景：MapScene（地图场景）。
/// 底页 = 初始道韵选择；道痕固化后保持空底页，地图通过共享 overlay 呼出。
/// 地图 = 呼出式 overlay，固定左边界向右揭示，TopBar 地图按钮 toggle。
/// </summary>
public partial class MapController : Control
{
    [Export] public PackedScene SettingsDialogScene { get; set; }

    private TopBar _topBar;

    // ============ 主内容区（底页）============
    private Control _mainArea;

    // ============ 道韵选择（底页模式，非 overlay）============
    private DaoMarkInfo[] _daoChoices;
    private HBoxContainer _daoCardContainer;

    // ============ 地图 overlay ============
    private MapOverlayController _mapOverlay;

    // ============ 道韵选择（底页模式）============
    private Button[] _daoSelectBtns;
    private Node _daoCurrentPageNode; // 当前底页中的道韵 UI 根节点

    public override void _EnterTree()
    {
        // MapScene 的底页没有独立流程价值。先隐藏根节点，直到入口目标页面构建成功，
        // 避免场景切换期间短暂暴露 TopBar + 空白底页。
        Visible = false;
    }

    public override void _Ready()
    {
        BuildStaticUI();
        var gm = GameManager.Instance;
        if (gm.PendingMapEntry == MapEntryMode.OpenInteractiveMap)
        {
            if (!TryOpenMap(true, out var mapError))
            {
                ShowMapEntryError(mapError);
                Visible = true;
                return;
            }

            if (!gm.TryCompleteMapEntry(MapEntryMode.OpenInteractiveMap, out var consumeError))
            {
                ShowMapEntryError(consumeError);
                Visible = true;
                return;
            }

            Visible = true;
            return;
        }

        ShowCurrentNodeContent();
        Visible = true;
    }

    // ==================== 静态 UI ====================

    private void BuildStaticUI()
    {
        // 背景
        var bg = new ColorRect();
        bg.Color = new Color(0.06f, 0.05f, 0.08f, 1);
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(bg);

        // 顶部栏：地图按钮 toggle
        _topBar = new TopBar();
        _topBar.SetPosition(new Vector2(0, 0));
        _topBar.Size = new Vector2(1920, 44);
        _topBar.OnDeckPressed = () => DeckViewer.Show(this);
        _topBar.OnMapPressed = () => ToggleMap();
        _topBar.OnSettingsPressed = () => SettingsHelper.Show(this, SettingsDialogScene);
        AddChild(_topBar);

        // 主内容区（底页，道韵选择 / 节点信息都在这里渲染）
        _mainArea = new Control();
        _mainArea.SetPosition(new Vector2(0, 44));
        _mainArea.Size = new Vector2(1920, 1036);
        AddChild(_mainArea);
    }

    // ==================== 底页：当前节点内容 ====================

    /// <summary>刷新底页显示（根据 GameManager 状态）</summary>
    private void ShowCurrentNodeContent()
    {
        var gm = GameManager.Instance;

        if (!gm.DaoMarkSelected)
        {
            // 道韵未选 → 底页直接渲染 3 张道韵卡
            ShowDaoMarkPage();
        }
        else
        {
            // 已选道韵后的 MapScene 主体保持空白，主流程入口是地图卷轴。
            ClearMainArea();
        }
    }

    private void ClearMainArea()
    {
        foreach (Node child in _mainArea.GetChildren())
            child.QueueFree();
        _daoCurrentPageNode = null;
    }

    // ==================== 地图呼出 ====================

    private void ToggleMap()
    {
        if (_mapOverlay != null && GodotObject.IsInstanceValid(_mapOverlay))
        {
            _mapOverlay.Close();
            return;
        }

        _mapOverlay = MapOverlayController.Open(this, true, OnMapNodePressed,
            () => _mapOverlay = null);
        if (_mapOverlay == null)
            GD.PrintErr("[地图] 共享地图 overlay 创建失败。");
    }

    private bool TryOpenMap(bool immediate, out string error)
    {
        error = "";
        if (_mapOverlay != null && GodotObject.IsInstanceValid(_mapOverlay))
        {
            error = "地图已经打开。";
            return false;
        }
        _mapOverlay = MapOverlayController.Open(this, true, OnMapNodePressed,
            () => _mapOverlay = null, immediate);
        if (_mapOverlay == null)
        {
            error = "共享地图 overlay 创建失败。";
            return false;
        }
        return true;
    }

    private void OnMapNodePressed(MapNodeDefinition info)
    {
        if (info == null)
        {
            GD.PrintErr("[地图] 节点回调缺少 MapNodeDefinition，拒绝进入。");
            return;
        }

        // 先验证目标内容和入口定义，再释放当前 overlay；失败时保留地图，
        // 避免把用户留在没有可恢复入口的空白页面。
        if (!GameManager.Instance.TryEnterNode(info, out var validationError))
        {
            GD.PrintErr($"[地图] 目标节点预检失败，保留地图 overlay：{validationError}");
            return;
        }

        _mapOverlay?.QueueFree();
        _mapOverlay = null;
        if (!NodeSceneRouter.TryGoTo(GameManager.Instance, info, out var routeError))
        {
            GD.PrintErr($"[地图] 节点已激活但场景路由失败，当前状态需人工检查：{routeError}");
        }
    }

    // ==================== 道韵选择（底页直接渲染） ====================

    /// <summary>在底页渲染道韵选择 UI（不再是 overlay）</summary>
    private void ShowDaoMarkPage()
    {
        var gm = GameManager.Instance;
        bool alreadySelected = gm.DaoMarkSelected;

        // 已选状态下，如果道韵页面还在就不删了
        if (alreadySelected && _daoCurrentPageNode != null && GodotObject.IsInstanceValid(_daoCurrentPageNode))
            return;

        ClearMainArea();

        if (alreadySelected)
            _daoChoices = gm.CurrentChoices?.Count == 3 ? gm.CurrentChoices.ToArray() : _daoChoices;
        else
            PickRandomDaoMarks();

        string selectedName = alreadySelected && gm.DaoMarks.Count > 0 ? gm.DaoMarks[^1].Name : null;
        _daoSelectBtns = new Button[3];

        // 标题
        var title = new Label();
        title.Text = alreadySelected ? "道痕已固化" : "道韵浮现，择一而固";
        title.AddThemeFontSizeOverride("font_size", 28);
        title.AddThemeColorOverride("font_color", new Color(0.9f, 0.7f, 0.3f));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.SetPosition(new Vector2(0, 40));
        title.Size = new Vector2(1920, 40);
        _mainArea.AddChild(title);

        // 3 张卡片横向排列
        _daoCardContainer = new HBoxContainer();
        _daoCardContainer.SetPosition(new Vector2(76, 110));
        _daoCardContainer.Size = new Vector2(1768, 560);
        _daoCardContainer.AddThemeConstantOverride("separation", 30);
        _mainArea.AddChild(_daoCardContainer);

        for (int i = 0; i < 3; i++)
        {
            var dm = _daoChoices[i];
            var card = new Panel();
            card.CustomMinimumSize = new Vector2(540, 520);
            _daoCardContainer.AddChild(card);

            bool isSelected = alreadySelected && dm.Name == selectedName;
            if (alreadySelected)
                card.Modulate = isSelected ? new Color(1, 1, 1, 1) : new Color(0.35f, 0.35f, 0.35f, 1);

            var desc = new RichTextLabel();
            desc.BbcodeEnabled = true;
            string hc = isSelected ? "gold" : "white";
            desc.Text = $"[center][font_size=20][color={hc}][b]{dm.Name}[/b][/color][/font_size][/center]\n" +
                        $"[center][{dm.Grade}][/center]\n\n{dm.Description}";
            desc.SetPosition(new Vector2(10, 10));
            desc.Size = new Vector2(520, 400);
            card.AddChild(desc);

            if (!alreadySelected)
            {
                var selBtn = new Button();
                selBtn.Text = "固 化";
                selBtn.SetPosition(new Vector2(190, 460));
                selBtn.Size = new Vector2(160, 40);
                selBtn.AddThemeFontSizeOverride("font_size", 18);
                int idx = i;
                selBtn.Pressed += () => OnDaoMarkSelected(idx);
                card.AddChild(selBtn);
                _daoSelectBtns[i] = selBtn;
            }
            else if (isSelected)
            {
                var checkLabel = new Label();
                checkLabel.Text = "✓ 已固化";
                checkLabel.AddThemeFontSizeOverride("font_size", 20);
                checkLabel.AddThemeColorOverride("font_color", new Color(0.2f, 0.8f, 0.3f));
                checkLabel.HorizontalAlignment = HorizontalAlignment.Center;
                checkLabel.SetPosition(new Vector2(190, 460));
                checkLabel.Size = new Vector2(160, 40);
                card.AddChild(checkLabel);
            }
        }

        _daoCurrentPageNode = _daoCardContainer;
    }

    private void PickRandomDaoMarks()
    {
        var pool = new List<DaoMarkInfo>(DataDefs.DaoMarkPool);
        var rng = new System.Random();
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        _daoChoices = pool.GetRange(0, 3).ToArray();
        GameManager.Instance.CurrentChoices = new List<DaoMarkInfo>(_daoChoices);
    }

    private void OnDaoMarkSelected(int index)
    {
        var selected = _daoChoices[index];
        var gm = GameManager.Instance;
        gm.DaoMarks.Add(selected);
        gm.DaoMarkSelected = true;
        gm.MapNodesUnlocked = true;

        if (selected.EffectType == DaoMarkEffect.加血上限)
        {
            gm.PlayerMaxHp += selected.EffectValue;
            gm.PlayerHp += selected.EffectValue;
        }

        GD.Print($"[道痕] 已固化：{selected.Name}");

        // 原地切换卡片状态（选中高亮、未选变暗、隐藏固化按钮）
        SwitchDaoPageToReadonly(index);
    }

    /// <summary>在原地将道韵底页的卡片切换为查看模式</summary>
    private void SwitchDaoPageToReadonly(int selectedIndex)
    {
        var cardContainer = _daoCardContainer;
        if (cardContainer == null) return;

        // 修改标题
        var title = _mainArea.GetChildOrNull<Label>(0);
        if (title != null) title.Text = "道痕已固化";

        for (int i = 0; i < 3; i++)
        {
            var card = cardContainer.GetChildOrNull<Panel>(i);
            if (card == null) continue;

            bool isSelected = (i == selectedIndex);
            card.Modulate = isSelected ? new Color(1, 1, 1, 1) : new Color(0.35f, 0.35f, 0.35f, 1);

            // 隐藏"固化"按钮
            var selBtn = card.GetChildOrNull<Button>(1);
            selBtn?.QueueFree();
            _daoSelectBtns[i] = null;

            if (isSelected)
            {
                var checkLabel = new Label();
                checkLabel.Text = "✓ 已固化";
                checkLabel.AddThemeFontSizeOverride("font_size", 20);
                checkLabel.AddThemeColorOverride("font_color", new Color(0.2f, 0.8f, 0.3f));
                checkLabel.HorizontalAlignment = HorizontalAlignment.Center;
                checkLabel.SetPosition(new Vector2(190, 460));
                checkLabel.Size = new Vector2(160, 40);
                card.AddChild(checkLabel);
            }
        }

        // 底部「继续」按钮 — 等效于点击 TopBar 地图按钮
        var continueBtn = new Button();
        continueBtn.Text = "继 续";
        continueBtn.AddThemeFontSizeOverride("font_size", 22);
        continueBtn.SetPosition(new Vector2(860, 700));
        continueBtn.Size = new Vector2(200, 50);
        continueBtn.Pressed += () => ToggleMap();
        _mainArea.AddChild(continueBtn);
    }

    // ==================== 节点进入 ====================

    private void ShowMapEntryError(string error)
    {
        GD.PrintErr($"[地图] 进入地图失败：{error}");
        ClearMainArea();
        var errorLabel = new Label();
        errorLabel.Text = $"地图加载失败\n{error}";
        errorLabel.AddThemeFontSizeOverride("font_size", 24);
        errorLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.35f, 0.25f));
        errorLabel.HorizontalAlignment = HorizontalAlignment.Center;
        errorLabel.VerticalAlignment = VerticalAlignment.Center;
        errorLabel.SetPosition(new Vector2(420, 380));
        errorLabel.Size = new Vector2(1080, 160);
        _mainArea.AddChild(errorLabel);
    }

    // ==================== 地图 refresh（从战斗返回后） ====================

    public void RefreshFromBattle()
    {
        ShowCurrentNodeContent();
    }
}
