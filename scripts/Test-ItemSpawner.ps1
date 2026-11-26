$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$sdk=Join-Path $root 'tools\dotnet'
$build=Join-Path $root 'work\build'
$refs=@(Get-ChildItem -LiteralPath (Join-Path $sdk 'packs\Microsoft.NETCore.App.Ref\8.0.31\ref\net8.0') -Filter '*.dll' -File | ForEach-Object {'-r:'+$_.FullName})
$sources=@(Join-Path $root 'tests\mods\ItemSpawner\ItemSpawnerTests.cs')
if(Test-Path -LiteralPath (Join-Path $root 'src\mods\ItemSpawner\ItemSpawnerCore.cs')){$sources+=Join-Path $root 'src\mods\ItemSpawner\ItemSpawnerCore.cs'}
$binary=Join-Path $build 'ItemSpawnerTests.dll'
$arguments=@('-nologo','-nullable:enable','-warnaserror','-target:exe',('-out:'+$binary))+$refs+$sources
$rsp=Join-Path $build 'ItemSpawnerTests.rsp'
[IO.File]::WriteAllLines($rsp,@($arguments|ForEach-Object {'"'+$_+'"'}))
& (Join-Path $sdk 'dotnet.exe') (Join-Path $sdk 'sdk\8.0.425\Roslyn\bincore\csc.dll') ('@'+$rsp)
if($LASTEXITCODE -ne 0){throw 'ItemSpawner test compilation failed'}
[IO.File]::WriteAllText((Join-Path $build 'ItemSpawnerTests.runtimeconfig.json'),'{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}')
& (Join-Path $sdk 'dotnet.exe') $binary
if($LASTEXITCODE -ne 0){throw 'ItemSpawner tests failed'}
