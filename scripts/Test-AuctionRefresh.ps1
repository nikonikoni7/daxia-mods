$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$sdk=Join-Path $root 'tools\dotnet'
$build=Join-Path $root 'work\build'
New-Item -ItemType Directory -Path $build -Force | Out-Null
$refs=@(Get-ChildItem -LiteralPath (Join-Path $sdk 'packs\Microsoft.NETCore.App.Ref\8.0.31\ref\net8.0') -Filter '*.dll' -File | ForEach-Object { '-r:'+$_.FullName })
$sources=@('tests\mods\AuctionRefresh\AuctionRefreshTests.cs')
if(Test-Path -LiteralPath (Join-Path $root 'src\mods\AuctionRefresh\AuctionRefreshCore.cs')){$sources+='src\mods\AuctionRefresh\AuctionRefreshCore.cs'}
$binary=Join-Path $build 'AuctionRefreshTests.dll'
$arguments=@('-nologo','-nullable:enable','-warnaserror','-target:exe',('-out:'+$binary))+$refs+@($sources | ForEach-Object {Join-Path $root $_})
$rsp=Join-Path $build 'AuctionRefreshTests.rsp'
[IO.File]::WriteAllLines($rsp,@($arguments | ForEach-Object {'"'+$_+'"'}))
& (Join-Path $sdk 'dotnet.exe') (Join-Path $sdk 'sdk\8.0.425\Roslyn\bincore\csc.dll') ('@'+$rsp)
if($LASTEXITCODE -ne 0){throw 'AuctionRefresh tests compile failed'}
[IO.File]::WriteAllText((Join-Path $build 'AuctionRefreshTests.runtimeconfig.json'),'{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}')
& (Join-Path $sdk 'dotnet.exe') $binary
if($LASTEXITCODE -ne 0){throw 'AuctionRefresh tests failed'}
