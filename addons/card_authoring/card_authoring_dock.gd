@tool
extends VBoxContainer

const CATALOG_PATH := "res://resources/cards/CardCatalog.tres"
const CARD_DIR := "res://resources/cards/"
const CardAuthoringTransaction = preload("res://addons/card_authoring/card_authoring_transaction.gd")
var _catalog: Resource
var _draft: Resource
var _original: Resource
var _active_effect_index := -1
var _suppress_effect_selection := false

@onready var _content_scroll: ScrollContainer = $ContentScroll
@onready var _list: ItemList = $ContentScroll/Split/CatalogList
@onready var _id: LineEdit = $ContentScroll/Split/Editor/Id
@onready var _display_name: LineEdit = $ContentScroll/Split/Editor/DisplayName
@onready var _description: TextEdit = $ContentScroll/Split/Editor/Description
@onready var _owner: LineEdit = $ContentScroll/Split/Editor/Owner
@onready var _owner_kind: OptionButton = $ContentScroll/Split/Editor/OwnerKind
@onready var _rarity: OptionButton = $ContentScroll/Split/Editor/Rarity
@onready var _pool: LineEdit = $ContentScroll/Split/Editor/RewardPool
@onready var _pools_configured: CheckBox = $ContentScroll/Split/Editor/RewardPoolsConfigured
@onready var _family: LineEdit = $ContentScroll/Split/Editor/UpgradeFamily
@onready var _upgrade_configured: CheckBox = $ContentScroll/Split/Editor/UpgradeConfigured
@onready var _can_upgrade: CheckBox = $ContentScroll/Split/Editor/CanUpgrade
@onready var _upgrade_next_card_id: LineEdit = $ContentScroll/Split/Editor/UpgradeNextCardId
@onready var _tags: LineEdit = $ContentScroll/Split/Editor/Tags
@onready var _availability: LineEdit = $ContentScroll/Split/Editor/Availability
@onready var _balance_role: LineEdit = $ContentScroll/Split/Editor/BalanceRole
@onready var _cost: SpinBox = $ContentScroll/Split/Editor/Cost
@onready var _cost_kind: OptionButton = $ContentScroll/Split/Editor/CostKind
@onready var _category: OptionButton = $ContentScroll/Split/Editor/Category
@onready var _selection_mode: OptionButton = $ContentScroll/Split/Editor/SelectionMode
@onready var _target_scope: OptionButton = $ContentScroll/Split/Editor/TargetScope
@onready var _target_min: SpinBox = $ContentScroll/Split/Editor/TargetMin
@onready var _target_min_configured: CheckBox = $ContentScroll/Split/Editor/TargetMinConfigured
@onready var _target_max: SpinBox = $ContentScroll/Split/Editor/TargetMax
@onready var _target_max_configured: CheckBox = $ContentScroll/Split/Editor/TargetMaxConfigured
@onready var _allow_dead: CheckBox = $ContentScroll/Split/Editor/AllowDead
@onready var _allow_dead_configured: CheckBox = $ContentScroll/Split/Editor/AllowDeadConfigured
@onready var _redirect: OptionButton = $ContentScroll/Split/Editor/Redirect
@onready var _effects: ItemList = $ContentScroll/Split/Editor/Effects
@onready var _effect_type: OptionButton = $ContentScroll/Split/Editor/EffectType
@onready var _effect_amount: SpinBox = $ContentScroll/Split/Editor/EffectAmount
@onready var _effect_amount_configured: CheckBox = $ContentScroll/Split/Editor/EffectAmountConfigured
@onready var _effect_target: OptionButton = $ContentScroll/Split/Editor/EffectTarget
@onready var _effect_status: OptionButton = $ContentScroll/Split/Editor/EffectStatus
@onready var _effect_duration: OptionButton = $ContentScroll/Split/Editor/EffectDuration
@onready var _effect_zone: OptionButton = $ContentScroll/Split/Editor/EffectZone
@onready var _preview_key: LineEdit = $ContentScroll/Split/Editor/PreviewKey
@onready var _preview_configured: CheckBox = $ContentScroll/Split/Editor/PreviewConfigured
@onready var _preview: RichTextLabel = $ContentScroll/Split/Editor/Preview
@onready var _status: RichTextLabel = $Status
@onready var _validation_bridge: Node = $CardAuthoringValidationBridge

