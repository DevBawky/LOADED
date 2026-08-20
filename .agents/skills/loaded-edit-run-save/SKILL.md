---
name: loaded-edit-run-save
description: Change LOADED run persistence, save DTOs, checkpoints, new-game/continue flow, RunSession state, node-map saves, battle restore, or desktop/WebGL storage. Use for RunDataModels, RunSaveSystem, RunSession, NodeMapSaveSystem, and any feature whose mutable state must survive scenes or reloads.
---

# Edit LOADED Run Save and Restore

## Storage boundaries

- `RunDataModels` defines serializable DTOs; `RunSaveData.version` is currently 3.
- `RunSaveSystem` owns normalization, validation, desktop JSON, and WebGL key `loaded.run.save.v3`.
- `RunSession` owns an in-memory cloned snapshot across scenes; it never owns scene-object references.
- `NodeMapSaveSystem` separately owns node-map progress at version 1 and WebGL key `loaded.node.map.v1`.
- `GameStatistics` persistence is lifetime statistics, not the run save.

Do not rename fields, change types, versions, filenames, or PlayerPrefs keys without an explicit migration and backward-compatibility plan.

## Change workflow

For every new or changed field, trace all of these paths:

1. authoritative runtime owner;
2. capture at each checkpoint;
3. DTO default and JSON serialization;
4. normalization and validation of missing, invalid, or legacy data;
5. cloned `RunSession` snapshot behavior;
6. restore after new game, continue, and scene transition;
7. desktop file and WebGL PlayerPrefs branches;
8. clear/delete behavior and failed-load fallback.

Store stable IDs and primitive state, not Unity object references. Re-resolve authored assets by the project's existing IDs. Keep node-map persistence separate unless the user explicitly requests a schema redesign. Save only at stable state boundaries, after a coherent transaction or action has settled.

## Verify

Add round-trip and normalization tests for changed DTOs. Exercise new game, checkpoint, return to menu, continue, scene transition, active battle restore, corrupt/missing save fallback, clear save, and both desktop and WebGL branches as relevant. Confirm the actual storage key/file and restored runtime owners, not only JSON text.

Read `Docs/Dev/0808_Conversation_Implementation_Log.md` sections on run save and `Docs/Dev/0809_WebGL_Save_And_Loading.md` for platform behavior.
