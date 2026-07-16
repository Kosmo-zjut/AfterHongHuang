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

    private static Node _mapOverlay;
    private static Node _deckOverlay;
    private static Node _settingsOverlay;
    private static Node _victoryModal;
    private static Node _cardRewardOverlay;

    /// <summary>
    /// Prepares the shared map layer without dismissing an active victory modal.
    /// A TopBar map request is global, so it closes only the optional CardReward child overlay.
    /// </summary>
    public static bool TryPrepareMap(out string error)
    {
        error = "";
        if (!TryDismissCardRewardForGlobalEntry(out error))
            return false;

        CloseAndClear(ref _deckOverlay);
        CloseAndClear(ref _settingsOverlay);
        return true;
    }

    /// <summary>
    /// Prepares a utility overlay. TopBar utilities remain globally available during victory;
    /// they close the optional CardReward child but retain the victory page below.
    /// </summary>
    public static bool TryPrepareUtilityOverlay(string overlayName, out string error)
    {
        error = "";
        if (!TryDismissCardRewardForGlobalEntry(out error))
            return false;

        CloseAndClear(ref _mapOverlay);
        CloseAndClear(ref _deckOverlay);
        CloseAndClear(ref _settingsOverlay);
        return true;
    }

    /// <summary>Registers the currently visible shared map overlay.</summary>
    public static void RegisterMap(Node overlay) => _mapOverlay = overlay;

    /// <summary>Registers the deck overlay after mutual-exclusion checks.</summary>
    public static void RegisterDeck(Node overlay) => _deckOverlay = overlay;

    /// <summary>Registers the settings modal after mutual-exclusion checks.</summary>
    public static void RegisterSettings(Node overlay) => _settingsOverlay = overlay;

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

        CloseAndClear(ref _mapOverlay);
        CloseAndClear(ref _deckOverlay);
        CloseAndClear(ref _settingsOverlay);
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
        if (SameInstance(_settingsOverlay, overlay)) _settingsOverlay = null;
        if (SameInstance(_victoryModal, overlay)) _victoryModal = null;
        if (SameInstance(_cardRewardOverlay, overlay)) _cardRewardOverlay = null;
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
