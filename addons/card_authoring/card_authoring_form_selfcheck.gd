@tool
extends SceneTree

const DOCK_SCENE := preload("res://addons/card_authoring/CardAuthoringDock.tscn")

func _initialize() -> void:
    var dock := DOCK_SCENE.instantiate()
    get_root().add_child(dock)
    await process_frame
    var result: Dictionary = dock.run_form_binding_self_check()
    if result.get("ok", false):
        result = dock.run_semantic_fields_self_check()
    dock.queue_free()
    if result.get("ok", false):
        print("[CardAuthoringFormSelfCheck] PASS Dock preserves pending, form, Pool, Upgrade, Preview, and effect ordering")
        quit(0)
        return
    push_error("[CardAuthoringFormSelfCheck] FAIL: %s" % result.get("error", "unknown error"))
    quit(1)
