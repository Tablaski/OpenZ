[CmdletBinding(DefaultParameterSetName = 'Commit')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Commit')][ValidateNotNullOrEmpty()][string]$Message,
    [Parameter(Mandatory, ParameterSetName = 'Commit')][ValidateNotNullOrEmpty()][string[]]$Paths,
    [Parameter(Mandatory, ParameterSetName = 'Retry')][ValidatePattern('^[0-9a-f]{40}$')][string]$RetryCommit,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

function Invoke-Git([string[]]$Arguments) {
    & git -C $root @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Git failed ($LASTEXITCODE): $($Arguments[0])." }
}

function Get-RemoteDev {
    $ref = Invoke-Git @('ls-remote', '--exit-code', 'origin', 'refs/heads/dev')
    return ($ref -split '\s+')[0]
}

if ((Invoke-Git @('branch', '--show-current')) -cne 'dev') { throw 'Switch to dev first.' }
$head = Invoke-Git @('rev-parse', 'HEAD')
foreach ($marker in 'MERGE_HEAD', 'CHERRY_PICK_HEAD', 'REVERT_HEAD', 'rebase-apply', 'rebase-merge', 'sequencer') {
    $markerPath = Invoke-Git @('rev-parse', '--path-format=absolute', '--git-path', $marker)
    if (Test-Path -LiteralPath $markerPath) { throw "Finish the current Git operation ($marker) first." }
}
if (Invoke-Git @('diff', '--cached', '--name-only')) { throw 'The index already contains staged changes. Review and unstage them before using this script.' }
$remote = if ($DryRun) { Invoke-Git @('rev-parse', 'refs/remotes/origin/dev') } else { Get-RemoteDev }

if ($RetryCommit) {
    if ($head -ne $RetryCommit -or ($remote -ne $head -and (Invoke-Git @('rev-parse', 'HEAD^')) -ne $remote)) {
        throw 'The retry must identify HEAD and origin/dev must be that commit or its direct parent.'
    }
    if ($remote -eq $head) { Write-Output "Already verified origin/dev at $head."; return }
} else {
    if ($head -ne $remote) { throw 'dev must match origin/dev before creating a new commit.' }
    if ([string]::IsNullOrWhiteSpace($Message)) { throw 'Supply a commit message.' }
    $email = Invoke-Git @('var', 'GIT_AUTHOR_IDENT')
    $committer = Invoke-Git @('var', 'GIT_COMMITTER_IDENT')
    if (@($email, $committer) -notmatch '<[^>]+@users\.noreply\.github\.com>') { throw 'Configure the GitHub noreply address for both Git identities.' }

    $selected = @()
    foreach ($path in $Paths) {
        $name = $path.Replace('\', '/') -replace '^\./', ''
        if (!$name -or [IO.Path]::IsPathRooted($name) -or $name -match '(^|/)\.\.?(/|$)|[\x00-\x1f:*?]' -or $name.EndsWith('/')) {
            throw "Use individual repository-relative file paths: $path"
        }
        if ($name -match '(^|/)\.git(/|$)') { throw "Invalid path: $path" }
        $itemPath = Join-Path $root $name
        if (Test-Path -LiteralPath $itemPath -PathType Container) { throw "Select individual files instead of a directory: $path" }
        if (!(Test-Path -LiteralPath $itemPath -PathType Leaf)) {
            $tracked = Invoke-Git @('-c', 'core.quotePath=false', '--literal-pathspecs', 'ls-files', '--', $name)
            if ($tracked -cne $name -or @($tracked).Count -ne 1) { throw "No such file or tracked deletion: $path" }
        }
        $selected += $name
    }
    $selected = @($selected | Select-Object -Unique)
    $preview = Invoke-Git (@('-c', 'core.quotePath=false', '--literal-pathspecs', 'add', '--dry-run', '-A', '--') + $selected)
    if (!$preview) { throw 'The selected files have no changes.' }
    $preview | Write-Output
    if (!$DryRun) {
        Invoke-Git (@('--literal-pathspecs', 'add', '-A', '--') + $selected)
        $staged = @(Invoke-Git @('-c', 'core.quotePath=false', 'diff', '--cached', '--name-only', '--no-renames'))
        if (!$staged.Count -or @($staged | Where-Object { $_ -cnotin $selected }).Count) { throw 'Review the index. Staged paths differ from the selection.' }
        Invoke-Git @('diff', '--cached', '--check')
        $tree = Invoke-Git @('write-tree')
        Invoke-Git @('commit', '-m', $Message)
        $commit = Invoke-Git @('rev-parse', 'HEAD')
        if ((Invoke-Git @('rev-parse', 'HEAD^{tree}')) -ne $tree -or (Invoke-Git @('rev-parse', 'HEAD^')) -ne $head) {
            throw 'The commit differs from the reviewed index or base. Review it before pushing.'
        }
        $head = $commit
    }
}

if ($DryRun) { Write-Output 'Dry run against the local origin/dev reference. No staging, commit or push.'; return }
if ((Get-RemoteDev) -ne $remote) { throw "origin/dev moved. Local commit: $head." }
Invoke-Git @('push', '--no-follow-tags', 'origin', "${head}:refs/heads/dev")
if ((Get-RemoteDev) -ne $head) { throw "Could not verify origin/dev at $head." }
Write-Output "Verified origin/dev at $head."
