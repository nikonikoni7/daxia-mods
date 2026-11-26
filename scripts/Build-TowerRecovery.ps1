[CmdletBinding()]
param(
    [string]$ReferenceRoot = '',
    [switch]$TestsOnly
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (!$ReferenceRoot) { $ReferenceRoot = Join-Path $root 'outputs\DaxiaMods\payload' }
$sdk = Join-Path $root 'tools\dotnet'
$dotnet = Join-Path $sdk 'dotnet.exe'
$csc = Join-Path $sdk 'sdk\8.0.425\Roslyn\bincore\csc.dll'
$work = Join-Path $root 'work\tower-recovery'
$source = Join-Path $root 'src\mods\TowerRecovery'
$core = Join-Path $source 'RecoveryRound.cs'
New-Item -ItemType Directory -Path $work -Force | Out-Null

function Compile($name, $arguments) {
    $rsp = Join-Path $work ($name + '.rsp')
    $options = @('-nologo', '-nullable:enable', '-warnaserror', '-optimize+', '-deterministic+') + $arguments
    [IO.File]::WriteAllLines($rsp, @($options | ForEach-Object { '"' + $_ + '"' }))
    & $dotnet $csc ('@' + $rsp)
    if ($LASTEXITCODE -ne 0) { throw "$name compilation failed" }
}

$refs = @(Get-ChildItem -LiteralPath (Join-Path $sdk 'packs\Microsoft.NETCore.App.Ref\8.0.31\ref\net8.0') -Filter '*.dll' -File |
    ForEach-Object { '-r:' + $_.FullName })
$tests = Join-Path $work 'TowerRecoveryTests.dll'
Compile TowerRecoveryTests (@('-target:exe', ('-out:' + $tests), $core, (Join-Path $root 'tests\mods\TowerRecovery\TowerRecoveryTests.cs')) + $refs)
[IO.File]::WriteAllText((Join-Path $work 'TowerRecoveryTests.runtimeconfig.json'), '{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}')
& $dotnet $tests
if ($LASTEXITCODE -ne 0) { throw 'TowerRecovery regression failed' }
if ($TestsOnly) { return }

$release = Join-Path $root 'outputs\TowerRecovery'
New-Item -ItemType Directory -Path $release -Force | Out-Null
$gameRefs = @(foreach ($directory in @('dotnet', 'BepInEx\core', 'BepInEx\interop')) {
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $ReferenceRoot $directory) -Filter '*.dll' -File) {
        try {
            $null = [Reflection.AssemblyName]::GetAssemblyName($file.FullName)
            '-r:' + $file.FullName
        }
        catch [BadImageFormatException] { }
    }
})
$dll = Join-Path $release 'TowerRecovery.dll'
Compile TowerRecovery (@('-target:library', ('-out:' + $dll), $core, (Join-Path $source 'TowerRecovery.cs')) + $gameRefs)
$hash = Get-FileHash -LiteralPath $dll
$installer = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Install-TowerRecovery.ps1') -Raw -Encoding utf8).Replace('@TOWER_RECOVERY_SHA256@', $hash.Hash)
[IO.File]::WriteAllText((Join-Path $release 'Install-TowerRecovery.ps1'), $installer)
Copy-Item -LiteralPath (Join-Path $source 'README.md') -Destination $release
$hash
