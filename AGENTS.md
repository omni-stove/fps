Game workflow: follow `.agents/refs/scriptability-rules.md` (no GUI; wire refs and run headless via script).

Never edit or regenerate an existing `.meta` (its GUID is what scenes and assets reference).

Write a code comment, `///` doc comments included, only to say why the code is not written another way. Leave existing comments as they are unless you change the code they describe.

This repo is a shooter kit: each game is made by swapping in its own assets and settings, so the weapons, rules, scenes, visuals, sounds and numbers in it are placeholders. When adding code:

- A value a game would want to change (weapon stats, rules, movement, enemy AI, mode scores and timers) goes in data (JSON / Spec / ScriptableObject) with only its default in code; a value fixed by the implementation (tick rate, buffer sizes) stays a constant.
- Read the configured value (the Spec or the component's field), not the `Default*` constant.
- Get visuals and sounds from a replaceable reference (a catalog or a prefab field); build shapes in code only as the fallback when that reference is empty.
- Keep player-facing text in `Resources/Localization/<language>/*.json` and read it through `GameText` / `LocalizedText`, with the same keys in every language.
- Don't write content names (Rifle, Campaign01) in kit code; look them up by Id or data.
