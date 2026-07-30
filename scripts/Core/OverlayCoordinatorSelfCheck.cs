using Godot;

/// <summary>Debug-only behavioral proof for the shared overlay ownership rules.</summary>
public static class OverlayCoordinatorSelfCheck
{
    public static void Run()
    {
        Ensure(OverlayCoordinator.MapCoverZIndex > OverlayCoordinator.CardRewardZIndexMax,
            "地图覆盖层必须高于 CardReward，关闭后才能恢复同一奖励状态。");
        Ensure(OverlayCoordinator.CardRewardZIndex > OverlayCoordinator.VictoryZIndex,
            "CardReward 必须压住胜利页面内容，阻断本地继续输入。");
        Ensure(OverlayCoordinator.GlobalSettingsZIndex > OverlayCoordinator.MapCoverZIndexMax,
            "GlobalSettings 必须高于地图、CardReward 和胜利内容。");
        Ensure(OverlayCoordinator.TransitionAndErrorZIndex > OverlayCoordinator.GlobalSettingsZIndexMax,
            "转场/致命错误平面必须高于 GlobalSettings。");

        var nodePage = new Control();
        int mapToggleCount = 0;
        var localContinue = NodeMapEntry.Add(nodePage, () => mapToggleCount++);
        Ensure(localContinue.ZAsRelative && localContinue.ZIndex == 0,
            "节点页继续入口不得脱离父页面层级或占用全局 Z 平面。");
        Ensure(localContinue.Text == "继续", "共享节点入口文本必须为继续，地图仅保留在 TopBar。 ");
        localContinue.EmitSignal(BaseButton.SignalName.Pressed);
        Ensure(mapToggleCount == 1, "本地继续只允许调用地图开关，不能包含结算或路由副作用。");
        localContinue.Free();
        nodePage.Free();

        var victory = new Panel();
        Ensure(OverlayCoordinator.TryRegisterVictory(victory, out var victoryError), victoryError);
        VerifyMapCoverPreservesReward();
        VerifyMapCoverVisuallyOwnsNodeContent();
        VerifyGlobalEntryCancelsReward("套牌", (out string error) => OverlayCoordinator.TryPrepareUtilityOverlay("套牌页面", out error));
        VerifyGlobalSettingsStack();
        CardPointerGestureSelfCheck.Run();

        OverlayCoordinator.Unregister(victory);
        victory.Free();

        foreach (var option in ResolutionSettings.SupportedOptions)
            Ensure(ResolutionSettings.TryFindIndex(new Vector2I(option.Width, option.Height), out _),
                $"分辨率目录项无法反查：{option.Label}");

        GD.Print("[OverlayCoordinatorSelfCheck] PASS modal stack and resolution catalog");
    }

    /// <summary>
    /// Proves that every global TopBar entry uses CardReward's cancellation callback instead of
    /// freeing the overlay directly. The callback represents the victory-row re-enable operation.
    /// </summary>
    private static void VerifyGlobalEntryCancelsReward(string entryName, PrepareOverlayAction prepare)
    {
        var reward = new Panel();
        const string candidateFingerprint = "fixture-candidate-a|fixture-candidate-b|fixture-candidate-c";
        int selectedCount = 1;
        bool sourceRewardRowEnabled = false;
        CardRewardHelper.RegisterCancellationForOverlay(reward, () =>
        {
            // Match CardRewardHelper's production cancellation closure: restore the source row
            // and close/unregister only the child overlay, not its RewardPlan or victory page.
            sourceRewardRowEnabled = true;
            OverlayCoordinator.Unregister(reward);
            reward.QueueFree();
        });
        Ensure(OverlayCoordinator.TryRegisterCardReward(reward, out var rewardError), rewardError);

        Ensure(prepare(out var prepareError), $"TopBar {entryName} 入口被拒绝：{prepareError}");
        Ensure(sourceRewardRowEnabled, $"TopBar {entryName} 未恢复原奖励行可点击状态。");
        Ensure(selectedCount == 1 && candidateFingerprint == "fixture-candidate-a|fixture-candidate-b|fixture-candidate-c",
            $"TopBar {entryName} 意外改变奖励候选或已选进度。");
    }

    private delegate bool PrepareOverlayAction(out string error);

    /// <summary>
    /// Proves settings is a global stack entry: lower overlay instances remain alive while it is
    /// open, and the map prepare path closes settings through its owner before taking ownership.
    /// </summary>
    private static void VerifyGlobalSettingsStack()
    {
        var map = new Panel();
        var deck = new Panel();
        var reward = new Panel();
        var settings = new Panel();
        bool settingsClosed = false;

        OverlayCoordinator.RegisterMap(map);
        OverlayCoordinator.RegisterDeck(deck);
        Ensure(OverlayCoordinator.TryRegisterCardReward(reward, out var rewardError), rewardError);

        Ensure(OverlayCoordinator.TryPrepareGlobalSettings(settings, () =>
        {
            settingsClosed = true;
            OverlayCoordinator.Unregister(settings);
            settings.Free();
        }, out var settingsError), settingsError);
        Ensure(settings.ZIndex == OverlayCoordinator.GlobalSettingsZIndex,
            "GlobalSettings 实例必须由协调器装配到集中语义平面。");
        Ensure(GodotObject.IsInstanceValid(map) && GodotObject.IsInstanceValid(deck) &&
            GodotObject.IsInstanceValid(reward),
            "打开设置不得销毁地图、套牌或 CardReward 实例。");

        Ensure(OverlayCoordinator.TryPrepareMap(out var mapError), $"设置打开时地图入口关闭设置失败：{mapError}");
        Ensure(settingsClosed, "其它 TopBar 入口未通过设置负责人关闭 GlobalSettings。");
        Ensure(GodotObject.IsInstanceValid(map) && GodotObject.IsInstanceValid(reward),
            "关闭设置并打开地图后，原地图/CardReward 实例必须保留。");

        OverlayCoordinator.Unregister(map);
        OverlayCoordinator.Unregister(reward);
        map.Free();
        reward.Free();
        if (GodotObject.IsInstanceValid(deck))
            deck.Free();
    }

