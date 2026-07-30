using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 全局游戏状态管理器（Autoload 单例）。
/// 负责管理玩家状态、卡组、道痕、敌人状态，以及场景切换。
/// </summary>
public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; }

    // ==================== 角色选择 ====================
    public string CharacterId { get; set; }
    public CharacterInfo SelectedCharacter { get; set; }
    public string Difficulty { get; set; } = "地仙";

    // ==================== RunState / BattleState ====================
    private readonly RunState _runState = new();
    private readonly NavigationState _navigationState = new();
    private readonly List<CardRuntime> _emptyBattleCards = new();
    private BattleState _activeBattle;
    private int _nextNodeResultSequence;
    private bool _activeResultSubmitted;
    private BattleVictoryPlan _activeBattleVictoryPlan;
    private BattleVictoryPlan _activeBattleVictorySnapshot;
    private BattleVictoryPlan _pendingBattleVictoryPlan;
    private BattleVictoryPlan _pendingBattleVictorySnapshot;
    private VictorySnapshot _victoryTransactionSnapshot;
    private readonly Dictionary<string, HashSet<int>> _claimedRewardCandidates = new();
    private readonly List<CardExecutionTraceEntry> _lastCardExecutionTrace = new();
    private ResolvedCardExecution _lastResolvedCardExecution;
#if DEBUG
    private bool _injectBattleExitFailureForSelfCheck;
    private bool _injectBattleVictoryCommitFailureForSelfCheck;
