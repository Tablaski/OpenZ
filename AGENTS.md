# Contributor and agent instructions

- Use `dev` for development and `main` for stable versions. Do not create extra branches unless requested.
- The only Unity project is `source/`. Edit its files directly; do not create a second source tree or copy/patch installation step.
- Write all new code, comments, documentation, commit messages and player-facing text in English.
- Preserve upstream OpenZ names, credits, licenses and existing content. Do not translate or rename upstream content as part of unrelated changes.
- Preserve the upstream code-only Git layout: track `source/Assets/OpenZ/Scripts/`, `source/Assets/OpenZ/Shaders/`, their metadata, `source/Packages/` and `source/ProjectSettings/`. Multimedia and scenes remain local and are not covered by a Git commit.
- Do not add Git LFS or import multimedia into Git. Do not commit Unity caches, personal settings, generated project files, logs or compiled players. If work changes an ignored resource, arrange its separate backup/distribution explicitly before delivery.
- Only compile or package a Unity player when the user explicitly requests a game build. Do not launch a player build automatically for development or validation. Source checks and logic or simulation tests remain allowed; compiling a test harness does not count as building the game.
- Before delivery, run the checks appropriate to the change that do not require an unrequested player build. Clearly distinguish completed checks from validation that still needs a freshly built player, including menu, rendering, runtime and native integration checks.
- Notify the user when a substantial set of features has accumulated since the last build they tested, or after a broad code change with a meaningful regression risk that warrants a build. Explain why a build would be useful and wait for the user to request it; a recommendation is not authorization to build.
- When the user requests a build, produce the final release version of the game, not a development or debug player. Disable development builds, script debugging, profiler connections and other development-only options. Run the affected native integration checks on that release player.
- Keep a persistent record of changes whose validation still requires a freshly built player. Whenever the user requests a build, use the same release player to test all accumulated changes with pending player validation, including earlier work, rather than only the latest change. Group these checks to minimize compilation time; do not create separate builds per feature. Report what passed, what failed and what still requires manual testing, and update the pending validation record accordingly.
- Commit completed work with the repository's configured GitHub identity, push it to `origin/dev`, and verify the remote commit. Do not change the user's identity to a placeholder.
- Preserve the user's personal email privacy: use the configured GitHub noreply address.
- A playable release must contain the entire player output folder and identify its source commit. Do not describe a source commit as a downloadable player release.
