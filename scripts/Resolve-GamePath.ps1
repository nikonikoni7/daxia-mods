function Resolve-WulinGamePath {
 [CmdletBinding()]
 param([string]$GamePath, [string[]]$SteamRoots)

 if (![string]::IsNullOrWhiteSpace($GamePath)) {
  $resolved=[IO.Path]::GetFullPath($GamePath).TrimEnd('\','/')
  if (!(Test-Path -LiteralPath (Join-Path $resolved 'Wulin.exe') -PathType Leaf)) { throw '指定目录中没有Wulin.exe，请通过Steam“浏览本地文件”确认游戏目录。' }
  return $resolved
 }
 if (!$PSBoundParameters.ContainsKey('SteamRoots')) {
  $SteamRoots=@()
  foreach($key in @('HKCU:\Software\Valve\Steam','HKLM:\SOFTWARE\Valve\Steam','HKLM:\SOFTWARE\WOW6432Node\Valve\Steam')) {
   $entry=Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue
   foreach($property in @('SteamPath','InstallPath')) {
    if($entry -and $entry.$property) {$SteamRoots+=@([string]$entry.$property)}
   }
  }
  foreach($programFiles in @(${env:ProgramFiles(x86)},$env:ProgramFiles)) {
   if($programFiles) {$SteamRoots+=@(Join-Path $programFiles 'Steam')}
  }
 }
 $libraries=@($SteamRoots)
 foreach($steam in $SteamRoots) {
  $vdf=Join-Path $steam 'steamapps\libraryfolders.vdf'
  if(Test-Path -LiteralPath $vdf -PathType Leaf) {
   $text=Get-Content -LiteralPath $vdf -Raw -Encoding UTF8
   foreach($match in [regex]::Matches($text,'"(?:path|\d+)"\s*"([^"\r\n]+)"')) {
    $library=$match.Groups[1].Value.Replace('\\','\')
    if([IO.Path]::IsPathRooted($library)) {$libraries+=@($library)}
   }
  }
 }
 $games=@()
 foreach($library in @($libraries | Sort-Object -Unique)) {
  $manifest=Join-Path $library 'steamapps\appmanifest_1948980.acf'
  if(!(Test-Path -LiteralPath $manifest -PathType Leaf)) {continue}
  $text=Get-Content -LiteralPath $manifest -Raw -Encoding UTF8
  $match=[regex]::Match($text,'"installdir"\s*"([^"\r\n]+)"')
  if(!$match.Success) {continue}
  $folder=$match.Groups[1].Value
  if($folder -in @('.','..') -or $folder -match '[\\/]' -or [IO.Path]::IsPathRooted($folder)) {throw 'Steam安装清单中的游戏目录无效。'}
  $candidate=[IO.Path]::GetFullPath((Join-Path $library ('steamapps\common\'+$folder))).TrimEnd('\','/')
  if(Test-Path -LiteralPath (Join-Path $candidate 'Wulin.exe') -PathType Leaf) {$games+=@($candidate)}
 }
 $games=@($games | Sort-Object -Unique)
 if($games.Count -eq 1) {return $games[0]}
 if($games.Count -gt 1) {throw ('发现多个游戏目录，请使用-GamePath明确选择：'+($games -join '；'))}
 throw '未找到Steam版大侠立志传。请通过Steam“浏览本地文件”找到游戏目录，再使用-GamePath指定。'
}
