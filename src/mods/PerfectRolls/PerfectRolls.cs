using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using GameData;
using WuLin;
namespace DaxiaMods;
[BepInPlugin("local.daxia.perfectrolls","大侠·装备极品数值","1.1.1")]
public sealed class PerfectRolls:BasePlugin {
 internal static PerfectRolls? Instance;
 ConfigEntry<bool> enabled=null!;
 Harmony? harmony;
 int depth;
 long generation;
 Admission? activeAdmission;
 readonly List<NoticeSource> sources=new();
 readonly List<PendingNotice> pending=new();
 readonly HashSet<string> warnings=new(StringComparer.Ordinal);
 internal sealed class Admission {
  internal IntPtr Bag,Team,Save;
  internal bool Root,Released,Completed,Succeeded;
  internal long Generation;
  internal HashSet<IntPtr> Before=null!;
 }
 internal sealed class NoticeSource {
  internal IntPtr Bag,Team,Save;
  internal long Generation;
  internal readonly List<GameItemInstance> Items=new();
  internal Admission? Transfer;
 }
 sealed class PendingNotice {internal NoticeSource Source=null!;internal GameItemInstance Item=null!;internal Admission? Confirmation;}
 public override void Load(){
  enabled=Config.Bind("General","Enabled",true,"获得装备时将已有浮动词条取完整区间最优值，包括锻造、竞宝大会与珍宝阁取回；不批量修改旧装备。");
  if(!BuildGuard.Check(Log))return;
  Instance=this;
  try{harmony=new Harmony("local.daxia.perfectrolls");harmony.PatchAll(typeof(PerfectRolls).Assembly);Log.LogInfo("装备极品数值1.1.1已加载：获得装备取完整区间最优值，包括锻造、竞宝大会与珍宝阁取回。");}
  catch(Exception e){Log.LogError("装备极品数值加载失败："+e);Unload();}
 }
 public override bool Unload(){harmony?.UnpatchSelf();ClearNotices();Instance=null;return true;}
 void Guard(Action action){try{action();}catch(Exception e){Warn("exception:"+e.GetType().Name,"保留原版结果："+e.Message);}}
 void Warn(string key,string message){if(warnings.Count<32&&warnings.Add(key))Log.LogWarning("装备极品数值："+message);}
 bool Available()=>enabled.Value&&GameManager.Instance&&!GameManager.IsSlgMode&&PlayerTeamManager.Instance&&SaveManager.Instance&&!SaveManager.Instance.IsSavingOrLoading;
 Admission? Capture(GameItemPack bag){
  // The outer transaction already owns the before/after comparison and rollback.
  if(depth>0)return null;
  if(!Available()||bag.Pointer!=PlayerTeamManager.Instance.TeamInventory.Pointer)return null;
  var s=new Admission{Bag=bag.Pointer,Team=PlayerTeamManager.Instance.Pointer,Save=SaveManager.Instance.Pointer,Root=depth==0,Generation=generation};
  if(s.Root){
   s.Before=new();
   foreach(var item in bag.Contents)if(item!=null)s.Before.Add(item.Pointer);
   // Native UnEquipItem clears cachedEquipingCharacter BEFORE AddItem, but clears
   // EquipSlot AFTER AddItem. Snapshot slots so returning old gear is not a reward.
   AddEquipped(s.Before,PlayerTeamManager.Instance.PlayerDataInstance);
   for(int i=0;i<PlayerTeamManager.Instance.TeamSize;i++)AddEquipped(s.Before,PlayerTeamManager.Instance.GetTeamMemberByIndex(i));
  }
  depth++;
  if(s.Root){activeAdmission=s;foreach(var notice in pending)if(!s.Before.Contains(notice.Item.Pointer))notice.Confirmation=s;}
  return s;
 }
 static void AddEquipped(HashSet<IntPtr> pointers,GameCharacterInstance actor){
  if(actor==null||actor.EquipSlot==null)return;
  foreach(var entry in actor.EquipSlot)if(entry.Value!=null)pointers.Add(entry.Value.Pointer);
 }
 void Release(Admission? state){
  if(state==null||state.Released)return;state.Released=true;if(state.Generation!=generation)return;
  depth=Math.Max(0,depth-1);
  if(state.Root){activeAdmission=null;if(!state.Succeeded)pending.RemoveAll(notice=>ReferenceEquals(notice.Confirmation,state));}
 }
 void Acquired(GameItemPack bag,bool success,Admission? state){
  if(state!=null&&state.Root){state.Completed=true;state.Succeeded=success;}
  if(state==null||!state.Root||!success||state.Generation!=generation||!Available()||state.Bag!=bag.Pointer||state.Team!=PlayerTeamManager.Instance.Pointer||state.Save!=SaveManager.Instance.Pointer||bag.Pointer!=PlayerTeamManager.Instance.TeamInventory.Pointer)return;
  foreach(var item in bag.Contents){
   if(item==null||state.Before.Contains(item.Pointer))continue;
   Guard(()=>Optimize(item));
  }
  FlushNotices();
 }
 NoticeSource NewSource()=>new(){Bag=PlayerTeamManager.Instance.TeamInventory.Pointer,Team=PlayerTeamManager.Instance.Pointer,Save=SaveManager.Instance.Pointer,Generation=generation};
 bool Current(NoticeSource source)=>source.Generation==generation&&source.Bag==PlayerTeamManager.Instance.TeamInventory.Pointer&&source.Team==PlayerTeamManager.Instance.Pointer&&source.Save==SaveManager.Instance.Pointer;
 NoticeSource? CaptureLog(Il2CppReferenceArray<Il2CppSystem.Object> args){
  if(!Available()||args==null)return null;
  var source=NewSource();var seen=new HashSet<IntPtr>();
  foreach(var arg in args){
   if(arg==null)continue;
   var item=arg.TryCast<GameItemInstance>();
   if(item!=null){if(seen.Add(item.Pointer))source.Items.Add(item);continue;}
   var pack=arg.TryCast<GameItemPack>();
   // A whole player inventory is never a safe identification of a reward.
   if(pack!=null){if(pack.Pointer!=source.Bag)foreach(var value in pack.Contents)if(value!=null&&seen.Add(value.Pointer))source.Items.Add(value);continue;}
   var list=arg.TryCast<Il2CppSystem.Collections.Generic.List<GameItemInstance>>();
   if(list!=null)foreach(var value in list)if(value!=null&&seen.Add(value.Pointer))source.Items.Add(value);
  }
  if(source.Items.Count==0)return null;
  sources.Add(source);return source;
 }
 NoticeSource? CapturePickup(GameItemPack pack){
  if(!Available()||pack==null||pack.Pointer==PlayerTeamManager.Instance.TeamInventory.Pointer)return null;
  var source=NewSource();foreach(var item in pack.Contents)if(item!=null)source.Items.Add(item);
  source.Transfer=Capture(PlayerTeamManager.Instance.TeamInventory);sources.Add(source);return source;
 }
 void CompletePickup(NoticeSource? source){if(source==null||!Available()||!Current(source))return;Acquired(PlayerTeamManager.Instance.TeamInventory,true,source.Transfer);FlushNotices();}
 void ReleaseSource(NoticeSource? source){if(source==null)return;sources.Remove(source);Release(source.Transfer);}
 bool TeamActor(GameCharacterInstance actor){
  var team=PlayerTeamManager.Instance;if(team.PlayerDataInstance!=null&&actor.Pointer==team.PlayerDataInstance.Pointer)return true;
  for(int i=0;i<team.TeamSize;i++){var member=team.GetTeamMemberByIndex(i);if(member!=null&&member.Pointer==actor.Pointer)return true;}
  return false;
 }
 bool Owns(GameItemInstance item){
  foreach(var owned in PlayerTeamManager.Instance.TeamInventory.Contents)if(owned!=null&&owned.Pointer==item.Pointer)return true;
  return EquippedByTeam(item);
 }
 bool EquippedByTeam(GameItemInstance item){
  var actor=item.cachedEquipingCharacter;if(actor==null||!TeamActor(actor)||actor.EquipSlot==null)return false;
  foreach(var slot in actor.EquipSlot)if(slot.Value!=null&&slot.Value.Pointer==item.Pointer)return true;
  return false;
 }
 void Notified(ItemData item,int amount,GameLogType type,GameCharacterInstance? actor){
  if(!Available()||item==null||item.IsStackable||amount<=0||type!=GameLogType.Left||(actor!=null&&!TeamActor(actor)))return;
  var candidates=new Dictionary<IntPtr,GameItemInstance>();
  foreach(var source in sources)if(Current(source))foreach(var value in source.Items)if(value.TempleteId==item.Uid)candidates[value.Pointer]=value;
  // Template-only notices may bind only to a confirmed in-flight inventory delta.
  // Never sweep all existing items with the same template ID.
  var admission=activeAdmission;
  if(admission!=null&&admission.Generation==generation)foreach(var value in PlayerTeamManager.Instance.TeamInventory.Contents)if(value!=null&&value.TempleteId==item.Uid&&!admission.Before.Contains(value.Pointer))candidates[value.Pointer]=value;
  foreach(var candidate in candidates.Values){
   bool exists=false;foreach(var notice in pending)if(notice.Item.Pointer==candidate.Pointer&&Current(notice.Source)){exists=true;break;}
   if(exists)continue;
   if(pending.Count>=128)pending.RemoveAt(0);
   pending.Add(new(){Source=NewSource(),Item=candidate,Confirmation=admission!=null&&!admission.Before.Contains(candidate.Pointer)?admission:null});
  }
  FlushNotices();
 }
 void FlushNotices(){
  if(!Available())return;
  HashSet<IntPtr>? owned=null;
  for(int i=pending.Count-1;i>=0;i--){
   var notice=pending[i];
   if(!Current(notice.Source)){pending.RemoveAt(i);continue;}
   if(notice.Confirmation!=null){if(!notice.Confirmation.Completed)continue;if(!notice.Confirmation.Succeeded){pending.RemoveAt(i);continue;}}
   if(owned==null){owned=new();foreach(var item in PlayerTeamManager.Instance.TeamInventory.Contents)if(item!=null)owned.Add(item.Pointer);}
   if(!owned.Contains(notice.Item.Pointer)&&!EquippedByTeam(notice.Item))continue;
   pending.RemoveAt(i);Guard(()=>Optimize(notice.Item,true));
  }
 }
 void ClearNotices(){generation++;pending.Clear();sources.Clear();activeAdmission=null;depth=0;}
 static decimal Managed(Il2CppSystem.Decimal value)=>new(value.lo,value.mid,value.hi,value.flags<0,(byte)((value.flags>>16)&255));
 static Il2CppSystem.Decimal Native(decimal value){var b=decimal.GetBits(value);return new(){lo=b[0],mid=b[1],hi=b[2],flags=b[3]};}
 void Optimize(GameItemInstance item,bool allowEquipped=false){
  if(!item.IsValid||item.Stack<=0||item.IsStackable)return;
  var original=item.GetModifyData();
  if(original==null||original.templeteUid!=item.TempleteId||original.forgeEffect==null||(item.cachedEquipingCharacter!=null&&(!allowEquipped||!Owns(item))))return;
  var recipe=BaseDataClass.GetGameData<ItemForgeDataScriptObject>().GetData(item.TempleteId);
  if(recipe==null||recipe.BonusExt==null)return;
  var effects=BaseDataClass.GetGameData<ItemForgeEffectDataScriptObject>();
  var replacements=new Il2CppSystem.Collections.Generic.Dictionary<int,KeyWithValues>();bool changed=false;
  foreach(var entry in original.forgeEffect){
   var current=entry.Value;replacements.Add(entry.Key,current);
   var effect=effects.GetData(entry.Key);var range=effect?.BonusExt;
   if(current==null||range==null||!range.isValid||range.dValues==null||range.dValues.Count!=2||current.dValues==null||current.dValues.Count!=1||current.values==null||current.values.Count!=1||current.key!=range.key){Warn("shape:"+entry.Key,"词条数据未知或格式异常，保留原值："+entry.Key);continue;}
   bool belongs=false;foreach(var candidate in recipe.BonusExt)if(candidate!=null&&candidate.key==effect!.UName){belongs=true;break;}
   if(!belongs){Warn("foreign:"+entry.Key,"词条不属于原版物品模板，保留原值："+entry.Key);continue;}
   decimal first=Managed(range.dValues[0]),second=Managed(range.dValues[1]);
   if(first==second)continue;
   var direction=PerfectRollCatalog.Direction(entry.Key,range.key,first,second);
   if(direction==RollDirection.Unknown){Warn("semantic:"+entry.Key,"词条未通过当前Build语义核对，保留原值："+entry.Key);continue;}
   if(!PerfectRollPolicy.TryBest(range.key,first,second,0,1,direction,out var best)){Warn("range:"+entry.Key,"浮动范围异常，保留原值："+entry.Key);continue;}
   if(Managed(current.dValues[0])==best&&current.values[0]==(int)best)continue;
   var replacement=new KeyWithValues{key=current.key,isValid=current.isValid,dValues=new(),values=new()};
   replacement.dValues.Add(Native(best));replacement.values.Add((int)best);
   replacements[entry.Key]=replacement;changed=true;
  }
  if(!changed)return;
  // Construct first, then commit once. Shared templates, old instances, fixed
  // bonuses, requirements and rawRnd are never mutated or recomputed.
  item.modifyData=new ItemModifyData{
   templeteUid=original.templeteUid,forgeLevel=original.forgeLevel,rawRnd=original.rawRnd,
   overrideBonus=original.overrideBonus,overrideBonusExt=original.overrideBonusExt,
   overrideEquipReq=original.overrideEquipReq,forgeEffect=replacements
  };
  if(item.cachedEquipingCharacter!=null&&TeamActor(item.cachedEquipingCharacter))item.cachedEquipingCharacter.ComputeEquipProp(true,true);
  Log.LogInfo("已优化新获得装备："+item.TempleteId+"；保留原版词条种类和数量。");
 }
 [HarmonyPatch]
 static class Pickup {
  static MethodBase TargetMethod()=>AccessTools.Method(typeof(PlayerTeamManager),nameof(PlayerTeamManager.PickupPack),new[]{typeof(GameItemPack),typeof(bool),typeof(bool),typeof(bool),typeof(bool),typeof(bool)});
  static void Prefix(GameItemPack __0,out NoticeSource? __state){NoticeSource? s=null;Instance?.Guard(()=>s=Instance.CapturePickup(__0));__state=s;}
  static void Postfix(NoticeSource? __state)=>Instance?.Guard(()=>Instance.CompletePickup(__state));
  static Exception? Finalizer(Exception? __exception,NoticeSource? __state){Instance?.ReleaseSource(__state);return __exception;}
 }
 [HarmonyPatch]
 static class RawNotices {
  static MethodBase TargetMethod()=>AccessTools.Method(typeof(GameLogManager),nameof(GameLogManager.OnLogRaw),new[]{typeof(Il2CppReferenceArray<Il2CppSystem.Object>)});
  static void Prefix(Il2CppReferenceArray<Il2CppSystem.Object> __0,out NoticeSource? __state){NoticeSource? s=null;Instance?.Guard(()=>s=Instance.CaptureLog(__0));__state=s;}
  static Exception? Finalizer(Exception? __exception,NoticeSource? __state){Instance?.ReleaseSource(__state);return __exception;}
 }
 [HarmonyPatch]
 static class ItemNotices {
  static MethodBase TargetMethod()=>AccessTools.Method(typeof(GameLogManager),nameof(GameLogManager.GameLogItem),new[]{typeof(ItemData),typeof(int),typeof(GameLogType),typeof(bool),typeof(GameCharacterInstance),typeof(string)});
  static void Postfix(ItemData __0,int __1,GameLogType __2,GameCharacterInstance __4)=>Instance?.Guard(()=>Instance.Notified(__0,__1,__2,__4));
 }
 [HarmonyPatch]
 static class LoadSave {
  static MethodBase TargetMethod()=>AccessTools.Method(typeof(SaveManager),nameof(SaveManager.Load),Type.EmptyTypes);
  static void Prefix()=>Instance?.ClearNotices();
 }
 [HarmonyPatch]
 static class Admissions {
  static IEnumerable<MethodBase> TargetMethods(){
   yield return AccessTools.Method(typeof(GameItemPack),nameof(GameItemPack.AddItem),new[]{typeof(GameItemInstance),typeof(bool)});
   foreach(var type in new[]{typeof(ItemData),typeof(string),typeof(int)})yield return AccessTools.Method(typeof(GameItemPack),nameof(GameItemPack.AddItem),new[]{type,typeof(int),typeof(bool)});
   yield return AccessTools.Method(typeof(GameItemPack),nameof(GameItemPack.AddItemAsIndependent),new[]{typeof(GameItemInstance)});
  }
  static void Prefix(GameItemPack __instance,out Admission? __state){Admission? s=null;Instance?.Guard(()=>s=Instance.Capture(__instance));__state=s;}
  static void Postfix(GameItemPack __instance,bool __result,Admission? __state)=>Instance?.Guard(()=>Instance.Acquired(__instance,__result,__state));
  static Exception? Finalizer(Exception? __exception,Admission? __state){Instance?.Release(__state);return __exception;}
 }
}
