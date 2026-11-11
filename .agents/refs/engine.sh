#!/bin/sh
# This repo's override of the skill-kit engine contract — Unity 6 (6000.6.0f1) on macOS, driven by the `unity` CLI.
# Function names are the kit's contract; only the echoed values are specific to this repo.

# --- Identity ---------------------------------------------------------------
engine_name()        { echo "Unity"; }

# --- Source layout ----------------------------------------------------------
# This project's own code. The rest of Assets/ is the URP template's leftovers.
source_root()        { echo "Assets/game"; }
# Space-separated path fragments to treat as third-party / off-limits.
third_party()        { echo "Packages/ Plugins/ Library/ ThirdParty/ Assets/TutorialInfo/"; }

# --- Logs -------------------------------------------------------------------
runtime_log()        { echo "$HOME/Library/Logs/Unity/Editor.log"; }
runtime_log_prev()   { echo "$HOME/Library/Logs/Unity/Editor-prev.log"; }
build_log()          { runtime_log; }

# --- Grep patterns (grep -E / -oE compatible) -------------------------------
# Unity emits `Assets/game/Foo.cs(12,3): error CS1002: ; expected`.
compile_error_pattern() { echo "(error|warning) [A-Z]+[0-9]+: .*"; }
runtime_error_pattern() { echo "Exception:|Error:|NullReference|IndexOutOfRange|ArgumentException|MissingReference"; }
# Unity stack frames end with `(at Assets/game/Foo/Bar.cs:42)`.
call_site_pattern()     { echo "at $(source_root)/[^)]+\)"; }

# --- Build / run ------------------------------------------------------------
# Batch-mode macOS player build into Builds/ (gitignored). Fails if the Editor has the project open.
build_cmd()          { echo "unity build --target StandaloneOSX -o Builds/fps.app"; }
# Play Mode needs the Editor GUI; `unity open` launches it with the project's pinned version.
run_cmd()            { echo "# Run \`unity open\` to launch the Editor on this project, then press Play. This tool cannot enter Play Mode."; }
# Headless EditMode tests in batch mode (needs the Editor closed on this project). Results go to test-results.xml.
headless_cmd()       { echo "unity test --mode EditMode"; }
recompile_hint()     { echo "Run \`unity recompile\` against the open Editor, or switch to the Unity Editor window; it recompiles on focus."; }

# --- Engine-control MCP (optional) ------------------------------------------
# Empty: skills fall back to log-scraping. The `setup-mcp` skill fills this in.
control_mcp()        { echo ""; }   # e.g. "mcp__unity__" when a Unity Editor MCP bridge is wired up
