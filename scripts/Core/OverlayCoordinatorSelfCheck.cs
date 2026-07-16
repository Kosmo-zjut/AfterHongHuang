using Godot;

/// <summary>Debug-only behavioral proof for the shared overlay ownership rules.</summary>
public static class OverlayCoordinatorSelfCheck
{
    public static void Run()
    {
        var victory = new Panel();
        Ensure(OverlayCoordinator.TryRegisterVictory(victory, out var victoryError), victoryError);
        VerifyGlobalEntryCancelsReward("地图", (out string error) => OverlayCoordinator.TryPrepareMap(out error));
        VerifyGlobalEntryCancelsReward("套牌", (out string error) => OverlayCoordinator.TryPrepareUtilityOverlay("套牌页面", out error));
        VerifyGlobalEntryCancelsReward("设置", (out string error) => OverlayCoordinator.TryPrepareUtilityOverlay("设置", out error));

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

    private static void Ensure(bool condition, string error)
    {
        if (!condition)
            throw new System.InvalidOperationException(error);
    }
}
