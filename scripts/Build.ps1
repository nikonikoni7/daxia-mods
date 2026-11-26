[CmdletBinding()]
param([switch]$RunTests)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$sdkRoot=Join-Path $projectRoot 'tools\dotnet'
$dotnet=Join-Path $sdkRoot 'dotnet.exe'
$compiler=Join-Path $sdkRoot 'sdk\8.0.425\Roslyn\bincore\csc.dll'
if (!(Test-Path -LiteralPath $compiler)) { throw '请先执行 py -3 scripts\get-sdk.py 安装项目本地SDK。' }
$package=Join-Path $projectRoot 'outputs\DaxiaMods'
if (Test-Path -LiteralPath (Join-Path $projectRoot 'payload')) { $package=$projectRoot }
$payload=Join-Path $package 'payload'
if (!(Test-Path -LiteralPath $payload)) { throw '找不到构建引用，请先解压完整交付包到outputs\DaxiaMods。' }
$refs=@()
foreach ($directory in @('dotnet','BepInEx\core','BepInEx\interop')) {
 foreach ($file in Get-ChildItem -LiteralPath (Join-Path $payload $directory) -Filter '*.dll' -File) {
  try { $null=[Reflection.AssemblyName]::GetAssemblyName($file.FullName); $refs += '-r:'+ $file.FullName }
  catch [BadImageFormatException] { }
 }
}
$buildDir=Join-Path $projectRoot 'work\build'
New-Item -ItemType Directory -Path $buildDir -Force | Out-Null
function Compile($name,$target,$sources,$references) {
 $out=Join-Path $buildDir ($name+'.dll')
 $arguments=@('-nologo','-nullable:enable','-warnaserror','-deterministic+','-optimize+',('-target:'+$target),('-out:'+$out))+$references+$sources
 $rsp=Join-Path $buildDir ($name+'.rsp')
 [IO.File]::WriteAllLines($rsp,@($arguments | ForEach-Object {'"'+$_+'"'}),(New-Object Text.UTF8Encoding($false)))
 & $dotnet $compiler ('@'+$rsp) | Out-Host
 if ($LASTEXITCODE -ne 0) { throw "编译失败：$name" }
 return $out
}
$shared=@((Join-Path $projectRoot 'src\Rules.cs'),(Join-Path $projectRoot 'src\BuildGuard.cs'),(Join-Path $projectRoot 'src\DlcResourcePathPolicy.cs'),(Join-Path $projectRoot 'src\DlcResourcePaths.cs'))
$pluginOutput=Join-Path $package 'plugins'
New-Item -ItemType Directory -Path $pluginOutput -Force | Out-Null
foreach ($name in @('FastFinish','ExploreCamera','ChestMemo','CompanionTraining','PerfectRolls','FreeNaming','FiefReuse','FiefPositionFix','ItemSpawner','AuctionRefresh','AuctionSkyLantern','BountyRefresh','QuestTracking','CompanionTravel','AnswerHints','FusionRename','KungfuOrder','FiefLibrary')) {
 $source='src\mods\'+$name+'\'+$name+'.cs'
 $sources=$shared+@(Join-Path $projectRoot $source)
 if($name -eq 'ExploreCamera'){$sources+=Join-Path $projectRoot 'src\mods\ExploreCamera\CameraMotion.cs'}
 if($name -in @('ChestMemo','ItemSpawner')){$sources+=Join-Path $projectRoot 'src\ModUiInput.cs'}
 if($name -eq 'ChestMemo'){$sources+=Join-Path $projectRoot 'src\mods\ChestMemo\ChestMemoCore.cs'}
 if($name -eq 'ChestMemo'){$sources+=Join-Path $projectRoot 'src\mods\ChestMemo\Performance\PortraitWarmupCore.cs';$sources+=Join-Path $projectRoot 'src\mods\ChestMemo\Performance\PortraitWarmup.cs'}
 if($name -eq 'CompanionTraining'){$sources+=Join-Path $projectRoot 'src\mods\CompanionTraining\CompanionTrainingCore.cs'}
 if($name -eq 'PerfectRolls'){$sources+=Join-Path $projectRoot 'src\mods\PerfectRolls\PerfectRollsCore.cs';$sources+=Join-Path $projectRoot 'src\mods\PerfectRolls\PerfectRollsCatalog.cs'}
 if($name -eq 'ItemSpawner'){$sources+=Join-Path $projectRoot 'src\mods\ItemSpawner\ItemSpawnerCore.cs'}
 if($name -eq 'AuctionRefresh'){$sources+=Join-Path $projectRoot 'src\mods\AuctionRefresh\AuctionRefreshCore.cs'}
 if($name -eq 'AuctionSkyLantern'){$sources+=Join-Path $projectRoot 'src\mods\AuctionSkyLantern\AuctionSkyLanternCore.cs'}
 if($name -eq 'BountyRefresh'){$sources+=Join-Path $projectRoot 'src\mods\BountyRefresh\BountyRefreshCore.cs'}
 if($name -eq 'QuestTracking'){$sources+=Join-Path $projectRoot 'src\mods\QuestTracking\QuestTrackingCore.cs'}
 if($name -eq 'CompanionTravel'){$sources+=Join-Path $projectRoot 'src\mods\CompanionTravel\CompanionTravelCore.cs'}
 if($name -eq 'AnswerHints'){$sources+=Join-Path $projectRoot 'src\mods\AnswerHints\AnswerHintsCore.cs'}
 if($name -eq 'KungfuOrder'){$sources+=Join-Path $projectRoot 'src\mods\KungfuOrder\KungfuOrderCore.cs'}
 if($name -eq 'FusionRename'){$sources+=Join-Path $projectRoot 'src\mods\FusionRename\FusionRenameCore.cs';$sources+=Join-Path $projectRoot 'src\ModUiInput.cs';$sources+=Join-Path $projectRoot 'src\mods\FusionRename\FusionRenameData.cs'}
 if($name -eq 'FiefLibrary'){$sources+=@(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src\mods\FiefLibrary') -Filter '*.cs' -File | Where-Object Name -ne 'FiefLibrary.cs' | ForEach-Object FullName)}
 $binary=Compile $name 'library' $sources $refs
 Copy-Item -LiteralPath $binary -Destination $pluginOutput -Force
 Write-Output "编译通过：$name"
}
& (Join-Path $PSScriptRoot 'Build-TowerRecovery.ps1') -ReferenceRoot $payload
Copy-Item -LiteralPath (Join-Path $projectRoot 'outputs\TowerRecovery\TowerRecovery.dll') -Destination $pluginOutput -Force
Write-Output '编译通过：TowerRecovery'
if ($RunTests) {
 & (Join-Path $PSScriptRoot 'Test-FiefLibrary.ps1')
 & (Join-Path $PSScriptRoot 'Test-KungfuOrder.ps1')
 & (Join-Path $PSScriptRoot 'Test-FusionRename.ps1')
 & (Join-Path $PSScriptRoot 'Test-AnswerHints.ps1')
 & (Join-Path $PSScriptRoot 'Test-CompanionTravel.ps1')
 & (Join-Path $PSScriptRoot 'Test-QuestTracking.ps1')
 & (Join-Path $PSScriptRoot 'Test-BountyRefresh.ps1')
 & (Join-Path $PSScriptRoot 'Test-PortraitWarmup.ps1')
 & (Join-Path $PSScriptRoot 'Test-FiefPositionFix.ps1')
 & (Join-Path $PSScriptRoot 'Test-ModUiInput.ps1')
 & (Join-Path $PSScriptRoot 'Test-AuctionRefresh.ps1')
 & (Join-Path $PSScriptRoot 'Test-AuctionSkyLantern.ps1')
 & (Join-Path $PSScriptRoot 'Test-ItemSpawner.ps1')
 & (Join-Path $PSScriptRoot 'Test-FiefReuse.ps1')
 & (Join-Path $PSScriptRoot 'Test-FreeNaming.ps1')
 & (Join-Path $PSScriptRoot 'Test-ChestMemo.ps1')
 & (Join-Path $PSScriptRoot 'Test-CompanionTraining.ps1')
 & (Join-Path $PSScriptRoot 'Test-PerfectRolls.ps1')
 $binary=Compile 'PolicyTests' 'exe' @((Join-Path $projectRoot 'src\Rules.cs'),(Join-Path $projectRoot 'tests\PolicyTests.cs')) $refs
 [IO.File]::WriteAllText((Join-Path $buildDir 'PolicyTests.runtimeconfig.json'),'{"runtimeOptions":{"framework":{"name":"Microsoft.NETCore.App","version":"8.0.0"}}}')
 & $dotnet $binary
 if ($LASTEXITCODE -ne 0) { throw '逻辑测试失败。' }
 $testRefs=@(Get-ChildItem -LiteralPath (Join-Path $sdkRoot 'packs\Microsoft.NETCore.App.Ref\8.0.31\ref\net8.0') -Filter '*.dll' -File | ForEach-Object { '-r:'+$_.FullName })
 $binary=Compile 'DlcResourcePathTests' 'exe' @((Join-Path $projectRoot 'src\DlcResourcePathPolicy.cs'),(Join-Path $projectRoot 'tests\DlcResourcePathTests.cs')) $testRefs
 Copy-Item -LiteralPath (Join-Path $buildDir 'PolicyTests.runtimeconfig.json') -Destination (Join-Path $buildDir 'DlcResourcePathTests.runtimeconfig.json') -Force
 & $dotnet $binary
 if ($LASTEXITCODE -ne 0) { throw 'DLC资源路径回归测试失败。' }
 $binary=Compile 'ExploreCameraInputTests' 'exe' @((Join-Path $projectRoot 'src\Rules.cs'),(Join-Path $projectRoot 'src\mods\ExploreCamera\CameraMotion.cs'),(Join-Path $projectRoot 'src\mods\ExploreCamera\ExploreCamera.cs'),(Join-Path $projectRoot 'tests\mods\ExploreCamera\ExploreCameraInputTests.cs')) $testRefs
 Copy-Item -LiteralPath (Join-Path $buildDir 'PolicyTests.runtimeconfig.json') -Destination (Join-Path $buildDir 'ExploreCameraInputTests.runtimeconfig.json') -Force
 & $dotnet $binary
 if ($LASTEXITCODE -ne 0) { throw '探索镜头输入回归测试失败。' }
}
