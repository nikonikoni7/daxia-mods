using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using WuLin;
namespace DaxiaMods;
[BepInPlugin("local.daxia.fastfinish","大侠·快速结算","1.0.0")]
public sealed class FastFinish : BasePlugin
{
 internal static FastFinish Instance=null!;
 private ConfigEntry<bool> enabled=null!;
 private ConfigEntry<float> duration=null!;
 private Harmony? harmony;
 private static readonly Dictionary<IntPtr,float> slowStarts=new();
 private static readonly Dictionary<IntPtr,float> bannerStarts=new();
 private static readonly Dictionary<IntPtr,(Animator Animator,float Speed)> animatorSpeeds=new();
 private bool failed;
 internal float Duration=>Rules.Duration(duration.Value);
 internal bool Enabled=>!failed && enabled.Value;
 public override void Load() {
  Instance=this;
  enabled=Config.Bind("General","Enabled",true,"启用快速结算。");
  duration=Config.Bind("General","DisplaySeconds",0.2f,"最后一击和结果提示的展示秒数，范围0.05～2秒。");
  if(!BuildGuard.Check(Log))return;
  try {
   harmony=new Harmony("local.daxia.fastfinish");harmony.PatchAll(typeof(FastFinish).Assembly);
   Log.LogInfo("快速结算已加载：保留原版结算，每段提示约"+Duration+"秒。");
  }catch(Exception e){harmony?.UnpatchSelf();Log.LogError("接口接入失败，快速结算已停用："+e);}
 }
 public override bool Unload() {harmony?.UnpatchSelf();Restore();return true;}
 private static void Restore() {
  foreach(var item in animatorSpeeds.Values)if(item.Animator)item.Animator.speed=item.Speed;
  animatorSpeeds.Clear();slowStarts.Clear();bannerStarts.Clear();
 }
 private static void Safe(Action action) {
  if(Instance.failed)return;
  try{action();}catch(Exception e){
   Instance.failed=true;Instance.harmony?.UnpatchSelf();
   try{Restore();}catch(Exception cleanup){Instance.Log.LogError(cleanup);}
   Instance.Log.LogError("运行接口异常，快速结算已停用，恢复原版流程："+e);
  }
 }

 [HarmonyPatch(typeof(BattleUI._FinishAttackPerform_d__120),"MoveNext")]
 private static class LastHitPatch {
  private static void Prefix(BattleUI._FinishAttackPerform_d__120 __instance)=>Safe(()=> {
   if(__instance.__1__state!=0 || !Instance.Enabled)return;
   var anim=__instance.__4__this.finishAttackAnimation;
   if(anim) {animatorSpeeds[__instance.Pointer]=(anim,anim.speed);anim.speed=1f/Instance.Duration;}
  });
  private static void Postfix(BattleUI._FinishAttackPerform_d__120 __instance,bool __result)=>Safe(()=> {
   if(__result && Instance.Enabled) {
    var wait=__instance.__2__current?.TryCast<WaitForSecondsRealtime>();
    if(wait!=null)wait.waitTime=Instance.Duration;
   }
   if(!__result && animatorSpeeds.Remove(__instance.Pointer,out var speed)) {
    if(speed.Animator)speed.Animator.speed=speed.Speed;
   }
  });
 }
 [HarmonyPatch(typeof(BattleView._BattleResultPerform_d__64),"MoveNext")]
 private static class SlowMotionPatch {
  private static void Prefix(BattleView._BattleResultPerform_d__64 __instance)=>Safe(()=> {
   if(Instance.Enabled && __instance.__1__state==2 && slowStarts.TryGetValue(__instance.Pointer,out var start))
    __instance._battleResultTimer_5__2=Rules.CompressTimer(Time.realtimeSinceStartup-start,Instance.Duration,__instance.__4__this.battleFinishTimeScaleCurve.length);
  });
  private static void Postfix(BattleView._BattleResultPerform_d__64 __instance,bool __result)=>Safe(()=> {
   if(__result && __instance.__1__state==2)slowStarts.TryAdd(__instance.Pointer,Time.realtimeSinceStartup);
   if(!__result)slowStarts.Remove(__instance.Pointer);
  });
 }
 [HarmonyPatch(typeof(BattleUI._BattleResultPerform_d__121),"MoveNext")]
 private static class BannerPatch {
  private static void Prefix(BattleUI._BattleResultPerform_d__121 __instance)=>Safe(()=> {
   if(!bannerStarts.TryGetValue(__instance.Pointer,out var start))return;
   var ui=__instance.__4__this;
   if(Rules.ShouldCompleteBanner(Instance.Enabled,__instance.battleFinishResult==BattleFinishResult.Win,
      __instance.__1__state==1,ui.m_bannerDisplayFinished,Time.realtimeSinceStartup-start,Instance.Duration)) {
    // 原版点击只触发收尾动画；复用动画结束回调释放等待，不重复执行退场或奖励。
    ui.OnBannerClicked(true);
    if(!ui.cachedBanner)throw new InvalidOperationException("胜利提示对象缺失。");
    ui.cachedBanner.OnBannerPlayFinished();
   }
  });
  private static void Postfix(BattleUI._BattleResultPerform_d__121 __instance,bool __result)=>Safe(()=> {
   if(__result && __instance.__1__state==1)bannerStarts.TryAdd(__instance.Pointer,Time.realtimeSinceStartup);
   if(!__result)bannerStarts.Remove(__instance.Pointer);
  });
 }
}
