using Godot;

/// <summary>
/// Coordinates the shared page overlays. Normal utility overlays are mutually exclusive;
/// victory and card-reward overlays form a separate modal stack above a node page.
/// </summary>
public static class OverlayCoordinator
{
    // Frozen UI planes. Node-page controls stay below these ranges, while each shared
    // overlay category receives a stable plane so active card interaction cannot overdraw it.
    public const int VictoryZIndex = 400;
    public const int VictoryZIndexMax = 449;
    public const int MapAndUtilityZIndex = 450;
    public const int MapAndUtilityZIndexMax = 469;
    public const int CardRewardZIndex = 470;
    public const int CardRewardZIndexMax = 489;
    // The map is a page-owned cover layer, not a utility drawer. It must cover an optional
    // CardReward child without cancelling it, then reveal the unchanged child on close.
    public const int MapCoverZIndex = 490;
    public const int MapCoverZIndexMax = 499;
    // Settings is a global temporary modal: it covers the complete host viewport, including the
    // visible TopBar. Its own panel and close/Esc actions are the only active inputs while open.
    public const int GlobalSettingsZIndex = 500;
    public const int GlobalSettingsZIndexMax = 509;
    public const int TransitionAndErrorZIndex = 510;
    public const int TransitionAndErrorZIndexMax = 519;

    private static Node _mapOverlay;
    private static Node _deckOverlay;
    private static Node _settingsOverlay;
    private static System.Action _closeSettings;
    private static Node _victoryModal;
    private static Node _cardRewardOverlay;

    /// <summary>
    /// Prepares the shared map cover without dismissing the current node page or its optional
    /// CardReward child. Closing the map must reveal exactly the same reward candidates/progress.
    /// </summary>
    public static bool TryPrepareMap(out string error)
    {
        error = "";
        if (!TryCloseGlobalSettings(out error))
            return false;

        CloseAndClear(ref _deckOverlay);
        return true;
    }

    /// <summary>
    /// Prepares a utility overlay. When settings is not active, TopBar utilities remain available
    /// during victory; they close the optional CardReward child but retain the victory page below.
    /// </summary>
    public static bool TryPrepareUtilityOverlay(string overlayName, out string error)
    {
        error = "";
        if (!TryCloseGlobalSettings(out error))
            return false;

        if (!TryDismissCardRewardForGlobalEntry(out error))
            return false;

        CloseAndClear(ref _mapOverlay);
        CloseAndClear(ref _deckOverlay);
        return true;
    }

    /// <summary>Registers the currently visible shared map overlay.</summary>
    public static void RegisterMap(Node overlay) => _mapOverlay = overlay;

    /// <summary>Registers the deck overlay after mutual-exclusion checks.</summary>
    public static void RegisterDeck(Node overlay) => _deckOverlay = overlay;

    /// <summary>
    /// Registers the shared settings instance on the global semantic plane. Settings is not part
    /// of the page map/deck mutual-exclusion group, so registration deliberately leaves all lower
    /// overlay instances alive. The close callback is owned by SettingsHelper and is used when a
    /// different TopBar entry needs to close settings before taking its own action.
    /// </summary>
    public static bool TryPrepareGlobalSettings(Node overlay, System.Action closeCallback, out string error)
    {
        error = "";
        if (!IsAlive(overlay))
        {
            error = "全局设置实例无效。";
            GD.PrintErr($"[OverlayCoordinator] {error}");
            return false;
        }

        if (closeCallback == null)
        {
            error = "全局设置缺少可追踪的关闭回调。";
            GD.PrintErr($"[OverlayCoordinator] {error}");
            return false;
        }

        if (IsAlive(_settingsOverlay) && !SameInstance(_settingsOverlay, overlay))
        {
            error = "已有全局设置实例，拒绝重复装配。";
            GD.PrintErr($"[OverlayCoordinator] {error}");
            return false;
        }

        if (overlay is CanvasItem canvasItem)
            canvasItem.ZIndex = GlobalSettingsZIndex;

        _settingsOverlay = overlay;
        _closeSettings = closeCallback;
        return true;
    }

    /// <summary>True while a higher content/modal overlay owns Battle input.</summary>
    public static bool IsBattleInputBlocked =>
        IsAlive(_settingsOverlay) ||
        IsAlive(_mapOverlay) ||
        IsAlive(_deckOverlay) ||
        IsAlive(_victoryModal) ||
        IsAlive(_cardRewardOverlay);

