Work the game engine by `.agents/refs/scriptability-rules.md`.

Leave existing `.meta` files untouched.

Write a code comment, `///` included, only to say why the code is not written another way. Keep existing comments unless you change the code they describe.

This repo is a shooter kit; each game replaces its content. In kit code:

- Put a value a game would tune (weapon stats, rules, movement, enemy AI, mode scores and timers) in data (JSON / Spec / ScriptableObject), with only its default in code.
- Read the configured value, not the `Default*` constant.
- Take visuals and sounds from a replaceable reference; build shapes in code only when it is empty.
- Put player-facing text in `Resources/Localization/<language>/*.json` with the same keys in every language, and read it through `GameText` / `LocalizedTexts`.
- Look up content by Id or data, never by its name.