func _ready() -> void:
    $Toolbar/New.pressed.connect(_new_draft)
    $Toolbar/Load.pressed.connect(_load_catalog)
    $Toolbar/Validate.pressed.connect(_validate)
    $Toolbar/Diff.pressed.connect(_show_diff)
    $Toolbar/Save.pressed.connect(_save)
    $Toolbar/Cancel.pressed.connect(_cancel)
    $ContentScroll/Split/Editor/EffectOrder/Up.pressed.connect(func(): _move_effect(-1))
    $ContentScroll/Split/Editor/EffectOrder/Down.pressed.connect(func(): _move_effect(1))
    $ContentScroll/Split/Editor/EffectActions/Add.pressed.connect(_add_effect)
    $ContentScroll/Split/Editor/EffectActions/Remove.pressed.connect(_remove_effect)
    _list.item_selected.connect(_select_card)
    _add_pending(_owner_kind); _owner_kind.add_item("角色", 0); _owner_kind.add_item("中立", 1)
    _add_pending(_rarity); _rarity.add_item("黄", 0); _rarity.add_item("黑", 1); _rarity.add_item("地", 2); _rarity.add_item("天", 3)
    _add_pending(_cost_kind); _cost_kind.add_item("灵力", 0); _cost_kind.add_item("生命", 1)
    _add_pending(_category); _category.add_item("斗击", 0); _category.add_item("术法", 1)
    _add_pending(_selection_mode); _selection_mode.add_item("无", 0); _selection_mode.add_item("可选", 1); _selection_mode.add_item("必选", 2)
    _add_pending(_target_scope); _target_scope.add_item("无", 0); _target_scope.add_item("自身", 1); _target_scope.add_item("单敌", 2)
    _add_pending(_redirect); _redirect.add_item("拒绝", 0); _redirect.add_item("重新选择", 1); _redirect.add_item("跳过", 2)
    _add_pending(_effect_target); _effect_target.add_item("已选目标", 0); _effect_target.add_item("自身", 1)
    _add_pending(_effect_status); _effect_status.add_item("无", 0); _effect_status.add_item("斗劲", 1); _effect_status.add_item("易损", 2); _effect_status.add_item("永炎", 3)
    _add_pending(_effect_duration); _effect_duration.add_item("战斗", 0); _effect_duration.add_item("回合", 1)
    _add_pending(_effect_zone); _effect_zone.add_item("无", 0); _effect_zone.add_item("消弭", 1)
    _add_pending(_effect_type)
    _effect_type.add_item("伤害", 0)
    _effect_type.add_item("护体", 1)
    _effect_type.add_item("自损", 2)
    _effect_type.add_item("状态", 3)
    _effect_type.add_item("消弭", 4)
    _effects.item_selected.connect(_on_effect_selected)
    _target_min_configured.toggled.connect(func(configured: bool): _target_min.editable = configured)
    _target_max_configured.toggled.connect(func(configured: bool): _target_max.editable = configured)
    _allow_dead_configured.toggled.connect(func(configured: bool): _allow_dead.disabled = not configured)
    _effect_amount_configured.toggled.connect(func(configured: bool): _effect_amount.editable = configured)
    _pools_configured.toggled.connect(func(configured: bool): _pool.editable = configured)
    _upgrade_configured.toggled.connect(func(_configured: bool): _refresh_upgrade_editability())
    _can_upgrade.toggled.connect(func(_enabled: bool): _refresh_upgrade_editability())
    _preview_configured.toggled.connect(func(configured: bool): _preview_key.editable = configured)
    _load_catalog()

func _load_catalog() -> void:
    _catalog = ResourceLoader.load(CATALOG_PATH)
    _list.clear()
    if not _catalog:
        _set_status("[color=red]无法加载 CardCatalog。[/color]")
        return
    for path in _catalog.CardResourcePaths:
        _list.add_item(path.get_file().get_basename())
        _list.set_item_metadata(_list.item_count - 1, path)
    _set_status("[color=gray]Catalog 已加载；编辑草稿不会触碰 RunState、BattleState 或 RewardPlan。[/color]")

