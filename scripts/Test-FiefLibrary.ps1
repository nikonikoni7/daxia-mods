$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$build=Join-Path $root 'work\build'
New-Item -ItemType Directory -Path $build -Force | Out-Null
$refs=@(Get-ChildItem -LiteralPath (Join-Path $root 'tools\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.31\ref\net8.0') -Filter '*.dll' -File|ForEach-Object {'-r:'+$_.FullName})
$sources=@((Join-Path $root 'tests\mods\FiefLibrary\FiefLibraryTests.cs'),(Join-Path $root 'src\mods\FiefLibrary\FiefLibraryCore.cs'))
$out=Join-Path $build 'FiefLibraryTests.dll'
$rsp=Join-Path $build 'FiefLibraryTests.rsp'
[IO.File]::WriteAllLines($rsp,@((@('-nologo','-nullable:enable','-warnaserror','-target:exe',('-out:'+$out))+$refs+$sources)|ForEach-Object {'"'+$_+'"'}))
& (Join-Path $root 'tools\dotnet\dotnet.exe') (Join-Path $root 'tools\dotnet\sdk\8.0.425\Roslyn\bincore\csc.dll') ('@'+$rsp)
if($LASTEXITCODE -ne 0){throw 'FiefLibrary test compilation failed'}
[IO.File]::WriteAllText((Join-Path $build 'FiefLibraryTests.runtimeconfig.json'),'{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}')
& (Join-Path $root 'tools\dotnet\dotnet.exe') $out
if($LASTEXITCODE -ne 0){throw 'FiefLibrary tests failed'}