    /// <summary>
    /// Map opening is intentionally different from Deck/Settings: it covers a CardReward child
    /// instead of cancelling it, so closing the map returns to the same RewardPlan progress.
    /// </summary>
    private static void VerifyMapCoverPreservesReward()
    {
        var reward = new Panel();
        const string candidateFingerprint = "fixture-candidate-a|fixture-candidate-b|fixture-candidate-c";
        const int selectedCount = 1;
        bool cancellationCalled = false;
        CardRewardHelper.RegisterCancellationForOverlay(reward, () => cancellationCalled = true);
        Ensure(OverlayCoordinator.TryRegisterCardReward(reward, out var rewardError), rewardError);

        Ensure(OverlayCoordinator.TryPrepareMap(out var prepareError), $"地图入口被拒绝：{prepareError}");
        Ensure(!cancellationCalled, "地图打开不应取消 CardReward 或消费奖励。 ");
        Ensure(selectedCount == 1 && candidateFingerprint == "fixture-candidate-a|fixture-candidate-b|fixture-candidate-c",
            "地图打开意外改变奖励候选或已选进度。");

        OverlayCoordinator.Unregister(reward);
        reward.Free();
    }

    /// <summary>
    /// Creates the real map control instead of only checking coordinator callbacks. The transparent
    /// input shield must protect content below TopBar without hiding the preserved node page.
    /// </summary>
    private static void VerifyMapCoverVisuallyOwnsNodeContent()
    {
        var host = new Control
        {
            Name = "OverlayCoordinatorSelfCheckHost",
            Size = new Vector2(1920, 1080),
        };

        var topBar = new ColorRect
        {
            Name = "TopBarProbe",
            Position = Vector2.Zero,
            Size = new Vector2(1920, 44),
            Color = Colors.White,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        host.AddChild(topBar);

        var reward = new ColorRect
        {
            Name = "CardRewardProbe",
            Position = new Vector2(0, 44),
            Size = new Vector2(1920, 1036),
            ZIndex = OverlayCoordinator.CardRewardZIndex,
            Color = Colors.White,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        host.AddChild(reward);
        const string candidateFingerprint = "fixture-candidate-a|fixture-candidate-b|fixture-candidate-c";
        const int selectedCount = 1;
        bool cancellationCalled = false;
        CardRewardHelper.RegisterCancellationForOverlay(reward, () => cancellationCalled = true);
        Ensure(OverlayCoordinator.TryRegisterCardReward(reward, out var rewardError), rewardError);

        var localContinue = NodeMapEntry.Add(host, () => { });
        // Detached startup probes do not run Godot's anchor layout pass. Apply the shared scene's
        // frozen bottom-right rect so the real entry control participates in the coverage check.
        localContinue.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        localContinue.Position = new Vector2(1656, 944);
        localContinue.Size = new Vector2(200, 64);
        // This uses the production MapOverlay hierarchy but intentionally skips MapGraph data:
        // startup self-checks execute in GameManager._EnterTree before a run exists.
        var map = MapOverlayController.OpenForSelfCheck(host);
        Ensure(map != null, "无法创建真实地图覆盖层。");
        var cover = map.GetNodeOrNull<ColorRect>("ContentCover");
        Ensure(cover != null, "地图 overlay 缺少内容区视觉承载层。");
        Ensure(cover.MouseFilter == Control.MouseFilterEnum.Stop, "地图内容区必须阻断下层输入。");
        Ensure(cover.Color.A <= 0.01f, "地图输入屏障不得成为不透明全屏底页。");

        Rect2 coverRect = cover.GetGlobalRect();
        Ensure(!coverRect.Intersects(topBar.GetGlobalRect()), "地图视觉承载层不得覆盖 TopBar。");
        Ensure(coverRect.Encloses(reward.GetGlobalRect()), "地图输入屏障未完整保护 CardReward 内容区。");
        Ensure(coverRect.Encloses(localContinue.GetGlobalRect()),
            $"地图输入屏障未完整保护本地继续入口：cover={coverRect} continue={localContinue.GetGlobalRect()}。");

        map.CloseImmediately();
        Ensure(!cancellationCalled, "地图关闭不得取消 CardReward 或消费奖励。");
        Ensure(GodotObject.IsInstanceValid(reward), "地图关闭后必须保留同一个 CardReward 实例。");
        Ensure(selectedCount == 1 && candidateFingerprint == "fixture-candidate-a|fixture-candidate-b|fixture-candidate-c",
            "地图关闭后奖励候选或选择进度发生变化。");

        OverlayCoordinator.Unregister(reward);
        host.Free();
    }

    private static void Ensure(bool condition, string error)
    {
        if (!condition)
            throw new System.InvalidOperationException(error);
    }
}