func _new_draft() -> void:
    _draft = CardDefinitionResource.new()
    _draft.EditorDraft = true
    # 新草稿故意保持未分配；字段级校验通过前不可写入任何生产资源。
    _draft.DescriptionFallback = ""
    _draft.OwnerCharacterId = ""
    # SchemaVersion 是唯一允许预置的字段；其余绑定数据都以不可写入的未分配状态开始。
    _draft.Costs = []
    _draft.TargetPolicy = {}
    _draft.Effects = []
    _draft.RewardPoolIds = []
    _draft.Upgrade = {}
    _draft.Tags = []
    _draft.Availability = {}
    _draft.Preview = {}
    _draft.Balance = {}
    _original = null
    _read_draft_to_form()
    _set_status("[color=yellow]新草稿处于未分配状态；请完成必填字段并通过校验后再写入。[/color]")

func _select_card(index: int) -> void:
    var path := str(_list.get_item_metadata(index))
    _draft = ResourceLoader.load(path).duplicate(true)
    _original = ResourceLoader.load(path)
    _read_draft_to_form()
    _set_status("[color=gray]已载入副本；未点击确认写入前，正式资源不变。[/color]")

func _read_draft_to_form() -> void:
    if not _draft: return
    _suppress_effect_selection = true
    _active_effect_index = -1
    _id.text = _draft.Id
    _display_name.text = _draft.DisplayName
    _description.text = _draft.DescriptionFallback
    _owner.text = _draft.OwnerCharacterId
    _select_option_id(_owner_kind, _draft.OwnerKind if not _draft.EditorDraft else -1)
    _select_option_id(_rarity, _draft.Rarity if not _draft.EditorDraft else -1)
    _pool.text = ", ".join(PackedStringArray(_draft.RewardPoolIds))
    _pools_configured.button_pressed = _draft.RewardPoolIds.size() > 0
    _pool.editable = _pools_configured.button_pressed
    _pool.tooltip_text = "" if _pools_configured.button_pressed else "待配置"
    _upgrade_configured.button_pressed = _has_complete_upgrade_fields(_draft.Upgrade)
    _can_upgrade.button_pressed = bool(_draft.Upgrade["canUpgrade"]) if _draft.Upgrade.has("canUpgrade") else false
    _family.text = str(_draft.Upgrade["familyId"]) if _draft.Upgrade.has("familyId") else ""
    _upgrade_next_card_id.text = str(_draft.Upgrade["nextCardId"]) if _draft.Upgrade.has("nextCardId") else ""
    _refresh_upgrade_editability()
    _tags.text = ",".join(PackedStringArray(_draft.Tags))
    _availability.text = str(_draft.Availability.get("minContentVersion", ""))
    _balance_role.text = str(_draft.Balance.get("role", ""))
    _cost.value = int(_draft.Costs[0]["amount"]) if _draft.Costs.size() > 0 and _draft.Costs[0].has("amount") else 0
    _select_option_id(_cost_kind, int(_draft.Costs[0]["costType"]) if _draft.Costs.size() > 0 and _draft.Costs[0].has("costType") else -1)
    _select_option_id(_category, _draft.Category if not _draft.EditorDraft else -1)
    _select_option_id(_selection_mode, int(_draft.TargetPolicy["selectionMode"]) if _draft.TargetPolicy.has("selectionMode") else -1)
    _select_option_id(_target_scope, int(_draft.TargetPolicy["scope"]) if _draft.TargetPolicy.has("scope") else -1)
    _select_option_id(_redirect, int(_draft.TargetPolicy["retargetOnInvalid"]) if _draft.TargetPolicy.has("retargetOnInvalid") else -1)
    _target_min.value = int(_draft.TargetPolicy["minimumTargets"]) if _draft.TargetPolicy.has("minimumTargets") else 0
    _target_min_configured.button_pressed = _draft.TargetPolicy.has("minimumTargets")
    _target_min.editable = _target_min_configured.button_pressed
    _target_min.tooltip_text = "" if _target_min_configured.button_pressed else "待配置"
    _target_max.value = int(_draft.TargetPolicy["maximumTargets"]) if _draft.TargetPolicy.has("maximumTargets") else 0
    _target_max_configured.button_pressed = _draft.TargetPolicy.has("maximumTargets")
    _target_max.editable = _target_max_configured.button_pressed
    _target_max.tooltip_text = "" if _target_max_configured.button_pressed else "待配置"
    _allow_dead.button_pressed = bool(_draft.TargetPolicy["allowDeadTargets"]) if _draft.TargetPolicy.has("allowDeadTargets") else false
    _allow_dead_configured.button_pressed = _draft.TargetPolicy.has("allowDeadTargets")
    _allow_dead.disabled = not _allow_dead_configured.button_pressed
    _allow_dead.tooltip_text = "" if _allow_dead_configured.button_pressed else "待配置"
    _preview_key.text = str(_draft.Preview["frameKey"]) if _draft.Preview.has("frameKey") else ""
    _preview_configured.button_pressed = _draft.Preview.has("frameKey") and not _preview_key.text.strip_edges().is_empty()
    _preview_key.editable = _preview_configured.button_pressed
    _preview_key.tooltip_text = "" if _preview_configured.button_pressed else "待配置"
    _effects.clear()
    for effect in _draft.Effects:
        _effects.add_item("%s: type=%s amount=%s target=%s" % [effect.get("order", "待配置"), effect.get("effectType", "待配置"), effect.get("amount", "待配置"), effect.get("targetSelector", "待配置")])
    _suppress_effect_selection = false
    if not _draft.Effects.is_empty():
        _effects.select(0)
        _select_effect(0)
    else:
        _reset_effect_form()
    _show_preview()

