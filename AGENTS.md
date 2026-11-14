# Contributor and agent instructions

- Use `dev` for development and `main` for stable versions. Do not create extra branches unless requested.
- The only Unity project is `source/`. Edit its files directly; do not create a second source tree or copy/patch installation step.
- Write all new code, comments, documentation, commit messages and player-facing text in English.
- Preserve upstream OpenZ names, credits, licenses and existing content. Do not translate or rename upstream content as part of unrelated changes.
- Preserve the upstream code-only Git layout: track `source/Assets/OpenZ/Scripts/`, `source/Assets/OpenZ/Shaders/`, their metadata, `source/Packages/` and `source/ProjectSettings/`. Multimedia and scenes remain local and are not covered by a Git commit.
- Do not add Git LFS or import multimedia into Git. Do not commit Unity caches, personal settings, generated project files, logs or compiled players. If work changes an ignored resource, arrange its separate backup/distribution explicitly before delivery.
- Before delivery, run the checks appropriate to the change. Menu or runtime changes require a Unity player build and the affected native integration checks.
- Commit completed work with the repository's configured GitHub identity, push it to `origin/dev`, and verify the remote commit. Do not change the user's identity to a placeholder.
- Preserve the user's personal email privacy: use the configured GitHub noreply address.
- A playable release must contain the entire player output folder and identify its source commit. Do not describe a source commit as a downloadable player release.
