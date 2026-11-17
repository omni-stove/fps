Work the game engine by `.agents/refs/scriptability-rules.md`.

Leave existing `.meta` files untouched.

Write tests and their expected values with each change, but do not run the Unity tests.

Write a code comment, `///` included, only to say why the code is not written another way. Keep existing comments unless you change the code they describe.

This repo is a shooter kit; each game replaces its content. In kit code:

- Put a value a game would tune (weapon stats, rules, movement, enemy AI, mode scores and timers) in data (JSON / Spec / ScriptableObject), with only its default in code.
- Read the configured value, not the `Default*` constant.
- Take visuals and sounds from a replaceable reference; build shapes in code only when it is empty.
- Keep kit game flow (Core, Runtime, NetV2) free of display text and of UI types: publish typed state to `GameSignals` and one-off events through `CombatEvents`, and keep asset display names as plain key strings in data. Build log lines in `*Log` classes.
- Look up content by Id or data, never by its name.

In the Demo sample's UI (`Samples~/Demo`):

- Turn `GameSignals` values and `ModeStatus.Detail` into text there (mode HUD text in a formatter registered with `ModeHudFormatters`).
- Put player-facing text in the sample's `Resources/Localization/<language>/*.json` with the same keys in every language, and read it through `GameText` / `LocalizedTexts`.
