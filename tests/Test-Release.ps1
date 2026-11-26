[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][string]$Archive,
 [Parameter(Mandatory=$true)][string]$GameAssembly
)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$testRoot=Join-Path $projectRoot ('work\release-test-'+[Guid]::NewGuid().ToString('N'))
$package=Join-Path $testRoot 'package'
$game=Join-Path $testRoot 'steamapps\common\WulinSH'
New-Item -ItemType Directory -Path $package,$game -Force | Out-Null
Expand-Archive -LiteralPath $Archive -DestinationPath $package
Copy-Item -LiteralPath $GameAssembly -Destination (Join-Path $game 'GameAssembly.dll')
Set-Content -LiteralPath (Join-Path $game 'Wulin.exe') -Value 'Isolated fixture; never executed'
Set-Content -LiteralPath (Join-Path $testRoot 'steamapps\appmanifest_1948980.acf') -Value '"AppState" { "buildid" "24794582" }'
$frontends=@('winhttp.dll','BepInEx\core\BepInEx.Unity.IL2CPP.dll','BepInEx\core\0Harmony.dll','BepInEx\interop\Assembly-CSharp.dll')
foreach($relative in $frontends){
 $target=Join-Path $game $relative
 New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
 Set-Content -LiteralPath $target -Value 'Pre-existing frontend fixture'
}
$protected=@('GameAssembly.dll','Wulin.exe')+$frontends
$baseline=@{}
foreach($relative in $protected){$p=Join-Path $game $relative;$baseline[$relative]=@((Get-FileHash -LiteralPath $p).Hash,(Get-Item -LiteralPath $p).LastWriteTimeUtc.Ticks)}
$count=0
function Check($condition,$label){if(!$condition){throw "FAIL: $label"};$script:count++;Write-Output "PASS: $label"}
$mods=Get-Content -LiteralPath (Join-Path $package 'mods.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach($mod in $mods){
 $name=$mod.name
 if($name -eq 'TowerRecovery'){
  $installer=Join-Path $package 'Install-TowerRecovery.ps1'
  & $installer -Action Install -GamePath $game
  $dll=Join-Path $game 'BepInEx\plugins\TowerRecovery\TowerRecovery.dll'
 }else{
  $installer=Join-Path $package 'Manage-Mods.ps1'
  & $installer -Action Install -Mod $name -GamePath $game
  $dll=Join-Path $game ('BepInEx\plugins\DaxiaMods\'+$name+'.dll')
 }
 Check (Test-Path -LiteralPath $dll) "$name installed"
 $expected=if($name -eq 'TowerRecovery'){Join-Path $package 'TowerRecovery.dll'}else{Join-Path $package ('payload\BepInEx\plugins\DaxiaMods\'+$name+'.dll')}
 Check ((Get-FileHash -LiteralPath $dll).Hash -eq (Get-FileHash -LiteralPath $expected).Hash) "$name installed hash"
 $config=Join-Path $game ('BepInEx\config\local.daxia.'+$name.ToLowerInvariant()+'.cfg')
 New-Item -ItemType Directory -Path (Split-Path $config -Parent) -Force | Out-Null
 Set-Content -LiteralPath $config -Value 'User configuration to preserve'
 if($name -eq 'TowerRecovery'){& $installer -Action Uninstall -GamePath $game}else{& $installer -Action Uninstall -Mod $name -GamePath $game}
 Check (!(Test-Path -LiteralPath $dll)) "$name uninstalled"
 Check ((Get-Content -LiteralPath $config -Raw).Trim() -eq 'User configuration to preserve') "$name configuration preserved"
}
foreach($relative in $protected){
 $p=Join-Path $game $relative
 Check ((Get-FileHash -LiteralPath $p).Hash -eq $baseline[$relative][0] -and (Get-Item -LiteralPath $p).LastWriteTimeUtc.Ticks -eq $baseline[$relative][1]) "protected bytes and mtime: $relative"
}
Write-Output "Release integration: $count checks passed; no game launched."
