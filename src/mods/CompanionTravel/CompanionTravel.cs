using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Events;
using WuLin;
using WuLin.StateMachine;
using Notes=Il2CppSystem.Collections.Generic.List<WuLin.StoryManager.SortNote>;
using Groups=Il2CppSystem.Collections.Generic.List<WuLin.StoryGroupNoteData>;
namespace DaxiaMods;

[BepInPlugin(Owner,"大侠·伙伴游历追踪","1.0.1")]
public sealed class CompanionTravel:BasePlugin {
 internal const string Owner="local.daxia.companiontravel";
 internal static CompanionTravel? Instance;
 static readonly int[] Characters={10092,10084};
 ConfigEntry<bool> enabled=null!;
 Harmony? harmony;
 SaveManager? subscribed;
 UnityAction? loadFinished,reset,accountChanged;
 SaveObjectStory? context;
 IntPtr characterContext;
 long generation;
 bool waitingForLoad,lastEnabled,refreshing;
 float nextTick,nextError;
 string? previousText;
 readonly Dictionary<IntPtr,(UIStoryNote Ui,IntPtr Data,long Generation)> panels=new();
 readonly Dictionary<string,(StoryHeadInfoNode Head,string Key,StoryHeadInfoNode.TaskLineType Type)> heads=new();
 static string Graph(int id)=>id==10092?"R10092-3":"R10084-3";
 static int Character(string? graph)=>graph=="R10092-3"?10092:graph=="R10084-3"?10084:0;
 static string Title(int id)=>id==10092?"华青青·游历":"洛千雪·游历";
 static string TitleKey(int id)=>Owner+".title."+id;
 static string TextKey(int id)=>Owner+".text."+id;
 public override void Load(){
  enabled=Config.Bind("General","Enabled",true,"在伙伴的故事中显示华青青、洛千雪剩余游历地点；手动勾选追踪，原版发放奖励。");
  if(!BuildGuard.Check(Log))return;
  Instance=this;lastEnabled=enabled.Value;
  try{harmony=new Harmony(Owner);harmony.PatchAll(typeof(CompanionTravel).Assembly);AddComponent<CompanionTravelWatcher>();BindEvents();Log.LogInfo("伙伴游历追踪已加载：原版入队历史、剧情标记与结算状态驱动显示，不新增进度文件。");}
  catch(Exception e){Unload();Log.LogError("伙伴游历追踪加载失败，已撤销补丁："+e);}
 }
 public override bool Unload(){Unsubscribe();RestoreHeads();harmony?.UnpatchSelf();panels.Clear();context=null;Instance=null;return true;}
 void Guard(Action action){try{action();}catch(Exception e){if(Time.unscaledTime>=nextError){nextError=Time.unscaledTime+5;Log.LogError("伙伴游历追踪未完成本次显示更新，原版进度不变："+e);}}}
 bool EnvironmentAvailable()=>enabled.Value&&!waitingForLoad&&GameManager.Instance&&!GameManager.IsSlgMode&&RoamingManager.IsRoamingMode&&SaveManager.Instance&&!SaveManager.Instance.IsSavingOrLoading&&StoryManager.Instance&&!StoryManager.Instance.InStoryProgress;
 void BindEvents(){
  var manager=SaveManager.Instance;if(!manager)return;
  if(subscribed&&subscribed!.Pointer==manager.Pointer)return;
  if(subscribed!=null)Invalidate(true);
  Unsubscribe();subscribed=manager;
  loadFinished=DelegateSupport.ConvertDelegate<UnityAction>((Action)(()=>Guard(()=>Invalidate(false))));
  reset=DelegateSupport.ConvertDelegate<UnityAction>((Action)(()=>Guard(()=>Invalidate(waitingForLoad))));
  accountChanged=DelegateSupport.ConvertDelegate<UnityAction>((Action)(()=>Guard(()=>Invalidate(true))));
  manager.onLoadFinished+=loadFinished;manager.onReset+=reset;manager.onAccountChange+=accountChanged;
 }
 void Unsubscribe(){if(subscribed){if(loadFinished!=null)subscribed!.onLoadFinished-=loadFinished;if(reset!=null)subscribed!.onReset-=reset;if(accountChanged!=null)subscribed!.onAccountChange-=accountChanged;}subscribed=null;}
 internal void Invalidate(bool awaitingLoad){generation++;waitingForLoad=awaitingLoad;context=null;characterContext=IntPtr.Zero;previousText=null;panels.Clear();RestoreHeads();nextTick=0;}
 bool EnsureContext(){
  if(!enabled.Value||waitingForLoad||!EnvironmentAvailable())return false;
  var data=StoryManager.Instance.dataContainer?.initedObject;var chars=GameCharacterManager.Instance?.SaveData;
  if(data==null||chars==null||!data||!chars)return false;
  if(context==null||context.Pointer!=data.Pointer||characterContext!=chars.Pointer){Invalidate(false);context=data;characterContext=chars.Pointer;}
  PrepareHeads();return true;
 }
 void PrepareHeads(){
  foreach(int id in Characters){var graph=StoryManager.Instance.GetGraph(Graph(id));if(!graph||!graph.headnode)continue;
   if(heads.ContainsKey(Graph(id)))continue;
   var head=graph.headnode;heads.Add(Graph(id),(head,head.Key,head.taskLineType));head.Key=TitleKey(id);head.taskLineType=StoryHeadInfoNode.TaskLineType.Branch;
  }
 }
 void RestoreHeads(){foreach(var saved in heads.Values)if(saved.Head){saved.Head.Key=saved.Key;saved.Head.taskLineType=saved.Type;}heads.Clear();}
 bool Finished(int id)=>context!=null&&context.GraphDataList.TryGetValue(Graph(id),out var raw)&&raw.State==SaveObjectStory.StoryState.Complated&&!context.ResultList.ContainsKey(Graph(id));
 string? Text(int id){
  if(context==null)return null;
  var actor=GameCharacterManager.Instance.GetCharacter(id);
  bool joined=actor!=null&&actor.IsValid&&actor.IsJoinedTeamOnce;
  // ResultList queues results, then SendResult applies them and removes the queue.
  // Only the post-settlement completed graph state is durable completion evidence.
  return CompanionTravelPolicy.Build(id,joined,Finished(id),tag=>context.TagString.Contains(tag));
 }
 SaveObjectStory.GraphData Projection(int id){
  var value=new SaveObjectStory.GraphData{State=SaveObjectStory.StoryState.Started,noteActive=true,note=new()};
  if(context!.GraphDataList.TryGetValue(Graph(id),out var raw)){value.OpenedTime=raw.OpenedTime;value.LastPlayTime=raw.LastPlayTime;}
  // A detached UI snapshot; never insert it in GraphDataList or register a result.
  value.note.Add(TextKey(id));return value;
 }
 internal void AddJournal(Groups result){
  if(!EnsureContext())return;
  foreach(var group in result)for(int i=group.graphDataList.Count-1;i>=0;i--)if(Character(group.graphDataList[i].configName)!=0)group.graphDataList.RemoveAt(i);
  foreach(int id in Characters){
   if(Text(id)==null)continue;
   int table=StoryManager.Instance.FindStoryTableIndex(Graph(id));if(table<0)throw new InvalidOperationException("原版伙伴故事分类不存在。");
   StoryGroupNoteData? group=null;foreach(var item in result)if(item.TableIndex==table){group=item;break;}
   if(group==null){group=new(){TableIndex=table,TextName=StoryManager.Instance.StoryGroupConfigList[table].TextName,graphDataList=new()};result.Add(group);}
   group.graphDataList.Add(new GraphUIData(Projection(id)){configName=Graph(id),storyName=TitleKey(id),check=StoryManager.Instance.IsCheckNote(Graph(id))});
  }
 }
 // This interop maps the native SortNote struct to a managed wrapper. List.Add
 // requires the unboxed native value, not the wrapper's object header.
 [DllImport("GameAssembly.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="il2cpp_runtime_invoke")]
 static extern IntPtr InvokeNative(IntPtr method,IntPtr target,IntPtr args,out IntPtr exception);
 static readonly System.Reflection.FieldInfo AddMethod=AccessTools.Field(typeof(Notes),"NativeMethodInfoPtr_Add_Public_Virtual_Final_New_Void_T_0");
 static void AddNote(Notes list,StoryManager.SortNote note){
  var args=Marshal.AllocHGlobal(IntPtr.Size);
  try{Marshal.WriteIntPtr(args,IL2CPP.il2cpp_object_unbox(note.Pointer));InvokeNative((IntPtr)AddMethod.GetValue(null)!,list.Pointer,args,out var error);Il2CppException.RaiseExceptionIfNecessary(error);}
  finally{Marshal.FreeHGlobal(args);}
 }
 internal Notes AddTracking(Notes original){
  if(!EnsureContext())return original;
  var values=new List<StoryManager.SortNote>();foreach(var item in original)values.Add(item);
  bool changed=false;
  foreach(int id in Characters){if(!StoryManager.Instance.IsCheckNote(Graph(id)))continue;var text=Text(id);if(text==null)continue;
   // Native travel graphs have no note text, so the native query omits them.
   var projected=Projection(id);var note=new StoryManager.SortNote{title=Title(id),text=text,opentime=projected.OpenedTime,taskLineType=1,limitTime=0,showLimitTime=false};
   int at=values.FindIndex(n=>n.taskLineType!=0&&n.opentime>note.opentime);if(at<0)values.Add(note);else values.Insert(at,note);changed=true;
  }
  if(!changed)return original;
  var result=new Notes();foreach(var note in values)AddNote(result,note);return result;
 }
 internal void PanelOpened(UIStoryNote ui){if(EnsureContext())panels[ui.Pointer]=(ui,context!.Pointer,generation);}
 bool Bound(UIStoryNote ui)=>EnsureContext()&&panels.TryGetValue(ui.Pointer,out var bound)&&bound.Data==context!.Pointer&&bound.Generation==generation;
 bool AllowClick(UIStoryNote ui,GameObject item){
  if(!item)return true;var entry=item.GetComponent<UIStorySubTitle>();if(!entry||!entry.config)return true;
  int id=Character(entry.config.name);return id==0||enabled.Value&&Bound(ui)&&Text(id)!=null;
 }
 void Right(UIStoryNote ui){
  if(!enabled.Value||!Bound(ui)||!ui.mSelectedSubTitle||!ui.mSelectedSubTitle.config)return;
  int id=Character(ui.mSelectedSubTitle.config.name);if(id==0)return;var text=Text(id);if(text==null)return;
  ui.rightTitle.text=Title(id);ui.rightText.text=text;ui.rightOldText.text=string.Empty;ui.rightLine.SetActive(false);
 }
 internal void RefreshViews(){
  if(refreshing||!EnsureContext())return;
  refreshing=true;
  try{
   foreach(int id in Characters)if(Finished(id)&&context!.NoteCheckName.Contains(Graph(id)))StoryManager.Instance.RemoveCheckNote(Graph(id));
   StoryManager.Instance.UpdateRomingMainStoryText();
   foreach(var bound in panels.Values){var ui=bound.Ui;if(!ui||!ui.gameObject.activeInHierarchy||bound.Data!=context!.Pointer||bound.Generation!=generation)continue;
    string? selected=ui.mSelectedSubTitle&&ui.mSelectedSubTitle.config?ui.mSelectedSubTitle.config.name:null;
    var expanded=new Dictionary<int,bool>();foreach(var group in ui.StoryMainTitleList)expanded[group.Key]=group.Value.GetComponent<UIStoryMainTitle>().IsOn();
    ui.UpdateContent();ui.initMainTitleOn();
    foreach(var group in ui.StoryMainTitleList)if(expanded.TryGetValue(group.Key,out var open))group.Value.GetComponent<UIStoryMainTitle>().SetOn(open);
    ui.OnClickStoryType(ui.mCurPageIndex);
    // Destroy is deferred until the end of the frame. Search the new native
    // lists, not the transform hierarchy that still contains retired rows.
    if(selected!=null)foreach(var group in ui.StoryMainTitleList.Values)foreach(var child in group.GetComponent<UIStoryMainTitle>().SubTitleList){var entry=child.GetComponent<UIStorySubTitle>();if(entry.config&&entry.config.name==selected){ui.OnClickSubTitle(child);break;}}
    ui.UpdateRight();
   }
  }finally{refreshing=false;}
 }
 internal void Tick()=>Guard(()=>{
  BindEvents();
  if(lastEnabled!=enabled.Value){lastEnabled=enabled.Value;RestoreHeads();previousText=null;
   if(!lastEnabled){foreach(var bound in panels.Values)if(bound.Ui&&bound.Ui.gameObject.activeInHierarchy){bound.Ui.UpdateContent();bound.Ui.initMainTitleOn();bound.Ui.OnClickStoryType(bound.Ui.mCurPageIndex);}if(StoryManager.Instance&&RoamingManager.IsRoamingMode)StoryManager.Instance.UpdateRomingMainStoryText();return;}
  }
  if(Time.unscaledTime<nextTick||!EnsureContext())return;nextTick=Time.unscaledTime+0.5f;
  var text=(Text(10092)??"<hidden>")+"\0"+(Text(10084)??"<hidden>");if(text==previousText)return;previousText=text;RefreshViews();
 });
 [HarmonyPatch(typeof(StoryManager),nameof(StoryManager.GetNoteList))]
 static class Journal {static void Postfix(Groups __result)=>Instance?.Guard(()=>Instance.AddJournal(__result));}
 [HarmonyPatch(typeof(StoryManager),nameof(StoryManager.AddCheckNote))]
 static class InitializeTrackedGraph {
  static bool Prefix(StoryManager __instance,string __0){
   if(Instance?.enabled.Value!=true||Character(__0)==0)return true;
   // A never-triggered graph may have no save entry. The native tracking query
   // dereferences its note list. Initialize only the native default NotOpen
   // record when the player explicitly tracks it; never persist the UI snapshot.
   bool ready=false;Instance.Guard(()=>ready=__instance.dataContainer?.initedObject?.GetOrCreateGraphData(__0)!=null);return ready;
  }
 }
 [HarmonyPatch(typeof(StoryManager),nameof(StoryManager.GetNoteListByTimeSort))]
 static class Tracked {static void Postfix(ref Notes __result){var plugin=Instance;if(plugin==null)return;var original=__result;Notes result=original;plugin.Guard(()=>result=plugin.AddTracking(original));__result=result;}}
 [HarmonyPatch(typeof(GameLoc),nameof(GameLoc.GetLocString))]
 static class LocalText {
  static bool Prefix(string __0,ref string __result){
   var plugin=Instance;if(plugin?.enabled.Value!=true||__0==null||!__0.StartsWith(Owner+".",StringComparison.Ordinal))return true;
   foreach(int id in Characters){if(__0==TitleKey(id)){__result=Title(id);return false;}if(__0==TextKey(id)){string text=string.Empty;plugin.Guard(()=>{if(plugin.EnsureContext())text=plugin.Text(id)??string.Empty;});__result=text;return false;}}
   return true;
  }
 }
 [HarmonyPatch(typeof(StoryManager),nameof(StoryManager.GetLastNoteTipKey))]
 static class DetailKey {
  static bool Prefix(string __0,ref string __result){
   var plugin=Instance;int id=Character(__0);if(plugin?.enabled.Value!=true||id==0)return true;
   bool use=false;plugin.Guard(()=>use=plugin.EnsureContext()&&plugin.Text(id)!=null);if(!use)return true;
   __result=TextKey(id);return false;
  }
 }
 [HarmonyPatch(typeof(UIStoryNote),"OnEnable")]
 static class Open {static void Prefix(UIStoryNote __instance)=>Instance?.Guard(()=>Instance.PanelOpened(__instance));}
 [HarmonyPatch(typeof(UIStoryNote),"OnDisable")]
 static class Close {static void Postfix(UIStoryNote __instance)=>Instance?.panels.Remove(__instance.Pointer);}
 [HarmonyPatch(typeof(UIStoryNote),"OnClickCheck")]
 static class Check {
  [HarmonyPriority(Priority.First),HarmonyBefore("local.daxia.questtracking")]
  static bool Prefix(UIStoryNote __instance,GameObject __0){bool allow=false;if(Instance==null)return true;Instance.Guard(()=>allow=Instance.AllowClick(__instance,__0));return allow;}
 }
 [HarmonyPatch(typeof(UIStoryNote),"UpdateRight")]
 static class Detail {static void Postfix(UIStoryNote __instance)=>Instance?.Guard(()=>Instance.Right(__instance));}
 [HarmonyPatch(typeof(SaveManager),nameof(SaveManager.Load),new Type[]{})]
 static class LoadSave {static void Prefix()=>Instance?.Guard(()=>Instance.Invalidate(true));}
 [HarmonyPatch(typeof(GameManager),nameof(GameManager.StartNewGame),new Type[]{})]
 static class NewGame {static void Prefix()=>Instance?.Guard(()=>Instance.Invalidate(false));}
 [HarmonyPatch(typeof(GameManager),nameof(GameManager.QuitToMenu),new Type[]{})]
 static class Quit {static void Prefix()=>Instance?.Guard(()=>Instance.Invalidate(true));}
}
public sealed class CompanionTravelWatcher:MonoBehaviour {
 public CompanionTravelWatcher(IntPtr p):base(p){}
 public void Update()=>CompanionTravel.Instance?.Tick();
}