func _apply_form() -> void:
    if not _draft: return
    _draft.Id = _id.text.strip_edges()
    _draft.DisplayName = _display_name.text.strip_edges()
    _draft.DescriptionFallback = _description.text.strip_edges()
    _draft.OwnerCharacterId = _owner.text.strip_edges()
    if _selected_option_value(_owner_kind) >= 0: _draft.OwnerKind = _selected_option_value(_owner_kind)
    if _selected_option_value(_rarity) >= 0: _draft.Rarity = _selected_option_value(_rarity)
    _draft.RewardPoolIds = _parse_pool_ids(_pool.text) if _pools_configured.button_pressed else PackedStringArray()
    if _upgrade_configured.button_pressed:
        _draft.Upgrade = {
            "canUpgrade": _can_upgrade.button_pressed,
            "nextCardId": _upgrade_next_card_id.text.strip_edges(),
            "familyId": _family.text.strip_edges(),
        }
    else:
        _draft.Upgrade = {}
    _draft.Tags = PackedStringArray(_tags.text.split(",", false))
    _draft.Availability = {"minContentVersion": _availability.text.strip_edges()}
    _draft.Balance = {"role": _balance_role.text.strip_edges()}
    _draft.Preview = {"frameKey": _preview_key.text.strip_edges()} if _preview_configured.button_pressed else {}
    if _selected_option_value(_category) >= 0: _draft.Category = _selected_option_value(_category)
    var costs: Array = _draft.Costs
    if not costs.is_empty() and _selected_option_value(_cost_kind) >= 0:
        costs[0]["amount"] = int(_cost.value)
        costs[0]["costType"] = _selected_option_value(_cost_kind)
    _draft.Costs = costs
    if _selected_option_value(_selection_mode) >= 0 and _selected_option_value(_target_scope) >= 0 and _selected_option_value(_redirect) >= 0 and _target_min_configured.button_pressed and _target_max_configured.button_pressed and _allow_dead_configured.button_pressed:
        _draft.TargetPolicy = {"selectionMode": _selected_option_value(_selection_mode), "scope": _selected_option_value(_target_scope), "minimumTargets": int(_target_min.value), "maximumTargets": int(_target_max.value), "allowDeadTargets": _allow_dead.button_pressed, "retargetOnInvalid": _selected_option_value(_redirect)}
    _write_active_effect()
    _show_preview()

func _move_effect(direction: int) -> void:
    if not _draft or _effects.get_selected_items().is_empty(): return
    _write_active_effect()
    var from := _effects.get_selected_items()[0]
    var to := clampi(from + direction, 0, _draft.Effects.size() - 1)
    if from == to: return
    var entries: Array = _draft.Effects.duplicate(true)
    var entry = entries[from]
    entries.remove_at(from)
    entries.insert(to, entry)
    for index in entries.size(): entries[index]["order"] = index + 1
    _draft.Effects = entries
    _read_draft_to_form()
    _effects.select(to)
    _select_effect(to)

