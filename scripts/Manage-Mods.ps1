[CmdletBinding()]
param(
 [ValidateSet('Install','Uninstall')][string]$Action = 'Install',
 [string]$GamePath = '',
 [ValidateSet('Both','FastFinish','ExploreCamera','ChestMemo','CompanionTraining','PerfectRolls','FreeNaming','FiefReuse','FiefPositionFix','ItemSpawner','AuctionRefresh','AuctionSkyLantern','BountyRefresh','QuestTracking','CompanionTravel','AnswerHints','FusionRename','KungfuOrder','FiefLibrary')][string]$Mod = 'Both'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Resolve-GamePath.ps1')
$root = Resolve-WulinGamePath -GamePath $GamePath
if (!(Test-Path -LiteralPath (Join-Path $root 'Wulin.exe') -PathType Leaf)) { throw '游戏目录错误：找不到Wulin.exe。' }
if (@(Get-Process -Name Wulin -ErrorAction SilentlyContinue).Count) { throw '请先退出游戏再安装或卸载。' }
function SafePath([string]$relative) {
 if ([IO.Path]::IsPathRooted($relative) -or $relative -match '(^|[\\/])\.\.([\\/]|$)') { throw "拒绝不安全路径：$relative" }
 $full = [IO.Path]::GetFullPath((Join-Path $root $relative))
 if (!$full.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)) { throw "路径超出游戏目录：$relative" }
 $probe = $full
 while ($probe.Length -ge $root.Length) {
  if (Test-Path -LiteralPath $probe) {
   if ((Get-Item -LiteralPath $probe -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "拒绝链接目录：$probe" }
  }
  if ($probe -eq $root) { break }; $probe = Split-Path $probe -Parent
 }
 return $full
}
function Hash([string]$path) { return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
function AuthorizedEntry($entry) {
 $valid=$false
 switch ($entry.Group) {
  'Runtime' { $valid=$entry.Relative -match '^(winhttp\.dll|doorstop_config\.ini|\.doorstop_version|dotnet\\[^\\]+|BepInEx\\(core|interop)\\[^\\]+|BepInEx\\config\\BepInEx\.cfg)$' }
  'FastFinish' { $valid=$entry.Relative -in @('BepInEx\plugins\DaxiaMods\FastFinish.dll','BepInEx\config\local.daxia.fastfinish.cfg') }
  'ExploreCamera' { $valid=$entry.Relative -in @('BepInEx\plugins\DaxiaMods\ExploreCamera.dll','BepInEx\config\local.daxia.explorecamera.cfg') }
  'ChestMemo' { $valid=$entry.Relative -in @('BepInEx\plugins\DaxiaMods\ChestMemo.dll','BepInEx\config\local.daxia.chestmemo.cfg') }
  'CompanionTraining' { $valid=$entry.Relative -in @('BepInEx\plugins\DaxiaMods\CompanionTraining.dll','BepInEx\config\local.daxia.companiontraining.cfg') }
  'PerfectRolls' { $valid=$entry.Relative -in @('BepInEx\plugins\DaxiaMods\PerfectRolls.dll','BepInEx\config\local.daxia.perfectrolls.cfg') }
  'FreeNaming' { $valid=$entry.Relative -in @('BepInEx\plugins\DaxiaMods\FreeNaming.dll','BepInEx\config\local.daxia.freenaming.cfg') }
  'FiefReuse' { $valid=$entry.Relative -in @('BepInEx\plugins\DaxiaMods\FiefReuse.dll','BepInEx\config\local.daxia.fiefreuse.cfg') }
  'FiefPositionFix' { $valid=$entry.Relative -in @('BepInEx\plugins\DaxiaMods\FiefPositionFix.dll','BepInEx\config\local.daxia.fiefpositionfix.cfg') }
  'ItemSpawner' { $valid=$entry.Relative -in @('BepInEx\plugins\DaxiaMods\ItemSpawner.dll','BepInEx\config\local.daxia.itemspawner.cfg') }
  'AuctionRefresh' { $valid=$entry.Relative -in @('BepInEx\plugins\DaxiaMods\AuctionRefresh.dll','BepInEx\config\local.daxia.auctionrefresh.cfg') }
  'AuctionSkyLantern' { $valid=$entry.Relative -in @('BepInEx\plugins\DaxiaMods\AuctionSkyLantern.dll','BepInEx\config\local.daxia.auctionskylantern.cfg') }
  'BountyRefresh' { $valid=$entry.Relative -in @('BepInEx\plugins\DaxiaMods\BountyRefresh.dll','BepInEx\config\local.daxia.bountyrefresh.cfg') }
  'QuestTracking' { $valid=$entry.Relative -in @('BepInEx\plugins\DaxiaMods\QuestTracking.dll','BepInEx\config\local.daxia.questtracking.cfg') }
  'CompanionTravel' { $valid=$entry.Relative -in @('BepInEx\plugins\DaxiaMods\CompanionTravel.dll','BepInEx\config\local.daxia.companiontravel.cfg') }
  'FiefLibrary' { $valid=$entry.Relative -in @('BepInEx\plugins\DaxiaMods\FiefLibrary.dll','BepInEx\config\local.daxia.fieflibrary.cfg') }
  'AnswerHints' { $valid=$entry.Relative -in @('BepInEx\plugins\DaxiaMods\AnswerHints.dll','BepInEx\config\local.daxia.answerhints.cfg') }
  'FusionRename' { $valid=$entry.Relative -in @('BepInEx\plugins\DaxiaMods\FusionRename.dll','BepInEx\config\local.daxia.fusionrename.cfg') }
  'KungfuOrder' { $valid=$entry.Relative -in @('BepInEx\plugins\DaxiaMods\KungfuOrder.dll','BepInEx\config\local.daxia.kungfuorder.cfg') }
 }
 if (!$valid) { throw "记录包含非授权文件：$($entry.Relative)" }
}
$store = SafePath '.daxia-mods-store'
$receipt = SafePath '.daxia-mods-store\receipt.json'
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'payload-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($file in $manifest.Files) {
 if ($file.Group -eq 'Runtime') { throw '仅插件包不得包含运行时载荷。' }
 AuthorizedEntry $file
}
$state = [pscustomobject]@{ Schema=1; GamePath=$root; Files=@() }
if (Test-Path -LiteralPath $receipt) {
 $state = Get-Content -LiteralPath $receipt -Raw -Encoding UTF8 | ConvertFrom-Json
 if ($state.Schema -ne 1 -or $state.GamePath -ne $root) { throw '安装记录与目录不匹配。' }
 foreach ($entry in $state.Files) {
  if ($entry.Group -in @('Runtime','FastFinish','ExploreCamera','ChestMemo','CompanionTraining','PerfectRolls','FreeNaming','FiefReuse','FiefPositionFix','ItemSpawner','AuctionRefresh','AuctionSkyLantern','BountyRefresh','QuestTracking','CompanionTravel','AnswerHints','FusionRename','KungfuOrder','FiefLibrary')) { AuthorizedEntry $entry }
  elseif ($entry.Relative -notmatch '^BepInEx\\(plugins\\DaxiaMods\\[A-Za-z0-9]+\.dll|config\\local\.daxia\.[A-Za-z0-9]+\.cfg)$') { throw '旧收据包含不支持的路径。' }
  if ($entry.InstalledHash -notmatch '^[A-Fa-f0-9]{64}$') { throw '记录中的文件哈希无效。' }
  $null=SafePath $entry.Relative
  if ($entry.Backup) {
   if ($entry.Backup -notmatch '^\.daxia-mods-store\\backups\\[a-f0-9]{32}\.bak$' -or $entry.BackupHash -notmatch '^[A-Fa-f0-9]{64}$') { throw '记录中的备份路径或哈希无效。' }
   $null=SafePath $entry.Backup
  }
 }
}
function SaveState {
 New-Item -ItemType Directory -Path $store -Force | Out-Null
 $temporary = SafePath '.daxia-mods-store\receipt.tmp'
 [IO.File]::WriteAllText($temporary,($state | ConvertTo-Json -Depth 8),(New-Object Text.UTF8Encoding($false)))
 Move-Item -LiteralPath $temporary -Destination $receipt -Force
}
function RestoreEntry($entry) {
 $target = SafePath $entry.Relative
 if ($entry.Backup) {
  $backup = SafePath $entry.Backup
  if (!(Test-Path -LiteralPath $backup) -or (Hash $backup) -ne $entry.BackupHash) { throw "备份丢失或损坏：$($entry.Relative)" }
 }
 if (Test-Path -LiteralPath $target) {
  if ((Hash $target) -ne $entry.InstalledHash) { Write-Warning "保留安装后被修改的文件：$($entry.Relative)"; return $false }
  Remove-Item -LiteralPath $target -Force
 }
 if ($entry.Backup) {
  New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
  Copy-Item -LiteralPath $backup -Destination $target
 }
 return $true
}
$selected = if ($Mod -eq 'Both') { @('FastFinish','ExploreCamera') } else { @($Mod) }
if ($Action -eq 'Install') {
 $steamManifest = Join-Path (Split-Path (Split-Path $root -Parent) -Parent) 'appmanifest_1948980.acf'
 if (!(Test-Path -LiteralPath $steamManifest) -or (Get-Content -LiteralPath $steamManifest -Raw -Encoding UTF8) -notmatch '"buildid"\s*"24794582"') { throw 'Steam Build ID不匹配，需要24794582。' }
 if ((Hash (Join-Path $root 'GameAssembly.dll')) -ne $manifest.GameAssemblyHash) { throw 'GameAssembly.dll版本不匹配，拒绝安装。' }
 foreach ($relative in @('winhttp.dll','BepInEx\core\BepInEx.Unity.IL2CPP.dll','BepInEx\core\0Harmony.dll','BepInEx\interop\Assembly-CSharp.dll')) {
  if (!(Test-Path -LiteralPath (SafePath $relative) -PathType Leaf)) { throw "缺少BepInEx 6 IL2CPP前置或本机生成接口：$relative；请先安装前置并启动游戏生成接口。" }
 }
 $files = @($manifest.Files | Where-Object { $_.Group -in $selected })
 if (!$files.Count) { throw '安装包缺少所选Mod。' }
 foreach ($file in $files) {
  AuthorizedEntry $file
  $null = SafePath $file.Relative
  if ((Hash (Join-Path $PSScriptRoot ('payload\'+$file.Relative))) -ne $file.Hash) { throw "安装包校验失败：$($file.Relative)" }
 }
 $added = @()
 try {
  foreach ($file in $files) {
   $target = SafePath $file.Relative
   $known = @($state.Files | Where-Object Relative -eq $file.Relative)
   if ($known.Count) {
    if ($file.Relative -like '*.cfg') { continue }
    if (!(Test-Path -LiteralPath $target) -or (Hash $target) -ne $file.Hash) { throw "已安装文件发生变化，请先处理：$($file.Relative)" }
    continue
   }
   if ((Test-Path -LiteralPath $target) -and ($file.Group -eq 'Runtime' -or $file.Relative -like '*.cfg')) {
    if ($file.Relative -like '*.cfg') { continue }
    if ((Hash $target) -ne $file.Hash) { throw "保留已有运行环境，拒绝覆盖：$($file.Relative)" }
    continue
   }
   $entry = [pscustomobject]@{ Relative=$file.Relative; Group=$file.Group; InstalledHash=$file.Hash; Backup=$null; BackupHash=$null }
   if (Test-Path -LiteralPath $target) {
    $entry.Backup = '.daxia-mods-store\backups\'+[Guid]::NewGuid().ToString('N')+'.bak'
    $entry.BackupHash = Hash $target
    $backup = SafePath $entry.Backup
    New-Item -ItemType Directory -Path (Split-Path $backup -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $target -Destination $backup
   }
   $state.Files = @($state.Files)+@($entry); $added += $entry; SaveState
   New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
   Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('payload\'+$file.Relative)) -Destination $target -Force
  }
 } catch {
  $originalError = $_
  [array]::Reverse($added)
  foreach ($entry in $added) {
   $target = SafePath $entry.Relative
   $untouched = $entry.Backup -and (Test-Path -LiteralPath $target) -and (Hash $target) -eq $entry.BackupHash
   if ($untouched -or (RestoreEntry $entry)) { $state.Files = @($state.Files | Where-Object Relative -ne $entry.Relative) }
  }
  SaveState; throw $originalError
 }
 SaveState
 Write-Output "安装完成：$Mod；记录：$receipt"
} else {
 foreach ($entry in @($state.Files | Where-Object { $_.Group -in $selected })) {
  if (RestoreEntry $entry) { $state.Files=@($state.Files | Where-Object Relative -ne $entry.Relative); SaveState }
 }
 SaveState
 Write-Output "卸载处理完成：$Mod。修改过的文件、备份和日志保留。"
}
