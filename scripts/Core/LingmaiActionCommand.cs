using System;

/// <summary>Lingmai actions that can be requested by a node page without letting UI mutate RunState directly.</summary>
public enum LingmaiActionKind
{
    Rest,
    Upgrade,
    HealCompanion,
}

/// <summary>Immutable presentation result from a validated Lingmai action request.</summary>
public sealed class LingmaiActionCommandResult
{
    public LingmaiActionCommandResult(LingmaiActionKind action, bool consumed, bool wasNoOp, string feedback)
    {
        Action = action;
        Consumed = consumed;
        WasNoOp = wasNoOp;
        Feedback = feedback ?? "";
    }

    public LingmaiActionKind Action { get; }
    public bool Consumed { get; }
    public bool WasNoOp { get; }
    public string Feedback { get; }
}

/// <summary>
/// Page-scoped core command for a single active Lingmai node. It owns action-consumption flags for that page,
/// while GameManager remains the sole owner of RunState, route progress and permanent deck changes.
/// </summary>
public sealed class LingmaiActionCommand
{
    private readonly GameManager _gameManager;
    private bool _restConsumed;
    private bool _upgradeConsumed;
    private bool _companionHealingConsumed;

    public LingmaiActionCommand(GameManager gameManager)
    {
        _gameManager = gameManager;
    }

    public bool CanRest => !_restConsumed;
    public bool CanUpgrade => !_upgradeConsumed;
    public bool CanHealCompanion => !_companionHealingConsumed;

    /// <summary>Applies the valid full-health no-op as a consumed action so UI never has to special-case HP writes.</summary>
    public bool TryRest(out LingmaiActionCommandResult result, out string error)
    {
        result = null;
        if (!TryValidateActiveLingmai(out error))
            return false;
        if (_restConsumed)
        {
            error = "本次灵脉的休养生息已使用。";
            return false;
        }
        if (_gameManager.PlayerMaxHp <= 0)
        {
            error = "玩家最大生命无效，不能结算休养生息。";
            return false;
        }

        var rest = LingmaiActionRules.ResolveRest(_gameManager.PlayerHp, _gameManager.PlayerMaxHp);
        _gameManager.PlayerHp = rest.ResultingHp;
        _restConsumed = true;
        string feedback = rest.WasAtFullHealth
            ? $"休养生息，气血已满，生命保持 {rest.ResultingHp}/{_gameManager.PlayerMaxHp}；本次行动已使用"
            : $"休养生息，生命 {rest.PreviousHp}/{_gameManager.PlayerMaxHp} → {rest.ResultingHp}/{_gameManager.PlayerMaxHp}";
        _gameManager.LastLingmaiResult = feedback;
        result = new LingmaiActionCommandResult(LingmaiActionKind.Rest, true, rest.WasAtFullHealth, feedback);
        error = "";
        return true;
    }

    /// <summary>Upgrades exactly one permanent-deck card after the active Lingmai context has been validated.</summary>
    public bool TryUpgrade(CardRuntime card, out LingmaiActionCommandResult result, out string error)
    {
        result = null;
        if (!TryValidateActiveLingmai(out error))
            return false;
        if (_upgradeConsumed)
        {
            error = "本次灵脉的精进道行已使用。";
            return false;
        }
        if (card == null || !_gameManager.TryGetUpgradeForCard(card.Info, out var upgradedInfo))
        {
            error = "目标卡牌不可升级或升级定义缺失。";
            return false;
        }

        string oldName = card.Info.Name;
        if (!_gameManager.TryUpgradeCard(card))
        {
            error = "升级卡牌失败，永久牌组未改变。";
            return false;
        }

        _upgradeConsumed = true;
        string feedback = $"精进道行，{oldName} → {upgradedInfo.Name}";
        _gameManager.LastLingmaiResult = feedback;
        result = new LingmaiActionCommandResult(LingmaiActionKind.Upgrade, true, false, feedback);
        error = "";
        return true;
    }

    /// <summary>Heals one explicit roster target. Invalid targets fail before the roster or consumption state changes.</summary>
    public bool TryHealCompanion(string targetMemberId, out LingmaiActionCommandResult result, out string error)
    {
        result = null;
        if (!TryValidateActiveLingmai(out error))
            return false;
        if (_companionHealingConsumed)
        {
            error = "本次灵脉的疗愈道友已使用。";
            return false;
        }
        if (!LingmaiActionRules.TryResolveCompanionHealing(_gameManager.RunState.Party, _gameManager.CharacterId,
                targetMemberId, out var action, out error))
            return false;

        _companionHealingConsumed = action.ActionConsumed;
        _gameManager.LastLingmaiResult = action.Feedback;
        result = new LingmaiActionCommandResult(LingmaiActionKind.HealCompanion,
            action.ActionConsumed, action.Healing.Rest.WasAtFullHealth, action.Feedback);
        return true;
    }

    private bool TryValidateActiveLingmai(out string error)
    {
        error = "";
        if (_gameManager == null || _gameManager.ActiveNode == null ||
            _gameManager.ActiveNode.NodeType != MapGraphNodeType.Lingmai ||
            _gameManager.CurrentState != PlayerState.灵脉中 || _gameManager.ActiveNodeResultSubmitted)
        {
            error = "当前没有可结算行动的活动灵脉节点。";
            return false;
        }
        return true;
    }
}
