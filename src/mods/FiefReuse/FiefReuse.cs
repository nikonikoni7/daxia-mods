using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using WuLin;
namespace DaxiaMods;
[BepInPlugin("local.daxia.fiefreuse","大侠·家园反复继承","1.0.0")]
public sealed class FiefReuse : BasePlugin
{
 private static FiefReuse instance=null!;
 private ConfigEntry<bool> enabled=null!;
 private Harmony? harmony;
 public override void Load(){
  instance=this;
  enabled=Config.Bind("General","Enabled",true,"仅对跨周目共享家园解除珍宝阁领取和悟道碑拓印次数限制；保留原版存取与刻印规则。");
  if(!BuildGuard.Check(Log))return;
  try{
   harmony=new Harmony("local.daxia.fiefreuse");
   harmony.PatchAll(typeof(FiefReuse).Assembly);
   Log.LogInfo("家园反复继承已加载：共享家园可分批领取装备和拓印功法；私人家园保持原版限制。");
  }catch(Exception e){harmony?.UnpatchSelf();Log.LogError("接口接入失败，家园反复继承已停用："+e);}
 }
 public override bool Unload(){harmony?.UnpatchSelf();return true;}
 // Keep native history and every transfer/inscription operation intact. The UI
 // and GameManager both reach these checks; patching only the UI is insufficient.
 private static bool CheckTaken(FiefUnsharedData data,ref bool result){
  if(!instance.enabled.Value||data.isPrivateFief)return true;
  result=false;
  return false;
 }
 [HarmonyPatch(typeof(FiefUnsharedData),nameof(FiefUnsharedData.IsFiefItemTaken))]
 private static class ItemTakenPatch{
  private static bool Prefix(FiefUnsharedData __instance,ref bool __result)=>CheckTaken(__instance,ref __result);
 }
 [HarmonyPatch(typeof(FiefUnsharedData),nameof(FiefUnsharedData.IsFiefKungfuBookTaken))]
 private static class KungfuTakenPatch{
  private static bool Prefix(FiefUnsharedData __instance,ref bool __result)=>CheckTaken(__instance,ref __result);
 }
}
