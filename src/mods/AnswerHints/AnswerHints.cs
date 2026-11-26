using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using WuLin;
using Selections=Il2CppSystem.Collections.Generic.List<WuLin.GenericSelection>;
namespace DaxiaMods;

[BepInPlugin(Owner,"大侠·答题提示","1.0.1")]
public sealed class AnswerHints:BasePlugin {
 internal const string Owner="local.daxia.answerhints";
 internal static AnswerHints? Instance;
 static SolveRiddlesManager? scope;
 ConfigEntry<bool> enabled=null!;
 Harmony? harmony;
 static readonly Color Gold=new(1f,.84f,.42f,1f);
 Graphic? background;
 Button? button;
 Color originalColor;
 ColorBlock originalColors,goldColors;
 Selectable.Transition originalTransition;
 bool lastEnabled;
 public override void Load(){
  enabled=Config.Bind("General","Enabled",true,"仅在隆中书院、太学馆、院试/乡试/会试、蔡攸之对诗和儒圣馆解惑中标记正确答案。");
  if(!BuildGuard.Check(Log))return;
  Instance=this;lastEnabled=enabled.Value;
  try{harmony=new Harmony(Owner);harmony.PatchAll(typeof(AnswerHints).Assembly);AddComponent<AnswerHintsWatcher>();Log.LogInfo("答题提示已加载：五类指定场景，正确选项浅金色底色，保留原版文字、选择和判分。");}
  catch(Exception e){Unload();Log.LogError("答题提示接入失败，已撤销补丁："+e);}
 }
 public override bool Unload(){harmony?.UnpatchSelf();ClearTarget();scope=null;Instance=null;return true;}
 void Apply(){if(!background||!button)return;button!.transition=Selectable.Transition.ColorTint;button.colors=goldColors;background!.color=Gold;}
 void Restore(){
  if(background&&background!.color==Gold)background.color=originalColor;
  if(button&&button!.colors==goldColors){button.transition=originalTransition;button.colors=originalColors;}
 }
 void ClearTarget(){Restore();background=null;button=null;}
 void Decorate(InteractiveUi ui,Selections selections){
  if(!enabled.Value||!scope)return;
  var config=scope!.CurrentRiddleConfig;if(config==null||config.Selections==null||selections==null)return;
  var indices=new int[selections.Count];
  for(int i=0;i<indices.Length;i++){if(selections[i]==null)return;indices[i]=selections[i].index;}
  int position=AnswerHintsPolicy.FindAnswer(config.GroupName,config.NpcID,config.RightIndex,config.Selections.Length,indices);
  var buttons=ui.createdGenricSelectionButtons;
  if(position<0||buttons==null||buttons.Count!=selections.Count||!buttons[position]||!selections[position].isInteractable)return;
  var graphic=buttons[position].targetGraphic;if(!graphic||graphic.TryCast<Image>()==null)return;
  button=buttons[position];background=graphic;originalColor=graphic.color;
  originalColors=button.colors;originalTransition=button.transition;goldColors=originalColors;
  goldColors.normalColor=Color.white;goldColors.highlightedColor=Color.white;goldColors.selectedColor=Color.white;
  goldColors.pressedColor=new Color(.9f,.9f,.9f,1f);goldColors.colorMultiplier=1f;
  // Reuse the actual native button background; do not alter text, layout,
  // option order, callbacks or controller navigation.
  Apply();
 }
 internal void Tick(){
  if(lastEnabled==enabled.Value)return;lastEnabled=enabled.Value;
  if(!lastEnabled)Restore();
  else if(background&&background!.color==originalColor)Apply();
 }
 [HarmonyPatch(typeof(SolveRiddlesManager),"ShowRiddles")]
 static class QuestionPatch {
  static void Prefix(SolveRiddlesManager __instance,out SolveRiddlesManager? __state){__state=scope;scope=__instance;}
  static Exception? Finalizer(Exception? __exception,SolveRiddlesManager? __state){scope=__state;return __exception;}
 }
 [HarmonyPatch(typeof(InteractiveUi),nameof(InteractiveUi.CreateGenricSelections))]
 static class SelectionPatch {
  static void Prefix(){Instance?.ClearTarget();}
  static void Postfix(InteractiveUi __instance,Selections __0){try{Instance?.Decorate(__instance,__0);}catch(Exception e){Instance?.ClearTarget();Instance?.Log.LogWarning("本题提示未能显示，保留原版答题："+e.Message);}}
 }
 [HarmonyPatch(typeof(SolveRiddlesManager),"Clear")]
 static class ClearPatch {static void Prefix(){Instance?.ClearTarget();}}
 [HarmonyPatch(typeof(InteractiveUi),nameof(InteractiveUi.Close))]
 static class ClosePatch {static void Prefix(){Instance?.ClearTarget();}}
}
public sealed class AnswerHintsWatcher:MonoBehaviour {
 public AnswerHintsWatcher(IntPtr p):base(p){}
 public void Update()=>AnswerHints.Instance?.Tick();
}