func _add_effect() -> void:
    if not _draft: return
    _write_active_effect()
    var entries: Array = _draft.Effects.duplicate(true)
    entries.append({"order": entries.size() + 1})
    _draft.Effects = entries
    _read_draft_to_form()
    _effects.select(entries.size() - 1)
    _select_effect(entries.size() - 1)

func _remove_effect() -> void:
    if not _draft or _effects.get_selected_items().is_empty(): return
    _write_active_effect()
    var entries: Array = _draft.Effects.duplicate(true)
    var removed_index := _effects.get_selected_items()[0]
    entries.remove_at(removed_index)
    for index in entries.size(): entries[index]["order"] = index + 1
    _draft.Effects = entries
    _read_draft_to_form()
    if entries.is_empty():
        _active_effect_index = -1
        _reset_effect_form()
        return
    var next_index := mini(removed_index, entries.size() - 1)
    _effects.select(next_index)
    _select_effect(next_index)

func _select_effect(index: int) -> void:
    if not _draft or index >= _draft.Effects.size(): return
    var effect: Dictionary = _draft.Effects[index]
    _select_option_id(_effect_type, int(effect["effectType"]) if effect.has("effectType") else -1)
    _effect_amount.value = int(effect["amount"]) if effect.has("amount") else 0
    _effect_amount_configured.button_pressed = effect.has("amount")
    _effect_amount.editable = _effect_amount_configured.button_pressed
    _effect_amount.tooltip_text = "" if _effect_amount_configured.button_pressed else "待配置"
    _select_option_id(_effect_target, int(effect["targetSelector"]) if effect.has("targetSelector") else -1)
    _select_option_id(_effect_status, int(effect["statusKind"]) if effect.has("statusKind") else -1)
    _select_option_id(_effect_duration, int(effect["durationScope"]) if effect.has("durationScope") else -1)
    _select_option_id(_effect_zone, int(effect["destinationZone"]) if effect.has("destinationZone") else -1)
    _active_effect_index = index

func _on_effect_selected(index: int) -> void:
    if _suppress_effect_selection: return
    _write_active_effect()
    _select_effect(index)

func _write_active_effect() -> void:
    if not _draft or _active_effect_index < 0 or _active_effect_index >= _draft.Effects.size(): return
    var effect: Dictionary = _draft.Effects[_active_effect_index].duplicate(true)
    if _selected_option_value(_effect_type) >= 0: effect["effectType"] = _selected_option_value(_effect_type)
    if _effect_amount_configured.button_pressed: effect["amount"] = int(_effect_amount.value)
    if _selected_option_value(_effect_target) >= 0: effect["targetSelector"] = _selected_option_value(_effect_target)
    if _selected_option_value(_effect_status) >= 0: effect["statusKind"] = _selected_option_value(_effect_status)
    if _selected_option_value(_effect_duration) >= 0: effect["durationScope"] = _selected_option_value(_effect_duration)
    if _selected_option_value(_effect_zone) >= 0: effect["destinationZone"] = _selected_option_value(_effect_zone)
    _draft.Effects[_active_effect_index] = effect

func _reset_effect_form() -> void:
    _select_option_id(_effect_type, -1)
    _effect_amount.value = 0
    _effect_amount_configured.button_pressed = false
    _effect_amount.editable = false
    _effect_amount.tooltip_text = "待配置"
    _select_option_id(_effect_target, -1)
    _select_option_id(_effect_status, -1)
    _select_option_id(_effect_duration, -1)
    _select_option_id(_effect_zone, -1)

func _select_option_id(button: OptionButton, item_id: int) -> void:
    # Godot reserves -1 as the absence of an item ID. The visible first entry is
    # therefore mapped to semantic value -1; production enum values use index + 1.
    button.select(0 if item_id < 0 else item_id + 1)

func _selected_option_value(button: OptionButton) -> int:
    return button.selected - 1 if button.selected >= 0 else -1

func _add_pending(button: OptionButton) -> void:
    button.add_item("待配置")

func _parse_pool_ids(raw_text: String) -> PackedStringArray:
    var result := PackedStringArray()
    for token in raw_text.replace("\n", ",").split(",", false):
        var pool_id := token.strip_edges()
        if not pool_id.is_empty() and not result.has(pool_id):
            result.append(pool_id)
    return result

func _has_complete_upgrade_fields(upgrade: Dictionary) -> bool:
    return upgrade.has("canUpgrade") and upgrade.has("nextCardId") and upgrade.has("familyId")

