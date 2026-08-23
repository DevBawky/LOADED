---
name: loaded-refactor-systems
description: Refactor LOADED Unity systems for clearer SOLID responsibility boundaries while preserving gameplay, Unity serialization, and public behavior. Use for manager/controller decomposition, oversized classes, ownership cleanup, or architecture-focused code review; do not use for an ordinary localized feature with no structural change.
---

# Refactor LOADED Systems

Use the root `AGENTS.md` as the shared project baseline. Pair this skill with the matching LOADED domain skill whenever the refactor touches gameplay rules.

## Establish the boundary

Read the complete target, its callers, event subscribers, serialized references, save fields, tests, and the relevant section of `Docs/Dev/0820_SOLID_Refactoring.md`.

Classify each responsibility before moving code:

- Unity lifecycle, Inspector references, and scene orchestration stay in a thin `MonoBehaviour` facade.
- Authored configuration stays in `ScriptableObject` data.
- Per-run mutation stays in runtime instances or its existing authoritative manager.
- Deterministic rules and calculations prefer plain C# collaborators.
- Persistence, presentation, input, and audio remain behind their existing owners.

Extract a responsibility only when it has a distinct reason to change, cohesive state, or a useful test seam. File length is evidence to inspect, not sufficient justification by itself.

## Refactor for extension

Build an ownership map before choosing an extraction. For each affected state,
record its authoritative owner, query consumers, mutation entry points,
presentation consumers, persistence checkpoint, and ordering-sensitive events.

Prefer this sequence:

1. Characterize current public outcomes and sensitive RNG, event, save, and
   coroutine order.
2. Centralize duplicated deterministic rules used by execution, preview, and
   UI.
3. Extract a plain C# query, formatter, calculation, or state-transition
   collaborator with explicit inputs.
4. Keep mutation validation and commit in the existing authoritative owner.
5. Leave Unity lifecycle, serialized references, and delegating public APIs in
   the facade.

Do not create handler classes merely to replace every `switch` arm. Extract a
cohesive effect or behavior family when it has shared dependencies, runtime
state, or a separate reason to change. For resumable multi-step behavior, use
explicit runtime states and legal transitions while preserving the existing
serialized representation unless migration is in scope.

## Preserve behavior

- Keep public APIs and event timing stable unless the user explicitly requests a contract change.
- Preserve serialized field names and types, component class/file names, asset GUIDs, hierarchy names, string IDs, and save schemas.
- Preserve RNG consumption, coroutine order, turn completion, and battle-clear/failure priority.
- Do not move authored data into runtime singletons or runtime state into shared assets.
- Do not add an interface, manager, service locator, or partial file mechanically. A partial file only counts as separation when a cohesive collaborator owns behavior and state.
- When serialization compatibility requires a nested collaborator, keep the `MonoBehaviour` as a delegating facade and document that constraint in code only if it is non-obvious.

## Verify the refactor

Run the narrowest relevant tests first, then Unity compilation and applicable EditMode coverage. Run `Assets/Editor/Tests/RefactoringPolicyTests.cs` and `SceneIntegrityTests.cs` when structural or serialized references changed. Manually exercise the affected scene when lifecycle, animation, input, audio, or hierarchy wiring is involved.

Report the responsibility moved, its new owner, compatibility preserved, checks actually run, and any remaining manual verification.

A completed refactor should make ownership unambiguous, prevent rule
duplication between execution and presentation, preserve observable ordering,
and allow presentation listeners to be absent without changing gameplay.
