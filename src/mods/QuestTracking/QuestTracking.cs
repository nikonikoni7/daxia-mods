using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using GameData;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WuLin;
using Object=UnityEngine.Object;
namespace DaxiaMods;

[BepInPlugin(Owner,"大侠·五任务追踪","1.0.0")]
public sealed class QuestTracking:BasePlugin {
 internal const string Owner="local.daxia.questtracking";
 const string LimitMessage="最多同时追踪5个任务";
 internal static QuestTracking? Instance;
 ConfigEntry<bool> enabled=null!;
 Harmony? harmony;
 bool lastEnabled;
 RoamingUI? panel;
 Il2CppReferenceArray<Image>? backgrounds;
 Il2CppReferenceArray<TMP_Text>? titles,contents,times;
 readonly List<GameObject> clones=new();
 readonly Dictionary<IntPtr,(UIStoryNote Ui,string Text)> tips=new();
 public override void Load(){
  enabled=Config.Bind("General","Enabled",true,"剧情任务与悬赏任务合计最多追踪5个，沿用原版任务栏。");
  if(!BuildGuard.Check(Log))return;
  Instance=this;lastEnabled=enabled.Value;
  try{harmony=new Harmony(Owner);harmony.PatchAll(typeof(QuestTracking).Assembly);AddComponent<QuestTrackingWatcher>();Log.LogInfo("五任务追踪已加载：剧情与悬赏共用5个名额，保留原版保存流程。");}
  catch(Exception e){Unload();Log.LogError("五任务追踪接入失败，已撤销补丁："+e);}
 }
 public override bool Unload(){harmony?.UnpatchSelf();RestoreRows();RestoreTips();Instance=null;return true;}
 static T Corresponding<T>(T original,Transform source,Transform clone) where T:Component {
  var route=new List<int>();var current=original.transform;
  while(current.Pointer!=source.Pointer){if(!current.parent)throw new InvalidOperationException("原版任务条目引用不在同一容器内。");route.Add(current.GetSiblingIndex());current=current.parent;}
  for(int i=route.Count-1;i>=0;i--)clone=clone.GetChild(route[i]);
  var result=clone.GetComponent<T>();if(!result)throw new InvalidOperationException("复制的任务条目缺少组件："+typeof(T).Name);return result;
 }
 internal bool EnsureRows(RoamingUI ui){
  if(!enabled.Value)return false;
  if(panel&&panel!.Pointer==ui.Pointer)return true;
  RestoreRows();
  var created=new List<GameObject>();
  try{
   var bg=ui.StoryImgBg;var title=ui.StoryTitle;var content=ui.StoryContent;var time=ui.StoryTimeLimit;
   if(bg==null||title==null||content==null||time==null||bg.Length!=3||title.Length!=3||content.Length!=3||time.Length!=3)throw new InvalidOperationException("原版任务栏引用数量不符合适配版本。");
   var nextBg=new Il2CppReferenceArray<Image>(5);var nextTitle=new Il2CppReferenceArray<TMP_Text>(5);var nextContent=new Il2CppReferenceArray<TMP_Text>(5);var nextTime=new Il2CppReferenceArray<TMP_Text>(5);
   for(int i=0;i<3;i++){if(!bg[i]||!title[i]||!content[i]||!time[i])throw new InvalidOperationException("原版任务栏存在空引用。");nextBg[i]=bg[i];nextTitle[i]=title[i];nextContent[i]=content[i];nextTime[i]=time[i];}
   var source=bg[2].transform.parent;
   for(int i=3;i<5;i++){
    var clone=Object.Instantiate(source.gameObject,source.parent,false);created.Add(clone);clone.name="QuestTracking.Task"+(i+1);clone.SetActive(false);
    clone.transform.SetSiblingIndex(source.GetSiblingIndex()+i-2);
    nextBg[i]=Corresponding(bg[2],source,clone.transform);nextTitle[i]=Corresponding(title[2],source,clone.transform);
    nextContent[i]=Corresponding(content[2],source,clone.transform);nextTime[i]=Corresponding(time[2],source,clone.transform);
   }
   // Publish all four arrays together only after both full rows are ready.
   panel=ui;backgrounds=bg;titles=title;contents=content;times=time;
   ui.StoryImgBg=nextBg;ui.StoryTitle=nextTitle;ui.StoryContent=nextContent;ui.StoryTimeLimit=nextTime;clones.AddRange(created);return true;
  }catch(Exception e){foreach(var clone in created)if(clone){clone.SetActive(false);Object.Destroy(clone);}Log.LogError("五任务追踪界面初始化失败，保留原版任务栏："+e);return false;}
 }
 internal void RestoreRows(){
  if(panel){panel!.StoryImgBg=backgrounds;panel.StoryTitle=titles;panel.StoryContent=contents;panel.StoryTimeLimit=times;}
  foreach(var clone in clones)if(clone){clone.SetActive(false);Object.Destroy(clone);}
  clones.Clear();panel=null;backgrounds=null;titles=null;contents=null;times=null;
 }
 internal bool Refresh(StoryManager manager,RoamingUI ui){
  if(!EnsureRows(ui))return false;
  // Preserve the native concatenation: sorted story notes, then sorted bounty notes.
  var notes=new List<StoryManager.SortNote>();
  foreach(var note in manager.GetNoteListByTimeSort())notes.Add(note);
  foreach(var note in BountyHunterManager.Instance.GetNoteListByTimeSort())notes.Add(note);
  QuestTrackingPolicy.Render(notes,(i,n)=>ui.SetStoryText(i,n.title,n.text,n.taskLineType,n.limitTime,n.showLimitTime),i=>ui.SetStoryText(i,string.Empty,string.Empty,0));
  return true;
 }
 void SetTip(UIStoryNote ui){if(!ui.txtTip)return;if(!tips.ContainsKey(ui.Pointer))tips.Add(ui.Pointer,(ui,ui.txtTip.text));ui.txtTip.text=LimitMessage;}
 void RestoreTips(){foreach(var saved in tips.Values)if(saved.Ui&&saved.Ui.txtTip)saved.Ui.txtTip.text=saved.Text;tips.Clear();}
 internal void Tick(){
  if(lastEnabled==enabled.Value)return;lastEnabled=enabled.Value;
  if(!lastEnabled){RestoreRows();RestoreTips();}
  if(StoryManager.Instance&&RoamingManager.IsRoamingMode)StoryManager.Instance.UpdateRomingMainStoryText();
 }
 // Return true only when the unmodified native handler should process the click.
 internal bool OnCheck(UIStoryNote ui,GameObject item){
  if(!enabled.Value||!item)return true;
  var entry=item.GetComponent<UIStorySubTitle>();if(!entry||entry.IsCheck())return true;
  var manager=StoryManager.Instance;var bounty=BountyHunterManager.Instance;
  if(!manager||bounty==null||manager.dataContainer?.initedObject==null||bounty.dataContainer?.initedObject==null)return true;
  int storyCount=manager.GetCheckNoteCount(),bountyCount=bounty.GetCheckNoteCount();
  if(storyCount+bountyCount<3)return true;
  SetTip(ui);
  if(storyCount+bountyCount>=5){LogDisplayer.Instance.ShowCenterLog(LimitMessage);return false;}
  // Extra choices are enabled only after the corresponding display rows exist.
  if(!EnsureRows(RoamingUI.Instance))return false;
  string? graph=null;
  if(entry.config)graph=entry.config.name;
  else{
   var config=BaseDataClass.GetGameData<BountyHunterBaseTaskScriptObject>()?.GetData(entry.taskID);
   if(config==null)return false;
   if(!string.IsNullOrWhiteSpace(config.ActiveStoryID))graph=config.ActiveStoryID;
  }
  bool already=graph!=null?manager.IsCheckNote(graph):bounty.IsCheckNote(entry.factionID,entry.taskID);
  if(already){entry.SetCheckState(2);return false;}
  QuestTrackingPolicy.TryAdd(storyCount,bountyCount,()=>{
   if(graph!=null)manager.AddCheckNote(graph);else bounty.AddCheckNote(entry.factionID,entry.taskID);
   entry.SetCheckState(2);manager.UpdateRomingMainStoryText();
  },()=>SetTip(ui));return false;
 }
 [HarmonyPatch(typeof(UIStoryNote),"OnClickCheck")]
 static class CheckPatch {
  static bool Prefix(UIStoryNote __instance,GameObject __0){try{return Instance?.OnCheck(__instance,__0)??true;}catch(Exception e){Instance?.Log.LogError("追踪操作失败，未继续执行原版重复添加："+e);return false;}}
 }
 [HarmonyPatch(typeof(UIStoryNote),"OnEnable")]
 static class NoteOpenPatch {static void Postfix(UIStoryNote __instance){if(Instance?.enabled.Value==true)Instance.SetTip(__instance);}}
 [HarmonyPatch(typeof(StoryManager),nameof(StoryManager.IsCheckNote))]
 static class StoryCheckPatch {
  static bool Prefix(StoryManager __instance,string __0,ref bool __result){
   if(Instance?.enabled.Value!=true)return true;
   var list=__instance.dataContainer?.initedObject?.NoteCheckName;if(list==null)return true;
   int index=list.IndexOf(__0);__result=index>=0&&index<QuestTrackingPolicy.Limit;return false;
  }
 }
 [HarmonyPatch(typeof(BountyHunterManager),nameof(BountyHunterManager.IsCheckNote))]
 static class BountyCheckPatch {
  static bool Prefix(BountyHunterManager __instance,int __0,int __1,ref bool __result){
   if(Instance?.enabled.Value!=true)return true;
   var list=__instance.dataContainer?.initedObject?.NoteCheckTask;if(list==null)return true;
   int index=list.IndexOf(new Vector2Int(__0,__1));__result=index>=0&&index<QuestTrackingPolicy.Limit;return false;
  }
 }
 [HarmonyPatch(typeof(StoryManager),nameof(StoryManager.UpdateRomingMainStoryText))]
 static class RefreshPatch {
  static bool Prefix(StoryManager __instance){
   if(Instance?.enabled.Value!=true)return true;
   try{return !Instance.Refresh(__instance,RoamingUI.Instance);}
   catch(Exception e){Instance.RestoreRows();Instance.Log.LogError("五任务追踪刷新失败，恢复原版显示："+e);return true;}
  }
 }
}
public sealed class QuestTrackingWatcher:MonoBehaviour {
 public QuestTrackingWatcher(IntPtr p):base(p){}
 public void Update()=>QuestTracking.Instance?.Tick();
}
