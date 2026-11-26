using System;
using System.IO;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Logging;
namespace DaxiaMods;
internal static class BuildGuard
{
 internal const string Hash="F8393E44423442526DFFBF3AF52F8CCD56FB33769D97E2C7161EBC49D68B3564";
 internal static bool Check(ManualLogSource log) {
  try{
  var file=Path.Combine(Paths.GameRootPath,"GameAssembly.dll");
  using var stream=File.OpenRead(file);
  using var sha=SHA256.Create();
  if(Convert.ToHexString(sha.ComputeHash(stream))==Hash){DlcResourcePaths.Initialize(log);return true;}
  log.LogError("游戏版本不匹配，插件已停用；适配目标：Steam Build 24794582。请重新适配后使用。");return false;
  }catch(Exception e){log.LogError("无法核对游戏版本，插件已停用："+e.Message);return false;}
 }
}
