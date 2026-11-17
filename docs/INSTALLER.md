# Install, retrieve sources and build OpenZ-VK

Download **OpenZ-VK-Installer.exe** from the [official release](https://github.com/Tablaski/OpenZ/releases/latest). It is a standalone Windows 10/11 x64 application using the .NET Framework included with Windows. It does not require administrator installation or Git. The supplied OpenZ-VK logo is embedded in it.

## Play a published version

Choose a published player, click **Read what's new**, then **Install game** or **Update / check files**. **Unity is not required.** The installer checks for updates when opened and every 30 minutes while open; installation starts only when requested. Unchanged resource blocks are reused and downloads/files are verified with SHA-256. The existing player is replaced only after the complete replacement is verified.

Use **Installation options** to choose the destination and optionally reuse files from an existing extracted build. **Manual ZIP download** opens the GitHub releases for manual installation: extract both player ZIPs from the same version into one destination. GitHub's automatic source ZIP is not a playable download.

Click **PLAY** or **UNIT EDITOR**. The editor's direct launcher is also in `Unit Editor/Open Unit Editor.bat` inside every new player.

## Retrieve any main/dev source revision and compile it

1. Choose **Build from sources · Unity**. The **Tools** page detects Git and Unity; setup buttons are disabled when available. Git is optional. Continue to **Choose version**, then select stable **main** or development **dev**. Browse commits with **Older versions**, or paste a commit ID. Published players are not required for this workflow.
2. Choose an empty working folder initially, then **Get this version**. Installed Git is used for clone/fetch and a detached checkout. Without Git, GitHub commit snapshots are used. Local source edits are protected rather than silently overwritten.
3. Continue to **Game resources**, then **Prepare resources**. Optionally select an existing complete Unity project with the matching resource package; otherwise the installer obtains the package required by that source revision. Unchanged resources stay in the workspace. A source revision that requires new base assets prepares the updated package before compilation. Source and map packages travel with the selected revision.
4. The required Unity version is read from that revision's ProjectVersion.txt. On **Tools**, **Choose another editor** supports another location; **Install Unity / Hub** guides installation. Install Windows Build Support and activate your Unity license in Hub yourself.
5. Close this project's editor, then click **Build game**. The result is under the workspace's `builds/OpenZ-VK-Local/` folder. The installer reports the log path and does not launch the game automatically. Keep the complete player folder together.

Compilation requires Unity even when Git is installed. Git-free source builds record the downloaded commit and source changes in `FORK_BUILD.json`. Historical fork revisions without Git-free provenance support receive an explicit build-only compatibility adaptation and are marked modified. Older upstream revisions use their enabled Build Settings scenes. Historical revisions may need their matching original assets if their asset layout differs.

The first build can take substantially longer while Unity imports assets and
compiles shaders. Later builds reuse the workspace's local Unity cache.

Your existing OpenZ/OpenZ preferences, saved profiles and local preference history retain their established locations outside the player/source folders. Close the game before replacing its player. Source workspaces and player installations are distinct; use a new workspace when switching between an archive export and a Git checkout.

The installer is not code-signed. If a network blocks partial player downloads, use the complete player ZIPs. Unity Hub licensing, authentication and editor installation remain user-controlled.

## Publish a future player or resource update

### Resource compatibility across upstream upgrades

`SOURCE_RESOURCES.json` pins an independent package ID, such as `openz-1.4-vk-1`,
and the release containing its resource index. An upstream upgrade that changes
assets (for example the planned 1.5 Arctic assets) requires a new package ID and
matching archives; a source rebase alone does not update Unity's ignored assets.
Keep older packages published so older commits remain buildable.

The index records individual file SHA-256 hashes and archive membership. Only
archives containing required changed files are fetched; unchanged files are reused.
A source resource update verifies all changed files before applying them, removes
obsolete managed files and keeps unknown files. Locally modified managed assets are
copied under `.local/resource-backups/` before replacement/removal. This folder and
personal game settings never belong in a release. If preparation is interrupted,
run it again before building. The required package is checked again before compilation.

The player checks its packaged base-resource marker and embedded build identity
against `FORK_BUILD.json` at startup. An incompatible package or mixed installation
blocks play with repair instructions. Published-player updates carry their own
complete matching resources and continue to reuse unchanged download blocks.

### Release preparation

Build and push the player source first. `tools/Publish-PlayerUpdate.ps1` prepares the installer and verified player payloads, uploads a draft, checks remote assets and publishes it. Pass the complete fallback ZIPs with `-AdditionalAssets`. Stable releases come from `origin/main` and become the official latest release:

```powershell
./tools/Publish-PlayerUpdate.ps1 -PlayerDirectory 'D:/path/to/player' -Channel stable -Tag v0.2.23 -NotesFile 'D:/path/to/notes.md' -AdditionalAssets 'D:/path/to/part1.zip','D:/path/to/part2.zip' -Publish
```

Source mode follows main/dev commits directly and does not require a published player for every commit. Keep `SOURCE_RESOURCES.json` current when shared assets change; generate a new one-time bundle with `tools/Package-UnityResources.ps1` and include its index/archives in that release. Preserve resource archives required by existing source revisions on GitHub. Restore the existing local artwork backup before building the installer on another checkout; do not commit large base assets, private configuration or local resource archives.
