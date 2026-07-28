@tool
extends SceneTree

const TRANSACTION := preload("res://addons/card_authoring/card_authoring_transaction.gd")
const DOCK_SCENE := preload("res://addons/card_authoring/CardAuthoringDock.tscn")
const CATALOG_PATH := "res://resources/cards/CardCatalog.tres"
const ROOT := "user://arch_gov_005"

func _initialize() -> void:
    var result := await _run()
    if result.get("ok", false):
        print("[CardAuthoringTransactionSelfCheck] PASS shared semantic validation and three-file rollback")
        quit(0)
        return
    push_error("[CardAuthoringTransactionSelfCheck] FAIL: %s" % result.get("error", "unknown error"))
    quit(1)

func _run() -> Dictionary:
    _clear_root()
    var dock := DOCK_SCENE.instantiate()
    get_root().add_child(dock)
    await process_frame
    var bridge: Node = dock.get_node("CardAuthoringValidationBridge")
    var source: Resource = ResourceLoader.load(CATALOG_PATH)
    if not source or not bridge:
        return {"ok": false, "error": "Dock bridge 或生产 Catalog 未加载。"}
    var catalog: Resource = source.duplicate(true)
    var base_card: Resource = ResourceLoader.load(catalog.CardResourcePaths[0])
    if not base_card:
        return {"ok": false, "error": "生产卡牌 fixture 未加载。"}

    var success_paths := _paths("success")
    var valid := base_card.duplicate(true)
    valid.Id = "fixture_arch_gov_005_valid"
    valid.DisplayName = "事务校验 fixture"
    var success := TRANSACTION.commit(valid, catalog, success_paths.card, true, bridge,
        {"catalog_path": success_paths.catalog, "manifest_path": success_paths.manifest})
    if not success.get("ok", false) or not _all_exist(success_paths):
        return {"ok": false, "error": "合法草稿未提交完整三件套：%s" % success.get("error", "")}

    var invalid_paths := _paths("semantic")
    var invalid := base_card.duplicate(true)
    invalid.Id = "fixture_arch_gov_005_invalid"
    invalid.TargetPolicy = {}
    var rejected := TRANSACTION.commit(invalid, catalog, invalid_paths.card, true, bridge,
        {"catalog_path": invalid_paths.catalog, "manifest_path": invalid_paths.manifest})
    if rejected.get("ok", false) or _any_exist(invalid_paths):
        return {"ok": false, "error": "语义无效草稿被写入或留下文件。"}

    for fault_step in ["after_card", "after_catalog", "after_manifest"]:
        var fault_paths := _paths(fault_step)
        var fault_draft := base_card.duplicate(true)
        fault_draft.Id = "fixture_arch_gov_005_%s" % fault_step
        var failed := TRANSACTION.commit(fault_draft, catalog, fault_paths.card, true, bridge,
            {"catalog_path": fault_paths.catalog, "manifest_path": fault_paths.manifest, "fault_step": fault_step})
        if failed.get("ok", false) or not failed.has("rollback_failed") or failed.rollback_failed or _any_exist(fault_paths):
            return {"ok": false, "error": "故障 %s 后三件套未完整回滚：%s" % [fault_step, failed.get("error", "")]}

    dock.queue_free()
    _clear_root()
    return {"ok": true}

func _paths(name: String) -> Dictionary:
    return {"card": "%s/%s_card.tres" % [ROOT, name], "catalog": "%s/%s_catalog.tres" % [ROOT, name], "manifest": "%s/%s_manifest.json" % [ROOT, name]}

func _all_exist(paths: Dictionary) -> bool:
    return FileAccess.file_exists(paths.card) and FileAccess.file_exists(paths.catalog) and FileAccess.file_exists(paths.manifest)

func _any_exist(paths: Dictionary) -> bool:
    return FileAccess.file_exists(paths.card) or FileAccess.file_exists(paths.catalog) or FileAccess.file_exists(paths.manifest)

func _clear_root() -> void:
    var absolute := ProjectSettings.globalize_path(ROOT)
    if DirAccess.dir_exists_absolute(absolute): _clear_directory(absolute)
    DirAccess.make_dir_recursive_absolute(absolute)

func _clear_directory(path: String) -> void:
    var directory := DirAccess.open(path)
    if not directory: return
    directory.list_dir_begin()
    var entry := directory.get_next()
    while not entry.is_empty():
        if not entry.begins_with("."):
            var child := path.path_join(entry)
            if directory.current_is_dir():
                _clear_directory(child)
                DirAccess.remove_absolute(child)
            else:
                DirAccess.remove_absolute(child)
        entry = directory.get_next()
    directory.list_dir_end()
