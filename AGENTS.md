Game workflow: follow `.agents/refs/scriptability-rules.md` (no GUI; wire refs and run headless via script).

Before committing a new file or folder under `Assets/`, run `make format`: it writes and `git add`s the `.meta` for `.cs` files and folders, so the `.meta` lands in the same commit and CI does not fail and rerun over it. Never edit or regenerate an existing `.meta` (its GUID is what scenes and assets reference).