#endif

    /// <summary>当前一局的持久状态。战斗离场时该对象继续存在。</summary>
    public RunState RunState => _runState;

    /// <summary>当前活动节点上下文；未进入节点时为空。</summary>
    public NodeContext ActiveNode { get; private set; }

    /// <summary>当前节点结果是否已提交；节点页 overlay 用它区分只读与可推进地图。</summary>
    public bool ActiveNodeResultSubmitted => _activeResultSubmitted;

    /// <summary>当前战斗的显式遭遇请求；Map 负责创建，Battle 只消费。</summary>
    public EncounterRequest ActiveEncounter { get; private set; }

    /// <summary>当前战斗临时状态；战斗结束/离场后销毁。</summary>
    public BattleState ActiveBattle => _activeBattle;
    /// <summary>最近一次出牌实际消费的有序效果，日志和自动化可用来核对执行事实。</summary>
    public IReadOnlyList<CardExecutionTraceEntry> LastCardExecutionTrace => _lastCardExecutionTrace;
    public ResolvedCardExecution LastResolvedCardExecution => _lastResolvedCardExecution;

    /// <summary>当前胜利结算持有的唯一奖励计划；未完成胜利事务时为空。</summary>
    public BattleVictoryPlan ActiveBattleVictoryPlan => _activeBattleVictoryPlan;

    /// <summary>按当前 RunState 派生独立命名随机流；每次访问都不共享可变 RNG。</summary>
    public StableRandomStreams RandomStreams =>
        new StableRandomStreams(_runState.RunSeed, _runState.ActId, _runState.RuleVersion);

    // 永久角色属性与资源。
    public int PlayerMaxHp { get => _runState.PlayerMaxHp; set => _runState.PlayerMaxHp = value; }
    public int PlayerHp { get => _runState.PlayerHp; set => _runState.PlayerHp = Mathf.Clamp(value, 0, PlayerMaxHp); }
    public int PlayerMaxLingli { get => _runState.PlayerMaxLingli; set => _runState.PlayerMaxLingli = value; }
    public int LingYun { get => _runState.LingYun; set => _runState.LingYun = value; }
    public int UnclaimedLingYun { get => _runState.UnclaimedLingYun; set => _runState.UnclaimedLingYun = value; }

    // 当前灵力、护体、斗劲、易损/永炎均只存在于 ActiveBattle。
    public int PlayerLingli { get => _activeBattle?.PlayerLingli ?? 0; set { if (_activeBattle != null) _activeBattle.PlayerLingli = value; } }
    public int PlayerDoujin { get => _activeBattle?.PlayerDoujin ?? 0; set { if (_activeBattle != null) _activeBattle.PlayerDoujin = value; } }
    public int PlayerHuti { get => _activeBattle?.PlayerHuti ?? 0; set { if (_activeBattle != null) _activeBattle.PlayerHuti = value; } }
    public int PlayerYirongCeng { get => _activeBattle?.PlayerYirongCeng ?? 0; set { if (_activeBattle != null) _activeBattle.PlayerYirongCeng = value; } }
    public int PlayerYongyan { get => _activeBattle?.PlayerYongyan ?? 0; set { if (_activeBattle != null) _activeBattle.PlayerYongyan = value; } }

    // 永久牌组只存 RunState；以下牌堆属性仅为 Battle 旧调用点提供兼容访问。
    public List<CardRuntime> DrawPile
    {
        get => _activeBattle?.DrawPile ?? _runState.PermanentDeck;
        set
        {
            if (_activeBattle != null) ReplaceList(_activeBattle.DrawPile, value);
            else ReplaceList(_runState.PermanentDeck, value);
        }
    }
    public List<CardRuntime> Hand => _activeBattle?.Hand ?? _emptyBattleCards;
    public List<CardRuntime> DiscardPile => _activeBattle?.DiscardPile ?? _emptyBattleCards;
    public List<CardRuntime> ExhaustPile => _activeBattle?.ExhaustPile ?? _emptyBattleCards;

    // 道痕和地图进度属于 RunState。
    public List<DaoMarkInfo> DaoMarks => _runState.DaoMarks;
    /// <summary>Queries real non-self allies eligible for the Lingmai healing action.</summary>
    public IReadOnlyList<PartyMember> GetEligibleLingmaiHealingTargets() =>
        _runState.Party.GetEligibleHealingTargets(CharacterId);
    public bool DaoMarkSelected { get => _runState.DaoMarkSelected; set => _runState.DaoMarkSelected = value; }
    public List<DaoMarkInfo> CurrentChoices
    {
        get => _runState.CurrentChoices;
        set => ReplaceList(_runState.CurrentChoices, value);
    }
    public bool MapNodesUnlocked { get => _runState.MapNodesUnlocked; set => _runState.MapNodesUnlocked = value; }
    public int CurrentMapLayer { get => _runState.CurrentMapLayer; set => _runState.CurrentMapLayer = value; }
    public int CurrentMapIndex { get => _runState.CurrentMapIndex; set => _runState.CurrentMapIndex = value; }
    public string CurrentMapNodeId { get => _runState.CurrentMapNodeId; set => _runState.CurrentMapNodeId = value; }
    public MapGraph MapGraph => _runState.MapGraph;
    /// <summary>迁移期导航兼容标记；正式 MapScene 入口读取 PendingMapEntry。</summary>
    public bool OpenMapOnEnter
    {
        get => _navigationState.OpenMapOnEnter;
        set
        {
            _navigationState.OpenMapOnEnter = value;
            if (value)
                _navigationState.MapEntryMode = MapEntryMode.OpenInteractiveMap;
            else if (_navigationState.MapEntryMode == MapEntryMode.OpenInteractiveMap)
                _navigationState.MapEntryMode = MapEntryMode.None;
        }
    }

    /// <summary>MapScene 的显式进入目标，不依赖 deferred 回调时序。</summary>
    public MapEntryMode PendingMapEntry => _navigationState.MapEntryMode;

    /// <summary>
    /// 在 MapScene 已成功构建目标页面后消费一次进入请求。消费失败不会清除请求，便于显式重试。
    /// </summary>
    public bool TryCompleteMapEntry(MapEntryMode entryMode, out string error)
    {
        error = "";
        if (_navigationState.MapEntryMode != entryMode)
        {
            error = $"地图进入请求不匹配：期望 {entryMode}，实际 {_navigationState.MapEntryMode}。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        _navigationState.MapEntryMode = MapEntryMode.None;
        _navigationState.OpenMapOnEnter = false;
        return true;
    }
    /// <summary>迁移期灵脉结果展示标记，正式结果仍以节点提交为准。</summary>
    public string LastLingmaiResult { get => _navigationState.LastLingmaiResult; set => _navigationState.LastLingmaiResult = value; }
    public HashSet<string> VisitedNodeIds => _runState.CompletedNodeIds;

    // 敌人状态也只属于当前战斗。
    public int EnemyHp { get => _activeBattle?.EnemyHp ?? 0; set { if (_activeBattle != null) _activeBattle.EnemyHp = value; } }
    public int EnemyMaxHp { get => _activeBattle?.EnemyMaxHp ?? 0; set { if (_activeBattle != null) _activeBattle.EnemyMaxHp = value; } }
    public int EnemyHuti { get => _activeBattle?.EnemyHuti ?? 0; set { if (_activeBattle != null) _activeBattle.EnemyHuti = value; } }
    public int EnemyYongyan { get => _activeBattle?.EnemyYongyan ?? 0; set { if (_activeBattle != null) _activeBattle.EnemyYongyan = value; } }
    public int EnemyYirong { get => _activeBattle?.EnemyYirong ?? 0; set { if (_activeBattle != null) _activeBattle.EnemyYirong = value; } }
    public bool EnemyMechanicActive { get => _activeBattle?.EnemyMechanicActive ?? false; set { if (_activeBattle != null) _activeBattle.EnemyMechanicActive = value; } }
    public int EnemyTurnIndex { get => _activeBattle?.EnemyTurnIndex ?? 0; set { if (_activeBattle != null) _activeBattle.EnemyTurnIndex = value; } }
    public bool IsPlayerTurn { get => _activeBattle?.IsPlayerTurn ?? false; set { if (_activeBattle != null) _activeBattle.IsPlayerTurn = value; } }
    public bool BattleOver { get => _activeBattle?.BattleOver ?? false; set { if (_activeBattle != null) _activeBattle.BattleOver = value; } }
    public bool PlayerWon { get => _activeBattle?.PlayerWon ?? false; set { if (_activeBattle != null) _activeBattle.PlayerWon = value; } }

    /// <summary>当前流程状态。只有空闲状态允许地图推进。</summary>
    public PlayerState CurrentState { get; set; } = PlayerState.空闲;

    public override void _EnterTree()
    {
        Instance = this;
        GD.Print("[GameManager] Autoload Instance 已初始化（_EnterTree）");
#if DEBUG
        try
        {
            StateContractSelfCheck.Run();
            MapGraphSelfCheck.Run();
            B2EncounterProductionSelfCheck.Run();
            BattleContentBindingSelfCheck.Run();
            EnemyDefinitionExecutionSelfCheck.Run();
            PreparedIntentSelfCheck.Run();
            EnemyTurnTransactionSelfCheck.Run();
            RewardBindingSelfCheck.Run();
            CharacterSelectionBindingSelfCheck.Run();
            RewardStateMachineSelfCheck.Run();
            BattleVictorySettlementCommandSelfCheck.Run();
            CardCatalogSelfCheck.Run();
            CardExecutionC7SelfCheck.Run();
            NodeContentBindingSelfCheck.Run();
            EventEffectSelfCheck.Run();
            OverlayCoordinatorSelfCheck.Run();
            LingmaiInteractionSelfCheck.Run();
            ShopPurchaseCommandSelfCheck.Run();
            NodePageNavigationSelfCheck.Run();
        }
        catch (System.Exception error)
        {
            GD.PrintErr($"[GameManager] 状态契约自检失败：{error.Message}");
            GetTree().Quit(1);
        }
#endif
    }

    // ==================== 初始化角色 ====================

    /// <summary>
    /// 开始一局新游戏并清理全部旧的节点/战斗上下文。
    /// 普通进入战斗、灵脉或返回地图不得调用该方法，以保证生命和永久牌组跨节点保留。
    /// </summary>
    public bool StartNewRun(string characterId) => StartNewRun(characterId, (ulong)GD.Randi());

    /// <summary>使用显式 run seed 开始新局，供确定性测试和后续发布复现使用。</summary>
    public bool StartNewRun(string characterId, ulong runSeed)
    {
        if (!DataDefs.TryGetCharacterDefinition(characterId, out var character))
        {
            GD.PrintErr($"[GameManager] 未找到角色：{characterId}");
            return false;
        }

        if (!CharacterDeckFactory.TryCreate(character, out var starterDeck, out var starterDeckError))
        {
            GD.PrintErr($"[GameManager] 新局牌组解析失败：{starterDeckError}");
            return false;
        }

        // No visible run state changes until both the character and its Catalog-backed deck exist.
        CharacterId = characterId;
        SelectedCharacter = character;

        DisposeBattleState();
        ActiveNode = null;
        ActiveEncounter = null;
        _activeBattleVictoryPlan = null;
        _pendingBattleVictoryPlan = null;
        _pendingBattleVictorySnapshot = null;
        _activeBattleVictorySnapshot = null;
        _victoryTransactionSnapshot = null;
        _claimedRewardCandidates.Clear();
        _activeResultSubmitted = false;

        _runState.RunSeed = runSeed;
        _runState.ActId = ActDefinition.Act1.ActId;
        _runState.RuleVersion = ActDefinition.Act1.RuleVersion;
        var graphGenerator = new MapGraphGenerator();
        if (!graphGenerator.TryGenerate(runSeed, ActDefinition.Act1, out var generatedGraph, out _, out var graphError))
        {
            GD.PrintErr($"[GameManager] 新局地图生成失败，阻止进入：{graphError}");
            return false;
        }
        _runState.MapGraph = generatedGraph;
        _runState.PermanentDeck.Clear();
        _runState.DaoMarks.Clear();
        _runState.Party.Clear();
        _runState.CurrentChoices.Clear();
        _runState.NodeStates.Clear();
        _runState.CompletedNodeIds.Clear();
        _runState.AppliedResultIds.Clear();
        _runState.AppliedRouteResultIds.Clear();
        _nextNodeResultSequence = 0;
        PlayerMaxHp = SelectedCharacter.MaxHp;
        PlayerHp = SelectedCharacter.MaxHp;
        PlayerMaxLingli = SelectedCharacter.MaxLingli;
        LingYun = 0;
        UnclaimedLingYun = 0;
        Difficulty = SelectedCharacter.Difficulty;

        // 初始化牌组（根据角色定义加载）；缺失牌组已在状态变更前显式拒绝。
        _runState.PermanentDeck.AddRange(starterDeck);

        DaoMarkSelected = false;
        MapNodesUnlocked = false;
        CurrentMapLayer = 0;
        CurrentMapIndex = 0;
        CurrentMapNodeId = generatedGraph.Layers[0][0].NodeId;
        OpenMapOnEnter = false;
        LastLingmaiResult = "";
        CurrentState = PlayerState.空闲;
        return true;
    }

    /// <summary>只洗当前 BattleState 抽牌堆；永久套牌顺序不由战斗抽牌流程修改。</summary>
    public void ShuffleBattleDrawPile()
    {
        if (_activeBattle == null)
        {
            GD.PrintErr("[GameManager] 战斗洗牌失败：当前没有活动战斗。");
            return;
        }

        var rng = RandomStreams.CreateCombatStream(ActiveNode.NodeId, _activeBattle.ShuffleIndex);
        _activeBattle.ShuffleIndex++;
        rng.Shuffle(_activeBattle.DrawPile);
    }

    // ==================== 抽牌逻辑 ====================

    public void DrawCards(int count)
    {
        if (_activeBattle == null)
        {
            GD.PrintErr("[GameManager] 抽牌失败：当前没有活动战斗状态。");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            if (_activeBattle.DrawPile.Count == 0)
            {
                if (_activeBattle.DiscardPile.Count == 0) break;
                _activeBattle.DrawPile.AddRange(_activeBattle.DiscardPile);
                _activeBattle.DiscardPile.Clear();
                ShuffleBattleDrawPile();
            }
            var card = _activeBattle.DrawPile[0];
            _activeBattle.DrawPile.RemoveAt(0);
            _activeBattle.Hand.Add(card);
        }
    }

    public void StartPlayerTurn()
    {
        IsPlayerTurn = true;
        if (_activeBattle != null)
        {
            _activeBattle.PlayerAttackedThisTurn = false;
            _activeBattle.LastPlayerTurnHadAttack = false;
            _activeBattle.PreparedEnemyIntent = null;
            _activeBattle.IntentStateVersion++;
        }
        PlayerHuti = 0;
        PlayerLingli = PlayerMaxLingli;
        DrawCards(5);
    }

    // ==================== 出牌逻辑 ====================

    public bool PlayCard(CardRuntime card)
    {
        if (!Hand.Contains(card)) return false;
        if (!TryPrepareCardExecution(card, out var resolved, out var prepareError))
        {
            GD.PrintErr($"[GameManager] 出牌被阻止：{prepareError}");
            return false;
        }
        return PlayCard(card, resolved);
    }

    /// <summary>仅消费玩家确认时冻结的同一解析对象；不允许执行端重新计算卡牌事实。</summary>
    public bool PlayCard(CardRuntime card, ResolvedCardExecution resolved)
    {
        if (!TryValidateResolvedCardForPlay(card, resolved, out var validationError))
        {
            GD.PrintErr($"[GameManager] 出牌被阻止：{validationError}");
            return false;
        }

        var snapshot = EnemyTurnSnapshot.Capture(this);
        var previousTrace = new List<CardExecutionTraceEntry>(_lastCardExecutionTrace);
        var previousResolved = _lastResolvedCardExecution;
        try
        {
            // The frozen fingerprint is validated before this deduction. Applying the fee is
            // therefore part of the same transaction, not a reason to invalidate its own plan.
            PlayerLingli -= resolved.Plan.EnergyCost;
            if (!TryApplyResolvedCard(card, resolved, out var exhausts, out var executionError))
            {
                snapshot.Restore(this);
                RestoreCardExecutionDiagnostics(previousTrace, previousResolved);
                GD.PrintErr($"[GameManager] 出牌执行被阻止：{executionError}");
                return false;
            }

            // 出牌可能改变敌方护体破盾/当前效果条件，下一次 UI 刷新必须重新解析预告。
            if (_activeBattle != null)
                _activeBattle.PreparedEnemyIntent = null;

            Hand.Remove(card);
            if (exhausts)
                ExhaustPile.Add(card);
            else
                DiscardPile.Add(card);
            if (_activeBattle != null)
                _activeBattle.IntentStateVersion++;
            return true;
        }
        catch (Exception exception)
        {
            snapshot.Restore(this);
            RestoreCardExecutionDiagnostics(previousTrace, previousResolved);
            GD.PrintErr($"[GameManager] 出牌事务异常，已回滚：{exception.Message}");
            return false;
        }
    }

    private bool TryValidateResolvedCardForPlay(CardRuntime card, ResolvedCardExecution resolved,
        out string error)
    {
        error = "";
        if (_activeBattle == null)
        {
            error = "当前没有活动战斗。";
            return false;
        }
        if (card == null || !Hand.Contains(card))
        {
            error = "卡牌不在当前手牌。";
            return false;
        }
        if (resolved == null || resolved.Plan == null)
        {
            error = "缺少目标确认时冻结的执行计划。";
            return false;
        }
        if (!ReferenceEquals(resolved.Plan, card.ExecutionPlan))
        {
            error = "执行计划不属于当前卡牌实例。";
            return false;
        }
        if (card.Info == null || !string.Equals(card.Info.DefinitionId ?? card.Info.Id,
                resolved.Plan.CardId, StringComparison.Ordinal))
        {
            error = "卡牌兼容投影与执行计划身份不匹配。";
            return false;
        }
        if (!CardDefinitionValidator.TryValidateExecutionPlan(resolved.Plan, out var planError))
        {
            error = $"执行计划校验失败：{planError}";
            return false;
        }
        if (resolved.Plan.EnergyCost > PlayerLingli)
        {
            error = "当前灵力不足。";
            return false;
        }
        if (resolved.Plan.TargetPolicy.Scope == CardTargetScope.SingleEnemy && EnemyHp <= 0)
        {
            error = "敌方目标不可用。";
            return false;
        }
        if (resolved.BattleStateVersion != _activeBattle.IntentStateVersion ||
            resolved.StateFingerprint != BuildCardStateFingerprint())
        {
            error = "出牌解析结果已失效，拒绝执行旧计划。";
            return false;
        }
        return true;
    }

    private void RestoreCardExecutionDiagnostics(IReadOnlyList<CardExecutionTraceEntry> trace,
        ResolvedCardExecution resolved)
    {
        _lastCardExecutionTrace.Clear();
        if (trace != null)
            _lastCardExecutionTrace.AddRange(trace);
        _lastResolvedCardExecution = resolved;
    }

    /// <summary>将目标条件和当前战斗快照冻结为唯一执行事实，UI 确认提示可直接消费 Summary。</summary>
    public bool TryPrepareCardExecution(CardRuntime card, out ResolvedCardExecution resolved, out string error)
    {
        resolved = null;
        error = "";
        if (_activeBattle == null)
        {
            error = "当前没有活动战斗。";
            return false;
        }
        if (card == null || !Hand.Contains(card))
        {
            error = "卡牌不在当前手牌。";
            return false;
        }
        if (card == null || card.ExecutionPlan == null || card.Info == null)
        {
            error = "卡牌缺少已验证执行计划。";
            return false;
        }
        var plan = card.ExecutionPlan;
        if (!string.Equals(card.Info.DefinitionId ?? card.Info.Id, plan.CardId, System.StringComparison.Ordinal))
        {
            error = "卡牌兼容投影与执行计划身份不匹配。";
            return false;
        }
        if (!CardDefinitionValidator.TryValidateExecutionPlan(plan, out var planError))
        {
            error = $"执行计划校验失败：{planError}";
            return false;
        }
        if (plan.TargetPolicy.Scope == CardTargetScope.SingleEnemy && EnemyHp <= 0)
        {
            error = "敌方目标不可用。";
            return false;
        }
        if (plan.TargetPolicy.Scope is not (CardTargetScope.None or CardTargetScope.Self or CardTargetScope.SingleEnemy))
        {
            error = "执行计划包含未支持目标范围。";
            return false;
        }
        resolved = new ResolvedCardExecution(plan, _activeBattle?.IntentStateVersion ?? 0,
            BuildCardStateFingerprint(), CardExecutionPlanFormatter.Format(plan));
        return true;
    }

    /// <summary>按冻结计划的顺序执行效果；旧 CardInfo 聚合字段只供兼容卡面展示。</summary>
    private bool TryApplyResolvedCard(CardRuntime card, ResolvedCardExecution resolved, out bool exhausts, out string error)
    {
        exhausts = false;
        error = "";
        if (resolved == null || card == null || !ReferenceEquals(resolved.Plan, card.ExecutionPlan))
        {
            error = "执行计划不属于当前卡牌实例。";
            return false;
        }
        _lastCardExecutionTrace.Clear();
        foreach (var effect in resolved.Plan.Effects)
        {
            _lastCardExecutionTrace.Add(new CardExecutionTraceEntry
            {
                Order = effect.Order,
                EffectType = effect.EffectType,
                Amount = effect.Amount,
            });
            switch (effect.EffectType)
            {
                case CardEffectKind.LoseHealth:
                    PlayerHp = Mathf.Max(PlayerHp - effect.Amount, 0);
                    break;
                case CardEffectKind.DealDamage:
                    ExecuteDamageEffect(effect.Amount);
                    break;
                case CardEffectKind.GainBlock:
                    PlayerHuti += effect.Amount;
                    break;
                case CardEffectKind.AddStatus:
                    if (!TryApplyCardStatus(effect, out error)) return false;
                    break;
                case CardEffectKind.MoveSelfToZone when effect.DestinationZone == CardDestinationZone.Exhaust:
                    exhausts = true;
                    break;
                default:
                    error = $"执行计划包含未支持效果：{effect.EffectType}";
                    return false;
            }
        }
        _lastResolvedCardExecution = resolved;
        return true;
    }

    private string BuildCardStateFingerprint() => _activeBattle == null ? "no-battle" :
        $"{_activeBattle.IntentStateVersion}|{PlayerHp}|{PlayerLingli}|{PlayerHuti}|{PlayerDoujin}|{EnemyHp}|{EnemyHuti}|{EnemyYirong}|{EnemyYongyan}|{_activeBattle.EnemyMechanicActive}";

    private void ExecuteDamageEffect(int baseValue)
    {
        float baseDamage = baseValue + PlayerDoujin;
        float damage = baseDamage;

        if (EnemyYirong > 0)
            damage *= 1.5f;

        int finalDamage = Mathf.FloorToInt(damage);

        var mechanic = _activeBattle?.EnemyInfo?.MechanicDefinition;
        if (mechanic != null && _activeBattle.EnemyMechanicActive && mechanic.CounterThreshold > 0 &&
            !string.IsNullOrWhiteSpace(mechanic.CounterKey))
        {
            int counter = _activeBattle.EnemyMechanicCounters.GetValueOrDefault(mechanic.CounterKey) + 1;
            if (counter >= mechanic.CounterThreshold)
            {
                _activeBattle.EnemyMechanicCounters[mechanic.CounterKey] = 0;
                if (mechanic.HasDamageOverride)
                    finalDamage = mechanic.DamageOverride;
                GD.Print($"[{mechanic.DisplayName}] {mechanic.Description}");
            }
            else
                _activeBattle.EnemyMechanicCounters[mechanic.CounterKey] = counter;
        }

        if (EnemyHuti > 0)
        {
            int absorbed = Mathf.Min(EnemyHuti, finalDamage);
            EnemyHuti -= absorbed;
            finalDamage -= absorbed;
            if (EnemyHuti == 0 && _activeBattle != null)
                _activeBattle.EnemyGuardWasBroken = true;
        }

        EnemyHp -= finalDamage;
        EnemyHp = Mathf.Max(EnemyHp, 0);
        if (_activeBattle != null)
            _activeBattle.PlayerAttackedThisTurn = true;

    }

    private bool TryApplyCardStatus(CardEffectDefinition effect, out string error)
    {
        error = "";
        switch (effect.StatusKind)
        {
            case CardStatusKind.Strength when effect.TargetSelector == CardEffectTarget.Self:
                PlayerDoujin += effect.Amount;
                return true;
            case CardStatusKind.Vulnerable when effect.TargetSelector == CardEffectTarget.SelectedTarget:
                EnemyYirong += effect.Amount;
                return true;
            case CardStatusKind.EternalFlame when effect.TargetSelector == CardEffectTarget.SelectedTarget:
                EnemyYongyan += effect.Amount;
                return true;
            default:
                error = $"状态效果目标或类型不受支持：{effect.StatusKind}/{effect.TargetSelector}";
                return false;
        }
    }

    // ==================== 回合结束逻辑 ====================

    public void EndPlayerTurn()
    {
        EndPlayerTurnEffects(updateAttackHistory: true);
    }

    private void EndPlayerTurnEffects(bool updateAttackHistory)
    {
        IsPlayerTurn = false;
        if (updateAttackHistory && _activeBattle != null)
            _activeBattle.LastPlayerTurnHadAttack = _activeBattle.PlayerAttackedThisTurn;
        // 剩余手牌全部弃掉
        DiscardPile.AddRange(Hand);
        Hand.Clear();
        // 护体保留到下个自己回合开始（StartPlayerTurn 清零），以抵挡怪物攻击

        if (PlayerYongyan > 0)
        {
            PlayerHp -= PlayerYongyan;
            PlayerHp = Mathf.Max(PlayerHp, 0);
        }
    }

    /// <summary>
    /// 原子提交玩家结束回合与敌方行动。EndPlayerTurn 会先改变牌堆和永炎，
    /// 所以整个过渡必须包在同一快照内；任何 Prepare/Execute/结束效果失败都完整回滚。
    /// </summary>
    public bool TryCommitEnemyTurn(out string error)
    {
        return TryCommitEnemyTurn(out _, out error);
    }

    /// <summary>提交敌方回合并返回实际消费的同一解析对象，供日志和 UI 绑定最终事实。</summary>
    public bool TryCommitEnemyTurn(out ResolvedEnemyIntent executedIntent, out string error)
    {
        executedIntent = null;
        error = "";
        if (_activeBattle == null || !_activeBattle.IsPlayerTurn || _activeBattle.BattleOver)
        {
            error = "当前不在可提交的玩家回合。";
            GD.PrintErr($"[GameManager] 敌方回合提交被阻止：{error}");
            return false;
        }

        var snapshot = EnemyTurnSnapshot.Capture(this);
        if (!TryCommitEnemyTurnInternal(out executedIntent, out error))
        {
            snapshot.Restore(this);
            GD.PrintErr($"[GameManager] 敌方回合事务已回滚：{error}");
            return false;
        }
        return true;
    }

    private bool TryCommitEnemyTurnInternal(out ResolvedEnemyIntent executedIntent, out string error)
    {
        executedIntent = null;
        error = "";
        // 预告必须先冻结；结束玩家回合只处理牌堆/永炎，不能先改写影响该预告身份的攻击统计。
        if (!TryPrepareEnemyIntent(out var prepared, out error))
            return false;
        EndPlayerTurnEffects(updateAttackHistory: false);
        if (!TryExecutePreparedEnemyIntent(prepared, out error))
            return false;
        executedIntent = prepared;
        if (_activeBattle != null)
            _activeBattle.LastPlayerTurnHadAttack = _activeBattle.PlayerAttackedThisTurn;
        EndEnemyTurn();
        return true;
    }

    /// <summary>兼容旧调用点；战斗流程应使用 TryExecuteEnemyTurn 获取失败原因。</summary>
    public void ExecuteEnemyTurn()
    {
        if (!TryExecuteEnemyTurn(out var error))
            GD.PrintErr($"[GameManager] 敌人回合未执行：{error}");
    }

    /// <summary>
    /// 解析并事务性执行当前敌方意图。所有定义和组合先校验，成功后才清理回合护体、
    /// 应用伤害/护体/机制/易损并推进阶段游标；失败会恢复完整战斗快照。
    /// </summary>
    public bool TryExecuteEnemyTurn(out string error)
    {
        if (!TryPrepareEnemyIntent(out var prepared, out error))
            return false;
        return TryExecutePreparedEnemyIntent(prepared, out error);
    }

    /// <summary>执行已冻结的最终意图；此入口不重新解释条件伤害或玩家上一回合状态。</summary>
    public bool TryExecutePreparedEnemyIntent(ResolvedEnemyIntent prepared, out string error)
    {
        error = "";
        if (_activeBattle?.EnemyInfo == null || prepared?.Intent == null)
        {
            error = "缺少活动敌人或已解析意图。";
            return false;
        }
        if (_activeBattle.PreparedEnemyIntent != prepared ||
            prepared.StateVersion != _activeBattle.IntentStateVersion)
        {
            error = "敌方意图缓存已过期，拒绝执行旧解析结果。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        var info = _activeBattle.EnemyInfo;
        string expectedIntentId = $"{(prepared.Intent.SequenceId ?? "base")}:{prepared.Intent.SequenceIndex}";
        if (prepared.EnemyDefinition != info || prepared.EnemyId != info.Id ||
            prepared.EnemyHp != _activeBattle.EnemyHp ||
            prepared.EnemyPhase != _activeBattle.EnemyPhase ||
            prepared.EnemySpecialTriggered != _activeBattle.EnemySpecialTriggered ||
            prepared.PhaseId != _activeBattle.EnemyPhaseId ||
            prepared.PhaseTurnIndex != _activeBattle.EnemyPhaseTurnIndex ||
            prepared.PhaseOpeningTurnIndex != _activeBattle.EnemyPhaseOpeningTurnIndex ||
            prepared.OpeningTurnIndex != _activeBattle.EnemyOpeningTurnIndex ||
            prepared.IntentId != expectedIntentId ||
            prepared.PlayerHuti != PlayerHuti || prepared.EnemyHuti != EnemyHuti ||
            prepared.EnemyGuardWasBroken != _activeBattle.EnemyGuardWasBroken ||
            prepared.EnemyMechanicActive != _activeBattle.EnemyMechanicActive ||
            prepared.PlayerAttackedThisTurn != _activeBattle.PlayerAttackedThisTurn ||
            prepared.LastPlayerTurnHadAttack != _activeBattle.LastPlayerTurnHadAttack ||
            prepared.MechanicStateFingerprint != BuildMechanicStateFingerprint())
        {
            error = "敌方意图身份或阶段游标已变化，拒绝执行旧解析结果。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }
        if (!EnemyDefinitionValidator.TryValidate(info, out error) ||
            !TryValidateResolvedEnemyIntent(info, prepared.Intent, out error))
        {
            GD.PrintErr($"[GameManager] 已解析意图校验失败，阻止本次行动：{error}");
            return false;
        }

        var snapshot = EnemyTurnSnapshot.Capture(this);
        try
        {
            var intent = prepared.Intent;
            if (intent.IsPhaseEntry)
            {
                _activeBattle.EnemyPhaseId = intent.SequenceId;
                _activeBattle.EnemyPhase = intent.PhaseIndex;
                _activeBattle.EnemyPhaseTurnIndex = 0;
                _activeBattle.EnemyPhaseOpeningTurnIndex = 0;
                _activeBattle.EnemyPhaseOpeningCompleted = !intent.IsOpeningIntent;
                if (intent.ResetGuardOnPhaseEnter)
                    EnemyHuti = 0;
            }

            EnemyHuti = 0;
            if (intent.IntentType == EnemyIntentType.攻击)
            {
                int actualDamage = prepared.FinalDamage;
                if (PlayerHuti > 0)
                {
                    int absorbed = Mathf.Min(PlayerHuti, actualDamage);
                    PlayerHuti -= absorbed;
                    actualDamage -= absorbed;
                }
                PlayerHp -= actualDamage;
                _activeBattle.LastEnemyActualDamage = actualDamage;
            }
            else
                _activeBattle.LastEnemyActualDamage = 0;

            if (intent.MechanicAction != EnemyMechanicAction.None)
            {
                _activeBattle.EnemyMechanicActive = true;
                if (!string.IsNullOrWhiteSpace(info.MechanicDefinition.CounterKey))
                    _activeBattle.EnemyMechanicCounters[info.MechanicDefinition.CounterKey] = 0;
            }
            EnemyHuti += intent.GuardValue + intent.MechanicGuardValue;
            PlayerYirongCeng += intent.VulnerableValue;

            if (intent.IsMechanicTrigger)
            {
                _activeBattle.EnemySpecialTriggered = true;
                if (intent.ClearGuardBrokenAfterExecute)
                    _activeBattle.EnemyGuardWasBroken = false;
            }

            AdvanceEnemyIntentCursor(info, intent);
            EnemyTurnIndex++;
            _activeBattle.LastExecutedEnemyIntent = prepared;
            _activeBattle.PreparedEnemyIntent = null;
            _activeBattle.IntentStateVersion++;
            return true;
        }
        catch (System.Exception exception)
        {
            snapshot.Restore(this);
            error = $"敌方意图执行事务异常：{exception.Message}";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }
    }

    /// <summary>解析并缓存当前敌方回合意图，UI 预告和正式执行共享同一实例。</summary>
    public bool TryPrepareEnemyIntent(out ResolvedEnemyIntent intent, out string error)
    {
        intent = null;
        error = "";
        if (_activeBattle?.EnemyInfo == null)
        {
            error = "缺少活动敌人定义。";
            return false;
        }
        if (_activeBattle.PreparedEnemyIntent != null &&
            _activeBattle.PreparedEnemyIntent.StateVersion == _activeBattle.IntentStateVersion)
        {
            intent = _activeBattle.PreparedEnemyIntent;
            return true;
        }
        if (!EnemyDefinitionValidator.TryValidate(_activeBattle.EnemyInfo, out error) ||
            !_activeBattle.EnemyInfo.TryResolveIntent(EnemyTurnIndex, _activeBattle, out var baseIntent, out error))
        {
            GD.PrintErr($"[GameManager] 敌方意图预告解析失败：{error}");
            intent = null;
            return false;
        }
        if (!TryResolveFinalEnemyIntent(baseIntent, out intent, out error))
        {
            GD.PrintErr($"[GameManager] 敌方最终意图解析失败：{error}");
            return false;
        }
        _activeBattle.PreparedEnemyIntent = intent;
        return true;
    }

    private bool TryResolveFinalEnemyIntent(EnemyIntent baseIntent, out ResolvedEnemyIntent resolved, out string error)
    {
        resolved = null;
        error = "";
        if (baseIntent == null || _activeBattle == null)
        {
            error = "最终意图解析缺少基础意图或战斗状态。";
            return false;
        }
        if (!TryValidateResolvedEnemyIntent(_activeBattle.EnemyInfo, baseIntent, out error))
            return false;

        int guardAtSnapshot = baseIntent.IsPhaseEntry && baseIntent.ResetGuardOnPhaseEnter ? 0 : EnemyHuti;
        bool conditionTriggered = false;
        int finalDamage = baseIntent.IntentType == EnemyIntentType.攻击 ? baseIntent.Value : 0;
        bool playerAttacked = _activeBattle.PlayerAttackedThisTurn || _activeBattle.LastPlayerTurnHadAttack;
        if (baseIntent.IntentType == EnemyIntentType.攻击 && baseIntent.BonusIfNoPlayerAttack > 0 &&
            !playerAttacked)
            finalDamage += baseIntent.BonusIfNoPlayerAttack;
        if (baseIntent.AlternateDamageCondition == EnemyAlternateDamageCondition.GuardBrokenOrEmpty)
        {
            conditionTriggered = _activeBattle.EnemyGuardWasBroken || guardAtSnapshot == 0;
            finalDamage = conditionTriggered ? baseIntent.AlternateValue : baseIntent.Value;
        }
        if (baseIntent.AlternateDamageCondition != EnemyAlternateDamageCondition.None &&
            (string.IsNullOrWhiteSpace(baseIntent.DamageConditionId) ||
             string.IsNullOrWhiteSpace(baseIntent.DamageConditionDisplayName)))
        {
            error = $"意图 {baseIntent.Name} 缺少条件显示定义。";
            return false;
        }

        resolved = new ResolvedEnemyIntent
        {
            Intent = baseIntent,
            EnemyDefinition = _activeBattle.EnemyInfo,
            EnemyId = _activeBattle.EnemyInfo?.Id,
            EnemyHp = _activeBattle.EnemyHp,
            EnemyPhase = _activeBattle.EnemyPhase,
            EnemySpecialTriggered = _activeBattle.EnemySpecialTriggered,
            PhaseId = _activeBattle.EnemyPhaseId ?? string.Empty,
            PhaseTurnIndex = _activeBattle.EnemyPhaseTurnIndex,
            PhaseOpeningTurnIndex = _activeBattle.EnemyPhaseOpeningTurnIndex,
            OpeningTurnIndex = _activeBattle.EnemyOpeningTurnIndex,
            IntentId = $"{(baseIntent.SequenceId ?? "base")}:{baseIntent.SequenceIndex}",
            PlayerHuti = PlayerHuti,
            EnemyHuti = EnemyHuti,
            EnemyGuardWasBroken = _activeBattle.EnemyGuardWasBroken,
            EnemyMechanicActive = _activeBattle.EnemyMechanicActive,
            PlayerAttackedThisTurn = _activeBattle.PlayerAttackedThisTurn,
            LastPlayerTurnHadAttack = _activeBattle.LastPlayerTurnHadAttack,
            MechanicStateFingerprint = BuildMechanicStateFingerprint(),
            ResolutionId = $"{ActiveNode?.NodeId}:{_activeBattle.EnemyInfo?.Id}:{_activeBattle.EnemyPhaseId}:{_activeBattle.EnemyPhaseTurnIndex}:{_activeBattle.IntentStateVersion}",
            StateVersion = _activeBattle.IntentStateVersion,
            FinalDamage = finalDamage,
            DamageConditionTriggered = conditionTriggered,
            DamageConditionId = baseIntent.DamageConditionId,
            DamageConditionDisplayName = baseIntent.DamageConditionDisplayName,
        };
        return true;
    }

    private string BuildMechanicStateFingerprint()
    {
        if (_activeBattle == null || _activeBattle.EnemyMechanicCounters.Count == 0)
            return _activeBattle?.EnemyMechanicActive == true ? "active" : "inactive";

        var parts = new List<string>();
        foreach (var pair in _activeBattle.EnemyMechanicCounters.OrderBy(item => item.Key))
            parts.Add($"{pair.Key}={pair.Value}");
        return $"{(_activeBattle.EnemyMechanicActive ? "active" : "inactive")}|{string.Join(";", parts)}";
    }

    private bool TryValidateResolvedEnemyIntent(EnemyInfo info, EnemyIntent intent, out string error)
    {
        error = "";
        if (intent == null || !System.Enum.IsDefined(typeof(EnemyIntentType), intent.IntentType))
        {
            error = "意图类型未定义。";
            return false;
        }
        if (intent.IntentType == EnemyIntentType.攻击 && intent.Value <= 0)
        {
            error = $"攻击伤害无效：{intent.Value}";
            return false;
        }
        if (intent.Value < 0 || intent.GuardValue < 0 || intent.MechanicGuardValue < 0 ||
            intent.VulnerableValue < 0 || intent.BonusIfNoPlayerAttack < 0 || intent.AlternateValue < 0)
        {
            error = "意图效果数值不能为负。";
            return false;
        }
        if (!System.Enum.IsDefined(typeof(EnemyMechanicAction), intent.MechanicAction) ||
            !System.Enum.IsDefined(typeof(EnemyAlternateDamageCondition), intent.AlternateDamageCondition))
        {
            error = "机制动作或条件伤害类型未定义。";
            return false;
        }
        if (intent.AlternateDamageCondition != EnemyAlternateDamageCondition.None && intent.AlternateValue <= 0)
        {
            error = "条件伤害缺少有效替代值。";
            return false;
        }
        if (intent.AlternateDamageCondition == EnemyAlternateDamageCondition.None && intent.AlternateValue != 0)
        {
            error = "没有条件伤害时不允许存在替代值。";
            return false;
        }
        if (intent.MechanicAction != EnemyMechanicAction.None && info.MechanicDefinition == null)
        {
            error = "意图声明机制动作但缺少机制定义。";
            return false;
        }
        if (intent.IsPhaseEntry && (string.IsNullOrWhiteSpace(intent.SequenceId) || intent.PhaseIndex <= 0))
        {
            error = "阶段入口缺少有效阶段 ID 或索引。";
            return false;
        }
        return true;
    }

    private void AdvanceEnemyIntentCursor(EnemyInfo info, EnemyIntent intent)
    {
        if (intent.IsOpeningIntent)
        {
            if (string.IsNullOrWhiteSpace(intent.SequenceId))
            {
                _activeBattle.EnemyOpeningTurnIndex++;
                if (_activeBattle.EnemyOpeningTurnIndex >= info.OpeningIntents.Count)
                    _activeBattle.EnemyOpeningCompleted = true;
            }
            else
            {
                _activeBattle.EnemyPhaseOpeningTurnIndex++;
                var phase = info.PhaseDefinitions.Find(item => item?.Id == intent.SequenceId);
                if (phase == null || _activeBattle.EnemyPhaseOpeningTurnIndex >= phase.OpeningIntents.Count)
                    _activeBattle.EnemyPhaseOpeningCompleted = true;
            }
            return;
        }

        _activeBattle.EnemyPhaseTurnIndex++;
    }

    /// <summary>敌方定义失败后的流程恢复，不伪造一次已执行回合。</summary>
    public void RestorePlayerTurnAfterEnemyActionFailure()
    {
        if (_activeBattle != null)
            _activeBattle.IsPlayerTurn = true;
    }

    private sealed class EnemyTurnSnapshot
    {
        private int _playerHp;
        private int _playerLingli;
        private int _playerDoujin;
        private int _playerHuti;
        private int _playerYongyan;
        private int _playerYirong;
        private int _enemyHp;
        private int _enemyMaxHp;
        private EnemyInfo _enemyInfo;
        private int _enemyHuti;
        private int _enemyYirong;
        private int _enemyYongyan;
        private bool _mechanicActive;
        private bool _specialTriggered;
        private bool _guardBroken;
        private ResolvedEnemyIntent _preparedIntent;
        private ResolvedEnemyIntent _lastExecutedIntent;
        private int _lastEnemyActualDamage;
        private int _intentStateVersion;
        private bool _isPlayerTurn;
        private bool _battleOver;
        private bool _playerWon;
        private bool _resultSubmitted;
        private bool _playerAttackedThisTurn;
        private bool _lastPlayerTurnHadAttack;
        private int _shuffleIndex;
        private List<CardRuntime> _drawPile;
        private List<CardRuntime> _hand;
        private List<CardRuntime> _discardPile;
        private List<CardRuntime> _exhaustPile;
        private int _enemyTurnIndex;
        private int _phase;
        private string _phaseId;
        private int _phaseTurnIndex;
        private int _phaseOpeningIndex;
        private bool _phaseOpeningCompleted;
        private int _openingIndex;
        private bool _openingCompleted;
        private Dictionary<string, int> _counters;

        public static EnemyTurnSnapshot Capture(GameManager manager)
        {
            var battle = manager._activeBattle;
            return new EnemyTurnSnapshot
            {
                _playerHp = manager.PlayerHp,
                _playerLingli = battle.PlayerLingli,
                _playerDoujin = battle.PlayerDoujin,
                _playerHuti = manager.PlayerHuti,
                _playerYongyan = battle.PlayerYongyan,
                _playerYirong = manager.PlayerYirongCeng,
                _enemyHp = battle.EnemyHp,
                _enemyMaxHp = battle.EnemyMaxHp,
                _enemyInfo = battle.EnemyInfo,
                _enemyHuti = battle.EnemyHuti,
                _enemyYirong = battle.EnemyYirong,
                _enemyYongyan = battle.EnemyYongyan,
                _mechanicActive = battle.EnemyMechanicActive,
                _specialTriggered = battle.EnemySpecialTriggered,
                _guardBroken = battle.EnemyGuardWasBroken,
                _preparedIntent = battle.PreparedEnemyIntent,
                _lastExecutedIntent = battle.LastExecutedEnemyIntent,
                _lastEnemyActualDamage = battle.LastEnemyActualDamage,
                _intentStateVersion = battle.IntentStateVersion,
                _isPlayerTurn = battle.IsPlayerTurn,
                _battleOver = battle.BattleOver,
                _playerWon = battle.PlayerWon,
                _resultSubmitted = battle.ResultSubmitted,
                _playerAttackedThisTurn = battle.PlayerAttackedThisTurn,
                _lastPlayerTurnHadAttack = battle.LastPlayerTurnHadAttack,
                _shuffleIndex = battle.ShuffleIndex,
                _drawPile = new List<CardRuntime>(battle.DrawPile),
                _hand = new List<CardRuntime>(battle.Hand),
                _discardPile = new List<CardRuntime>(battle.DiscardPile),
                _exhaustPile = new List<CardRuntime>(battle.ExhaustPile),
                _enemyTurnIndex = battle.EnemyTurnIndex,
                _phase = battle.EnemyPhase,
                _phaseId = battle.EnemyPhaseId,
                _phaseTurnIndex = battle.EnemyPhaseTurnIndex,
                _phaseOpeningIndex = battle.EnemyPhaseOpeningTurnIndex,
                _phaseOpeningCompleted = battle.EnemyPhaseOpeningCompleted,
                _openingIndex = battle.EnemyOpeningTurnIndex,
                _openingCompleted = battle.EnemyOpeningCompleted,
                _counters = new Dictionary<string, int>(battle.EnemyMechanicCounters),
            };
        }

        public void Restore(GameManager manager)
        {
            var battle = manager._activeBattle;
            manager.PlayerHp = _playerHp;
            battle.PlayerLingli = _playerLingli;
            battle.PlayerDoujin = _playerDoujin;
            manager.PlayerHuti = _playerHuti;
            battle.PlayerYongyan = _playerYongyan;
            manager.PlayerYirongCeng = _playerYirong;
            battle.EnemyHp = _enemyHp;
            battle.EnemyMaxHp = _enemyMaxHp;
            battle.EnemyInfo = _enemyInfo;
            battle.EnemyHuti = _enemyHuti;
            battle.EnemyYirong = _enemyYirong;
            battle.EnemyYongyan = _enemyYongyan;
            battle.EnemyMechanicActive = _mechanicActive;
            battle.EnemySpecialTriggered = _specialTriggered;
            battle.EnemyGuardWasBroken = _guardBroken;
            battle.PreparedEnemyIntent = _preparedIntent;
            battle.LastExecutedEnemyIntent = _lastExecutedIntent;
            battle.LastEnemyActualDamage = _lastEnemyActualDamage;
            battle.IntentStateVersion = _intentStateVersion;
            battle.IsPlayerTurn = _isPlayerTurn;
            battle.BattleOver = _battleOver;
            battle.PlayerWon = _playerWon;
            battle.ResultSubmitted = _resultSubmitted;
            battle.PlayerAttackedThisTurn = _playerAttackedThisTurn;
            battle.LastPlayerTurnHadAttack = _lastPlayerTurnHadAttack;
            battle.ShuffleIndex = _shuffleIndex;
            RestoreList(battle.DrawPile, _drawPile);
            RestoreList(battle.Hand, _hand);
            RestoreList(battle.DiscardPile, _discardPile);
            RestoreList(battle.ExhaustPile, _exhaustPile);
            battle.EnemyTurnIndex = _enemyTurnIndex;
            battle.EnemyPhase = _phase;
            battle.EnemyPhaseId = _phaseId;
            battle.EnemyPhaseTurnIndex = _phaseTurnIndex;
            battle.EnemyPhaseOpeningTurnIndex = _phaseOpeningIndex;
            battle.EnemyPhaseOpeningCompleted = _phaseOpeningCompleted;
            battle.EnemyOpeningTurnIndex = _openingIndex;
            battle.EnemyOpeningCompleted = _openingCompleted;
            battle.EnemyMechanicCounters.Clear();
            foreach (var pair in _counters)
                battle.EnemyMechanicCounters[pair.Key] = pair.Value;
        }

        private static void RestoreList(List<CardRuntime> target, List<CardRuntime> source)
        {
            target.Clear();
            target.AddRange(source);
        }
    }

    public void EndEnemyTurn()
    {
        if (EnemyYongyan > 0)
        {
            EnemyHp -= EnemyYongyan;
            EnemyHp = Mathf.Max(EnemyHp, 0);
        }
        if (EnemyYirong > 0)
            EnemyYirong--;
        if (_activeBattle != null)
            _activeBattle.IntentStateVersion++;
    }

    public void StartEnemyTurn()
    {
        // 敌方护体现在由 TryExecuteEnemyTurn 在事务提交时清理，避免非法定义先污染状态。
    }

    // ==================== 显式节点与战斗生命周期 ====================

    /// <summary>从生产 MapGraph 节点创建活动战斗请求，不在这里完成节点。</summary>
    public bool TryEnterBattle(MapNodeDefinition node, out string error)
    {
        error = "";
        if (node == null || (node.NodeType != MapGraphNodeType.Battle && node.NodeType != MapGraphNodeType.Boss))
        {
            error = "节点不是可进入的战斗节点。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }
        if (!TryActivateNode(node, out error))
            return false;

        if (!EncounterPool.Act1.TryResolveForNode(ActiveNode, RandomStreams,
                out var enemyInfo, out error))
        {
            RecoverFromInvalidNodeEntry(out _);
            GD.PrintErr($"[GameManager] 遭遇解析失败，未进入战斗：{error}");
            return false;
        }

        if (!EnemyDefinitionValidator.TryValidate(enemyInfo, out error))
        {
            RecoverFromInvalidNodeEntry(out _);
            GD.PrintErr($"[GameManager] 敌人定义校验失败，未进入战斗：{error}");
            return false;
        }
        if (!RewardResolver.TryGetProfile(enemyInfo.RewardProfileId, out _, out error))
        {
            RecoverFromInvalidNodeEntry(out _);
            GD.PrintErr($"[GameManager] 战斗奖励档案校验失败，未进入战斗：{error}");
            return false;
        }

        ActiveEncounter = new EncounterRequest
        {
            Node = ActiveNode,
            ActId = _runState.ActId,
            Layer = node.LayerIndex,
            PoolId = node.PoolId,
            EnemyId = enemyInfo.Id,
            EnemyInfo = enemyInfo,
            RewardProfileId = enemyInfo.RewardProfileId,
            EncounterSeed = RandomStreams.DeriveSeed(RandomStreamKey.Encounter, node.NodeId, 0),
            CombatSeed = RandomStreams.DeriveSeed(RandomStreamKey.Combat, node.NodeId, 0),
        };
        _activeResultSubmitted = false;
        CurrentState = PlayerState.战斗中;
        return true;
    }

    /// <summary>
    /// Enters a Lingmai page and advances the one-way map position immediately.
    /// The node is not consumed here: an optional Lingmai action is tracked by the page,
    /// while the next legal map click performs the actual page transition.
    /// </summary>
    public bool TryEnterLingmai(MapNodeDefinition node, out string error)
    {
        if (node == null || node.NodeType != MapGraphNodeType.Lingmai)
        {
            error = "节点不是灵脉节点。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }
        if (!TryActivateNode(node, out error))
            return false;

        ActiveEncounter = null;
        _activeResultSubmitted = false;
        CurrentState = PlayerState.灵脉中;
        if (!TryAdvanceServiceRouteOnEntry(node, out error))
        {
            AbortActiveNodeEntry(out _);
            return false;
        }
        return true;
    }

    public bool TryEnterShop(MapNodeDefinition node, out string error)
    {
        if (node == null || node.NodeType != MapGraphNodeType.Shop)
        {
            error = "节点不是商店节点。";
            return false;
        }
        if (!ShopDefinitionCatalog.TryGet(node.ContentId, out _, out error))
        {
            GD.PrintErr($"[GameManager] 商店定义校验失败，未激活节点：{error}");
            return false;
        }
        if (!TryActivateNode(node, out error))
            return false;
        ActiveEncounter = null;
        _activeResultSubmitted = false;
        CurrentState = PlayerState.商店中;
        if (!TryAdvanceServiceRouteOnEntry(node, out error))
        {
            AbortActiveNodeEntry(out _);
            return false;
        }
        return true;
    }

    public bool TryEnterEvent(MapNodeDefinition node, out string error)
    {
        if (node == null || node.NodeType != MapGraphNodeType.Event)
        {
            error = "节点不是事件节点。";
            return false;
        }
        if (!EventDefinitionCatalog.TryGet(node.ContentId, out _, out error))
        {
            GD.PrintErr($"[GameManager] 事件定义校验失败，未激活节点：{error}");
            return false;
        }
        if (!TryActivateNode(node, out error))
            return false;
        ActiveEncounter = null;
        CurrentState = PlayerState.事件中;
        return true;
    }

    /// <summary>
    /// MapScene 的统一节点激活端口。它只负责校验并创建 ActiveNode/ActiveEncounter，
    /// 不负责切换场景；场景路由由 NodeSceneRouter 统一执行，避免各页面复制状态顺序。
    /// </summary>
    public bool TryEnterNode(MapNodeDefinition node, out string error)
    {
        error = "";
        if (!TryValidateNodeEntry(node, out error))
            return false;

        bool entered = node.NodeType switch
        {
            MapGraphNodeType.Battle or MapGraphNodeType.Boss => TryEnterBattle(node, out error),
            MapGraphNodeType.Lingmai => TryEnterLingmai(node, out error),
            MapGraphNodeType.Shop => TryEnterShop(node, out error),
            MapGraphNodeType.Event => TryEnterEvent(node, out error),
            _ => false,
        };

        if (!entered && string.IsNullOrWhiteSpace(error))
        {
            error = $"不支持的节点类型：{node.NodeType}";
            GD.PrintErr($"[GameManager] {error}");
        }

        if (entered)
        {
            // 该端口进入底层节点页，不是 MapScene；不把旧地图入口意图带入节点页。
            _navigationState.MapEntryMode = MapEntryMode.None;
            _navigationState.OpenMapOnEnter = false;
        }

        return entered;
    }

    /// <summary>
    /// 在销毁当前结算页前预检目标节点定义。该方法只读，不创建 ActiveNode，
    /// 用于避免目标入口失败时先丢失原结果页。
    /// </summary>
    public bool TryValidateNodeEntry(MapNodeDefinition node, out string error,
        bool allowActiveServiceNode = false)
    {
        error = "";
        if (node == null || MapGraph?.GetNode(node.NodeId) == null)
        {
            error = "目标节点不属于当前生产 MapGraph。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        if (ActiveNode != null && !_activeResultSubmitted && !allowActiveServiceNode)
        {
            error = $"当前节点尚未提交结果，拒绝进入新节点：{ActiveNode.NodeId}";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        if (string.IsNullOrWhiteSpace(CurrentMapNodeId) ||
            node.LayerIndex != CurrentMapLayer + 1 ||
            VisitedNodeIds.Contains(node.NodeId))
        {
            error = $"目标节点不是当前节点的下一层未访问节点：{node.NodeId}";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        bool connected = false;
        foreach (var edge in MapGraph.GetOutgoing(CurrentMapNodeId))
        {
            if (edge.ToNodeId == node.NodeId)
            {
                connected = true;
                break;
            }
        }

        if (!connected)
        {
            error = $"目标节点不在当前节点的合法出边上：{CurrentMapNodeId} -> {node.NodeId}";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        switch (node.NodeType)
        {
            case MapGraphNodeType.Battle:
            case MapGraphNodeType.Boss:
                var requestNode = new NodeContext
                {
                    NodeId = node.NodeId,
                    NodeType = node.NodeType,
                    ActIndex = _runState.ActIndex,
                    LayerIndex = node.LayerIndex,
                    EncounterTier = node.Tier,
                    PoolId = node.PoolId,
                    Seed = node.NodeSeed,
                };
                if (!EncounterPool.Act1.TryResolveForNode(requestNode, RandomStreams,
                        out var enemyInfo, out error))
                    return false;
                if (!EnemyDefinitionValidator.TryValidate(enemyInfo, out error))
                    return false;
                if (!RewardProfileCatalog.TryGet(enemyInfo.RewardProfileId, out _, out error))
                    return false;
                break;
            case MapGraphNodeType.Shop:
                if (!ShopDefinitionCatalog.TryGet(node.ContentId, out _, out error))
                    return false;
                break;
            case MapGraphNodeType.Event:
                if (!EventDefinitionCatalog.TryGet(node.ContentId, out _, out error))
                    return false;
                break;
            case MapGraphNodeType.Lingmai:
                break;
            default:
                error = $"目标节点类型不支持：{node.NodeType}";
                return false;
        }

        return true;
    }

    /// <summary>
    /// Service pages are entered from the map itself, so their position becomes the route
    /// origin at entry time. No lifecycle terminal state or reward is created by this step.
    /// </summary>
    private bool TryAdvanceServiceRouteOnEntry(MapNodeDefinition node, out string error)
    {
        error = "";
        var graphNode = _runState.MapGraph?.GetNode(node.NodeId);
        if (graphNode == null || graphNode.NodeType != node.NodeType)
        {
            error = $"服务节点不属于当前 MapGraph：{node.NodeId}";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        CurrentMapLayer = graphNode.LayerIndex;
        CurrentMapIndex = graphNode.LayerOrder;
        CurrentMapNodeId = graphNode.NodeId;
        return true;
    }

    /// <summary>
    /// BattleScene 的唯一初始化入口：从永久套牌复制本场牌堆，绝不继承上一场临时牌堆。
    /// </summary>
    public bool TryBeginActiveBattle(out string error)
    {
        error = "";
        if (ActiveEncounter?.EnemyInfo == null || ActiveNode == null)
        {
            error = "缺少活动遭遇请求，阻止进入战斗。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        if (_activeBattle != null)
        {
            error = "当前已有活动战斗，拒绝重复初始化。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        _activeBattle = new BattleState
        {
            EnemyInfo = ActiveEncounter.EnemyInfo,
            EnemyHp = ActiveEncounter.EnemyInfo.MaxHp,
            EnemyMaxHp = ActiveEncounter.EnemyInfo.MaxHp,
            EnemyMechanicActive = false,
            IsPlayerTurn = true,
        };

        foreach (var card in _runState.PermanentDeck)
            _activeBattle.DrawPile.Add(card.Clone());

        ShuffleBattleDrawPile();
        foreach (var dm in DaoMarks)
        {
            if (dm.EffectType == DaoMarkEffect.永炎开局)
                _activeBattle.EnemyYongyan += dm.EffectValue;
        }

        CurrentState = PlayerState.战斗中;
        return true;
    }

    private bool TryActivateNode(MapNodeDefinition info, out string error)
    {
        error = "";
        if (CurrentState != PlayerState.空闲)
        {
            error = $"当前流程状态不允许进入节点：{CurrentState}";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }
        if (ActiveNode != null)
        {
            error = $"已有活动节点：{ActiveNode.NodeId}，不能重复进入新节点。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        if (_runState.NodeStates.TryGetValue(info.NodeId, out var lifecycle) && lifecycle != NodeLifecycleState.Active)
        {
            error = $"节点已处于终态，不能重新进入：{info.NodeId}/{lifecycle}";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        ActiveNode = new NodeContext
        {
            NodeId = info.NodeId,
            NodeType = info.NodeType,
            ActIndex = _runState.ActIndex,
            LayerIndex = info.LayerIndex,
            EncounterTier = info.Tier,
            PoolId = info.PoolId,
            Seed = info.NodeSeed,
        };
        _runState.NodeStates[info.NodeId] = NodeLifecycleState.Active;
        return true;
    }

    /// <summary>按结果类型创建带唯一 ResultId 的节点结果，统一真值表。</summary>
    public NodeResult CreateNodeResult(NodeResultType resultType, string summary, out string error)
    {
        error = "";
        if (ActiveNode == null)
        {
            error = "没有活动节点，无法创建结果。";
            GD.PrintErr($"[GameManager] {error}");
            return null;
        }

        if (!TryGetResultPolicy(resultType, out bool consumeNode, out bool advanceRoute))
        {
            error = $"不支持的节点结果类型：{resultType}";
            GD.PrintErr($"[GameManager] {error}");
            return null;
        }

        _nextNodeResultSequence++;
        return new NodeResult
        {
            ResultId = $"{_runState.RunSeed:X16}:{ActiveNode.NodeId}:{_nextNodeResultSequence}",
            NodeId = ActiveNode.NodeId,
            ResultType = resultType,
            ConsumeNode = consumeNode,
            AdvanceRoute = advanceRoute,
            Summary = summary ?? "",
        };
    }

    /// <summary>
    /// 幂等提交节点终态。合法的 Completed/Skipped/Abandoned/Defeated 都返回 true；
    /// 只有契约校验失败、ResultId 重复或节点已终态时返回 false。
    /// </summary>
    public bool SubmitNodeResult(NodeResult result, out string error)
    {
        error = "";
        if (result == null || string.IsNullOrEmpty(result.ResultId) || ActiveNode == null || result.NodeId != ActiveNode.NodeId)
        {
            error = "节点结果、ResultId 或活动节点不匹配。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        if (_runState.AppliedResultIds.Contains(result.ResultId))
        {
            error = $"ResultId 重复提交：{result.ResultId}";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        if (_runState.NodeStates.TryGetValue(result.NodeId, out var lifecycle) && lifecycle != NodeLifecycleState.Active)
        {
            error = $"节点已处于终态，拒绝覆盖：{result.NodeId}/{lifecycle}";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        if (!TryGetResultPolicy(result.ResultType, out bool expectedConsume, out bool expectedAdvance) ||
            result.ConsumeNode != expectedConsume || result.AdvanceRoute != expectedAdvance)
        {
            error = $"结果类型与 ConsumeNode/AdvanceRoute 不一致：{result.ResultType}";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        _runState.AppliedResultIds.Add(result.ResultId);
        if (!ApplyNodeResult(result, out error))
        {
            _runState.AppliedResultIds.Remove(result.ResultId);
            return false;
        }

        _runState.NodeStates[result.NodeId] = GetLifecycleForResult(result.ResultType);
        if (result.ConsumeNode)
            _runState.CompletedNodeIds.Add(result.NodeId);

        _activeResultSubmitted = true;
        if (_activeBattle != null)
        {
            _activeBattle.ResultSubmitted = true;
            CurrentState = result.ResultType == NodeResultType.Completed
                ? PlayerState.战斗胜利结算
                : PlayerState.失败;
        }

        GD.Print($"[GameManager] 节点结果已提交：{result.ResultId} / {result.ResultType}");
        return true;
    }

    /// <summary>兼容旧接口；新流程不得构造旧布尔结果。</summary>
    [System.Obsolete("Use SubmitNodeResult instead.")]
    public bool SubmitEncounterResult(EncounterResult result, out string error)
    {
        return SubmitNodeResult(result, out error);
    }

    /// <summary>按 AdvanceRoute 一次性应用活动节点坐标，重复调用保持幂等。</summary>
    public bool ApplyNodeResult(NodeResult result, out string error)
    {
        error = "";
        if (result == null || ActiveNode == null || result.NodeId != ActiveNode.NodeId ||
            !_runState.AppliedResultIds.Contains(result.ResultId))
        {
            error = "结果尚未合法登记或活动节点不匹配。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        if (!result.AdvanceRoute || _runState.AppliedRouteResultIds.Contains(result.ResultId))
            return true;

        var graphNode = _runState.MapGraph?.GetNode(ActiveNode.NodeId);
        if (graphNode == null)
        {
            error = $"生产 MapGraph 找不到活动节点：{ActiveNode.NodeId}";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        CurrentMapLayer = graphNode.LayerIndex;
        CurrentMapIndex = graphNode.IndexInLayer;
        CurrentMapNodeId = graphNode.NodeId;
        _runState.AppliedRouteResultIds.Add(result.ResultId);
        return true;
    }

    /// <summary>胜利/失败结果已提交后，统一销毁战斗临时状态并恢复目标流程。</summary>
    public bool DisposeActiveBattleAfterResult(PlayerState targetState, bool openMapOnEnter, out string error)
    {
        return DisposeActiveBattleAfterResult(targetState,
            openMapOnEnter ? MapEntryMode.OpenInteractiveMap : MapEntryMode.None, out error);
    }

    /// <summary>
    /// 已提交节点结果后的统一战斗实例销毁端口。生产导航直接写入显式 MapEntryMode，
    /// 不经过 OpenMapOnEnter 兼容属性的隐式转换。
    /// </summary>
    public bool DisposeActiveBattleAfterResult(PlayerState targetState, MapEntryMode mapEntryMode,
        out string error)
    {
        error = "";
        if (_activeBattle == null || !_activeResultSubmitted)
        {
            error = "战斗结果尚未合法提交，拒绝销毁活动战斗。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

#if DEBUG
        if (_injectBattleExitFailureForSelfCheck)
        {
            _injectBattleExitFailureForSelfCheck = false;
            error = "自检注入：战斗离场失败。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }
#endif

        DisposeBattleState();
        CurrentState = targetState;
        _navigationState.MapEntryMode = mapEntryMode;
        _navigationState.OpenMapOnEnter = false;
        return true;
    }

    /// <summary>
    /// 胜利页“继续”专用导航端口。敌人死亡时已完成节点结果，
    /// 此处只销毁战斗实例、清理未领取的临时奖励并设置显式地图入口。
    /// </summary>
    public bool TryExitBattleToMapAfterVictory(out string error)
    {
        error = "";
        if (_activeBattle == null || !_activeBattle.BattleOver || !_activeBattle.PlayerWon)
        {
            error = "当前没有可离场的胜利战斗。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        // 自检注入只模拟离场失败；正常路径不依赖奖励状态，也不提交任何新结果。
#if DEBUG
        if (_injectBattleExitFailureForSelfCheck)
        {
            _injectBattleExitFailureForSelfCheck = false;
            error = "自检注入：战斗离场失败。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }
#endif

        DisposeBattleState();
        UnclaimedLingYun = 0;
        CurrentState = PlayerState.空闲;
        // 当前方法由完成页点击下一节点的事务调用；目标由 NodeSceneRouter 直接进入，
        // 因此不能把 MapScene 入口意图残留到目标节点页。
        _navigationState.MapEntryMode = MapEntryMode.None;
        _navigationState.OpenMapOnEnter = false;
        return true;
    }

    public bool ExitBattleToMap(out string error) =>
        DisposeActiveBattleAfterResult(PlayerState.空闲,
            MapEntryMode.OpenInteractiveMap, out error);

    public bool ExitBattleToTitleAfterDefeat(out string error) =>
        DisposeActiveBattleAfterResult(PlayerState.失败, false, out error);

#if DEBUG
    /// <summary>仅供 Debug 自检注入一次离场失败，不提供正式玩法入口。</summary>
    internal void InjectBattleExitFailureForSelfCheck() => _injectBattleExitFailureForSelfCheck = true;

    /// <summary>仅供 Debug 自检验证胜利计划提交失败不污染永久奖励状态。</summary>
    internal void InjectBattleVictoryCommitFailureForSelfCheck() => _injectBattleVictoryCommitFailureForSelfCheck = true;
#endif

    /// <summary>灵脉结果提交后统一清理节点并返回地图。</summary>
    public bool ExitLingmaiToMap(out string error)
    {
        error = "";
        if (ActiveNode == null || ActiveNode.NodeType != MapGraphNodeType.Lingmai || !_activeResultSubmitted)
        {
            error = "灵脉结果尚未合法提交，拒绝离开场景。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        ActiveEncounter = null;
        ActiveNode = null;
        _activeResultSubmitted = false;
        CurrentState = PlayerState.空闲;
        _navigationState.MapEntryMode = MapEntryMode.OpenInteractiveMap;
        _navigationState.OpenMapOnEnter = false;
        return true;
    }

    /// <summary>
    /// 事件唯一提交入口：先解析 Definition/Option 计划，再原子应用永久效果、节点结果和离场。
    /// 任一步失败都会恢复事件前的完整 RunState 与导航上下文。
    /// </summary>
    public bool TryCommitEventOption(string optionId, CardRuntime selectedCard,
        out EventTransactionResult result, out string error)
    {
        result = null;
        error = "";
        if (!EventEffectExecutor.TryBuildPlan(this, optionId, selectedCard, out var plan, out error))
            return false;
        if (plan.RequiresCardChoice)
        {
            result = new EventTransactionResult { RequiresCardChoice = true };
            return true;
        }

        var snapshot = EventTransactionSnapshot.Capture(this);
        try
        {
            if (plan.RemoveTarget != null && !TryRemoveCardFromDeck(plan.RemoveTarget, out error))
                throw new System.InvalidOperationException(error);

            PlayerHp = plan.ResultHp;
            LingYun = plan.ResultLingYun;
            if (plan.AddedCard != null)
                AddCardToDeck(plan.AddedCard);

            var nodeResult = CreateNodeResult(NodeResultType.Completed,
                $"事件选项已结算：{plan.OptionId}", out error);
            if (nodeResult == null || !SubmitNodeResult(nodeResult, out error))
                throw new System.InvalidOperationException(error);
            if (!plan.RequestsExit)
                throw new System.InvalidOperationException("事件计划未声明有效离场。");

            result = new EventTransactionResult
            {
                RequestsExit = true,
                AddedCard = plan.AddedCard,
                Summary = $"事件选项已结算：{plan.OptionId}",
            };
            return true;
        }
        catch (System.Exception exception)
        {
            snapshot.Restore(this);
            if (string.IsNullOrWhiteSpace(error))
                error = exception.Message;
            GD.PrintErr($"[GameManager] 事件事务已回滚：{error}");
            return false;
        }
    }

    /// <summary>
    /// 结算后的灵脉/商店/事件页面共用离场端口。只有共享地图 overlay 点击合法下一节点时调用，
    /// 关闭地图或刷新结果页不会触发这里。
    /// </summary>
    public bool TryExitCompletedNodeToMap(out string error)
    {
        error = "";
        if (ActiveNode == null ||
            (ActiveNode.NodeType != MapGraphNodeType.Lingmai &&
             ActiveNode.NodeType != MapGraphNodeType.Shop &&
             ActiveNode.NodeType != MapGraphNodeType.Event) ||
            !_activeResultSubmitted)
        {
            error = "当前节点尚未完成合法结算，拒绝离场。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        ActiveEncounter = null;
        ActiveNode = null;
        _activeResultSubmitted = false;
        CurrentState = PlayerState.空闲;
        return true;
    }

    /// <summary>
    /// 原子完成“当前节点结果页 -> 下一节点上下文”的状态迁移。
    /// 目标内容在清理当前 ActiveNode 前完成预检；若后续激活仍失败，恢复原节点页所需的
    /// ActiveNode、BattleState、奖励暂存、生命周期和导航字段，调用方可以保留 overlay 重试。
    /// </summary>
    public bool TryTransitionFromCompletedNode(MapNodeDefinition target, out string error) =>
        TryTransitionFromCompletedNodeInternal(target, routeToTarget: false, out error);

    /// <summary>
    /// Atomically leaves a completed node page, activates the target and queues its scene route.
    /// A route error restores the original completed-node context so the visible page remains retryable.
    /// </summary>
    public bool TryTransitionAndRouteFromCompletedNode(MapNodeDefinition target, out string error) =>
        TryTransitionFromCompletedNodeInternal(target, routeToTarget: true, out error);

    /// <summary>
    /// Returns whether a node page may show a map and whether its map may advance the route.
    /// This is the shared state decision used by Battle, Lingmai and Shop page coordinators.
    /// </summary>
    public bool TryGetNodePageMapInteractivity(out bool interactive, out string error)
    {
        interactive = false;
        error = "";
        if (MapGraph == null)
        {
            error = "RunState 缺少生产 MapGraph，拒绝打开节点页地图。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        // Invalid-entry recovery may have cleared ActiveNode. It can inspect the map but cannot
        // route again until a page has entered a valid node context.
        if (ActiveNode == null)
            return true;

        if (_activeBattle != null)
        {
            interactive = _activeBattle.BattleOver && _activeBattle.PlayerWon &&
                CurrentState == PlayerState.战斗胜利结算 && _activeResultSubmitted;
            return true;
        }

        bool activeService = !_activeResultSubmitted &&
            (CurrentState == PlayerState.灵脉中 || CurrentState == PlayerState.商店中) &&
            (ActiveNode.NodeType == MapGraphNodeType.Lingmai || ActiveNode.NodeType == MapGraphNodeType.Shop);
        if (activeService)
        {
            interactive = true;
            return true;
        }

        error = $"当前节点页状态不支持地图导航：{ActiveNode.NodeType}/{CurrentState}";
        GD.PrintErr($"[GameManager] {error}");
        return false;
    }

    /// <summary>
    /// The only node-page route command. It is called exclusively after a shared map overlay
    /// reports a legal target click; closing the map never reaches this method.
    /// </summary>
    public bool TryRouteFromNodePage(MapNodeDefinition target, out string error)
    {
        if (target == null)
        {
            error = "地图目标节点为空，拒绝离开当前节点页。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        if (_activeBattle != null)
            return TryTransitionAndRouteFromCompletedNode(target, out error);

        bool activeService = ActiveNode != null && !_activeResultSubmitted &&
            (CurrentState == PlayerState.灵脉中 || CurrentState == PlayerState.商店中) &&
            (ActiveNode.NodeType == MapGraphNodeType.Lingmai || ActiveNode.NodeType == MapGraphNodeType.Shop);
        if (activeService)
            return TryTransitionAndRouteFromActiveServiceNode(target, out error);

        error = "当前节点页不允许通过地图离场。";
        GD.PrintErr($"[GameManager] {error}");
        return false;
    }

    /// <summary>
    /// Leaves an active Lingmai or Shop page only after the player selects a legal next map node.
    /// These pages advance route position on entry and do not require a synthetic completion result
    /// merely to keep exploring. A routing failure restores the original page and active context.
    /// </summary>
    public bool TryTransitionAndRouteFromActiveServiceNode(MapNodeDefinition target, out string error)
    {
        error = "";
        bool isActiveServiceNode = ActiveNode != null && !_activeResultSubmitted &&
            (CurrentState == PlayerState.灵脉中 || CurrentState == PlayerState.商店中) &&
            (ActiveNode.NodeType == MapGraphNodeType.Lingmai || ActiveNode.NodeType == MapGraphNodeType.Shop);
        if (!isActiveServiceNode)
        {
            error = "当前不在可从地图继续的灵脉或商店节点页。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        if (!TryValidateNodeEntry(target, out error, allowActiveServiceNode: true))
            return false;

        var snapshot = CompletedNodeTransitionSnapshot.Capture(this);
        try
        {
            string sourceNodeId = ActiveNode.NodeId;
            _runState.NodeStates.Remove(sourceNodeId);
            ActiveNode = null;
            ActiveEncounter = null;
            _activeResultSubmitted = false;
            CurrentState = PlayerState.空闲;

            if (!TryEnterNode(target, out error))
                throw new System.InvalidOperationException(error);
            if (!NodeSceneRouter.TryRouteActiveNode(this, target, out error))
                throw new System.InvalidOperationException(error);
            return true;
        }
        catch (System.Exception exception)
        {
            snapshot.Restore(this);
            if (string.IsNullOrWhiteSpace(error))
                error = exception.Message;
            GD.PrintErr($"[GameManager] 服务节点迁移已回滚，保留当前节点页：{error}");
            return false;
        }
    }

    private bool TryTransitionFromCompletedNodeInternal(MapNodeDefinition target, bool routeToTarget,
        out string error)
    {
        error = "";
        if (ActiveNode == null || !_activeResultSubmitted)
        {
            error = "当前节点尚未完成合法结算，拒绝进入下一节点。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        bool fromBattle = _activeBattle != null;
        bool fromService = CurrentState == PlayerState.灵脉中 ||
                           CurrentState == PlayerState.商店中 ||
                           CurrentState == PlayerState.事件中;
        if ((!fromBattle && !fromService) ||
            (fromBattle && (CurrentState != PlayerState.战斗胜利结算 ||
                            !_activeBattle.BattleOver || !_activeBattle.PlayerWon)))
        {
            error = $"当前流程状态不允许从结算页进入下一节点：{CurrentState}";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        if (!TryValidateNodeEntry(target, out error))
            return false;

        var snapshot = CompletedNodeTransitionSnapshot.Capture(this);
        try
        {
            bool exited = fromBattle
                ? TryExitBattleToMapAfterVictory(out error)
                : TryExitCompletedNodeToMap(out error);
            if (!exited)
                throw new System.InvalidOperationException(error);

            bool entered = target.NodeType switch
            {
                MapGraphNodeType.Battle or MapGraphNodeType.Boss => TryEnterBattle(target, out error),
                MapGraphNodeType.Lingmai => TryEnterLingmai(target, out error),
                MapGraphNodeType.Shop => TryEnterShop(target, out error),
                MapGraphNodeType.Event => TryEnterEvent(target, out error),
                _ => false,
            };
            if (!entered)
                throw new System.InvalidOperationException(
                    string.IsNullOrWhiteSpace(error) ? $"不支持的目标节点类型：{target?.NodeType}" : error);

            // 完成页直接进入目标节点，不消费 MapScene 入口，也不留下兼容导航标记。
            _navigationState.MapEntryMode = MapEntryMode.None;
            _navigationState.OpenMapOnEnter = false;

            if (routeToTarget && !NodeSceneRouter.TryRouteActiveNode(this, target, out error))
                throw new System.InvalidOperationException(error);

            return true;
        }
        catch (System.Exception exception)
        {
            snapshot.Restore(this);
            if (string.IsNullOrWhiteSpace(error))
                error = exception.Message;
            GD.PrintErr($"[GameManager] 节点迁移已回滚，保留当前结算页：{error}");
            return false;
        }
    }

    private sealed class CompletedNodeTransitionSnapshot
    {
        private readonly NodeContext _activeNode;
        private readonly EncounterRequest _activeEncounter;
        private readonly BattleState _activeBattle;
        private readonly bool _activeResultSubmitted;
        private readonly PlayerState _currentState;
        private readonly int _currentMapLayer;
        private readonly int _currentMapIndex;
        private readonly string _currentMapNodeId;
        private readonly int _unclaimedLingYun;
        private readonly string _lastLingmaiResult;
        private readonly bool _openMapOnEnter;
        private readonly MapEntryMode _mapEntryMode;
        private readonly Dictionary<string, NodeLifecycleState> _nodeStates;
        private readonly BattleVictoryPlan _activeVictoryPlan;
        private readonly BattleVictoryPlan _activeVictorySnapshot;
        private readonly BattleVictoryPlan _pendingVictoryPlan;
        private readonly BattleVictoryPlan _pendingVictorySnapshot;
        private readonly VictorySnapshot _victoryTransactionSnapshot;
        private readonly int _nextNodeResultSequence;
        private readonly Dictionary<string, HashSet<int>> _claimedRewards;

        private CompletedNodeTransitionSnapshot(GameManager manager)
        {
            _activeNode = manager.ActiveNode;
            _activeEncounter = manager.ActiveEncounter;
            _activeBattle = manager._activeBattle;
            _activeResultSubmitted = manager._activeResultSubmitted;
            _currentState = manager.CurrentState;
            _currentMapLayer = manager.CurrentMapLayer;
            _currentMapIndex = manager.CurrentMapIndex;
            _currentMapNodeId = manager.CurrentMapNodeId;
            _unclaimedLingYun = manager.UnclaimedLingYun;
            _lastLingmaiResult = manager.LastLingmaiResult;
            _openMapOnEnter = manager._navigationState.OpenMapOnEnter;
            _mapEntryMode = manager._navigationState.MapEntryMode;
            _nodeStates = new Dictionary<string, NodeLifecycleState>(manager._runState.NodeStates);
            _activeVictoryPlan = manager._activeBattleVictoryPlan;
            _activeVictorySnapshot = manager._activeBattleVictorySnapshot;
            _pendingVictoryPlan = manager._pendingBattleVictoryPlan;
            _pendingVictorySnapshot = manager._pendingBattleVictorySnapshot;
            _victoryTransactionSnapshot = manager._victoryTransactionSnapshot;
            _nextNodeResultSequence = manager._nextNodeResultSequence;
            _claimedRewards = new Dictionary<string, HashSet<int>>();
            foreach (var pair in manager._claimedRewardCandidates)
                _claimedRewards[pair.Key] = new HashSet<int>(pair.Value);
        }

        public static CompletedNodeTransitionSnapshot Capture(GameManager manager) =>
            new CompletedNodeTransitionSnapshot(manager);

        public void Restore(GameManager manager)
        {
            manager.ActiveNode = _activeNode;
            manager.ActiveEncounter = _activeEncounter;
            manager._activeBattle = _activeBattle;
            manager._activeResultSubmitted = _activeResultSubmitted;
            manager.CurrentState = _currentState;
            manager.CurrentMapLayer = _currentMapLayer;
            manager.CurrentMapIndex = _currentMapIndex;
            manager.CurrentMapNodeId = _currentMapNodeId;
            manager.UnclaimedLingYun = _unclaimedLingYun;
            manager.LastLingmaiResult = _lastLingmaiResult;
            manager._navigationState.OpenMapOnEnter = _openMapOnEnter;
            manager._navigationState.MapEntryMode = _mapEntryMode;
            manager._runState.NodeStates.Clear();
            foreach (var pair in _nodeStates)
                manager._runState.NodeStates[pair.Key] = pair.Value;
            manager._activeBattleVictoryPlan = _activeVictoryPlan;
            manager._activeBattleVictorySnapshot = _activeVictorySnapshot;
            manager._pendingBattleVictoryPlan = _pendingVictoryPlan;
            manager._pendingBattleVictorySnapshot = _pendingVictorySnapshot;
            manager._victoryTransactionSnapshot = _victoryTransactionSnapshot;
            manager._nextNodeResultSequence = _nextNodeResultSequence;
            manager._claimedRewardCandidates.Clear();
            foreach (var pair in _claimedRewards)
                manager._claimedRewardCandidates[pair.Key] = new HashSet<int>(pair.Value);
        }
    }

    /// <summary>兼容旧调用者；正式节点页不直接调用此方法。</summary>
    public bool ExitServiceNodeToMap(out string error)
    {
        return TryExitCompletedNodeToMap(out error);
    }

    /// <summary>
    /// 无效场景入口的无副作用恢复端口。不会提交节点结果、不会推进路线、不会写完成集合。
    /// </summary>
    public bool RecoverFromInvalidNodeEntry(out string error)
    {
        error = "";
        if (_activeBattle != null)
        {
            error = "当前存在活动战斗，拒绝按无效灵脉入口恢复。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        if (ActiveNode != null)
            _runState.NodeStates.Remove(ActiveNode.NodeId);
        ActiveEncounter = null;
        ActiveNode = null;
        _activeResultSubmitted = false;
        CurrentState = PlayerState.空闲;
        _navigationState.MapEntryMode = MapEntryMode.OpenInteractiveMap;
        _navigationState.OpenMapOnEnter = false;
        GD.Print("[GameManager] 无效节点入口已安全清理，未提交结果且未推进路线。");
        return true;
    }

    /// <summary>
    /// 节点已激活但底层场景路由失败时的无副作用回滚端口。
    /// 不提交结果、不推进地图，只销毁本次未开始的节点实例，允许地图重新尝试。
    /// </summary>
    public bool AbortActiveNodeEntry(out string error)
    {
        error = "";
        if (ActiveNode == null)
        {
            error = "没有活动节点可回滚。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        if (_activeResultSubmitted)
        {
            error = "活动节点已经提交结果，拒绝按未开始入口回滚。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        string nodeId = ActiveNode.NodeId;
        DisposeBattleState();
        _runState.NodeStates.Remove(nodeId);
        ActiveNode = null;
        ActiveEncounter = null;
        _activeResultSubmitted = false;
        CurrentState = PlayerState.空闲;
        GD.Print($"[GameManager] 场景路由失败，已回滚未开始节点：{nodeId}");
        return true;
    }

    private sealed class EventTransactionSnapshot
    {
        private int _playerHp;
        private int _lingyun;
        private int _unclaimedLingyun;
        private List<CardRuntime> _deck;
        private Dictionary<string, NodeLifecycleState> _nodeStates;
        private HashSet<string> _completed;
        private HashSet<string> _appliedResults;
        private HashSet<string> _appliedRoutes;
        private int _mapLayer;
        private int _mapIndex;
        private string _mapNodeId;
        private NodeContext _activeNode;
        private EncounterRequest _activeEncounter;
        private BattleState _activeBattle;
        private bool _activeResultSubmitted;
        private int _nextResultSequence;
        private PlayerState _currentState;
        private bool _openMapOnEnter;
        private string _lastLingmaiResult;

        public static EventTransactionSnapshot Capture(GameManager manager)
        {
            return new EventTransactionSnapshot
            {
                _playerHp = manager.PlayerHp,
                _lingyun = manager.LingYun,
                _unclaimedLingyun = manager.UnclaimedLingYun,
                _deck = new List<CardRuntime>(manager._runState.PermanentDeck),
                _nodeStates = new Dictionary<string, NodeLifecycleState>(manager._runState.NodeStates),
                _completed = new HashSet<string>(manager._runState.CompletedNodeIds),
                _appliedResults = new HashSet<string>(manager._runState.AppliedResultIds),
                _appliedRoutes = new HashSet<string>(manager._runState.AppliedRouteResultIds),
                _mapLayer = manager.CurrentMapLayer,
                _mapIndex = manager.CurrentMapIndex,
                _mapNodeId = manager.CurrentMapNodeId,
                _activeNode = manager.ActiveNode,
                _activeEncounter = manager.ActiveEncounter,
                _activeBattle = manager._activeBattle,
                _activeResultSubmitted = manager._activeResultSubmitted,
                _nextResultSequence = manager._nextNodeResultSequence,
                _currentState = manager.CurrentState,
                _openMapOnEnter = manager.OpenMapOnEnter,
                _lastLingmaiResult = manager.LastLingmaiResult,
            };
        }

        public void Restore(GameManager manager)
        {
            manager.PlayerHp = _playerHp;
            manager.LingYun = _lingyun;
            manager.UnclaimedLingYun = _unclaimedLingyun;
            RestoreList(manager._runState.PermanentDeck, _deck);
            RestoreDictionary(manager._runState.NodeStates, _nodeStates);
            RestoreSet(manager._runState.CompletedNodeIds, _completed);
            RestoreSet(manager._runState.AppliedResultIds, _appliedResults);
            RestoreSet(manager._runState.AppliedRouteResultIds, _appliedRoutes);
            manager.CurrentMapLayer = _mapLayer;
            manager.CurrentMapIndex = _mapIndex;
            manager.CurrentMapNodeId = _mapNodeId;
            manager.ActiveNode = _activeNode;
            manager.ActiveEncounter = _activeEncounter;
            manager._activeBattle = _activeBattle;
            manager._activeResultSubmitted = _activeResultSubmitted;
            manager._nextNodeResultSequence = _nextResultSequence;
            manager.CurrentState = _currentState;
            manager.OpenMapOnEnter = _openMapOnEnter;
            manager.LastLingmaiResult = _lastLingmaiResult;
        }

        private static void RestoreList<T>(List<T> target, List<T> source)
        {
            target.Clear();
            target.AddRange(source);
        }

        private static void RestoreDictionary<TKey, TValue>(Dictionary<TKey, TValue> target,
            Dictionary<TKey, TValue> source)
        {
            target.Clear();
            foreach (var pair in source)
                target[pair.Key] = pair.Value;
        }

        private static void RestoreSet<T>(HashSet<T> target, HashSet<T> source)
        {
            target.Clear();
            foreach (var item in source)
                target.Add(item);
        }
    }

    private void DisposeBattleState()
    {
        _activeBattle = null;
        ActiveEncounter = null;
        ActiveNode = null;
        _activeResultSubmitted = false;
        _activeBattleVictoryPlan = null;
        _activeBattleVictorySnapshot = null;
        _pendingBattleVictoryPlan = null;
        _pendingBattleVictorySnapshot = null;
        _victoryTransactionSnapshot = null;
        _claimedRewardCandidates.Clear();
    }

    private static bool TryGetResultPolicy(NodeResultType resultType, out bool consumeNode, out bool advanceRoute)
    {
        switch (resultType)
        {
            case NodeResultType.Completed:
                consumeNode = true;
                advanceRoute = true;
                return true;
            case NodeResultType.Exited:
            case NodeResultType.NodeSkipped:
                consumeNode = false;
                advanceRoute = true;
                return true;
            case NodeResultType.Defeated:
            case NodeResultType.Abandoned:
                consumeNode = false;
                advanceRoute = false;
                return true;
            default:
                consumeNode = false;
                advanceRoute = false;
                return false;
        }
    }

    private static NodeLifecycleState GetLifecycleForResult(NodeResultType resultType)
    {
        return resultType == NodeResultType.Completed
            ? NodeLifecycleState.Completed
            : resultType == NodeResultType.Exited || resultType == NodeResultType.NodeSkipped
                ? NodeLifecycleState.Skipped
                : NodeLifecycleState.Abandoned;
    }

    private ulong DeriveNodeSeed(string nodeId, int layer, int index)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;
        foreach (char c in nodeId ?? "")
        {
            hash ^= c;
            hash *= prime;
        }

        return _runState.RunSeed ^ hash ^ ((ulong)(layer + 1) << 32) ^ (uint)(index + 1);
    }

    // ==================== 卡牌奖励 ====================

    /// <summary>
    /// 敌人生命归零后的战斗终止端口。它先锁定 BattleState 的胜负状态，
    /// 再幂等提交战斗节点 Completed；不解析奖励、不写永久奖励，奖励系统失败也不能复活战斗。
    /// </summary>
    public bool TryRegisterEnemyDefeated(out string error)
    {
        error = "";
        if (_activeBattle == null || ActiveNode == null || ActiveEncounter == null)
        {
            error = "敌人死亡登记缺少活动战斗上下文。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }
        if (_activeBattle.EnemyHp > 0)
        {
            error = "敌人仍有生命，不能登记战斗胜利。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }
        if (_activeBattle.BattleOver)
        {
            if (!_activeBattle.PlayerWon)
            {
                error = "战斗已经以失败终止，不能覆盖为胜利。";
                GD.PrintErr($"[GameManager] {error}");
                return false;
            }
            if (_activeResultSubmitted)
                return true;
        }
        else
        {
            _activeBattle.BattleOver = true;
            _activeBattle.PlayerWon = true;
            _activeBattle.IsPlayerTurn = false;
            GD.Print($"[GameManager] 敌人生命归零，战斗立即结束：{ActiveEncounter.EnemyId}");
        }
        CurrentState = PlayerState.战斗胜利结算;

        var result = CreateNodeResult(NodeResultType.Completed, "战斗胜利", out error);
        if (result == null || !SubmitNodeResult(result, out error))
        {
            if (string.IsNullOrWhiteSpace(error))
                error = "战斗胜利节点结果提交失败。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Records a player defeat through the same node-result boundary as victory. Presentation controllers must not
    /// mutate BattleOver/PlayerWon or submit defeat results directly.
    /// </summary>
    public bool TryRegisterPlayerDefeated(out string error)
    {
        error = "";
        if (_activeBattle == null || ActiveNode == null || ActiveEncounter == null)
        {
            error = "玩家失败登记缺少活动战斗上下文。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }
        if (PlayerHp > 0)
        {
            error = "玩家仍有生命，不能登记战斗失败。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }
        if (_activeBattle.BattleOver)
        {
            if (_activeBattle.PlayerWon)
            {
                error = "战斗已经以胜利终止，不能覆盖为失败。";
                GD.PrintErr($"[GameManager] {error}");
                return false;
            }
            if (_activeResultSubmitted)
                return true;

            error = "战斗失败状态缺少已提交节点结果。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        _activeBattle.BattleOver = true;
        _activeBattle.PlayerWon = false;
        _activeBattle.IsPlayerTurn = false;
        CurrentState = PlayerState.战斗中;
        var result = CreateNodeResult(NodeResultType.Defeated, "战斗失败", out error);
        if (result == null || !SubmitNodeResult(result, out error))
        {
            if (string.IsNullOrWhiteSpace(error))
                error = "战斗失败节点结果提交失败。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }
        return true;
    }

    public bool TryBuildBattleVictoryPlan(out BattleVictoryPlan plan, out string error)
    {
        plan = null;
        error = "";
        if (_activeBattle == null || _activeBattle.EnemyInfo == null || ActiveEncounter == null ||
            ActiveNode == null || (ActiveNode.NodeType != MapGraphNodeType.Battle &&
                                   ActiveNode.NodeType != MapGraphNodeType.Boss) ||
            _activeBattle.EnemyHp > 0 || !_activeBattle.BattleOver || !_activeBattle.PlayerWon ||
            _activeBattleVictoryPlan != null || _pendingBattleVictoryPlan != null)
        {
            error = "当前战斗不满足胜利奖励计划解析条件。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        if (!RewardContext.TryCreate(DaoMarks, out var context, out error))
            return false;
        if (!RewardResolver.TryResolveLingYunAmount(ActiveEncounter.EnemyInfo,
                RandomStreams.CreateRewardStream(ActiveNode.NodeId, 0), out int lingYun,
                out error))
            return false;
        if (!TryCreateRewardId("lingyun", "battle_main", out var lingYunRewardId, out error))
            return false;
        if (!RewardResolver.TryResolveBattleCardPlan(ActiveEncounter,
                RandomStreams.CreateRewardStream(ActiveNode.NodeId, 1), out var basePlan, out error))
            return false;

        var plans = new List<RewardPlan> { basePlan };
        int streamIndex = 2;
        foreach (var source in context.ExtraCardSources)
        {
            for (int index = 0; index < source.CardRewardCount; index++)
            {
                if (!RewardResolver.TryResolveExtraCardPlan(source, ActiveNode.NodeId, index,
                        RandomStreams.CreateRewardStream(ActiveNode.NodeId, streamIndex++),
                        out var extraPlan, out error))
                    return false;
                plans.Add(extraPlan);
            }
        }

        var slots = new HashSet<string>();
        foreach (var rewardPlan in plans)
        {
            if (!RewardPlanValidator.TryValidate(rewardPlan, out error) ||
                rewardPlan.NodeId != ActiveNode.NodeId || !slots.Add(rewardPlan.RewardSlotId))
            {
                if (string.IsNullOrWhiteSpace(error))
                    error = $"奖励计划槽位重复或节点不匹配：{rewardPlan?.RewardSlotId}";
                return false;
            }
        }

        plan = new BattleVictoryPlan
        {
            NodeId = ActiveNode.NodeId,
            EncounterId = ActiveEncounter.EnemyId,
            RewardProfileId = ActiveEncounter.RewardProfileId,
            LingYunRewardId = lingYunRewardId,
            LingYunAmount = lingYun,
            RewardContext = context,
            CardRewards = plans.AsReadOnly(),
        };
        _pendingBattleVictoryPlan = plan;
        _pendingBattleVictorySnapshot = CloneBattleVictoryPlan(plan);
        return true;
    }

    private static BattleVictoryPlan CloneBattleVictoryPlan(BattleVictoryPlan source)
    {
        var cards = new List<RewardPlan>();
        foreach (var plan in source.CardRewards)
        {
            var candidates = new List<CardInfo>();
            foreach (var card in plan.Candidates)
                candidates.Add(RewardPlanValidator.CloneCardDefinition(card));
            cards.Add(new RewardPlan
            {
                NodeId = plan.NodeId,
                EncounterId = plan.EncounterId,
                ProfileId = plan.ProfileId,
                SourceId = plan.SourceId,
                RewardSlotId = plan.RewardSlotId,
                CardPoolId = plan.CardPoolId,
                DisplayName = plan.DisplayName,
                Description = plan.Description,
                CandidateCount = plan.CandidateCount,
                ChoiceCount = plan.ChoiceCount,
                MergeRule = plan.MergeRule,
                Candidates = candidates.AsReadOnly(),
            });
        }

        return new BattleVictoryPlan
        {
            NodeId = source.NodeId,
            EncounterId = source.EncounterId,
            RewardProfileId = source.RewardProfileId,
            LingYunRewardId = source.LingYunRewardId,
            LingYunAmount = source.LingYunAmount,
            RewardContext = source.RewardContext.Clone(),
            CardRewards = cards.AsReadOnly(),
        };
    }

    private static bool AreBattleVictoryPlansEquivalent(BattleVictoryPlan left,
        BattleVictoryPlan right)
    {
        if (left == null || right == null || left.NodeId != right.NodeId ||
            left.EncounterId != right.EncounterId || left.RewardProfileId != right.RewardProfileId ||
            left.LingYunRewardId != right.LingYunRewardId || left.LingYunAmount != right.LingYunAmount ||
            left.RewardContext == null || right.RewardContext == null ||
            left.CardRewards == null || right.CardRewards == null ||
            left.CardRewards.Count != right.CardRewards.Count ||
            left.RewardContext.ExtraCardSources.Count != right.RewardContext.ExtraCardSources.Count)
            return false;

        foreach (var source in left.RewardContext.ExtraCardSources)
            if (!right.RewardContext.ContainsSource(source))
                return false;
        for (int index = 0; index < left.CardRewards.Count; index++)
            if (!RewardPlanValidator.AreEquivalent(left.CardRewards[index], right.CardRewards[index]))
                return false;
        return true;
    }

    /// <summary>
    /// 原子提交此前解析成功的胜利计划。节点结果、灵韵暂存和战斗胜利状态任一失败都会恢复快照。
    /// </summary>
    public bool TryCommitBattleVictory(BattleVictoryPlan plan, out string error)
    {
        error = "";
        if (plan == null || _activeBattle == null || ActiveNode == null || ActiveEncounter == null ||
            plan.NodeId != ActiveNode.NodeId || plan.EncounterId != ActiveEncounter.EnemyId ||
            plan.RewardProfileId != ActiveEncounter.RewardProfileId || _activeBattleVictoryPlan != null ||
            !ReferenceEquals(plan, _pendingBattleVictoryPlan) || !_activeBattle.BattleOver ||
            !_activeBattle.PlayerWon)
        {
            error = "胜利计划与当前活动战斗不匹配，拒绝提交。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }
        if (!TryValidateBattleVictoryPlan(plan, out error))
        {
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

#if DEBUG
        if (_injectBattleVictoryCommitFailureForSelfCheck)
        {
            _injectBattleVictoryCommitFailureForSelfCheck = false;
            error = "自检注入：胜利计划提交失败。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }
#endif

        var snapshot = VictorySnapshot.Capture(this);
        try
        {
            _victoryTransactionSnapshot = snapshot;
            if (!StageLingYunReward(plan.LingYunRewardId, plan.LingYunAmount, out error))
                throw new System.InvalidOperationException(error);

            _activeBattleVictoryPlan = plan;
            _activeBattleVictorySnapshot = CloneBattleVictoryPlan(plan);
            _pendingBattleVictoryPlan = null;
            _pendingBattleVictorySnapshot = null;
            _claimedRewardCandidates.Clear();
            return true;
        }
        catch (System.Exception exception)
        {
            snapshot.Restore(this);
            _victoryTransactionSnapshot = null;
            if (string.IsNullOrWhiteSpace(error))
                error = exception.Message;
            GD.PrintErr($"[GameManager] 胜利计划暂存失败，已回滚：{error}");
            return false;
        }
    }

    /// <summary>
    /// 从当前 GameManager 持有的胜利计划领取一个候选。UI 只能传回计划内的槽位和索引，
    /// 选择达到槽位 ChoiceCount 前保持 overlay，达到后返回 slotComplete=true。
    /// </summary>
    public bool TryClaimBattleCard(BattleVictoryPlan plan, RewardPlan slotPlan, int candidateIndex,
        out bool slotComplete, out string error)
    {
        slotComplete = false;
        error = "";
        if (plan == null || slotPlan == null || !ReferenceEquals(plan, _activeBattleVictoryPlan) ||
            ActiveNode == null || _activeBattle == null || !_activeBattle.BattleOver || !_activeBattle.PlayerWon ||
            plan.NodeId != ActiveNode.NodeId ||
            !AreBattleVictoryPlansEquivalent(plan, _activeBattleVictorySnapshot) ||
            !RewardPlanValidator.TryValidate(slotPlan, out error))
        {
            if (string.IsNullOrWhiteSpace(error))
                error = "卡牌奖励计划未由当前胜利事务持有。";
            GD.PrintErr($"[GameManager] 卡牌奖励领取被阻止：{error}");
            return false;
        }

        RewardPlan owned = null;
        RewardPlan trusted = null;
        foreach (var candidate in plan.CardRewards)
        {
            if (ReferenceEquals(candidate, slotPlan))
            {
                owned = candidate;
                break;
            }
        }
        foreach (var candidate in _activeBattleVictorySnapshot.CardRewards)
            if (candidate.RewardSlotId == slotPlan.RewardSlotId)
                trusted = candidate;
        if (owned == null || owned.RewardSlotId != slotPlan.RewardSlotId ||
            trusted == null ||
            candidateIndex < 0 || candidateIndex >= owned.CandidateCount)
        {
            error = "奖励槽或候选索引不属于当前胜利计划。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        if (!_claimedRewardCandidates.TryGetValue(owned.RewardSlotId, out var selected))
        {
            selected = new HashSet<int>();
            _claimedRewardCandidates[owned.RewardSlotId] = selected;
        }
        if (selected.Contains(candidateIndex) || selected.Count >= owned.ChoiceCount)
        {
            error = "该奖励候选已领取或奖励槽已完成。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        if (!TryCreateRewardId("card", $"{owned.RewardSlotId}:{candidateIndex}",
                out var rewardId, out error) || !GrantReward(new RewardGrant
                {
                    RewardId = rewardId,
                    GrantType = RewardGrantType.Card,
                    Card = RewardPlanValidator.CloneCardDefinition(trusted.Candidates[candidateIndex]),
                }, out error))
        {
            GD.PrintErr($"[GameManager] 卡牌奖励写入失败：{error}");
            return false;
        }

        selected.Add(candidateIndex);
        slotComplete = selected.Count >= owned.ChoiceCount;
        return true;
    }

    /// <summary>
    /// 读取当前奖励槽的领取进度。返回的是副本集合，UI 不能通过它反向修改奖励状态。
    /// </summary>
    public bool TryGetBattleCardRewardProgress(BattleVictoryPlan plan, RewardPlan slotPlan,
        out int selectedCount, out int choiceCount, out bool slotComplete, out string error,
        out System.Collections.Generic.IReadOnlyCollection<int> claimedIndices)
    {
        selectedCount = 0;
        choiceCount = 0;
        slotComplete = false;
        claimedIndices = System.Array.Empty<int>();
        error = "";
        if (plan == null || slotPlan == null || !ReferenceEquals(plan, _activeBattleVictoryPlan) ||
            ActiveNode == null || _activeBattle == null || !_activeBattle.BattleOver ||
            !_activeBattle.PlayerWon || plan.NodeId != ActiveNode.NodeId ||
            !AreBattleVictoryPlansEquivalent(plan, _activeBattleVictorySnapshot) ||
            !RewardPlanValidator.TryValidate(slotPlan, out error))
        {
            if (string.IsNullOrWhiteSpace(error))
                error = "奖励槽不属于当前冻结的胜利计划。";
            GD.PrintErr($"[GameManager] 奖励槽进度读取被阻止：{error}");
            return false;
        }

        RewardPlan owned = null;
        foreach (var candidate in plan.CardRewards)
        {
            if (ReferenceEquals(candidate, slotPlan))
            {
                owned = candidate;
                break;
            }
        }
        if (owned == null)
        {
            error = "奖励槽不属于当前胜利计划。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        choiceCount = owned.ChoiceCount;
        if (_claimedRewardCandidates.TryGetValue(owned.RewardSlotId, out var selected))
        {
            claimedIndices = new HashSet<int>(selected);
            selectedCount = selected.Count;
        }
        slotComplete = selectedCount >= choiceCount;
        return true;
    }

    private bool TryValidateBattleVictoryPlan(BattleVictoryPlan plan, out string error)
    {
        error = "";
        if (!ReferenceEquals(plan, _pendingBattleVictoryPlan) ||
            !AreBattleVictoryPlansEquivalent(plan, _pendingBattleVictorySnapshot))
        {
            error = "胜利计划不是当前解析并冻结的完整奖励计划。";
            return false;
        }
        if (plan.RewardContext == null || plan.CardRewards == null || plan.CardRewards.Count == 0 ||
            plan.LingYunAmount < ActiveEncounter.EnemyInfo.RewardMin ||
            plan.LingYunAmount > ActiveEncounter.EnemyInfo.RewardMax ||
            !TryValidateRewardId(plan.LingYunRewardId, "lingyun", out error))
            return false;

        if (!RewardContext.TryCreate(DaoMarks, out var expectedContext, out error) ||
            expectedContext.ExtraCardSources.Count != plan.RewardContext.ExtraCardSources.Count)
        {
            if (string.IsNullOrWhiteSpace(error))
                error = "胜利计划奖励来源与当前道痕上下文不一致。";
            return false;
        }

        foreach (var expectedSource in expectedContext.ExtraCardSources)
        {
            if (!plan.RewardContext.ContainsSource(expectedSource))
            {
                error = $"胜利计划缺少当前奖励来源：{expectedSource.SourceId}";
                return false;
            }
        }

        var slots = new HashSet<string>();
        var sourcePlanCounts = new Dictionary<string, int>();
        int primaryPlanCount = 0;
        foreach (var rewardPlan in plan.CardRewards)
        {
            if (!RewardPlanValidator.TryValidate(rewardPlan, out error) ||
                rewardPlan.NodeId != ActiveNode.NodeId || !slots.Add(rewardPlan.RewardSlotId))
                return false;

            if (!CardPoolCatalog.TryGet(rewardPlan.CardPoolId, out var pool, out error))
                return false;
            if (!RewardPlanValidator.TryValidateCandidatesAgainstPool(rewardPlan, pool, out error))
                return false;

            if (string.IsNullOrWhiteSpace(rewardPlan.SourceId))
            {
                primaryPlanCount++;
                if (rewardPlan.EncounterId != ActiveEncounter.EnemyId ||
                    !RewardProfileCatalog.TryGet(ActiveEncounter.RewardProfileId, out var expectedProfile, out error) ||
                    rewardPlan.ProfileId != expectedProfile.ProfileId ||
                    rewardPlan.RewardSlotId != expectedProfile.SlotId ||
                    rewardPlan.CardPoolId != expectedProfile.CardPoolId ||
                    rewardPlan.CandidateCount != expectedProfile.CandidateCount ||
                    rewardPlan.ChoiceCount != expectedProfile.ChoiceCount ||
                    rewardPlan.MergeRule != expectedProfile.MergeRule ||
                    rewardPlan.DisplayName != expectedProfile.DisplayName ||
                    rewardPlan.Description != expectedProfile.Description)
                {
                    if (string.IsNullOrWhiteSpace(error))
                        error = "战斗主奖励计划未逐字段匹配 RewardProfileDefinition。";
                    return false;
                }
            }
            else
            {
                RewardSourceContext matchedSource = null;
                foreach (var source in plan.RewardContext.ExtraCardSources)
                    if (source.SourceId == rewardPlan.SourceId)
                        matchedSource = source;
                if (matchedSource == null || rewardPlan.EncounterId != matchedSource.SourceId ||
                    rewardPlan.ProfileId != $"source:{matchedSource.SourceId}" ||
                    matchedSource.CardPoolId != rewardPlan.CardPoolId ||
                    matchedSource.CandidateCount != rewardPlan.CandidateCount ||
                    matchedSource.ChoiceCount != rewardPlan.ChoiceCount ||
                    matchedSource.MergeRule != rewardPlan.MergeRule ||
                    rewardPlan.DisplayName != matchedSource.SourceName ||
                    rewardPlan.Description != matchedSource.DisplayDescription ||
                    !rewardPlan.RewardSlotId.StartsWith(matchedSource.SlotId + ":", System.StringComparison.Ordinal))
                {
                    error = $"额外奖励来源未逐字段匹配 RewardSourceContext：{rewardPlan.RewardSlotId}";
                    return false;
                }
                sourcePlanCounts[matchedSource.SourceId] = sourcePlanCounts.GetValueOrDefault(matchedSource.SourceId) + 1;
            }
        }

        if (primaryPlanCount != 1)
        {
            error = $"胜利计划必须且只能包含一个主奖励槽，实际为 {primaryPlanCount}。";
            return false;
        }
        foreach (var source in expectedContext.ExtraCardSources)
        {
            int expectedCount = source.CardRewardCount;
            if (sourcePlanCounts.GetValueOrDefault(source.SourceId) != expectedCount)
            {
                error = $"额外奖励来源槽位数量不一致：{source.SourceId}";
                return false;
            }
        }
        return true;
    }

    private sealed class VictorySnapshot
    {
        private int _hp;
        private int _lingYun;
        private int _unclaimed;
        private List<CardRuntime> _deck;
        private Dictionary<string, NodeLifecycleState> _states;
        private HashSet<string> _completed;
        private HashSet<string> _results;
        private HashSet<string> _routes;
        private int _mapLayer;
        private int _mapIndex;
        private string _mapNodeId;
        private NodeContext _node;
        private EncounterRequest _encounter;
        private BattleState _battle;
        private EnemyTurnSnapshot _battleSnapshot;
        private BattleVictoryPlan _victoryPlan;
        private BattleVictoryPlan _activeVictorySnapshot;
        private BattleVictoryPlan _pendingVictoryPlan;
        private BattleVictoryPlan _pendingVictorySnapshot;
        private Dictionary<string, HashSet<int>> _claimedRewards;
        private bool _activeResult;
        private int _nextResult;
        private PlayerState _state;
        private bool _openMap;
        private string _lastLingmai;

        public static VictorySnapshot Capture(GameManager manager)
        {
            return new VictorySnapshot
            {
                _hp = manager.PlayerHp,
                _lingYun = manager.LingYun,
                _unclaimed = manager.UnclaimedLingYun,
                _deck = CloneCardRuntimeList(manager._runState.PermanentDeck),
                _states = new Dictionary<string, NodeLifecycleState>(manager._runState.NodeStates),
                _completed = new HashSet<string>(manager._runState.CompletedNodeIds),
                _results = new HashSet<string>(manager._runState.AppliedResultIds),
                _routes = new HashSet<string>(manager._runState.AppliedRouteResultIds),
                _mapLayer = manager.CurrentMapLayer,
                _mapIndex = manager.CurrentMapIndex,
                _mapNodeId = manager.CurrentMapNodeId,
                _node = manager.ActiveNode,
                _encounter = manager.ActiveEncounter,
                _battle = manager._activeBattle,
                _battleSnapshot = EnemyTurnSnapshot.Capture(manager),
                _victoryPlan = manager._activeBattleVictoryPlan,
                _activeVictorySnapshot = manager._activeBattleVictorySnapshot,
                _pendingVictoryPlan = manager._pendingBattleVictoryPlan,
                _pendingVictorySnapshot = manager._pendingBattleVictorySnapshot,
                _claimedRewards = CloneClaimedRewards(manager._claimedRewardCandidates),
                _activeResult = manager._activeResultSubmitted,
                _nextResult = manager._nextNodeResultSequence,
                _state = manager.CurrentState,
                _openMap = manager.OpenMapOnEnter,
                _lastLingmai = manager.LastLingmaiResult,
            };
        }

        public void Restore(GameManager manager)
        {
            manager.PlayerHp = _hp;
            manager.LingYun = _lingYun;
            manager.UnclaimedLingYun = _unclaimed;
            RestoreList(manager._runState.PermanentDeck, _deck);
            RestoreDictionary(manager._runState.NodeStates, _states);
            RestoreSet(manager._runState.CompletedNodeIds, _completed);
            RestoreSet(manager._runState.AppliedResultIds, _results);
            RestoreSet(manager._runState.AppliedRouteResultIds, _routes);
            manager.CurrentMapLayer = _mapLayer;
            manager.CurrentMapIndex = _mapIndex;
            manager.CurrentMapNodeId = _mapNodeId;
            manager.ActiveNode = _node;
            manager.ActiveEncounter = _encounter;
            manager._activeBattle = _battle;
            _battleSnapshot.Restore(manager);
            manager._activeResultSubmitted = _activeResult;
            manager._nextNodeResultSequence = _nextResult;
            manager.CurrentState = _state;
            manager.OpenMapOnEnter = _openMap;
            manager.LastLingmaiResult = _lastLingmai;
            manager._activeBattleVictoryPlan = _victoryPlan;
            manager._activeBattleVictorySnapshot = _activeVictorySnapshot;
            manager._pendingBattleVictoryPlan = _pendingVictoryPlan;
            manager._pendingBattleVictorySnapshot = _pendingVictorySnapshot;
            manager._claimedRewardCandidates.Clear();
            foreach (var pair in _claimedRewards)
                manager._claimedRewardCandidates[pair.Key] = new HashSet<int>(pair.Value);
            manager._victoryTransactionSnapshot = null;
        }

        private static void RestoreList<T>(List<T> target, List<T> source)
        {
            target.Clear();
            target.AddRange(source);
        }

        private static void RestoreDictionary<TKey, TValue>(Dictionary<TKey, TValue> target,
            Dictionary<TKey, TValue> source)
        {
            target.Clear();
            foreach (var pair in source)
                target[pair.Key] = pair.Value;
        }

        private static void RestoreSet<T>(HashSet<T> target, HashSet<T> source)
        {
            target.Clear();
            foreach (var item in source)
                target.Add(item);
        }

        private static List<CardRuntime> CloneCardRuntimeList(IEnumerable<CardRuntime> source)
        {
            var result = new List<CardRuntime>();
            foreach (var card in source)
                if (card != null) result.Add(card.Clone());
            return result;
        }

        private static Dictionary<string, HashSet<int>> CloneClaimedRewards(
            Dictionary<string, HashSet<int>> source)
        {
            var result = new Dictionary<string, HashSet<int>>();
            foreach (var pair in source)
                result[pair.Key] = new HashSet<int>(pair.Value);
            return result;
        }
    }

    /// <summary>从当前活动节点构造可追踪奖励 ID；没有节点时拒绝创建伪奖励。</summary>
    public bool TryCreateRewardId(string rewardKind, string rewardSlot,
        out string rewardId, out string error)
    {
        rewardId = "";
        error = "";
        if (ActiveNode == null || string.IsNullOrWhiteSpace(ActiveNode.NodeId))
        {
            error = "奖励缺少活动节点上下文。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        if (string.IsNullOrWhiteSpace(rewardKind) || string.IsNullOrWhiteSpace(rewardSlot))
        {
            error = "奖励缺少来源类型或奖励槽。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        rewardId = $"{rewardKind}:{ActiveNode.NodeId}:{rewardSlot}";
        return true;
    }

    private bool TryValidateRewardId(string rewardId, string rewardKind, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(rewardId) || string.IsNullOrWhiteSpace(rewardKind) ||
            ActiveNode == null || string.IsNullOrWhiteSpace(ActiveNode.NodeId))
        {
            error = "奖励缺少有效活动节点上下文或 RewardId。";
            return false;
        }

        string prefix = $"{rewardKind}:{ActiveNode.NodeId}:";
        if (!rewardId.StartsWith(prefix, System.StringComparison.Ordinal))
        {
            error = $"RewardId 与当前活动节点不匹配：{rewardId}";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 统一奖励写入端口。当前阶段校验 RewardId 但尚未持久化消费表，完整幂等留待奖励阶段。
    /// </summary>
    public bool GrantReward(RewardGrant grant, out string error)
    {
        error = "";
        if (grant == null || string.IsNullOrEmpty(grant.RewardId))
        {
            error = "奖励缺少 RewardId。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        string rewardKind = grant.GrantType == RewardGrantType.Card ? "card" : "lingyun";
        if (!TryValidateRewardId(grant.RewardId, rewardKind, out error))
        {
            GD.PrintErr($"[GameManager] 奖励写入被阻止：{error}");
            return false;
        }

        switch (grant.GrantType)
        {
            case RewardGrantType.LingYun when grant.LingYunAmount >= 0:
                LingYun += grant.LingYunAmount;
                return true;
            case RewardGrantType.Card when grant.Card != null:
                if (!CardCatalogService.TryCreateRuntimeCard(grant.Card.DefinitionId ?? grant.Card.Id, out var rewardCard, out error))
                {
                    GD.PrintErr($"[GameManager] 奖励卡写入被阻止：{error}");
                    return false;
                }
                _runState.PermanentDeck.Add(rewardCard);
                return true;
            default:
                error = $"奖励内容无效：{grant.RewardId}/{grant.GrantType}";
                GD.PrintErr($"[GameManager] {error}");
                return false;
        }
    }

    public bool StageLingYunReward(string rewardId, int amount, out string error)
    {
        error = "";
        if (amount < 0 || !TryValidateRewardId(rewardId, "lingyun", out error))
        {
            if (string.IsNullOrEmpty(error))
                error = "暂存灵韵奖励参数无效。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        UnclaimedLingYun = amount;
        return true;
    }

    public bool ClaimUnclaimedLingYun(string rewardId, out string error)
    {
        error = "";
        if (!TryValidateRewardId(rewardId, "lingyun", out error))
        {
            GD.PrintErr($"[GameManager] 灵韵领取被阻止：{error}");
            return false;
        }

        if (UnclaimedLingYun <= 0)
            return true;

        int amount = UnclaimedLingYun;
        if (!GrantReward(new RewardGrant
            {
                RewardId = rewardId,
                GrantType = RewardGrantType.LingYun,
                LingYunAmount = amount,
            }, out error))
            return false;

        UnclaimedLingYun = 0;
        return true;
    }

    public void AddCardToDeck(CardInfo cardDef)
    {
        if (cardDef == null)
        {
            GD.PrintErr("[GameManager] 添加卡牌失败：卡牌定义为空。");
            return;
        }

        if (!CardCatalogService.TryCreateRuntimeCard(cardDef.DefinitionId ?? cardDef.Id, out var runtime, out var error))
        {
            GD.PrintErr($"[GameManager] 添加卡牌失败：{error}");
            return;
        }
        _runState.PermanentDeck.Add(runtime);
    }

    public bool TryRemoveCardFromDeck(CardRuntime card, out string error)
    {
        error = "";
        if (card == null || _runState.PermanentDeck.Count <= 1 || !_runState.PermanentDeck.Remove(card))
        {
            error = "牌组至少保留一张牌，或目标牌已不存在。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }
        return true;
    }

    /// <summary>
    /// 返回永久牌组中的运行时卡牌引用。战斗牌堆是临时复制品，不能作为套牌页面事实来源。
    /// </summary>
    public List<CardRuntime> GetAllDeckCards()
    {
        return new List<CardRuntime>(_runState.PermanentDeck);
    }

    /// <summary>返回永久套牌，供 DeckViewer 和奖励/升级系统使用。</summary>
    public IReadOnlyList<CardRuntime> GetPermanentDeckCards() => _runState.PermanentDeck;

    /// <summary>
    /// 返回当前牌组中存在升级版的卡牌运行时引用。
    /// </summary>
    public List<CardRuntime> GetUpgradeableCards()
    {
        var upgradeable = new List<CardRuntime>();
        foreach (var card in GetAllDeckCards())
        {
            if (TryGetUpgradeForCard(card.Info, out _))
                upgradeable.Add(card);
        }
        return upgradeable;
    }

    /// <summary>
    /// 查询一张卡的升级版定义。升级边由 CardDefinitionResource 的 Upgrade 字段提供，
    /// 不按卡名特判，也不再读取 DataDefs 旧快照。
    /// </summary>
    public bool TryGetUpgradeForCard(CardInfo cardInfo, out CardInfo upgradedInfo)
    {
        upgradedInfo = null;
        if (cardInfo == null || string.IsNullOrEmpty(cardInfo.UpgradeToId))
            return false;

        return CardCatalogService.TryGetCardProjection(cardInfo.UpgradeToId, out upgradedInfo, out _);
    }

    /// <summary>
    /// 将当前牌组中的指定运行时卡牌永久替换为升级版。
    /// </summary>
    public bool TryUpgradeCard(CardRuntime card)
    {
        if (card == null || !GetAllDeckCards().Contains(card))
            return false;

        if (!TryGetUpgradeForCard(card.Info, out var upgradedInfo))
            return false;

        if (!CardCatalogService.TryCreateRuntimeCard(upgradedInfo.DefinitionId ?? upgradedInfo.Id, out var upgradedRuntime, out var error))
        {
            GD.PrintErr($"[GameManager] 升级卡牌失败：{error}");
            return false;
        }
        card.ReplaceWith(upgradedRuntime);
        return true;
    }

    public int GetDeckSize()
    {
        return _runState.PermanentDeck.Count;
    }

    private static void ReplaceList<T>(List<T> target, IEnumerable<T> values)
    {
        target.Clear();
        if (values != null)
            target.AddRange(values);
    }

    // ==================== 场景切换 ====================

    /// <summary>统一执行底层场景切换，并把 Godot 返回的错误转成可追踪状态。</summary>
    public bool ChangeSceneToFile(string scenePath, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(scenePath))
        {
            error = "场景路径为空，拒绝切换。";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        var result = GetTree().ChangeSceneToFile(scenePath);
        if (result != Error.Ok)
        {
            error = $"场景切换失败：{scenePath} / {result}";
            GD.PrintErr($"[GameManager] {error}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Abandons the current run without submitting a node result or reward, then routes to the
    /// title through the core scene boundary. This deliberately clears in-memory run state because
    /// the current MVP has no save/resume contract to preserve.
    /// </summary>
    public bool TryAbandonRunToTitle(out string error)
    {
        error = "";
        ClearRunStateForAbandon();
        return ChangeSceneToFile("res://scenes/Title/Title.tscn", out error);
    }

    /// <summary>仅供 Debug 启动自检验证放弃本局的状态清理，不执行场景切换。</summary>
    internal bool TryClearRunStateForSelfCheck(out string error)
    {
        error = "";
        ClearRunStateForAbandon();
        return true;
    }

    private void ClearRunStateForAbandon()
    {
        DisposeBattleState();
        ActiveNode = null;
        ActiveEncounter = null;
        _activeResultSubmitted = false;
        _nextNodeResultSequence = 0;
        _lastCardExecutionTrace.Clear();
        _lastResolvedCardExecution = null;

        _runState.PermanentDeck.Clear();
        _runState.DaoMarks.Clear();
        _runState.Party.Clear();
        _runState.CurrentChoices.Clear();
        _runState.NodeStates.Clear();
        _runState.CompletedNodeIds.Clear();
        _runState.AppliedResultIds.Clear();
        _runState.AppliedRouteResultIds.Clear();
        _runState.MapGraph = null;
        _runState.MapNodesUnlocked = false;
        _runState.DaoMarkSelected = false;
        _runState.PlayerMaxHp = 0;
        _runState.PlayerHp = 0;
        _runState.PlayerMaxLingli = 0;
        _runState.LingYun = 0;
        _runState.UnclaimedLingYun = 0;
        _runState.ActIndex = 0;
        _runState.ActId = "";
        _runState.RuleVersion = 0;
        _runState.RunSeed = 0;
        _runState.CurrentMapLayer = 0;
        _runState.CurrentMapIndex = 0;
        _runState.CurrentMapNodeId = string.Empty;

        CharacterId = null;
        SelectedCharacter = null;
        Difficulty = "地仙";
        _navigationState.OpenMapOnEnter = false;
        _navigationState.MapEntryMode = MapEntryMode.None;
        _navigationState.LastLingmaiResult = "";
        CurrentState = PlayerState.空闲;
    }

    /// <summary>旧场景入口兼容包装；正式节点流程使用 ChangeSceneToFile。</summary>
    public void GoToScene(string scenePath)
    {
        ChangeSceneToFile(scenePath, out _);
    }

    public void GoToTitle()
    {
        ChangeSceneToFile("res://scenes/Title/Title.tscn", out _);
    }
}

public class CardRuntime
{
    public CardInfo Info;
    public CardExecutionPlan ExecutionPlan { get; private set; }

    public CardRuntime(CardInfo info, CardExecutionPlan executionPlan)
    {
        Info = info;
        ExecutionPlan = executionPlan;
    }

    /// <summary>战斗/快照永远复制实例和兼容投影，不共享永久套牌对象。</summary>
    public CardRuntime Clone()
    {
        return new CardRuntime(RewardPlanValidator.CloneCardDefinition(Info), ExecutionPlan);
    }

    internal void ReplaceWith(CardRuntime replacement)
    {
        Info = replacement?.Info;
        ExecutionPlan = replacement?.ExecutionPlan;
    }
}
