param([string]$Executable = (Join-Path $PSScriptRoot '../builds/OpenZ-Release/openz-vk.exe'))
$ErrorActionPreference = 'Stop'
$playerPath = (Resolve-Path -LiteralPath $Executable).Path
$proofDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ('../tests/Multiplayer/.cli/invitations-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))))
New-Item -ItemType Directory -Path $proofDirectory -Force | Out-Null
$arguments = @('-batchmode', '-nographics', '-logFile', ('"' + (Join-Path $proofDirectory 'player.log') + '"'))
$player = Start-Process -FilePath $playerPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
try {
    $expected = '"' + $playerPath + '" "%1"'
    $deadline = (Get-Date).AddSeconds(45)
    do {
        $registered = [Microsoft.Win32.Registry]::GetValue('HKEY_CURRENT_USER\Software\Classes\openz\shell\open\command', '', '')
        if ($registered -eq $expected) {
            $result = 'PASS: the actual Windows player registered its own per-user openz:// invitation handler without administrator rights.'
            [IO.File]::WriteAllText((Join-Path $proofDirectory 'result.txt'), $result)
            Write-Output $result
            return
        }
        if ($player.HasExited) { throw "The invitation player exited before registering its handler. Inspect $proofDirectory" }
        Start-Sleep -Milliseconds 200
    } while ((Get-Date) -lt $deadline)
    throw "The player did not register its invitation handler. Inspect $proofDirectory"
} finally {
    if (-not $player.HasExited) { Stop-Process -Id $player.Id -Force }
}
