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
- When the user requests a build, produce the final release version of the game, not a development or debug player. Disable development builds, script debugging, profiler connections and other development-only options.
- After compilation, do not launch, play, automate, inspect screenshots of, or run native integration checks against the game unless the user explicitly asks for those tests. The user performs game testing. Do not spend tokens or time attempting post-build gameplay or UI validation. Only check the build process result and report completion and the output location.
- Keep a persistent record of changes awaiting player validation. A successful build does not mark those checks as passed: leave them pending for the user's manual testing unless the user explicitly requests agent testing. If testing is explicitly requested, group the requested checks on the same release player and report what passed, failed or remains pending.
- Commit completed work with the repository's configured GitHub identity, push it to `origin/dev`, and verify the remote commit. Do not change the user's identity to a placeholder.
- Preserve the user's personal email privacy: use the configured GitHub noreply address.
- A playable release must contain the entire player output folder and identify its source commit. Do not describe a source commit as a downloadable player release.
