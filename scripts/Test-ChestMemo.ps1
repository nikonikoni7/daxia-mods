$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$sdk=Join-Path $root 'tools\dotnet'
$build=Join-Path $root 'work\build'
New-Item -ItemType Directory -Path $build -Force | Out-Null
$refs=@(Get-ChildItem -LiteralPath (Join-Path $sdk 'packs\Microsoft.NETCore.App.Ref\8.0.31\ref\net8.0') -Filter '*.dll' -File | ForEach-Object { '-r:'+$_.FullName })
$source=Join-Path $root 'src\mods\ChestMemo\ChestMemoCore.cs'
$argsList=@('-nologo','-nullable:enable','-warnaserror','-target:exe',('-out:'+(Join-Path $build 'ChestMemoTests.dll')))+$refs+@($source,(Join-Path $root 'tests\mods\ChestMemo\ChestMemoTests.cs'))
$rsp=Join-Path $build 'ChestMemoTests.rsp'
[IO.File]::WriteAllLines($rsp,@($argsList | ForEach-Object {'"'+$_+'"'}))
& (Join-Path $sdk 'dotnet.exe') (Join-Path $sdk 'sdk\8.0.425\Roslyn\bincore\csc.dll') ('@'+$rsp)
if($LASTEXITCODE -ne 0){throw 'ChestMemo test compile failed'}
[IO.File]::WriteAllText((Join-Path $build 'ChestMemoTests.runtimeconfig.json'),'{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}')
& (Join-Path $sdk 'dotnet.exe') (Join-Path $build 'ChestMemoTests.dll')
if($LASTEXITCODE -ne 0){throw 'ChestMemo tests failed'}
$argsList=@('-nologo','-nullable:enable','-warnaserror','-target:exe',('-out:'+(Join-Path $build 'ChestMemoGameTests.dll')))+$refs+@($source,(Join-Path $root 'src\mods\ChestMemo\ChestMemo.cs'),(Join-Path $root 'src\ModUiInput.cs'),(Join-Path $root 'tests\mods\ChestMemo\ChestMemoGameTests.cs'),(Join-Path $root 'tests\mods\ChestMemo\ChestMemoGameStubs.cs'))
[IO.File]::WriteAllLines($rsp,@($argsList | ForEach-Object {'"'+$_+'"'}))
& (Join-Path $sdk 'dotnet.exe') (Join-Path $sdk 'sdk\8.0.425\Roslyn\bincore\csc.dll') ('@'+$rsp)
if($LASTEXITCODE -ne 0){throw 'ChestMemo game-boundary compile failed'}
Copy-Item -LiteralPath (Join-Path $build 'ChestMemoTests.runtimeconfig.json') -Destination (Join-Path $build 'ChestMemoGameTests.runtimeconfig.json') -Force
& (Join-Path $sdk 'dotnet.exe') (Join-Path $build 'ChestMemoGameTests.dll')
if($LASTEXITCODE -ne 0){throw 'ChestMemo game-boundary tests failed'}
& py -3 (Join-Path $root 'tests\mods\ChestMemo\ChestMemoRecoveryTests.py')
if($LASTEXITCODE -ne 0){throw 'ChestMemo offline recovery tests failed'}