    /// <summary>Registers the full-screen victory input barrier and closes normal utility overlays.</summary>
    public static bool TryRegisterVictory(Node modal, out string error)
    {
        error = "";
        if (modal == null)
        {
            error = "胜利模态层为空。";
            GD.PrintErr($"[OverlayCoordinator] {error}");
            return false;
        }

        if (!TryCloseGlobalSettings(out error))
            return false;
        CloseAndClear(ref _mapOverlay);
        CloseAndClear(ref _deckOverlay);
        if (!TryDismissCardRewardForGlobalEntry(out error))
            return false;
        _victoryModal = modal;
        return true;
    }

    /// <summary>Only a live victory modal may open its card-reward child overlay.</summary>
    public static bool TryRegisterCardReward(Node overlay, out string error)
    {
        error = "";
        if (overlay == null || !IsAlive(_victoryModal))
        {
            error = "卡牌奖励必须由当前胜利总面板打开。";
            GD.PrintErr($"[OverlayCoordinator] {error}");
            return false;
        }
        if (IsAlive(_cardRewardOverlay))
        {
            error = "已有卡牌奖励 overlay，拒绝重复创建。";
            GD.PrintErr($"[OverlayCoordinator] {error}");
            return false;
        }

        _cardRewardOverlay = overlay;
        return true;
    }

    /// <summary>Clears a tracked overlay reference after it has been closed or freed.</summary>
    public static void Unregister(Node overlay)
    {
        // Godot may expose different managed wrappers for the same native node. InstanceId keeps
        // ownership cleanup reliable when CardReward is dismissed through a global TopBar action.
        if (SameInstance(_mapOverlay, overlay)) _mapOverlay = null;
        if (SameInstance(_deckOverlay, overlay)) _deckOverlay = null;
        if (SameInstance(_settingsOverlay, overlay))
        {
            _settingsOverlay = null;
            _closeSettings = null;
        }
        if (SameInstance(_victoryModal, overlay)) _victoryModal = null;
        if (SameInstance(_cardRewardOverlay, overlay)) _cardRewardOverlay = null;
    }

    /// <summary>
    /// Closes settings through its owner callback and verifies that ownership was released. This
    /// keeps TopBar map/deck actions from QueueFree-ing the dialog behind SettingsHelper's back.
    /// </summary>
    private static bool TryCloseGlobalSettings(out string error)
    {
        error = "";
        if (!IsAlive(_settingsOverlay))
        {
            _settingsOverlay = null;
            _closeSettings = null;
            return true;
        }

        if (_closeSettings == null)
        {
            error = "全局设置实例缺少关闭负责人。";
            GD.PrintErr($"[OverlayCoordinator] {error}");
            return false;
        }

        var close = _closeSettings;
        close();
        if (IsAlive(_settingsOverlay))
        {
            error = "全局设置关闭负责人未释放当前实例。";
            GD.PrintErr($"[OverlayCoordinator] {error}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Closes the active CardReward through its registered cancellation callback. This preserves
    /// the RewardPlan and restores the victory-row button without granting or consuming anything.
    /// </summary>
    private static bool TryDismissCardRewardForGlobalEntry(out string error)
    {
        error = "";
        if (!IsAlive(_cardRewardOverlay))
        {
            _cardRewardOverlay = null;
            return true;
        }

        Node rewardOverlay = _cardRewardOverlay;
        if (!CardRewardHelper.TryCancelForGlobalOverlay(rewardOverlay, out error))
        {
            GD.PrintErr($"[OverlayCoordinator] 无法以可恢复方式关闭卡牌奖励：{error}");
            return false;
        }

        if (IsAlive(_cardRewardOverlay))
        {
            error = "卡牌奖励取消回调未注销 overlay。";
            GD.PrintErr($"[OverlayCoordinator] {error}");
            return false;
        }
        return true;
    }

    private static bool IsAlive(GodotObject node) => node != null && GodotObject.IsInstanceValid(node);

    private static bool SameInstance(Node first, Node second) =>
        first != null && second != null && first.GetInstanceId() == second.GetInstanceId();

    private static void CloseAndClear(ref Node overlay)
    {
        if (IsAlive(overlay))
            overlay.QueueFree();
        overlay = null;
    }
}
