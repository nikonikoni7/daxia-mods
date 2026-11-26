[CmdletBinding()]
param([switch]$CoreOnly)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$sdk=Join-Path $root 'tools\dotnet'
$build=Join-Path $root 'work\build'
New-Item -ItemType Directory -Path $build -Force | Out-Null
$refs=@(Get-ChildItem -LiteralPath (Join-Path $sdk 'packs\Microsoft.NETCore.App.Ref\8.0.31\ref\net8.0') -Filter '*.dll' -File | ForEach-Object { '-r:'+$_.FullName })
function RunRollTest($name,$sources){
 $binary=Join-Path $build ($name+'.dll')
 $arguments=@('-nologo','-nullable:enable','-warnaserror','-target:exe',('-out:'+$binary))+$refs+@($sources | ForEach-Object {Join-Path $root $_})
 $rsp=Join-Path $build ($name+'.rsp')
 [IO.File]::WriteAllLines($rsp,@($arguments | ForEach-Object {'"'+$_+'"'}))
 & (Join-Path $sdk 'dotnet.exe') (Join-Path $sdk 'sdk\8.0.425\Roslyn\bincore\csc.dll') ('@'+$rsp)
 if($LASTEXITCODE -ne 0){throw "$name compile failed"}
 [IO.File]::WriteAllText((Join-Path $build ($name+'.runtimeconfig.json')),'{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}')
 & (Join-Path $sdk 'dotnet.exe') $binary
 if($LASTEXITCODE -ne 0){throw "$name failed"}
}
RunRollTest 'PerfectRollsTests' @('src\mods\PerfectRolls\PerfectRollsCore.cs','tests\mods\PerfectRolls\PerfectRollsTests.cs')
if(!$CoreOnly){RunRollTest 'PerfectRollsGameTests' @('src\mods\PerfectRolls\PerfectRollsCore.cs','src\mods\PerfectRolls\PerfectRollsCatalog.cs','src\mods\PerfectRolls\PerfectRolls.cs','tests\mods\PerfectRolls\PerfectRollsGameTests.cs','tests\mods\PerfectRolls\PerfectRollsGameStubs.cs')}
