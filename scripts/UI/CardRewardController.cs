using Godot;
using System.Collections.Generic;

/// <summary>
/// 卡牌奖励弹窗（静态工具类，不依赖 .tscn）。
/// 在传入的 parent 上创建 overlay，居中显示 RewardPlan 候选卡牌。
    /// 每个槽位必须达到 RewardPlan.ChoiceCount 才能完成；UI 不直接写永久牌组。
/// </summary>
public static class CardRewardHelper
{
    private static readonly Dictionary<ulong, System.Action> CancelActions = new();

    /// <summary>关闭指定父节点下仍存在的卡牌奖励 overlay，用于离开战斗场景时的生命周期清理。</summary>
    public static void CloseAllForParent(Node parent)
    {
        if (parent == null)
            return;
        foreach (Node child in parent.GetChildren())
        {
            if (child.Name.ToString().StartsWith("CardRewardOverlay"))
            {
                if (child is Control overlay)
                    CloseOverlay(overlay);
                else
                    child.QueueFree();
            }
        }
    }

    /// <summary>
    /// Cancels a visible reward overlay through the same path as its skip button. Global TopBar
    /// entry points use this instead of freeing the control, so the source reward row is re-enabled.
    /// </summary>
    public static bool TryCancelForGlobalOverlay(Node overlayNode, out string error)
    {
        error = "";
        if (overlayNode is not Control overlay || !GodotObject.IsInstanceValid(overlay))
        {
            error = "当前卡牌奖励 overlay 无效。";
            return false;
        }
        if (!CancelActions.TryGetValue(overlay.GetInstanceId(), out var cancelAction))
        {
            error = "当前卡牌奖励缺少可恢复取消回调。";
            return false;
        }

        cancelAction();
        return true;
    }

    /// <summary>Registers the one cancellation path shared by the skip button and global TopBar entry.</summary>
    internal static void RegisterCancellationForOverlay(Control overlay, System.Action cancelAction)
    {
        if (overlay == null || cancelAction == null)
            throw new System.ArgumentException("卡牌奖励取消回调注册参数无效。");
        CancelActions[overlay.GetInstanceId()] = cancelAction;
    }

