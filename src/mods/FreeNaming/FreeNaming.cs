using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using WuLin;
namespace DaxiaMods;
[BepInPlugin("local.daxia.freenaming","大侠·自由取名","1.0.0")]
public sealed class FreeNaming : BasePlugin
{
 private static FreeNaming instance=null!;
 private ConfigEntry<bool> enabled=null!;
 private Harmony? harmony;
 public override void Load() {
  instance=this;
  enabled=Config.Bind("General","Enabled",true,"取消本地输入的敏感词限制；保留原版字数和格式校验。");
  if(!BuildGuard.Check(Log))return;
  try {
   harmony=new Harmony("local.daxia.freenaming");
   harmony.PatchAll(typeof(FreeNaming).Assembly);
   Log.LogInfo("自由取名已加载：取消本地敏感词限制，保留原版字数和格式校验。");
  }catch(Exception e){harmony?.UnpatchSelf();Log.LogError("接口接入失败，自由取名已停用："+e);}
 }
 public override bool Unload(){harmony?.UnpatchSelf();return true;}
 [HarmonyPatch(typeof(GameUtil),nameof(GameUtil.ContainsForbiddenWord))]
 private static class ForbiddenWordPatch {
  private static bool Prefix(ref bool __result) {
   if(!instance.enabled.Value)return true;
   __result=false;
   return false;
  }
 }
}
