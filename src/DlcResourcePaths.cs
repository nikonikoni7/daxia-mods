using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using UnityEngine.AddressableAssets.Initialization;
namespace DaxiaMods;
internal static class DlcResourcePaths
{
 private const string Property="WuLin.ThirdPlatformManager.DlcPath";
 internal static void Initialize(ManualLogSource log) {
  try {
   var current=AddressablesRuntimeProperties.EvaluateProperty(Property);
   var changed=DlcResourcePathPolicy.TryRepair(Paths.GameRootPath,current,File.Exists,
    root=>AddressablesRuntimeProperties.SetPropertyValue(Property,root));
   if(changed)log.LogInfo("已修复DLC资源路径："+current+" -> "+Paths.GameRootPath+"；原版文件未改动。");
  }catch(Exception error){log.LogWarning("DLC资源路径检查失败，保留原版处理："+error.Message);}
 }
}
