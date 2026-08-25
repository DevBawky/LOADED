# Main Button Shader Feedback

`Assets/Prefabs/UI/Button/Button _ Main.prefab` uses one procedural UI
material and `MainButtonShaderFeedback` instead of the legacy Animator and
stacked Normal/Hover rendering layers.

The idle state is a static worn-brass plate. Pointer hover is the only state
that enables the ember rim and moving scan light. A successful `Button`
click, including keyboard or programmatic submission, emits one short impact
ring from the pointer position or the center fallback. Selection by itself has
no visual animation. Disabled buttons only receive a static desaturated tint.

Each live button clones the shared material so simultaneous button feedback
cannot overwrite another instance. The clone is released when the component
is disabled or destroyed. Presentation uses unscaled time and never controls
whether a click succeeds.

Existing scene instances override the old Normal BG color for role-specific
tints. Its original object and component file IDs remain as the inactive,
non-rendering `Data | Legacy Tint` child, and the runtime material reads that
color. This preserves those overrides without keeping a second visual layer.

Run `Tools > LOADED > UI > Rebuild Main Button Shader Prefab` after changing
the shader, material defaults, or prefab structure. The builder preserves the
prefab GUID and existing instance overrides while removing the Hover child and
Animator component and disabling the legacy tint data carrier.
