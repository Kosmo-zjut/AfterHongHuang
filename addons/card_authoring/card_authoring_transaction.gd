@tool
class_name CardAuthoringTransaction
const MANIFEST_PATH := "res://resources/cards/CardCatalog.manifest.json"

# 编辑器唯一保存事务。正式写入前必须由 Dock 生命周期内的 C# bridge 校验 staging 三件套。
static func commit(draft: Resource, catalog: Resource, card_path: String, is_new: bool,
        validation_bridge: Node, options := {}) -> Dictionary:
    if not draft or not catalog:
        return {"ok": false, "error": "草稿或 Catalog 未加载。"}
    if not validation_bridge or not validation_bridge.has_method("ValidateCandidateStaging"):
        return {"ok": false, "error": "编辑器语义校验桥接未装配，已拒绝写入。"}
    if draft.Id.strip_edges().is_empty() or card_path.is_empty():
        return {"ok": false, "error": "CardId 或目标资源路径未分配。"}
    var matching_id := false
    var matching_path := false
    for path in catalog.CardResourcePaths:
        if path == card_path: matching_path = true
        var existing: Resource = ResourceLoader.load(path)
        if existing and existing.Id == draft.Id: matching_id = true
    if is_new and (matching_id or matching_path or FileAccess.file_exists(card_path)):
        return {"ok": false, "error": "新草稿不得覆盖已有 CardId 或资源路径。"}
    if not is_new and not matching_path:
        return {"ok": false, "error": "编辑草稿的原资源不属于当前 Catalog。"}
    var candidate := catalog.duplicate(true)
    if not candidate.CardResourcePaths.has(card_path): candidate.CardResourcePaths.append(card_path)
    var catalog_path: String = options.get("catalog_path", catalog.resource_path)
    var manifest_path: String = options.get("manifest_path", MANIFEST_PATH)
    if catalog_path.is_empty() or manifest_path.is_empty():
        return {"ok": false, "error": "Catalog 或 manifest 正式路径未配置。"}
    var staging_dir := "user://card_authoring/staging/%s_%s" % [Time.get_ticks_usec(), OS.get_process_id()]
    if DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(staging_dir)) != OK:
        return {"ok": false, "error": "无法创建编辑器 staging 目录。"}
    var card_tmp := staging_dir.path_join("candidate_card.tres")
    var catalog_tmp := staging_dir.path_join("candidate_catalog.tres")
    var manifest_tmp := staging_dir.path_join("CardCatalog.manifest.json")
    var manifest := JSON.stringify({"schemaVersion": 1, "cardResourcePaths": candidate.CardResourcePaths}, "\t")
    if ResourceSaver.save(draft, card_tmp) != OK or ResourceSaver.save(candidate, catalog_tmp) != OK or not _write(manifest_tmp, manifest.to_utf8_buffer()):
        _cleanup_staging(staging_dir, card_tmp, catalog_tmp, manifest_tmp)
        return {"ok": false, "error": "候选 Resource/Catalog/manifest 临时写入失败。"}
    var validation: Dictionary = validation_bridge.ValidateCandidateStaging(card_tmp, catalog_tmp, manifest, card_path)
    if not validation.get("ok", false):
        var error := _format_issues(validation.get("issues", []))
        _cleanup_staging(staging_dir, card_tmp, catalog_tmp, manifest_tmp)
        return {"ok": false, "error": "候选三件套语义校验失败：%s" % error}
    var before_card := FileAccess.get_file_as_bytes(card_path)
    var before_catalog := FileAccess.get_file_as_bytes(catalog_path)
    var before_manifest := FileAccess.get_file_as_bytes(manifest_path)
    var card_ok := _write(card_path, FileAccess.get_file_as_bytes(card_tmp))
    if card_ok and options.get("fault_step", "") == "after_card": card_ok = false
    var catalog_ok := card_ok and _write(catalog_path, FileAccess.get_file_as_bytes(catalog_tmp))
    if catalog_ok and options.get("fault_step", "") == "after_catalog": catalog_ok = false
    var manifest_ok := catalog_ok and _write(manifest_path, FileAccess.get_file_as_bytes(manifest_tmp))
    if manifest_ok and options.get("fault_step", "") == "after_manifest": manifest_ok = false
    if not manifest_ok:
        var restore_card := _restore(card_path, before_card)
        var restore_catalog := _restore(catalog_path, before_catalog)
        var restore_manifest := _restore(manifest_path, before_manifest)
        _cleanup_staging(staging_dir, card_tmp, catalog_tmp, manifest_tmp)
        var rollback_ok := restore_card and restore_catalog and restore_manifest
        return {"ok": false, "rollback_failed": not rollback_ok,
            "error": "提交失败，回滚 Resource=%s Catalog=%s Manifest=%s" % [restore_card, restore_catalog, restore_manifest]}
    _cleanup_staging(staging_dir, card_tmp, catalog_tmp, manifest_tmp)
    return {"ok": true, "error": ""}

static func _write(path: String, bytes: PackedByteArray) -> bool:
    var file := FileAccess.open(path, FileAccess.WRITE)
    if not file: return false
    file.store_buffer(bytes)
    return file.get_error() == OK

static func _restore(path: String, bytes: PackedByteArray) -> bool:
    if bytes.is_empty():
        _remove(path)
        return not FileAccess.file_exists(path)
    return _write(path, bytes)

static func _remove(path: String) -> bool:
    var absolute := ProjectSettings.globalize_path(path)
    var result := DirAccess.remove_absolute(absolute)
    return result == OK or not FileAccess.file_exists(path)

static func _cleanup_staging(staging_dir: String, card_tmp: String, catalog_tmp: String, manifest_tmp: String) -> void:
    _remove(card_tmp); _remove(catalog_tmp); _remove(manifest_tmp)
    DirAccess.remove_absolute(ProjectSettings.globalize_path(staging_dir))

static func _format_issues(issues: Array) -> String:
    var messages: PackedStringArray = []
    for issue in issues:
        messages.append("[%s] %s：%s" % [issue.get("scope", "unknown"), issue.get("path", "unknown"), issue.get("message", "未知错误")])
    return " | ".join(messages) if not messages.is_empty() else "桥接未返回问题详情。"
