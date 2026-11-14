param([string]$ProjectPath = (Join-Path $PSScriptRoot '..\OpenZ_1.4_source'))
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$project = (Resolve-Path -LiteralPath $ProjectPath).Path
if (-not (Test-Path -LiteralPath (Join-Path $project 'Assets\OpenZ\Scenes\MainMenu.unity'))) {
    throw 'Expected the complete OpenZ Unity project.'
}
$scripts = Join-Path $project 'Assets\OpenZ\Scripts'
$buildings = Join-Path $scripts 'Sim\ZObject.Buildings.cs'
$world = Join-Path $scripts 'Sim\ZWorld.cs'
$utf8 = New-Object System.Text.UTF8Encoding($false)

# Check every upstream hook before changing any upstream file. Re-running is safe.
$jobs = @(
    @{Path=$buildings; Anchor="public double BuildTimeModified(int baseSeconds)`n        {";
      Replacement="public double BuildTimeModified(int baseSeconds)`n        {`n            // OpenZ duel hook: production policy belongs to the match.`n            if (World?.DuelConfiguration != null) return World.DuelBuildSeconds(this, baseSeconds);";
      Marker='// OpenZ duel hook: production policy'},
    @{Path=$buildings; Anchor="public void ResetBuildTime(double zoneOwnage)`n        {";
      Replacement="public void ResetBuildTime(double zoneOwnage)`n        {`n            // OpenZ duel hook: preserve work already completed.`n            if (DuelResetZoneBuildTime(zoneOwnage)) return;";
      Marker='// OpenZ duel hook: preserve work'},
    @{Path=$world; Anchor="public void Tick(double dt)`n        {";
      Replacement="public void Tick(double dt)`n        {`n            // OpenZ duel hook: a replica never runs gameplay rules.`n            if (DuelIsReplica) return;";
      Marker='// OpenZ duel hook: a replica'}
)
$commandHooks = @(
    @('ZWorld.cs', 'public bool SendWaypoints(Team team, ZObject o, IList<ZWaypoint> list)', 'o', 'Waypoints', 'Points = new List<ZWaypoint>(list).ToArray()'),
    @('ZWorld.cs', 'public bool Eject(Team team, ZObject v)', 'v', 'Eject', ''),
    @('ZObject.Patrol.cs', 'public bool SendPatrol(Team team, ZObject o, int bx, int by)', 'o', 'Patrol', 'X = bx, Y = by'),
    @('ZObject.Patrol.cs', 'public bool SendHold(Team team, ZObject o)', 'o', 'Hold', ''),
    @('ZWorld.Rules.cs', 'public bool StartBuilding(Team team, ZObject b, string unit)', 'b', 'Build', 'Unit = unit'),
    @('ZWorld.Rules.cs', 'public bool StopBuilding(Team team, ZObject b)', 'b', 'StopBuild', ''),
    @('ZWorld.Rules.cs', 'public bool AddBuildingQueue(Team team, ZObject b, string unit)', 'b', 'Queue', 'Unit = unit'),
    @('ZWorld.Rules.cs', 'public bool CancelBuildingQueue(Team team, ZObject b, int i, string unit)', 'b', 'CancelQueue', 'Index = i, Unit = unit'),
    @('ZWorld.Rules.cs', 'public bool SetBuildingRepeat(Team team, ZObject b, bool repeat)', 'b', 'Repeat', 'Enabled = repeat'),
    @('ZObject.RepeatQueue.cs', 'public bool SetBuildingRepeatQueue(Team team, ZObject b, bool on)', 'b', 'RepeatQueue', 'Enabled = on'),
    @('ZWorld.Rules.cs', 'public bool SetRallyPoints(Team team, ZObject b, IList<ZWaypoint> pts)', 'b', 'Rally', 'Points = new List<ZWaypoint>(pts).ToArray()'),
    @('ZWorld.ShuttleKick.cs', 'public bool ShuttleKickOrder(Team team, ZObject s)', 's', 'ShuttleKick', '')
)
foreach ($hook in $commandHooks) {
    $anchor = $hook[1] + "`n        {"
    $marker = '// OpenZ duel command: ' + $hook[3]
    $fields = if ($hook[4]) { ', ' + $hook[4] } else { '' }
    $code = "            if (DuelIsReplica) return DuelSend(team, $($hook[2]), new DuelOrder { Kind = DuelOrderKind.$($hook[3])$fields });"
    $jobs += @{Path=(Join-Path $scripts ('Sim\' + $hook[0])); Anchor=$anchor;
        Replacement=($anchor + "`n            $marker`n" + $code); Marker=$marker}
}
$anchor = "public ZObject PlaceCannon(Team team, ZObject b, int tx, int ty, int index = 0)`n        {"
$jobs += @{Path=(Join-Path $scripts 'Sim\ZWorld.Rules.cs'); Anchor=$anchor;
    Replacement=($anchor + "`n            // OpenZ duel command: PlaceCannon`n            if (DuelIsReplica) return DuelSend(team, b, new DuelOrder { Kind = DuelOrderKind.PlaceCannon, X = tx, Y = ty, Index = index }) ? b : null;");
    Marker='// OpenZ duel command: PlaceCannon'}
$contents = @{}
$game = Join-Path $scripts 'View\ZGame.cs'
$runtimeHooks = @(
    @((Join-Path $scripts 'View\Zod\ZodFlyover.cs'), "public bool Begin(string first = `"overview`")`n        {", "public bool Begin(string first = `"overview`")`n        {`n            if (OpenZ.Multiplayer.DuelRuntime.Active) return false;", '// duel starts at the fort without solo flyover'),
    @((Join-Path $scripts 'Sim\ZWorld.Dragons.cs'), "public int DragonOrder(ZDragonOrder q)`n        {", "public int DragonOrder(ZDragonOrder q)`n        {`n            if (DuelIsReplica) return WhyOff;", '// duel environmental attacks belong to host'),
    @((Join-Path $scripts 'UI\OpenZHud.cs'), 'bool intro = missionIntro && PlayerPrefs.GetInt("oz_hud_intro", 1) == 1 && !OzMotion.Reduced;', 'bool intro = !OpenZ.Multiplayer.DuelRuntime.Active && missionIntro && PlayerPrefs.GetInt("oz_hud_intro", 1) == 1 && !OzMotion.Reduced;', '// duel HUD ready for shared countdown'),
    @((Join-Path $scripts 'UI\OpenZEndScreen.cs'), "void Update()`n        {", "void Update()`n        {`n            if (OpenZ.Multiplayer.DuelRuntime.Active) return;", '// duel owns its end screen'),
    @((Join-Path $scripts 'Sim\ZConst.cs'), 'public struct ZWaypoint : System.IEquatable<ZWaypoint>', '[System.Serializable] public struct ZWaypoint : System.IEquatable<ZWaypoint>', '// duel waypoint wire serialization'),
    @((Join-Path $scripts 'Sim\ZObject.cs'), 'public struct ZEvent', '[System.Serializable] public struct ZEvent', '// duel event wire serialization'),
    @((Join-Path $scripts 'View\RtsCamera.cs'), "void Update()`n        {", "void Update()`n        {`n            if (OpenZ.Multiplayer.DuelRuntime.CapturesInput) return;", '// duel camera input capture'),
    @((Join-Path $scripts 'UI\OzHighScores.cs'), "public static int Submit(string level, GameDifficulty d, OzScoreEntry e)`n        {", "public static int Submit(string level, GameDifficulty d, OzScoreEntry e)`n        {`n            if (OpenZ.Multiplayer.DuelRuntime.Active) return 0;", '// duel separate from solo scores'),
    @((Join-Path $scripts 'View\TeamPaint.cs'), 'public static Team Shown(Team t) => !Swap || t == Team.Neutral ? t : t == Team.Red ? Team.Blue : Team.Red;', 'public static Team Shown(Team t) => OpenZ.Multiplayer.DuelRuntime.Active ? t : !Swap || t == Team.Neutral ? t : t == Team.Red ? Team.Blue : Team.Red;', '// duel stable team colours'),
    @($game, 'World = ZWorld.Create(st.text, map.text, seed, ParkedStartOn(level));',
      'OpenZ.Multiplayer.DuelRuntime.PrepareGame(this); World = ZWorld.Create(st.text, map.text, seed, ParkedStartOn(level)); OpenZ.Multiplayer.DuelRuntime.WorldCreated(this);', '// duel game creation'),
    @($game, "void Update()`n        {", "void Update()`n        {`n            if (OpenZ.Multiplayer.DuelRuntime.Drive(this)) return;", '// duel game clock'),
    @($game, "public void ApplyTechBonus()`n        {", "public void ApplyTechBonus()`n        {`n            if (OpenZ.Multiplayer.DuelRuntime.Active) return;", '// duel no solo technology bonus'),
    @($game, 'bool headStartNow = OpenZDifficulty.HeadStartFor(ShownDifficulty);', 'bool headStartNow = !OpenZ.Multiplayer.DuelRuntime.Active && OpenZDifficulty.HeadStartFor(ShownDifficulty);', '// duel no solo starting army'),
    @((Join-Path $scripts 'View\PlayerInput.cs'), "void Update()`n        {", "void Update()`n        {`n            if (OpenZ.Multiplayer.DuelRuntime.CapturesInput) return;", '// duel input capture'),
    @((Join-Path $scripts 'UI\OpenZHud.cs'), "void Update()`n        {", "void Update()`n        {`n            if (OpenZ.Multiplayer.DuelRuntime.CapturesInput) return;", '// duel HUD input capture'),
    @((Join-Path $scripts 'View\CombatView.cs'), "void LateUpdate()`n        {", "void LateUpdate()`n        {`n            OpenZ.Multiplayer.DuelRuntime.CaptureEvents(game);", '// duel event forwarding'),
    @((Join-Path $scripts 'UI\OpenZPauseMenu.cs'), "public void Open()`n        {", "public void Open()`n        {`n            if (OpenZ.Multiplayer.DuelRuntime.Active) { OpenZ.Multiplayer.DuelRuntime.I.Menus.ShowInGameMenu(); return; }", '// duel in-game menu'),
    @((Join-Path $scripts 'UI\OpenZPauseMenu.cs'), "void Update()`n        {", "void Update()`n        {`n            if (OpenZ.Multiplayer.DuelRuntime.Active) return;", '// duel menu keyboard ownership'),
    @((Join-Path $scripts 'View\OzPlayBlue.cs'), 'public static bool NoStoryIn(string sceneName) => Active && sceneName != MenuScene;', 'public static bool NoStoryIn(string sceneName) => OpenZ.Multiplayer.DuelRuntime.Active || (Active && sceneName != MenuScene);', '// duel no solo briefing')
)
foreach ($hook in $runtimeHooks) { $jobs += @{Path=$hook[0];Anchor=$hook[1];Replacement=($hook[3]+"`n            "+$hook[2]);Marker=$hook[3]} }
foreach ($job in $jobs) {
    if (-not $contents.ContainsKey($job.Path)) { $contents[$job.Path] = [IO.File]::ReadAllText($job.Path).Replace("`r`n", "`n") }
    $content = $contents[$job.Path]
    if ($content.Contains($job.Marker)) { continue }
    if (($content.Split(@($job.Anchor), [StringSplitOptions]::None).Length - 1) -ne 1) {
        throw "Upstream source changed; review hook before installing: $($job.Path)"
    }
    $contents[$job.Path] = $content.Replace($job.Anchor, $job.Replacement)
}
$source = Join-Path $repo 'source\Assets\OpenZ\Scripts\Multiplayer'
$destination = Join-Path $scripts 'Multiplayer'
if (-not (Test-Path -LiteralPath $destination)) { New-Item -ItemType Directory -Path $destination | Out-Null }
Copy-Item -Path (Join-Path $source '*') -Destination $destination -Recurse -Force
Copy-Item -LiteralPath ($source + '.meta') -Destination ($destination + '.meta') -Force
foreach ($path in $contents.Keys) { [IO.File]::WriteAllText($path, $contents[$path], $utf8) }
Write-Output "Installed duel module, production policy and replica command forwarding into $project"