    /// <summary>
    /// 显示 GameManager 当前胜利计划中的一个奖励槽。外部传入的伪造计划不会被接受。
    /// </summary>
    public static bool Show(Node parent, RewardPlan plan,
        System.Action onComplete,
        System.Action onCancelled = null)
    {
        var gm = GameManager.Instance;
        string planError = "";
        if (parent == null || gm == null || gm.ActiveNode == null ||
            gm.ActiveBattleVictoryPlan == null || plan == null ||
            !RewardPlanValidator.TryValidate(plan, out planError) ||
            plan.Candidates.Count == 0 || string.IsNullOrWhiteSpace(plan.RewardSlotId))
        {
            GD.PrintErr($"[CardReward] 奖励 overlay 被阻止：{planError ?? "缺少胜利计划"}");
            return false;
        }
        bool ownedPlan = false;
        foreach (var ownedSlot in gm.ActiveBattleVictoryPlan.CardRewards)
            ownedPlan |= ReferenceEquals(ownedSlot, plan);
        if (!ownedPlan)
        {
            GD.PrintErr($"[CardReward] 奖励 overlay 被阻止：槽位不属于当前胜利计划：{plan.RewardSlotId}");
            return false;
        }
        foreach (Node child in parent.GetChildren())
        {
            if (child.Name.ToString().StartsWith("CardRewardOverlay"))
            {
                GD.PrintErr("[CardReward] 已有奖励 overlay，拒绝重复创建。");
                return false;
            }
        }

        if (!gm.TryGetBattleCardRewardProgress(gm.ActiveBattleVictoryPlan, plan,
                out int selectedCount, out int choiceCount, out bool slotComplete,
                out var progressError, out var claimedIndices))
        {
            GD.PrintErr($"[CardReward] 奖励槽状态读取失败：{progressError}");
            return false;
        }
        if (slotComplete)
        {
            GD.PrintErr($"[CardReward] 奖励槽已完成，拒绝再次打开：{plan.RewardSlotId}");
            return false;
        }

        // overlay 放在 TopBar 下方（y=44），不影响顶部栏交互
        var overlay = new Panel();
        overlay.Name = "CardRewardOverlay";
        overlay.SetPosition(new Vector2(0, 44));
        overlay.Size = new Vector2(1920, 1036);
        // CardReward blocks only content-area input. The coordinator's global operation layer
        // remains above it so the shared NodeMapEntry can cancel this overlay recoverably.
        overlay.ZIndex = OverlayCoordinator.CardRewardZIndex;
        // Godot 枚举使用语义值而非本地约定数字；overlay 必须停止输入向下穿透。
        overlay.MouseFilter = Control.MouseFilterEnum.Stop;
        if (!OverlayCoordinator.TryRegisterCardReward(overlay, out var coordinatorError))
        {
            GD.PrintErr($"[CardReward] {coordinatorError}");
            return false;
        }
        parent.AddChild(overlay);

        // 半透明遮罩
        var mask = new ColorRect();
        mask.Color = new Color(0, 0, 0, 0.8f);
        mask.SetPosition(new Vector2(0, 0));
        mask.Size = new Vector2(1920, 1036);
        mask.MouseFilter = Control.MouseFilterEnum.Ignore;
        overlay.AddChild(mask);

        // 标题
        var title = new Label();
        title.Text = plan.DisplayName;
        title.AddThemeFontSizeOverride("font_size", 32);
        title.AddThemeColorOverride("font_color", new Color(0.9f, 0.7f, 0.3f));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.SetPosition(new Vector2(360, 80));
        title.Size = new Vector2(1200, 50);
        overlay.AddChild(title);

        var subtitle = new Label();
        subtitle.Text = plan.Description;
        subtitle.AddThemeFontSizeOverride("font_size", 16);
        subtitle.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        subtitle.SetPosition(new Vector2(360, 130));
        subtitle.Size = new Vector2(1200, 30);
        overlay.AddChild(subtitle);

        // 使用传入的预抽卡牌
        float cardSpacing = 450f;
        float cardW = 400f;
        float cardH = 520f;
        float cardY = 190f;

        Button[] selectBtns = new Button[plan.Candidates.Count];
        RichTextLabel[] descLabels = new RichTextLabel[plan.Candidates.Count];
        var selectedIndices = new System.Collections.Generic.HashSet<int>(claimedIndices);
        bool completed = false;

        void CancelWithoutSettlement()
        {
            if (completed)
                return;
            // Cancelling only restores the source victory-row interaction; it never consumes a reward.
            completed = true;
            CloseOverlay(overlay);
            onCancelled?.Invoke();
        }

        RegisterCancellationForOverlay(overlay, CancelWithoutSettlement);

        float totalWidth = plan.Candidates.Count * 400f + (plan.Candidates.Count - 1) * 50f;
        float cardStart = (1920f - totalWidth) / 2f;
        for (int i = 0; i < plan.Candidates.Count; i++)
        {
            float x = cardStart + i * cardSpacing;
            var cardPanel = new Panel();
            cardPanel.SetPosition(new Vector2(x, cardY));
            cardPanel.Size = new Vector2(cardW, cardH);
            overlay.AddChild(cardPanel);

            var desc = new RichTextLabel();
            desc.BbcodeEnabled = true;
            desc.SetPosition(new Vector2(10, 10));
            desc.Size = new Vector2(cardW - 20, cardH - 80);
            descLabels[i] = desc;
            cardPanel.AddChild(desc);

            var btn = new Button();
            btn.Text = "选 择";
            btn.AddThemeFontSizeOverride("font_size", 18);
            btn.SetPosition(new Vector2(120, cardH - 55));
            btn.Size = new Vector2(160, 40);
            selectBtns[i] = btn;
            cardPanel.AddChild(btn);
        }

        // 跳过只关闭本次 overlay，不改变领取进度；多选槽位可稍后重新打开继续领取。
        var skipBtn = new Button();
        skipBtn.Text = "跳 过";
        skipBtn.AddThemeFontSizeOverride("font_size", 16);
        skipBtn.SetPosition(new Vector2(860, cardY + cardH + 60));
        skipBtn.Size = new Vector2(200, 40);
        skipBtn.Pressed += () =>
        {
            CancelWithoutSettlement();
        };
        overlay.AddChild(skipBtn);

        // 填充卡牌数据
        for (int i = 0; i < plan.Candidates.Count; i++)
        {
            var card = plan.Candidates[i];
            string type = card.Type == CardType.斗击 ? "⚔ 斗击" : "✦ 术法";
            string cost = card.Cost == 0 ? "0费" : $"{card.Cost}费";
            descLabels[i].Text = $"[center][b][font_size=20]{card.Name}[/font_size][/b][/center]\n" +
                                 $"[center]{type} | {cost}[/center]\n\n" +
                                 $"{card.Description.Replace("{0}", card.Value.ToString())}";
        }

        // 选择回调
        skipBtn.Disabled = false;
        for (int i = 0; i < plan.Candidates.Count; i++)
        {
            int idx = i;
            selectBtns[i].Pressed += () =>
            {
                if (completed || selectedIndices.Contains(idx) || selectedCount >= plan.ChoiceCount)
                    return;

                if (!gm.TryClaimBattleCard(gm.ActiveBattleVictoryPlan, plan, idx,
                        out bool slotComplete, out var grantError))
                {
                    GD.PrintErr($"[CardReward] 奖励写入失败：{grantError}");
                    return;
                }

                selectedIndices.Add(idx);
                selectedCount++;
                selectBtns[idx].Disabled = true;
                skipBtn.Disabled = false;
                GD.Print($"[CardReward] 已选择 {plan.Candidates[idx].Name}，槽位进度：{selectedCount}/{choiceCount}");
                if (slotComplete)
                {
                    completed = true;
                    CloseOverlay(overlay);
                    onComplete?.Invoke();
                }
            };
            if (selectedIndices.Contains(idx))
                selectBtns[i].Disabled = true;
        }

        return true;
    }

    private static void CloseOverlay(Control overlay)
    {
        if (overlay != null)
            CancelActions.Remove(overlay.GetInstanceId());
        OverlayCoordinator.Unregister(overlay);
        if (overlay != null && GodotObject.IsInstanceValid(overlay))
            overlay.QueueFree();
    }
}
