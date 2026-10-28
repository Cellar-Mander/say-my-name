# Windows port of scripts/dev-plugin; run it through scripts\dev-plugin.cmd. Both scripts use the
# same claim format, so checkouts using either one share the dev folder safely.
# Runs on Windows PowerShell 5.1, so no PowerShell 7-only syntax.
$ErrorActionPreference = 'Stop'

$devDir = $env:SAYMYNAME_DEV_DIR
if (-not $devDir) { $devDir = Join-Path $env:APPDATA 'XIVLauncher\devPlugins\SayMyName' }
# Creating a directory that already exists fails, so of two deploys racing for a free folder
# exactly one gets the claim
$claim = Join-Path $devDir '.claim'
$claimInfo = Join-Path $claim 'info'
$top = (git -C $PSScriptRoot rev-parse --show-toplevel)
if ($LASTEXITCODE) { exit 1 }

function Show-Usage {
    [Console]::Error.WriteLine(@"
Usage: scripts\dev-plugin.cmd deploy [task-ref]   build, claim the dev folder and copy the plugin in
       scripts\dev-plugin.cmd status              show who holds the dev folder
       scripts\dev-plugin.cmd release [--force]   free the dev folder (--force: even if another checkout holds it)

Dev folder: $devDir (set SAYMYNAME_DEV_DIR to change)
"@)
    exit 2
}

function Get-ClaimOwner {
    if (-not (Test-Path -LiteralPath $claimInfo -PathType Leaf)) { return '' }
    foreach ($line in Get-Content -LiteralPath $claimInfo) {
        if ($line.StartsWith('checkout=')) { return $line.Substring(9) }
    }
    return ''
}

function Write-Claim([switch]$ToError) {
    if (Test-Path -LiteralPath $claimInfo -PathType Leaf) {
        $text = (Get-Content -LiteralPath $claimInfo | ForEach-Object { "  $_" }) -join "`n"
    } else {
        $text = '  (claim has no details; it may be left over from an interrupted deploy)'
    }
    if ($ToError) { [Console]::Error.WriteLine($text) } else { Write-Output $text }
}

# Copy to a temp name, then rename into place, so Dalamud's reload on change never sees a
# half-copied file
function Copy-Into([string]$file) {
    $name = Split-Path -Leaf $file
    $tmp = Join-Path $devDir ".$name.tmp"
    Copy-Item -LiteralPath $file -Destination $tmp -Force
    Move-Item -LiteralPath $tmp -Destination (Join-Path $devDir $name) -Force
}

function Invoke-Deploy([string]$task) {
    # x64 so the build lands in bin/x64/Debug, where the copy below reads it; a bare project
    # build is AnyCPU and goes to bin/Debug
    dotnet build "$top/SayMyName/SayMyName.csproj" -c Debug -p:Platform=x64 --nologo -v q
    if ($LASTEXITCODE) { exit $LASTEXITCODE }

    New-Item -ItemType Directory -Force -Path $devDir | Out-Null
    $claimed = $true
    try { New-Item -ItemType Directory -Path $claim -ErrorAction Stop | Out-Null } catch { $claimed = $false }
    if (-not $claimed -and (Get-ClaimOwner) -ne $top) {
        [Console]::Error.WriteLine('Refusing to deploy: another checkout is testing in the dev folder.')
        Write-Claim -ToError
        [Console]::Error.WriteLine("Ask whoever holds it to run 'scripts/dev-plugin release' when they're done.")
        exit 1
    }

    $commit = (git -C $top rev-parse --short HEAD)
    if (git -C $top status --porcelain) { $commit += ' (uncommitted changes)' }
    $branch = (git -C $top branch --show-current)
    $info = "checkout=$top`nbranch=$branch`ncommit=$commit`ntask=$task`ndeployed=$((Get-Date).ToString('yyyy-MM-ddTHH:mm:sszzz'))`n"
    # LF and no BOM, as the bash script writes it, so either script can read the other's claim
    [IO.File]::WriteAllText($claimInfo, $info)

    # Plugin dll last, so a reload never sees a new dll next to old dependencies
    $out = "$top/SayMyName/bin/x64/Debug"
    Get-ChildItem -LiteralPath $out -File | Where-Object Name -ne 'SayMyName.dll' | ForEach-Object { Copy-Into $_.FullName }
    Copy-Into "$out/SayMyName.dll"

    Write-Output "Deployed to ${devDir}:"
    Write-Claim
}

function Invoke-Status {
    if (Test-Path -LiteralPath $claim -PathType Container) {
        Write-Output 'Claimed:'
        Write-Claim
    } else {
        Write-Output "Unclaimed: $devDir"
    }
}

function Invoke-Release([string[]]$rest) {
    $force = $false
    if ($rest.Count -eq 1 -and $rest[0] -eq '--force') { $force = $true }
    elseif ($rest.Count -ne 0) { Show-Usage }

    if (-not (Test-Path -LiteralPath $claim -PathType Container)) {
        Write-Output 'Already unclaimed.'
        return
    }
    if ((Get-ClaimOwner) -ne $top -and -not $force) {
        [Console]::Error.WriteLine('Refusing to release: the claim belongs to another checkout.')
        Write-Claim -ToError
        exit 1
    }
    Remove-Item -LiteralPath $claim -Recurse -Force
    Write-Output "Released $devDir"
}

$command = if ($args.Count) { $args[0] } else { '' }
$rest = @($args | Select-Object -Skip 1)
switch -CaseSensitive ($command) {
    'deploy' { Invoke-Deploy ($rest + '')[0] }
    'status' { Invoke-Status }
    'release' { Invoke-Release $rest }
    default { Show-Usage }
}
