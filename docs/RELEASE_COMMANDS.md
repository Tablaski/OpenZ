# Release commands

Run these commands from the repository root in Windows PowerShell. Git uses the configured GitHub noreply identity and cached GitHub credentials. The stable workflow also needs the .NET SDK, the pinned Unity editor and installed source resources.

## Push selected files to dev

```powershell
.\tools\Push-Dev.ps1 -Message 'Describe the change' -Paths @('README.md', 'docs/CHANGELOG.md') -DryRun
.\tools\Push-Dev.ps1 -Message 'Describe the change' -Paths @('README.md', 'docs/CHANGELOG.md')
```

Replace the example paths with individual files you have reviewed. If a push failed after the commit, retry the exact commit with `.\tools\Push-Dev.ps1 -RetryCommit (git rev-parse HEAD)`.

## Publish stable from main

Start on a clean, synchronized `dev`. Review the README and the changelog introduction and version entry on `dev`. Save reviewed English GitHub notes and short Discord emoji bullets outside the checkout. Label gameplay changes `1 vs 1`, `Solo` or `Solo + 1 vs 1`. Keep Discord highlights focused on new fork features, starting with multiplayer.

```powershell
$notes = Join-Path $env:TEMP 'openz-release-notes.md'
$discord = Join-Path $env:TEMP 'openz-discord-highlights.md'
.\tools\Publish-Main.ps1 -NotesFile $notes -DryRun
.\tools\Publish-Main.ps1 -NotesFile $notes -PrepareOnly
.\tools\Publish-Main.ps1 -NotesFile $notes -DiscordHighlightsFile $discord -ConfirmTag 'v1.5.51'
```

Dry Run uses the local remote-tracking refs. Prepare Only can push a source-only metadata commit to `dev`. It runs the checks and packages the compiled game under `builds/release-<version>/`.

An explicit maintainer request to release on `main` authorizes publication. Run the final command with `-ConfirmTag` set to the target tag. The script runs the automated release checks before publishing. Track manual gameplay checks, including both Changes & Updates tabs, in [the validation record](PLAYER_VALIDATION.md). Run them when requested. A matching interrupted draft can be resumed with the same command and assets.

To prepare a new version, supply reviewed text for the changelog introduction and entry. The script adds the heading and date and updates the version in code and the README.

```powershell
.\tools\Publish-Main.ps1 -NotesFile $notes -Version '1.5.43' -ChangelogIntroFile (Join-Path $env:TEMP 'openz-changelog-intro.md') -ChangelogEntryFile (Join-Path $env:TEMP 'openz-changelog-entry.md') -PrepareOnly
```

After verification, use the ready-to-post `DISCORD_ANNOUNCEMENT.md` in the release folder. Use `-DeclareStable` only when the maintainer explicitly declares the version stable.
