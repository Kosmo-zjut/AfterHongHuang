using Godot;
using System.Collections.Generic;

/// <summary>
/// 套牌查看弹窗（静态工具类）。
/// 显示当前牌组构筑：总量、堆分布、每张卡名称+消耗+描述+数量。
/// </summary>
public static class DeckViewer
{
    private static Panel _currentOverlay;

    public static void Show(Node parent)
    {
        if (_currentOverlay != null && GodotObject.IsInstanceValid(_currentOverlay))
        {
            CloseCurrentOverlay();
            return;
        }
        if (!OverlayCoordinator.TryPrepareUtilityOverlay("套牌页面", out var coordinatorError))
        {
            GD.PrintErr($"[套牌] {coordinatorError}");
            return;
        }

        var gm = GameManager.Instance;

        // 全屏遮罩（TopBar 下方）
        var overlay = new Panel();
        overlay.SetPosition(new Vector2(0, 44));
        overlay.Size = new Vector2(1920, 1036);
        // Global TopBar entry stays in the shared map/utility plane above victory content.
        overlay.ZIndex = OverlayCoordinator.MapAndUtilityZIndex;
        overlay.MouseFilter = Control.MouseFilterEnum.Stop;
        parent.AddChild(overlay);
        _currentOverlay = overlay;
        OverlayCoordinator.RegisterDeck(overlay);

        var bg = new ColorRect();
        bg.Color = new Color(0.06f, 0.05f, 0.04f, 0.95f);
        bg.SetAnchorsPreset((Control.LayoutPreset)15);
        bg.MouseFilter = Control.MouseFilterEnum.Stop;
        bg.GuiInput += (@event) =>
        {
            if (@event is InputEventMouseButton mb &&
                mb.ButtonIndex == MouseButton.Left &&
                mb.Pressed)
            {
                CloseCurrentOverlay();
            }
        };
        overlay.AddChild(bg);

        // 关闭按钮
        var closeBtn = new Button();
        closeBtn.Text = "✕";
        closeBtn.AddThemeFontSizeOverride("font_size", 22);
        closeBtn.SetPosition(new Vector2(1820, 10));
        closeBtn.Size = new Vector2(44, 44);
        closeBtn.Pressed += CloseCurrentOverlay;
        overlay.AddChild(closeBtn);

        // 标题
        var title = new Label();
        title.Text = "卡组全览";
        title.AddThemeFontSizeOverride("font_size", 28);
        title.AddThemeColorOverride("font_color", new Color(0.9f, 0.7f, 0.3f));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.SetPosition(new Vector2(0, 20));
        title.Size = new Vector2(1920, 44);
        overlay.AddChild(title);

        // 牌组总览
        var permanentDeck = gm.GetPermanentDeckCards();
        int total = permanentDeck.Count;

        var summary = new Label();
        summary.Text = $"卡组全览 · 共 {total} 张";
        summary.AddThemeFontSizeOverride("font_size", 16);
        summary.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.75f));
        summary.HorizontalAlignment = HorizontalAlignment.Center;
        summary.SetPosition(new Vector2(0, 70));
        summary.Size = new Vector2(1920, 30);
        overlay.AddChild(summary);

        // 按卡名汇总（三堆合并）
        var stats = new Dictionary<string, int>();
        var infoMap = new Dictionary<string, CardInfo>();
        foreach (var c in permanentDeck)
            AddCard(stats, infoMap, c);

        // 卡牌列表（ScrollContainer）
        var scroll = new ScrollContainer();
        scroll.SetPosition(new Vector2(310, 120));
        scroll.Size = new Vector2(1300, 860);
        overlay.AddChild(scroll);

        var cardList = new VBoxContainer();
        cardList.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(cardList);

        int index = 0;
        foreach (var kv in stats)
        {
            var info = infoMap[kv.Key];
            int count = kv.Value;

            // 行背景
            var row = new Panel();
            row.CustomMinimumSize = new Vector2(1260, 68);
            row.Modulate = index % 2 == 0
                ? new Color(0.18f, 0.15f, 0.10f, 1)
                : new Color(0.22f, 0.18f, 0.12f, 1);
            row.SetPosition(new Vector2(0, 0));
            row.Size = new Vector2(1260, 68);

            // 卡名
            var nameLabel = new Label();
            nameLabel.Text = info.Name;
            nameLabel.AddThemeFontSizeOverride("font_size", 18);
            nameLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.9f, 0.7f));
            nameLabel.SetPosition(new Vector2(16, 6));
            nameLabel.Size = new Vector2(200, 24);
            row.AddChild(nameLabel);

            // 消耗
            var costLabel = new Label();
            costLabel.Text = $"消耗 {info.Cost}";
            costLabel.AddThemeFontSizeOverride("font_size", 14);
            costLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.8f, 0.9f));
            costLabel.SetPosition(new Vector2(16, 36));
            costLabel.Size = new Vector2(120, 20);
            row.AddChild(costLabel);

            // 描述
            var descLabel = new Label();
            descLabel.Text = info.Description;
            descLabel.AddThemeFontSizeOverride("font_size", 13);
            descLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            descLabel.SetPosition(new Vector2(230, 10));
            descLabel.Size = new Vector2(750, 48);
            descLabel.AutowrapMode = TextServer.AutowrapMode.Word;
            descLabel.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(descLabel);

            // 数量
            var countLabel = new Label();
            countLabel.Text = $"×{count}";
            countLabel.AddThemeFontSizeOverride("font_size", 22);
            countLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.7f, 0.3f));
            countLabel.HorizontalAlignment = HorizontalAlignment.Center;
            countLabel.SetPosition(new Vector2(1100, 12));
            countLabel.Size = new Vector2(120, 36);
            row.AddChild(countLabel);

            cardList.AddChild(row);
            index++;
        }

        // 底部提示
        var hint = new Label();
        hint.Text = "点击右上角 ✕ 关闭";
        hint.AddThemeFontSizeOverride("font_size", 13);
        hint.AddThemeColorOverride("font_color", new Color(0.4f, 0.4f, 0.4f));
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        hint.SetPosition(new Vector2(0, 1000));
        hint.Size = new Vector2(1920, 24);
        overlay.AddChild(hint);
    }

    /// <summary>Closes the shared deck overlay without changing node or battle state.</summary>
    public static void CloseCurrentOverlay()
    {
        if (_currentOverlay != null && GodotObject.IsInstanceValid(_currentOverlay))
        {
            OverlayCoordinator.Unregister(_currentOverlay);
            _currentOverlay.QueueFree();
        }
        _currentOverlay = null;
    }

    private static void AddCard(Dictionary<string, int> stats, Dictionary<string, CardInfo> infoMap, CardRuntime card)
    {
        string name = card.Info.Name;
        if (stats.ContainsKey(name))
            stats[name]++;
        else
        {
            stats[name] = 1;
            infoMap[name] = card.Info;
        }
    }
}