func _refresh_upgrade_editability() -> void:
    var configured := _upgrade_configured.button_pressed
    _can_upgrade.disabled = not configured
    _family.editable = configured
    _upgrade_next_card_id.editable = configured
    _family.tooltip_text = "" if configured else "待配置"
    _upgrade_next_card_id.tooltip_text = "" if configured else "待配置"

func _validate() -> bool:
    _apply_form()
    if not _draft:
        _set_status("[color=red]没有可校验的草稿。[/color]")
        return false
    # 仅对候选生产副本解除草稿标记；失败后恢复，避免 UI 草稿被伪装为正式定义。
    var was_draft: bool = _draft.EditorDraft
    _draft.EditorDraft = false
    var errors: PackedStringArray = _draft.EditorValidationErrors
    _draft.EditorDraft = was_draft
    if not errors.is_empty():
        _set_status("[color=red]校验失败：\n%s[/color]" % "\n".join(errors))
        return false
    _set_status("[color=green]字段校验通过。生成/保存仍需显式点击。[/color]")
    return true

func _show_preview() -> void:
    if not _draft: return
    var target := "待配置"
    if _selected_option_value(_target_scope) == 1: target = "自身"
    elif _selected_option_value(_target_scope) == 2: target = "单敌"
    var preview_frame := _preview_key.text.strip_edges()
    if not _preview_configured.button_pressed or preview_frame.is_empty():
        preview_frame = "待配置"
    var pools := _pool.text.strip_edges()
    if not _pools_configured.button_pressed or pools.is_empty():
        pools = "待配置"
    _preview.text = "[center][b]%s[/b][/center]\n归属：%s\n费用：%s\n目标：%s\n卡池：%s\n预览框：%s\n效果：%s" % [_draft.DisplayName, _draft.OwnerCharacterId, _cost.value, target, pools, preview_frame, _draft.DescriptionFallback]

func _show_diff() -> void:
    _apply_form()
    if not _draft:
        _set_status("[color=red]没有可生成预览/Diff 的草稿。[/color]")
        return
    var before := "新建资源" if not _original else "%s / %s费" % [_original.DisplayName, _original.Costs[0].get("amount", 0)]
    _set_status("[color=aqua]生成预览：%s -> %s / %s费 / %s 项效果。[/color]" % [before, _draft.DisplayName, _cost.value, _draft.Effects.size()])

func _save() -> void:
    if not _validate(): return
    var was_draft: bool = _draft.EditorDraft
    _draft.EditorDraft = false
    var target_path: String = CARD_DIR + str(_draft.Id) + ".tres"
    var result := CardAuthoringTransaction.commit(_draft, _catalog, target_path, _original == null, _validation_bridge)
    if not result.ok:
        _draft.EditorDraft = was_draft
        _set_status("[color=red]%s[/color]" % result.error)
        return
    _set_status("[color=green]已确认写入单卡和 Catalog 索引：%s[/color]" % target_path)
    _load_catalog()

func _cancel() -> void:
    _draft = null
    _original = null
    _set_status("[color=gray]草稿已丢弃；正式 Catalog 和玩家运行状态未变化。[/color]")

func _set_status(message: String) -> void:
    _status.text = message

