using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Events;
using WuLin;

namespace DaxiaMods;
[BepInPlugin("local.daxia.companiontraining","大侠·队友修炼记忆","1.0.1")]
public sealed class CompanionTraining : BasePlugin {
 const string Owner="local.daxia.companiontraining";
 internal static CompanionTraining? Instance;
 ConfigEntry<bool> enabled=null!;
 Harmony? harmony;
 TrainingBook book=new();
 TrainingStore store=null!;
 SaveManager? subscribed;
 UnityAction? saveReady,loadFinished,reset,accountChanged;
 bool ready,awaitingLoad,storageBlocked,primarySynced;
 long generation;
 TrainingBook? pendingSnapshot;
 string? pendingPath;
 IntPtr pendingPrimary,pendingBackup;
 internal sealed class Transition {
  internal int CharacterId,TemplateId,BookId;
  internal bool WasMember;
  internal long Generation;
 }
 public override void Load(){
  enabled=Config.Bind("General","Enabled",true,"归队时恢复离队前选择的空闲秘籍；仅保存成功的游戏进度会跨重启保留。");
  if(!BuildGuard.Check(Log))return;
  store=new TrainingStore(Path.Combine(Paths.ConfigPath,"CompanionTraining"));
  Instance=this;
  try{harmony=new Harmony(Owner);harmony.PatchAll(typeof(CompanionTraining).Assembly);AddComponent<TrainingWatcher>();BindSaveEvents();Log.LogInfo("队友修炼记忆已加载：离队暂停并释放秘籍，归队时按原版条件恢复。");}
  catch(Exception e){Log.LogError("队友修炼记忆加载失败："+e);Unload();}
 }
 public override bool Unload(){Unsubscribe();harmony?.UnpatchSelf();ClearPending();ready=false;Instance=null;return true;}
 void Guard(Action action){try{action();}catch(Exception e){Log.LogError("队友修炼记忆："+e);}}
 internal void Tick()=>Guard(()=>{
  BindSaveEvents();
  if(!ready && !awaitingLoad && RoamingManager.IsRoamingMode && SaveManager.Instance && !SaveManager.Instance.IsSavingOrLoading)Restore();
 });
 void BindSaveEvents(){
  var manager=SaveManager.Instance;if(!manager || (subscribed && subscribed!.Pointer==manager.Pointer))return;
  // A replacement manager is a different runtime context, even with the same slot name.
  if(subscribed)BeforeLoad();Unsubscribe();subscribed=manager;
  saveReady=DelegateSupport.ConvertDelegate<UnityAction>((Action)(()=>Guard(SavedDataReady)));
  loadFinished=DelegateSupport.ConvertDelegate<UnityAction>((Action)(()=>Guard(Restore)));
  reset=DelegateSupport.ConvertDelegate<UnityAction>((Action)(()=>Guard(NewGame)));
  accountChanged=DelegateSupport.ConvertDelegate<UnityAction>((Action)(()=>Guard(BeforeLoad)));
  manager.onSaveFinished+=saveReady;manager.onLoadFinished+=loadFinished;manager.onReset+=reset;manager.onAccountChange+=accountChanged;
 }
 void Unsubscribe(){
  if(subscribed){
   if(saveReady!=null)subscribed!.onSaveFinished-=saveReady;
   if(loadFinished!=null)subscribed!.onLoadFinished-=loadFinished;
   if(reset!=null)subscribed!.onReset-=reset;
   if(accountChanged!=null)subscribed!.onAccountChange-=accountChanged;
  }
  subscribed=null;
 }
 static string NativeSlotPath()=>TrainingStore.ResolveSlotPath(SaveManager.SavePath,SaveManager.FullPathForAllSaveFolder);
 void ClearPending(){pendingSnapshot=null;pendingPath=null;pendingPrimary=IntPtr.Zero;pendingBackup=IntPtr.Zero;primarySynced=false;}
 void BeforeLoad(){generation++;ready=false;awaitingLoad=true;storageBlocked=false;book=new();ClearPending();}
 void NewGame(){generation++;ready=true;awaitingLoad=false;storageBlocked=false;book=new();ClearPending();}
 void Restore(){
  generation++;ready=false;ClearPending();book=new();storageBlocked=false;
  try{
   var path=NativeSlotPath();
   if(Directory.Exists(path) || File.Exists(path))book=store.Load(path);
  }catch(Exception e){storageBlocked=true;Log.LogError("修炼记录无法读取，已保留文件并停止自动恢复及写入："+e.Message);}
  ready=true;awaitingLoad=false;
 }
 void SavedDataReady(){
  ClearPending();var manager=SaveManager.Instance;
  if(!ready || storageBlocked || !enabled.Value || !manager || !manager.IsSaving || GameManager.IsSlgMode || manager.m_saveFile==null || manager.m_saveFileBackup==null)return;
  // In this build onSaveFinished precedes both file Sync calls. Freeze the book,
  // but do not persist yet: IsSaving also becomes false before the Sync calls.
  pendingSnapshot=book.Clone();pendingPath=NativeSlotPath();pendingPrimary=manager.m_saveFile.Pointer;pendingBackup=manager.m_saveFileBackup.Pointer;
 }
 void Synced(ES3File file){
  if(pendingSnapshot==null || pendingPath==null || !ready || storageBlocked)return;
  if(file.Pointer==pendingPrimary){primarySynced=true;return;}
  if(file.Pointer!=pendingBackup || !primarySynced)return;
  var snapshot=pendingSnapshot;var path=pendingPath;ClearPending();
  try{store.Save(path,snapshot);Log.LogInfo("已随存档保存队友修炼记录："+snapshot.Records.Count+"条。");}
  catch(Exception e){storageBlocked=true;Log.LogError("修炼记录保存失败，原版保存流程继续；已停止自动恢复及写入："+e.Message);}
 }
 bool Available()=>enabled.Value && ready && !storageBlocked && GameManager.Instance && !GameManager.IsSlgMode && SaveManager.Instance && !SaveManager.Instance.IsSavingOrLoading;
 Transition? Capture(PlayerTeamManager team,GameCharacterInstance actor){
  if(!Available() || actor==null || !actor.IsValid || !actor.IsAlive || actor.InstanceId==team.PlayerDataInstance.InstanceId)return null;
  var selected=actor.GetEquipingItem(ItemType.KungfuBook);
  return new(){CharacterId=actor.InstanceId,TemplateId=actor.TempleteUid,BookId=selected?.TempleteId??0,WasMember=team.HasCharacterAsMember(actor,false),Generation=generation};
 }
 bool Valid(Transition? state,GameCharacterInstance actor)=>state!=null && state.Generation==generation && Available() && state.CharacterId==actor.InstanceId && state.TemplateId==actor.TempleteUid;
 void Departed(PlayerTeamManager team,GameCharacterInstance actor,bool success,Transition? state){
  if(!Valid(state,actor))return;
  book.RememberLeave(success,state!.WasMember,team.HasCharacterAsMember(actor,false),state.CharacterId,state.TemplateId,state.BookId);
 }
 void Returned(PlayerTeamManager team,GameCharacterInstance actor,bool success,Transition? state){
  if(!Valid(state,actor))return;
  var record=book.TakeOnReturn(success,state!.WasMember,team.HasCharacterAsMember(actor,false),state.CharacterId,state.TemplateId);
  // Consume before any native call; failure/exception must never schedule a later retry.
  if(record==null || actor.GetEquipingItem(ItemType.KungfuBook)!=null)return;
  var item=team.TeamInventory.GetItem(record.BookId);
  if(item==null || !item.IsValid || item.Stack<=0 || !item.IsType(ItemType.KungfuBook) || item.cachedEquipingCharacter!=null)return;
  for(int i=0;i<team.TeamSize;i++){
   var member=team.GetTeamMemberByIndex(i);var selected=member.GetEquipingItem(ItemType.KungfuBook);
   if(selected!=null && selected.TempleteId==record.BookId)return;
  }
  if(actor.CouldEquiping(item)!=GameCharacterInstance.EquipingCheckResult.Ok)return;
  if(!actor.EquipItem(item,true,true,team.TeamInventory))Log.LogWarning("队友归队后原版接口未接受秘籍，保持未修炼："+state.CharacterId);
 }
 [HarmonyPatch(typeof(PlayerTeamManager),nameof(PlayerTeamManager.RemoveCharacter))]
 static class Leave {
  static void Prefix(PlayerTeamManager __instance,GameCharacterInstance gameCharacterInstance,out Transition? __state){Transition? s=null;Instance?.Guard(()=>s=Instance.Capture(__instance,gameCharacterInstance));__state=s;}
  static void Postfix(PlayerTeamManager __instance,GameCharacterInstance gameCharacterInstance,bool __result,Transition? __state)=>Instance?.Guard(()=>Instance.Departed(__instance,gameCharacterInstance,__result,__state));
 }
 [HarmonyPatch(typeof(PlayerTeamManager),nameof(PlayerTeamManager.AddCharacter))]
 static class Join {
  static void Prefix(PlayerTeamManager __instance,GameCharacterInstance gameCharacterInstance,out Transition? __state){Transition? s=null;Instance?.Guard(()=>s=Instance.Capture(__instance,gameCharacterInstance));__state=s;}
  static void Postfix(PlayerTeamManager __instance,GameCharacterInstance gameCharacterInstance,bool __result,Transition? __state)=>Instance?.Guard(()=>Instance.Returned(__instance,gameCharacterInstance,__result,__state));
 }
 [HarmonyPatch(typeof(SaveManager),nameof(SaveManager.Load),new Type[]{})]
 static class LoadSave {static void Prefix()=>Instance?.Guard(()=>Instance.BeforeLoad());}
 [HarmonyPatch(typeof(ES3File),nameof(ES3File.Sync),new Type[]{})]
 static class Saved {static void Postfix(ES3File __instance)=>Instance?.Guard(()=>Instance.Synced(__instance));}
 public sealed class TrainingWatcher : MonoBehaviour {
  public TrainingWatcher(IntPtr pointer):base(pointer){}
  void Update()=>Instance?.Tick();
 }
}
