using System.Collections.Generic;
using System.Linq;

/// <summary>Persistent party membership for run-level interactions outside battle.</summary>
public sealed class PartyRoster
{
    private readonly List<PartyMember> _members = new();

    /// <summary>Current members in stable roster order.</summary>
    public IReadOnlyList<PartyMember> Members => _members;

    /// <summary>Clears run-local party membership when a new run starts.</summary>
    public void Clear() => _members.Clear();

    /// <summary>Adds a member fixture or future recruited ally to the run roster.</summary>
    public void Add(PartyMember member)
    {
        if (member == null || string.IsNullOrWhiteSpace(member.MemberId))
            throw new System.ArgumentException("队伍成员必须包含稳定 ID。", nameof(member));
        if (_members.Any(existing => existing.MemberId == member.MemberId))
            throw new System.InvalidOperationException($"队伍成员重复：{member.MemberId}");
        _members.Add(member);
    }

    /// <summary>Returns only available, healable allies other than the active player.</summary>
    public IReadOnlyList<PartyMember> GetEligibleHealingTargets(string playerMemberId)
    {
        if (string.IsNullOrWhiteSpace(playerMemberId))
            return System.Array.Empty<PartyMember>();

        return _members.Where(member => member.MemberId != playerMemberId &&
                                        member.IsAvailable && member.CanReceiveLingmaiHealing &&
                                        member.CurrentHp > 0 && member.MaxHp > 0)
            .ToArray();
    }

    /// <summary>
    /// Applies the shared Lingmai heal to one explicitly selected eligible member. The caller owns
    /// node-action consumption; this roster method only mutates the selected party member.
    /// </summary>
    public bool TryApplyLingmaiHealing(string playerMemberId, string targetMemberId,
        out PartyHealingResult result, out string error)
    {
        result = default;
        error = "";
        var target = GetEligibleHealingTargets(playerMemberId)
            .FirstOrDefault(member => member.MemberId == targetMemberId);
        if (target == null)
        {
            error = $"疗愈目标不可用：{targetMemberId}";
            return false;
        }

        var rest = LingmaiActionRules.ResolveRest(target.CurrentHp, target.MaxHp);
        target.CurrentHp = rest.ResultingHp;
        result = new PartyHealingResult(target.MemberId, target.DisplayName, rest);
        return true;
    }
}

/// <summary>Minimal future-facing roster member contract; it does not implement multiplayer.</summary>
public sealed class PartyMember
{
    public string MemberId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public int CurrentHp { get; set; }
    public int MaxHp { get; init; }
    public bool IsAvailable { get; init; } = true;
    public bool CanReceiveLingmaiHealing { get; init; } = true;
}

/// <summary>Stable result returned after healing one real party target.</summary>
public readonly record struct PartyHealingResult(string MemberId, string DisplayName, LingmaiRestResult Rest);