## Editor-only smoke check used by card_authoring_form_selfcheck.gd.
## It exercises the actual Dock scene and its control bindings without touching Catalog files.
func run_form_binding_self_check() -> Dictionary:
    if _content_scroll == null or _content_scroll.vertical_scroll_mode == ScrollContainer.SCROLL_MODE_DISABLED:
        return {"ok": false, "error": "Dock 缺少可用的垂直滚动内容区。"}
    if _status.get_parent() != self or _status.custom_minimum_size.y < 48:
        return {"ok": false, "error": "状态区未保留在 Dock 当前可见区域。"}
    _new_draft()
    _show_diff()
    if not _status.text.contains("生成预览"):
        return {"ok": false, "error": "生成预览/Diff 未写入当前可见状态区。"}
    _validate()
    if not _status.text.contains("校验失败"):
        return {"ok": false, "error": "字段校验失败未写入当前可见状态区。"}
    var pending_controls: Array[OptionButton] = [_selection_mode, _target_scope, _redirect, _effect_type, _effect_target, _effect_status, _effect_duration, _effect_zone]
    for control in pending_controls:
        if _selected_option_value(control) != -1:
            return {"ok": false, "error": "%s 新草稿未显示待配置（selected=%s semantic=%s）。" % [control.name, control.selected, _selected_option_value(control)]}

    _select_option_id(_selection_mode, 2)
    _select_option_id(_target_scope, 2)
    _select_option_id(_redirect, 0)
    _target_min_configured.button_pressed = true
    _target_min.value = 1
    _target_max_configured.button_pressed = true
    _target_max.value = 1
    _allow_dead_configured.button_pressed = true
    _allow_dead.button_pressed = false
    _apply_form()
    _add_effect()
    _select_option_id(_effect_type, 0)
    _effect_amount_configured.button_pressed = true
    _effect_amount.value = 7
    _select_option_id(_effect_target, 0)
    _select_option_id(_effect_status, 0)
    _select_option_id(_effect_duration, 0)
    _select_option_id(_effect_zone, 0)
    _write_active_effect()
    _add_effect()
    _select_option_id(_effect_type, 1)
    _effect_amount_configured.button_pressed = true
    _effect_amount.value = 11
    _select_option_id(_effect_target, 1)
    _select_option_id(_effect_status, 1)
    _select_option_id(_effect_duration, 1)
    _select_option_id(_effect_zone, 1)
    _write_active_effect()
    _apply_form()

    var policy: Dictionary = _draft.TargetPolicy
    for key in ["selectionMode", "scope", "minimumTargets", "maximumTargets", "allowDeadTargets", "retargetOnInvalid"]:
        if not policy.has(key): return {"ok": false, "error": "TargetPolicy 缺少 %s（selection=%s scope=%s redirect=%s min=%s max=%s dead=%s）。" % [key, _selected_option_value(_selection_mode), _selected_option_value(_target_scope), _selected_option_value(_redirect), _target_min_configured.button_pressed, _target_max_configured.button_pressed, _allow_dead_configured.button_pressed]}
    for effect in _draft.Effects:
        for key in ["order", "effectType", "amount", "targetSelector", "statusKind", "durationScope", "destinationZone"]:
            if not effect.has(key): return {"ok": false, "error": "Effect 缺少 %s。" % key}

    _effects.select(1)
    _on_effect_selected(1)
    _move_effect(-1)
    if _draft.Effects.size() != 2: return {"ok": false, "error": "效果重排后数量错误。"}
    var reordered_first: Dictionary = _draft.Effects[0]
    var reordered_second: Dictionary = _draft.Effects[1]
    if int(reordered_first.get("order", -1)) != 1 or int(reordered_second.get("order", -1)) != 2:
        return {"ok": false, "error": "效果 Order 未连续重排。"}
    if int(reordered_first.get("amount", -1)) != 11 or int(reordered_second.get("amount", -1)) != 7:
        return {"ok": false, "error": "效果重排丢失参数。"}
    return {"ok": true}

