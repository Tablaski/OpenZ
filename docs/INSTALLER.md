# Install, retrieve sources and build OpenZ-VK

Download **OpenZ-VK-Installer.exe** from the [official release](https://github.com/Tablaski/OpenZ/releases/latest). It is a standalone Windows 10/11 x64 application using the .NET Framework included with Windows. It does not require administrator installation or Git. The supplied OpenZ-VK logo is embedded in it.

## Play a published version

Choose a published player, read its changelog and click **INSTALL / UPDATE**. **Unity is not required.** The installer checks for updates when opened and every 30 minutes while open; installation starts only when requested. Unchanged resource blocks are reused and downloads/files are verified with SHA-256. The existing player is replaced only after the complete replacement is verified.

Use **REUSE FILES** to seed the installation from an existing extracted build. **COMPLETE ZIPS** opens the GitHub releases for manual installation: extract both player ZIPs from the same version into one destination. GitHub's automatic source ZIP is not a playable download.

Click **PLAY** or **UNIT EDITOR**. The editor's direct launcher is also in `Unit Editor/Open Unit Editor.bat` inside every new player.

## Retrieve any main/dev source revision and compile it

1. Open **SOURCES & UNITY**. Choose stable **main** or development **dev**. Browse commits, use **OLDER COMMITS**, or paste a commit ID. This includes source revisions without a published player.
2. Choose an empty **SOURCE WORKSPACE** initially, then **GET SOURCES**. Installed Git is detected and used for clone/fetch and a detached checkout of the selected revision. If Git is absent, the installer offers its optional installation and can use GitHub commit snapshots instead. Local source edits are protected rather than silently overwritten.
3. Click **PREPARE RESOURCES**. Optionally select an existing complete Unity project to reuse its assets; otherwise the installer downloads the published Unity resource archives once. Resources stay in the workspace when switching commits; subsequent source updates do not download the large asset archives again. Source and map packages travel with the selected revision.
4. The required Unity version is read from that revision's ProjectVersion.txt. Its installed editor is detected in standard locations; **BROWSE** supports another location. **GET UNITY** opens the matching Unity Hub editor installation, or the Hub download page. Install Windows Build Support and activate your Unity license in Hub yourself.
5. Close this project's editor, then click **BUILD RELEASE**. The result is under the workspace's `builds/OpenZ-VK-Local/` folder. The installer reports the log path and does not launch the game automatically. Keep the complete player folder together.

Compilation requires Unity even when Git is installed. Git-free source builds record the downloaded commit and source changes in `FORK_BUILD.json`. Historical fork revisions without Git-free provenance support receive an explicit build-only compatibility adaptation and are marked modified. Older upstream revisions use their enabled Build Settings scenes. Historical revisions may need their matching original assets if their asset layout differs.

Your existing OpenZ/OpenZ preferences, saved profiles and local preference history retain their established locations outside the player/source folders. Close the game before replacing its player. Source workspaces and player installations are distinct; use a new workspace when switching between an archive export and a Git checkout.

The installer is not code-signed. If a network blocks partial player downloads, use the complete player ZIPs. Unity Hub licensing, authentication and editor installation remain user-controlled.

## Publish a future player or resource update

Build and push the player source first. `tools/Publish-PlayerUpdate.ps1` prepares the installer and verified player payloads, uploads a draft, checks remote assets and publishes it. Pass the complete fallback ZIPs with `-AdditionalAssets`. Stable releases come from `origin/main` and become the official latest release:

```powershell
./tools/Publish-PlayerUpdate.ps1 -PlayerDirectory 'D:/path/to/player' -Channel stable -Tag v0.2.23 -NotesFile 'D:/path/to/notes.md' -AdditionalAssets 'D:/path/to/part1.zip','D:/path/to/part2.zip' -Publish
```

Source mode follows main/dev commits directly and does not require a published player for every commit. Keep `SOURCE_RESOURCES.json` current when shared assets change; generate a new one-time bundle with `tools/Package-UnityResources.ps1` and include its index/archives in that release. Preserve resource archives required by existing source revisions on GitHub. Restore the existing local artwork backup before building the installer on another checkout; do not commit large base assets, private configuration or local resource archives.
