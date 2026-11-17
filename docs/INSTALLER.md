# Install and update OpenZ-VK

1. Download **OpenZ-VK-Installer.exe** from the [official release](https://github.com/Tablaski/OpenZ/releases/latest) and run it on Windows 10/11 x64. It uses the .NET Framework supplied with Windows; no Git, Unity or administrator installation is required.
2. Choose **Stable · main** or **Dev · dev**, then a published version. Read its changes in the installer before clicking **INSTALL / UPDATE**.
3. Choose an empty game folder, or keep the default under your local Programs directory. If you already have a complete extracted OpenZ-VK build, select it under **REUSE FILES** to avoid downloading matching resources again. That source folder is only read.
4. Click **PLAY** or **UNIT EDITOR**. Keep the game folder complete. The direct editor launcher is also under `Unit Editor/Open Unit Editor.bat`.

The installer checks for newer versions when opened and every 30 minutes while it remains open. It displays the changelog and shows an update notification for a higher version; updates start only when you choose them. It does not run a background service when closed.

Updates reuse unchanged blocks from the installed game, including matching content whose offset changed inside a large Unity data file. Only missing blocks are downloaded, with SHA-256 verification of every block and complete file. Download size depends on what Unity changed: a substantially changed asset may still require a large download. Downloaded blocks survive interrupted attempts and are removed after a successful installation.

The previous game remains available until the complete new version has been assembled and verified. Close the game and editor before updating. A failed download leaves the existing installation intact. Your OpenZ/OpenZ preferences, saved profiles and local preference history keep their established locations outside the player folder.

Only **compiled, published builds** appear in the version list. Installer support starts with 0.2.22. Pushing source to `main` or `dev` alone does not compile a Unity player. Older releases remain available as complete ZIPs on GitHub.

If partial downloads are blocked by your network, retry later or use both complete player ZIPs from the release. The `.pack` payloads are managed by the installer; do not extract them manually. The downloadable installer is not code-signed.

## Publish a future channel build

Build a release player with `tools/Build-Duel.ps1`. Commit and push the player sources first; retain its `FORK_BUILD.json`. Publish stable builds whose source commit is on `origin/main`, and dev builds whose source commit is on `origin/dev`:

```powershell
./tools/Publish-PlayerUpdate.ps1 -PlayerDirectory 'D:/path/to/complete-player' -Channel stable -Tag v0.2.23 -NotesFile 'D:/path/to/release-notes.md' -AdditionalAssets 'D:/path/to/part1.zip','D:/path/to/part2.zip' -Publish
```

Use `-Channel dev` for a development channel build; omitting `-Tag` creates a commit-specific dev tag. Omitting `-Publish` prepares and verifies local assets without publishing. The publisher builds the installer, generates content-defined payload packs and a channel manifest, uploads a draft, verifies remote asset sizes/checksums, then publishes it. Stable releases become the official latest release; dev releases are marked prerelease.

Every changed delivered build receives a new fork version. Commit the full source and preserve upstream licenses; publish the complete fallback ZIPs as well as the installer payloads. The supplied logo is embedded from the existing local Unity resource; restore the local resource backup before building the installer on another checkout. Do not commit large game resources or private configuration.