## Editor-only semantic field check. It obtains fixture values from the loaded catalog instead of
## recognizing a specific card, character, pool, or preview asset.
func run_semantic_fields_self_check() -> Dictionary:
    _new_draft()
    if _pools_configured.button_pressed or _upgrade_configured.button_pressed or _preview_configured.button_pressed:
        return {"ok": false, "error": "新草稿的 Pool/Upgrade/Preview 必须为待配置。"}
    if not _preview.text.contains("预览框：待配置"):
        return {"ok": false, "error": "新草稿预览未显示待配置 frameKey。"}

    var pool_ids := _get_catalog_pool_ids()
    if pool_ids.size() < 2:
        return {"ok": false, "error": "Catalog 未提供两个以上卡池，无法验证完整 Pool 列表。"}
    var source: Variant = _find_catalog_card()
    if source == null:
        return {"ok": false, "error": "Catalog 未提供可读取的生产卡定义。"}

    _pools_configured.button_pressed = true
    _pool.text = "%s, %s, %s" % [pool_ids[0], pool_ids[1], pool_ids[0]]
    _upgrade_configured.button_pressed = true
    _can_upgrade.button_pressed = true
    _upgrade_next_card_id.text = source.Id
    _family.text = "editor_fixture_family"
    _preview_configured.button_pressed = true
    _preview_key.text = "editor_fixture_frame"
    _apply_form()

    if _draft.RewardPoolIds.size() != 2 or _draft.RewardPoolIds[0] != pool_ids[0] or _draft.RewardPoolIds[1] != pool_ids[1]:
        return {"ok": false, "error": "Pool 列表未按顺序写回或未去重。"}
    if not _has_complete_upgrade_fields(_draft.Upgrade) or not bool(_draft.Upgrade["canUpgrade"]) or str(_draft.Upgrade["nextCardId"]) != source.Id:
        return {"ok": false, "error": "可升级 Upgrade 三元组未完整写回。"}
    if not _draft.Preview.has("frameKey") or str(_draft.Preview["frameKey"]) != "editor_fixture_frame":
        return {"ok": false, "error": "Preview.frameKey 未写回。"}

    _can_upgrade.button_pressed = false
    _upgrade_next_card_id.text = ""
    _family.text = "editor_fixture_terminal_family"
    _apply_form()
    if bool(_draft.Upgrade["canUpgrade"]) or not str(_draft.Upgrade["nextCardId"]).is_empty() or str(_draft.Upgrade["familyId"]) != "editor_fixture_terminal_family":
        return {"ok": false, "error": "不可升级卡未保留显式无后继 Upgrade 语义。"}

    var missing_preview = _clone_definition_for_editor_validation(source)
    missing_preview.EditorDraft = false
    missing_preview.Preview = {}
    if not _has_validation_error(missing_preview.EditorValidationErrors, ".preview"):
        return {"ok": false, "error": "缺失 Preview.frameKey 未被 validator 拒绝。"}
    var missing_upgrade = _clone_definition_for_editor_validation(source)
    missing_upgrade.EditorDraft = false
    missing_upgrade.Upgrade = {}
    if not _has_validation_error(missing_upgrade.EditorValidationErrors, ".upgrade"):
        return {"ok": false, "error": "缺失 Upgrade 三元组未被 validator 拒绝。"}
    var missing_pools = _clone_definition_for_editor_validation(source)
    missing_pools.EditorDraft = false
    missing_pools.RewardPoolIds = PackedStringArray()
    if not _has_validation_error(missing_pools.EditorValidationErrors, ".rewardPoolIds"):
        return {"ok": false, "error": "缺失 RewardPoolIds 未被 validator 拒绝。"}
    return {"ok": true}

func _get_catalog_pool_ids() -> PackedStringArray:
    var result := PackedStringArray()
    if not _catalog:
        return result
    for pool in _catalog.Pools:
        if pool is Dictionary and pool.has("id"):
            var pool_id := str(pool["id"]).strip_edges()
            if not pool_id.is_empty() and not result.has(pool_id):
                result.append(pool_id)
    return result

func _find_catalog_card() -> Resource:
    if not _catalog:
        return null
    for path in _catalog.CardResourcePaths:
        var card: Resource = ResourceLoader.load(path)
        if card != null:
            return card
    return null

# Resource.duplicate() returns the base Resource type to GDScript, which loses the C# helper API.
# Editor-only validation fixtures therefore copy the serialized fields into an actual CardDefinitionResource.
func _clone_definition_for_editor_validation(source) -> CardDefinitionResource:
    var clone := CardDefinitionResource.new()
    clone.SchemaVersion = source.SchemaVersion
    clone.EditorDraft = source.EditorDraft
    clone.Id = source.Id
    clone.DisplayName = source.DisplayName
    clone.DescriptionFallback = source.DescriptionFallback
    clone.OwnerKind = source.OwnerKind
    clone.OwnerCharacterId = source.OwnerCharacterId
    clone.Category = source.Category
    clone.Rarity = source.Rarity
    clone.Costs = source.Costs.duplicate(true)
    clone.TargetPolicy = source.TargetPolicy.duplicate(true)
    clone.Effects = source.Effects.duplicate(true)
    clone.RewardPoolIds = source.RewardPoolIds.duplicate()
    clone.Upgrade = source.Upgrade.duplicate(true)
    clone.Tags = source.Tags.duplicate()
    clone.Availability = source.Availability.duplicate(true)
    clone.Preview = source.Preview.duplicate(true)
    clone.Balance = source.Balance.duplicate(true)
    return clone

func _has_validation_error(errors: PackedStringArray, expected_path: String) -> bool:
    for issue in errors:
        if issue.contains(expected_path):
            return true
    return false
