[CmdletBinding()]
param(
    [ValidateSet('Install','Uninstall')][string]$Action = 'Install',
    [string]$GamePath = ''
)
$ErrorActionPreference = 'Stop'
$packageHash = '@TOWER_RECOVERY_SHA256@'
$gameHash = 'F8393E44423442526DFFBF3AF52F8CCD56FB33769D97E2C7161EBC49D68B3564'
. (Join-Path $PSScriptRoot 'Resolve-GamePath.ps1')
$game = Resolve-WulinGamePath -GamePath $GamePath
$targetDirectory = Join-Path $game 'BepInEx\plugins\TowerRecovery'
$target = Join-Path $targetDirectory 'TowerRecovery.dll'
$receipt = Join-Path $targetDirectory '.tower-recovery-receipt'

function Hash($path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
function RequirePlainPath([string]$path) {
    while ($path) {
        $item = Get-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
        if ($item -and ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Refusing to write through a link: $path"
        }
        $path = Split-Path $path -Parent
    }
}

if (!(Test-Path -LiteralPath (Join-Path $game 'Wulin.exe') -PathType Leaf)) { throw 'Wulin.exe is missing from the game directory.' }
if (Get-Process Wulin -ErrorAction SilentlyContinue) { throw 'Exit Wulin before installing or uninstalling Tower Recovery.' }
RequirePlainPath $target
RequirePlainPath $receipt

$mutex = New-Object Threading.Mutex($false, 'Local\DaxiaTowerRecoveryInstaller')
$locked = $false
try {
    try { $locked = $mutex.WaitOne(0) }
    catch [Threading.AbandonedMutexException] { $locked = $true }
    if (!$locked) { throw 'Another Tower Recovery installer is running.' }

    $owned = Test-Path -LiteralPath $receipt -PathType Leaf
    $exists = Test-Path -LiteralPath $target -PathType Leaf
    $installedHash = if ($owned) { (Get-Content -LiteralPath $receipt -Raw -Encoding ascii).Trim() } else { '' }
    if ($exists -and (!$owned -or $installedHash -notmatch '^[0-9A-F]{64}$' -or (Hash $target) -ne $installedHash)) {
        throw 'The installed DLL is untracked or has changed. It was left in place.'
    }

    if ($Action -eq 'Uninstall') {
        if ($exists) { Remove-Item -LiteralPath $target }
        if ($owned) { Remove-Item -LiteralPath $receipt }
        Write-Output 'Tower Recovery uninstalled. Configuration retained.'
        return
    }

    $manifest = Join-Path (Split-Path (Split-Path $game -Parent) -Parent) 'appmanifest_1948980.acf'
    if (!(Test-Path -LiteralPath $manifest -PathType Leaf) -or
        (Get-Content -LiteralPath $manifest -Raw -Encoding utf8) -notmatch '"buildid"\s*"24794582"' -or
        (Hash (Join-Path $game 'GameAssembly.dll')) -ne $gameHash) {
        throw 'Tower Recovery requires the matching Steam Build 24794582 binary.'
    }
    foreach ($file in @('winhttp.dll','BepInEx\core\BepInEx.Unity.IL2CPP.dll','BepInEx\core\0Harmony.dll','BepInEx\interop\Assembly-CSharp.dll')) {
        if (!(Test-Path -LiteralPath (Join-Path $game $file) -PathType Leaf)) { throw "The existing BepInEx 6 IL2CPP installation is missing $file." }
    }
    $source = Join-Path $PSScriptRoot 'TowerRecovery.dll'
    if ($packageHash -notmatch '^[0-9A-F]{64}$' -or (Hash $source) -ne $packageHash) { throw 'Tower Recovery package SHA-256 mismatch.' }
    if ($owned -and $installedHash -ne $packageHash) { throw 'Uninstall the previously tracked Tower Recovery version first.' }
    if ($exists) { Write-Output "Tower Recovery already installed: $target"; return }

    New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
    $temp = Join-Path $targetDirectory ('.install-' + [Guid]::NewGuid().ToString('N') + '.tmp')
    try {
        Copy-Item -LiteralPath $source -Destination $temp
        if ((Hash $temp) -ne $packageHash) { throw 'Staged DLL SHA-256 mismatch.' }
        if (!$owned) { [IO.File]::WriteAllText($receipt, $packageHash, [Text.Encoding]::ASCII) }
        [IO.File]::Move($temp, $target)
    }
    finally {
        if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp }
    }
    Write-Output "Tower Recovery installed: $target"
}
finally {
    if ($locked) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}
