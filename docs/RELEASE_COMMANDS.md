# Release commands

Run these commands from the repository root in Windows PowerShell. Git uses the configured GitHub noreply identity and cached GitHub credentials. The stable workflow also needs the .NET SDK, the pinned Unity editor and installed source resources.

## Push selected files to dev

```powershell
.\tools\Push-Dev.ps1 -Message 'Describe the change' -Paths @('README.md', 'docs/CHANGELOG.md') -DryRun
.\tools\Push-Dev.ps1 -Message 'Describe the change' -Paths @('README.md', 'docs/CHANGELOG.md')
```

Replace the example paths with individual files you have reviewed. If a push failed after the commit, retry the exact commit with `.\tools\Push-Dev.ps1 -RetryCommit (git rev-parse HEAD)`.

## Publish stable from main

Start on a clean, synchronized `dev`. Review the README and the changelog introduction and version entry on `dev`. Save reviewed English GitHub notes outside the checkout as `$env:TEMP\openz-release-notes.md`. Label gameplay changes `1 vs 1`, `Solo` or `Solo + 1 vs 1`.

```powershell
$notes = Join-Path $env:TEMP 'openz-release-notes.md'
.\tools\Publish-Main.ps1 -NotesFile $notes -DryRun
.\tools\Publish-Main.ps1 -NotesFile $notes -PrepareOnly
.\tools\Publish-Main.ps1 -NotesFile $notes
```

Dry Run uses the local remote-tracking refs. Prepare Only can push a source-only metadata commit to `dev`. It runs the checks, builds and packages the candidate under `builds/release-<version>/`. Test that same compiled game, including both Changes & Updates tabs and the relevant checks in [the validation record](PLAYER_VALIDATION.md). Run the final command and type the displayed `PUBLISH v<version>` confirmation. A matching interrupted draft can be resumed with the same command and assets.

For a reviewed candidate, `-ConfirmTag 'v1.5.43'` can supply the explicit confirmation in a noninteractive invocation.

To prepare a new version, supply reviewed text for the changelog introduction and entry. The script adds the heading and date and updates the version in code and the README.

```powershell
.\tools\Publish-Main.ps1 -NotesFile $notes -Version '1.5.43' -ChangelogIntroFile (Join-Path $env:TEMP 'openz-changelog-intro.md') -ChangelogEntryFile (Join-Path $env:TEMP 'openz-changelog-entry.md') -PrepareOnly
```

Use the verified public release URL for the Discord announcement. Keep the release logs and validation record with the candidate.
