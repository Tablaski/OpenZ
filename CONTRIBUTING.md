# Working on OpenZ Duel

Write all new code, comments, documentation, commit messages and in-game text in
English. Preserve upstream OpenZ names, content, licenses and credits.

## Set up a new machine

Install Git and Unity **6000.3.27f2** with Windows Build Support. Install the .NET
8 SDK for the core checks. Git LFS is not required.

~~~powershell
git clone --branch dev https://github.com/Tablaski/OpenZ.git
cd OpenZ
~~~

This repository follows the upstream code-only layout. Obtain the complete
OpenZ **1.4** source project from the
[official release](https://github.com/playopenz/OpenZ/releases/tag/v1.4) and
extract both source parts to a temporary folder. Add the missing Assets files
to the clone's source/Assets/ without replacing files already in the clone.
Keep the clone's Packages/ and ProjectSettings/.

For example, on Windows, replace the first path below with the extracted project:

~~~powershell
robocopy 'C:\Downloads\OpenZ_1.4_source\Assets' '.\source\Assets' /E /XC /XN /XO
~~~

This copies missing files while preserving the repository's code. Robocopy
exit codes below 8 indicate success. Do not overwrite the working project with
a newer source release: updating upstream is a separate, reviewed change.

Add the clone's source/ folder in Unity Hub. The first import rebuilds the local
cache. Open Assets/OpenZ/Scenes/MainMenu.unity to play in the Editor.
The current working machine is already set up; do not repeat this step for each
build or code change.

## Edit and validate

Edit source/ directly. Our module is in
source/Assets/OpenZ/Scripts/Multiplayer/. There is no copy or preparation step.
Code, shaders, their metadata, packages and project settings are tracked.
The online invitation service and its deployment instructions are in
`services/duel-signaling/README.md`. Build the local WebRTC native dependency
once with `./tools/Build-DuelNative.ps1` before building a player on a new machine.
Its binary archive is backed up separately under `builds/`; do not commit binaries
or private relay access codes.
Models, textures, sounds, scenes and other multimedia remain local, as upstream.
Do not treat a commit as a backup of those ignored resources.

Core checks:

~~~powershell
dotnet run --project tests/Multiplayer/Multiplayer.Checks.csproj -c Release
~~~

Build the Windows player:

~~~powershell
./tools/Build-Duel.ps1
~~~

The script finds Unity in D:\Unity or the usual Windows Unity Hub location.
For another installation, pass -Unity 'C:\path\to\Unity.exe' or set
UNITY_EDITOR_PATH. The Unity menu OpenZ > Multiplayer > Build Windows POC
also builds the player.

The player includes the Unit Editor playground alongside the four missions.
Check its main menu entry, playground initialization and return to the menu with
`./tools/Test-UnitEditor.ps1` after building. Pass `-Executable` to check a player
in another output folder.

Full local validation (core checks, build, four two-player missions, menus and Unit Editor):

~~~powershell
./tools/Build-And-Test-Duel.ps1
~~~

## Save code changes

Use dev for development and main for stable versions.

~~~powershell
git status
git add -A
git diff --cached --stat
git commit -m "Describe the change in English"
git push origin dev
git status
~~~

Review the staged changes before committing. A successful push and synchronized
clean status confirm that tracked work is on GitHub. Uncommitted edits and
ignored resources remain local.

## Integrate upstream changes

Keep https://github.com/playopenz/OpenZ.git as the upstream remote. Fetch it,
review the changes and merge upstream/main into dev. A rebase is possible but
rewrites published commits and requires coordination. Git handles code updates;
resources published only in a new source release must be integrated separately.
Compare the old official base, new official files and our changes before
resolving conflicts. Never blindly extract an update over the working project.
Build and test before publishing.

## Share a playable version

Zip the whole builds/OpenZ-Duel/ folder, not just the executable, and publish it
as a GitHub release tied to the tested source commit. Include licenses and
credits. Players extract it and run OpenZ-Duel.exe without Unity or Git.

See [the repository guide](docs/FORK.md) and
[multiplayer details](docs/MULTIPLAYER_POC.md).
