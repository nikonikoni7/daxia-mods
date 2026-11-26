using System;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.AddressableAssets;
using WuLin;
using RM=WuLin.GameFrameworks.ResourceManager;
namespace DaxiaMods;

// Prepare the shared dialogue portrait atlas while the main menu is visible.
// Native async asset completion can still stall a frame, so never start in play.
internal sealed class PortraitWarmup {
 readonly Action<string> info,warning;
 readonly PortraitWarmupQueue queue=new(1);
 bool planned,finished,stopped;
 float nextCheck,started;
 internal PortraitWarmup(Action<string> info,Action<string> warning){this.info=info;this.warning=warning;}
 internal void Stop(){stopped=true;queue.Reset();}
 internal void Tick(bool enabled){
  if(stopped||finished)return;
  if(planned){if(Time.unscaledTime-started>30){finished=true;warning("对话立绘准备超时，保留原版按需加载。");}return;}
  if(!enabled||Time.unscaledTime<nextCheck)return;nextCheck=Time.unscaledTime+.5f;
  if(!MainMenuVisible())return;
  planned=true;started=Time.unscaledTime;
  try{
   // Build 24794582's default protagonist uses the same dialogue atlas as NPCs.
   // Let native DLC/variant/fallback rules choose the path; do not invent one.
   var paths=EnumeratePaths();
   foreach(var path in paths){if(ResourceExists(path)){queue.Add(path);break;}}
   if(!queue.TryStart(out var request)){finished=true;return;}
   RM.Instance.GetSpriteAsync(request!.Path,DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Sprite>>((Action<Sprite>)(sprite=>{
    var current=queue.Complete(request);if(!current||stopped||finished)return;finished=true;
    if(sprite)info("已在主菜单准备对话立绘，沿用原版缓存与释放。");
    else warning("对话立绘准备未取得资源，保留原版按需加载。");
   })));
  }catch(Exception e){finished=true;warning("对话立绘准备已停止，保留原版按需加载："+e.Message);}
 }
 static bool MainMenuVisible(){
  if(RoamingManager.IsRoamingMode||GameManager.IsLoadingMap||!SaveManager.Instance||SaveManager.Instance.IsSavingOrLoading||!ThirdPlatformManager.IsLogined)return false;
  var stack=UiFunctionGroup.displayingUiStack;if(stack==null)return false;
  for(int i=stack.Count-1;i>=0;i--){var ui=stack[i];if(ui&&ui.gameObject.activeInHierarchy&&ui.GetComponentInParent<MainMenuUI>())return true;}
  return false;
 }
 static bool ResourceExists(string path){
  // This interop build has an invalid IList signature on Locate. Keys + native
  // Contains avoids that signature and does not load assets or copy the catalog.
  var locators=Addressables.ResourceLocators.GetEnumerator();var iterator=locators.Cast<Il2CppSystem.Collections.IEnumerator>();
  while(iterator.MoveNext())if(Il2CppSystem.Linq.Enumerable.Contains<Il2CppSystem.Object>(locators.Current.Keys,path))return true;
  return false;
 }
 static System.Collections.Generic.List<string> EnumeratePaths(){
  var capture=new Harmony("local.daxia.chestmemo.portrait-enumeration");
  try{
   capture.Patch(AccessTools.Method(typeof(RM),nameof(RM.GetSprite)),prefix:new HarmonyMethod(typeof(EnumerateNativePaths),nameof(EnumerateNativePaths.Prefix)));
   return PortraitWarmupCapture.Run(()=>GameCharacterManager.GetPortrait(100401,null,GameCharacterInstance.PortraitType.Scenario,"","",false));
  }finally{capture.UnpatchSelf();}
 }
 static class EnumerateNativePaths {
  internal static bool Prefix(string __0,ref Sprite? __result){
   if(!PortraitWarmupCapture.TryCapture(__0))return true;
   __result=null;return false;
  }
 }
}
