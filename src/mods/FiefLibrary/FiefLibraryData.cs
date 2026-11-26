using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Events;
using WuLin;
using Object=UnityEngine.Object;
namespace DaxiaMods;
public sealed partial class FiefLibrary {
 const string DataKey=Owner+".warehouse.v1";
 GameItemPack warehouse=new(int.MaxValue);
 FiefLibraryTransaction operation=new();
 long revision,generation;
 bool ready,awaitingLoad,blocked;
 SaveObjectGame? liveGame;SaveObjectPlayerTeam? liveData;PlayerTeamManager? liveTeam;
 string? liveAccount;
 SaveManager? subscribed;UnityAction? loaded,reset,account;
 LibrarySaveJournal? pending;string? journalPath;
 readonly HashSet<string> syncedPaths=new(StringComparer.OrdinalIgnoreCase);
 readonly HashSet<string> copiedPaths=new(StringComparer.OrdinalIgnoreCase);
 IntPtr windowGame,windowData,windowInventory;string? windowSlot;long windowGeneration;
 static ES3Settings MemorySettings()=>new ES3Settings(true){encryptionType=ES3.EncryptionType.None,compressionType=ES3.CompressionType.None,prettyPrint=false};
 static string EncodePack(GameItemPack pack)=>Convert.ToBase64String(ES3.Serialize<Il2CppSystem.Object>(pack,MemorySettings()));
 static GameItemPack DecodePack(string bytes){var obj=ES3.Deserialize<Il2CppSystem.Object>(Convert.FromBase64String(bytes),MemorySettings());return obj?.TryCast<GameItemPack>()??throw new InvalidDataException("原版秘籍物品包无法反序列化。");}
 static string PackDigest(GameItemPack pack)=>LibraryEnvelope.Hash(EncodePack(pack));
 string Payload()=>LibraryEnvelope.Encode(EncodePack(warehouse),revision);
 static string Slot(){
  var p=SaveManager.SavePath;var root=Path.GetFullPath(SaveManager.FullPathForAllSaveFolder);
  if(string.IsNullOrWhiteSpace(p))throw new InvalidDataException("尚无有效保存槽位。");
  var full=Path.TrimEndingDirectorySeparator(Path.IsPathFullyQualified(p)?Path.GetFullPath(p):Path.GetFullPath(p,root));
  var relative=Path.GetRelativePath(root,full);if(Path.IsPathRooted(relative)||relative.StartsWith("..",StringComparison.Ordinal))throw new InvalidDataException("槽位不在原版保存目录。");return full;
 }
 static string FileHash(string path)=>File.Exists(path)?Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))):"";
 static string JournalPath(string slot)=>Path.Combine(Paths.ConfigPath,"FiefLibrary","recovery",LibraryEnvelope.Hash(slot.ToUpperInvariant())+".json");
 static string TeamFile(string slot)=>Path.Combine(slot,"SaveObjectPlayerTeam.save");
 static string GameFile(string slot)=>Path.Combine(slot,"SaveObjectGame.save");
 static string CheckpointText(string path)=>ES3.LoadRawString(path,GameManager.gameSaveSettings);
 static string ReadSavedPayload(string path)=>LibraryCheckpoint.Payload(CheckpointText(path),DataKey);
 static GameItemPack ReadSavedInventory(string path){
  var raw=LibraryCheckpoint.Member(CheckpointText(path),"Data","value","playerTeamInventory");
  if(!raw.StartsWith("{",StringComparison.Ordinal))throw new InvalidDataException("保存的背包不是物品包。");
  // GameItemPack is a value object, with no Unity _ES3Ref. Read only this pack,
  // never the containing SaveObjectPlayerTeam or its live reference identity.
  var typed="{\"__type\":\"WuLin.GameItemPack,Assembly-CSharp\","+raw.Substring(1);
  return DecodePack(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(typed)));
 }
 static string ReadPayload(SaveObjectGame game)=>game.miscStringDict!=null&&game.miscStringDict.TryGetValue(DataKey,out var raw)?raw:"";
 bool LiveContext()=>ready&&liveGame&&liveData&&liveTeam&&GameManager.SaveData?.Pointer==liveGame!.Pointer&&PlayerTeamManager.Instance?.Pointer==liveTeam!.Pointer&&liveTeam.dataContainer.initedObject?.Pointer==liveData!.Pointer&&liveAccount==SaveManager.savePathUser;
 bool SameContext()=>LiveContext()&&windowGeneration==generation&&GameManager.SaveData.Pointer==windowGame&&liveData!.Pointer==windowData&&PlayerTeamManager.Instance.TeamInventory.Pointer==windowInventory&&SaveManager.SavePath==windowSlot;
 void Bind(){
  var m=SaveManager.Instance;if(!m||(subscribed&&subscribed!.Pointer==m.Pointer))return;
  Unbind();Invalidate(false);subscribed=m;
  loaded=DelegateSupport.ConvertDelegate<UnityAction>((Action)(()=>Guard(Restore)));
  reset=DelegateSupport.ConvertDelegate<UnityAction>((Action)(()=>Invalidate(awaitingLoad)));
  account=DelegateSupport.ConvertDelegate<UnityAction>((Action)(()=>Invalidate(false)));
  m.onLoadFinished+=loaded;m.onReset+=reset;m.onAccountChange+=account;
 }
 void Unbind(){if(subscribed){if(loaded!=null)subscribed!.onLoadFinished-=loaded;if(reset!=null)subscribed!.onReset-=reset;if(account!=null)subscribed!.onAccountChange-=account;}subscribed=null;}
 void Invalidate(bool loading){generation++;Close();ready=false;awaitingLoad=loading;blocked=false;pending=null;syncedPaths.Clear();copiedPaths.Clear();liveGame=null;liveData=null;liveTeam=null;liveAccount=null;warehouse=new(int.MaxValue);revision=0;operation=new();}
 void Restore(){
  Invalidate(false);
  try{
   liveGame=GameManager.SaveData;liveTeam=PlayerTeamManager.Instance;liveData=liveTeam?.dataContainer.initedObject;liveAccount=SaveManager.savePathUser;
   if(!liveGame||!liveTeam||!liveData)throw new InvalidDataException("当前档的原版数据尚未准备好。");
   string raw=ReadPayload(liveGame!);
   if(!string.IsNullOrWhiteSpace(SaveManager.SavePath)){
    string slot=Slot();var journal=LibrarySaveJournal.Read(JournalPath(slot));
    if(journal!=null&&journal.Slot!=slot)throw new InvalidDataException("恢复日志不属于当前槽位。");
    if(journal!=null&&journal.Phase=="Pending"){
     raw=journal.Resolve(FileHash(TeamFile(slot)),PackDigest(liveTeam!.TeamInventory));
     if(raw!=ReadPayload(liveGame!)){GameManager.Instance.SetInGameString(DataKey,raw);Log.LogWarning("已依据精确背包检查点在内存中恢复藏经阁配对；请正常保存。原始文件及恢复日志保留。");}
    }
   }
   if(raw.Length>0){var e=LibraryEnvelope.Decode(raw);warehouse=DecodePack(e.Pack);revision=e.Revision;ValidateWarehouse();}
   operation=new();ready=true;blocked=false;
  }catch(Exception e){blocked=true;ready=false;Log.LogError("仓库读取失败，原数据保留，停止转移："+e.Message);}
 }
 static bool Eligible(GameItemInstance? item)=>item!=null&&item.IsValid&&item.Stack>0&&item.IsType(WuLin.ItemType.KungfuBook)&&item.cachedEquipingCharacter==null;
 void ValidateWarehouse(){foreach(var i in warehouse.Contents)if(!Eligible(i))throw new InvalidDataException("仓库存在无效或非秘籍实例，已停止操作并保留数据。");}
 sealed class PackSnapshot {
  readonly GameItemPack pack;readonly GameItemInstance[] items;readonly int[] stacks;readonly bool[] isNew,forbidden;
  readonly Il2CppSystem.Collections.Generic.Dictionary<int,int>? index;
  internal PackSnapshot(GameItemPack p){
   pack=p;items=p.Contents.ToArray();stacks=items.Select(i=>i.Stack).ToArray();isNew=items.Select(i=>i.IsNew).ToArray();forbidden=items.Select(i=>i.IsForbidden).ToArray();
   // Loaded player inventories can legitimately have no template-import index.
   // Preserve that state rather than enumerating null or inventing an index.
   if(p.itemsFromTemplete!=null){index=new();foreach(var entry in p.itemsFromTemplete)index.Add(entry.Key,entry.Value);}
  }
  internal void Restore(){
   for(int i=0;i<items.Length;i++){if(items[i].Stack!=stacks[i]&&!items[i].ChangeStack(stacks[i]-items[i].Stack))throw new InvalidDataException("原版拒绝还原物品堆叠。");items[i].SetNew(isNew[i]);items[i].SetForbidden(forbidden[i]);}
   pack.Contents.Clear();foreach(var item in items)pack.Contents.Add(item);pack.itemsFromTemplete=index;
  }
 }
 sealed class PackEvents:IDisposable {
  readonly GameItemPack pack;readonly Il2CppSystem.Action? changed;readonly Il2CppSystem.Action<GameItemInstance>? got,lost,modified;
  internal PackEvents(GameItemPack p,LibraryDeferredEvents queue){
   pack=p;changed=p.onItemChanged;got=p.onItemGet;lost=p.onItemLost;modified=p.onItemModify;
   if(changed!=null)p.onItemChanged=DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((Action)(()=>queue.Enqueue(()=>changed.Invoke())));
   if(got!=null)p.onItemGet=DelegateSupport.ConvertDelegate<Il2CppSystem.Action<GameItemInstance>>((Action<GameItemInstance>)(x=>queue.Enqueue(()=>got.Invoke(x))));
   if(lost!=null)p.onItemLost=DelegateSupport.ConvertDelegate<Il2CppSystem.Action<GameItemInstance>>((Action<GameItemInstance>)(x=>queue.Enqueue(()=>lost.Invoke(x))));
   if(modified!=null)p.onItemModify=DelegateSupport.ConvertDelegate<Il2CppSystem.Action<GameItemInstance>>((Action<GameItemInstance>)(x=>queue.Enqueue(()=>modified.Invoke(x))));
  }
  public void Dispose(){pack.onItemChanged=changed;pack.onItemGet=got;pack.onItemLost=lost;pack.onItemModify=modified;}
 }
 internal bool Transfer(GameItemInstance item,int count,bool deposit){
  if(!visible||!SameContext()||!Available()||blocked||operation.Blocked||pending!=null)return false;
  var bag=PlayerTeamManager.Instance.TeamInventory;var source=deposit?bag:warehouse;var dest=deposit?warehouse:bag;
  bool Valid()=>visible&&SameContext()&&!blocked&&Eligible(item)&&count>0&&count<=item.Stack&&source.Contents.Contains(item);
  if(!Valid())return false;
  // Preserve native stack behavior without the template reconstruction in
  // AddItem(instance): only identical unmodified/unequipped instances merge.
  GameItemInstance? merge=null;
  if(item.IsStackable&&item.GetModifyData()==null)foreach(var x in dest.Contents){if(x.TempleteId==item.TempleteId&&x.IsStackable&&x.GetModifyData()==null&&x.cachedEquipingCharacter==null&&x.IsForbidden==item.IsForbidden&&x.IsNew==item.IsNew){merge=x;break;}}
  // The ordinary player inventory stores capacity 0 and accepts native adds
  // without a slot limit. Preserve explicit finite-pack limits and overflow checks.
  if(merge!=null?count>int.MaxValue-merge.Stack:dest.Capacity!=0&&dest.Contents.Count>=dest.Capacity)return false;
  var a=new PackSnapshot(source);var b=new PackSnapshot(dest);bool hadPayload=liveGame!.miscStringDict!=null&&liveGame.miscStringDict.ContainsKey(DataKey);string old=ReadPayload(liveGame!);long oldRevision=revision;
  int sourceBefore=source.GetItemCountById(item.TempleteId),destBefore=dest.GetItemCountById(item.TempleteId);int id=item.TempleteId;
  // Native GiveItemTo recreates stackable items from templates. Clone complete
  // instances with ES3 before deduction so dynamic identity and flags survive.
  var copyPack=new GameItemPack(int.MaxValue);copyPack.AddItemAsIndependent(item);
  GameItemInstance copy;
  try{copy=DecodePack(EncodePack(copyPack)).Contents[0];}finally{copyPack.Contents.Clear();}
  if(copy.Stack!=count&&!copy.ChangeStack(count-copy.Stack))throw new InvalidOperationException("无法准备转移数量。");
  int token=uiGeneration;long epoch=generation;
  bool Stable()=>visible&&token==uiGeneration&&epoch==generation&&SameContext()&&!blocked;
  var notifications=new LibraryDeferredEvents();bool done;
  using(var sourceEvents=new PackEvents(source,notifications))using(var destEvents=new PackEvents(dest,notifications)){
  try{done=operation.Execute(Stable,()=>{
   if(!source.RemoveItem(item,count))throw new InvalidOperationException("原版背包扣除失败。");
   if(merge!=null){if(!merge.ChangeStack(count))throw new InvalidOperationException("原版堆叠接收失败。");dest.onItemModify?.Invoke(merge);}
   else{if(!dest.AddItemAsIndependent(copy))throw new InvalidOperationException("原版背包接收失败。");dest.onItemGet?.Invoke(copy);}
   // AddItemAsIndependent/ChangeStack do not emit pack notifications.
   dest.onItemChanged?.Invoke();
  },()=>{revision=checked(revision+1);GameManager.Instance.SetInGameString(DataKey,Payload());},()=>{
   a.Restore();b.Restore();revision=oldRevision;if(hadPayload)GameManager.Instance.SetInGameString(DataKey,old);else liveGame!.miscStringDict?.Remove(DataKey);
  },()=>{
   if(source.GetItemCountById(id)!=sourceBefore-count||dest.GetItemCountById(id)!=destBefore+count)throw new InvalidDataException("转移数量核验失败。");ValidateWarehouse();
  });if(!done)notifications.Discard();}catch{notifications.Discard();throw;}}
  if(done){try{notifications.Commit();}catch(Exception e){Log.LogError(e);Close();}}
  if(done){try{RefreshLists();}catch(Exception e){Log.LogError("转移已完成，界面刷新失败并关闭："+e.Message);Close();}}return done;
 }
 void BeginSave(){
  Close();
  if(operation.Busy||operation.Blocked)throw new InvalidOperationException("仓库事务未完成，拒绝保存不一致状态。");
  if(!enabled.Value||!LiveContext()||blocked||ReadPayload(liveGame!).Length==0)return;
  if(pending!=null){if(Slot()==pending.Slot&&Payload()==pending.AfterPayload&&PackDigest(liveTeam!.TeamInventory)==pending.AfterInventoryDigest)return;throw new InvalidOperationException("上次保存尚未完成核验，请等待或重新读档。");}
  string slot=Slot();string path=JournalPath(slot);
  var record=new LibrarySaveJournal{Slot=slot,AfterInventoryDigest=PackDigest(liveTeam!.TeamInventory),AfterPayload=Payload(),BeforeInventoryHash=FileHash(TeamFile(slot))};
  if(File.Exists(GameFile(slot)))record.BeforePayload=ReadSavedPayload(GameFile(slot));
  var old=LibrarySaveJournal.Read(path);if(old!=null&&old.Phase=="Pending"){
   if(old.Slot!=slot||!File.Exists(TeamFile(slot)))throw new InvalidDataException("旧恢复日志无法匹配当前保存基线。");
   record.BeforePayload=old.Resolve(record.BeforeInventoryHash,PackDigest(ReadSavedInventory(TeamFile(slot))));
   string evidence=path+"."+old.Generation+".preserved";if(!File.Exists(evidence))File.Copy(path,evidence,false);
  }
  LibrarySaveJournal.Write(path,record);pending=record;journalPath=path;syncedPaths.Clear();copiedPaths.Clear();
  GameManager.Instance.SetInGameString(DataKey,record.AfterPayload);
 }
 internal void Synced(ES3File file){if(pending!=null)syncedPaths.Add(Path.GetFullPath(file.settings.FullPath));}
 internal void BackupCopied(ES3File file){if(pending!=null)copiedPaths.Add(Path.GetFullPath(file.settings.FullPath));}
 void CompleteSave(){
  if(pending==null||!LiveContext()||SaveManager.Instance.IsSavingOrLoading||SaveManager.syncCounter!=0)return;
  var record=pending;if(Slot()!=record.Slot){blocked=true;return;}
  string root=Path.GetFullPath(SaveManager.FullPathForAllSaveFolder);string backup=Path.Combine(root+"Backup",Path.GetRelativePath(root,record.Slot));
  var required=new[]{GameFile(record.Slot),TeamFile(record.Slot),GameFile(backup),TeamFile(backup)};
  if(required.Any(p=>!File.Exists(p))||required.Take(2).Any(p=>!syncedPaths.Contains(Path.GetFullPath(p))||!copiedPaths.Contains(Path.GetFullPath(p))))return;
  if(FileHash(required[0])!=FileHash(required[2])||FileHash(required[1])!=FileHash(required[3])){blocked=true;throw new InvalidDataException("主备份物品检查点不一致，已保留恢复日志并锁定仓库。");}
  if(ReadSavedPayload(required[0])!=record.AfterPayload)throw new InvalidDataException("保存的仓库未匹配冻结快照。");
  if(PackDigest(ReadSavedInventory(required[1]))!=record.AfterInventoryDigest)throw new InvalidDataException("保存的背包未匹配冻结快照。");
  record.GameHash=FileHash(required[0]);record.InventoryHash=FileHash(required[1]);record.Phase="Committed";LibrarySaveJournal.Write(journalPath!,record);pending=null;syncedPaths.Clear();copiedPaths.Clear();
 }
 void DataTick(){
  Bind();if(ready&&!LiveContext())Invalidate(false);
  if(!ready&&!blocked&&!awaitingLoad&&Available())Restore();
  if(pending!=null&&!blocked){try{CompleteSave();}catch{blocked=true;Close();throw;}}
 }
 [HarmonyPatch(typeof(SaveManager),nameof(SaveManager.Load),new Type[]{})]static class Loading{static void Prefix()=>Instance?.Invalidate(true);}
 [HarmonyPatch(typeof(GameManager),nameof(GameManager.StartNewGame),new Type[]{})]static class NewGame{static void Prefix()=>Instance?.Invalidate(false);}
 [HarmonyPatch(typeof(GameManager),nameof(GameManager.QuitToMenu),new Type[]{})]static class Quit{static void Prefix()=>Instance?.Invalidate(false);}
 [HarmonyPatch(typeof(SaveManager),nameof(SaveManager.Save))]static class Saving{static void Prefix()=>Instance?.BeginSave();}
 [HarmonyPatch(typeof(SaveManager),nameof(SaveManager.SaveAsync))]static class SavingAsync{static void Prefix()=>Instance?.BeginSave();}
 [HarmonyPatch(typeof(SaveManager),"BackupSaveFileInternal")]static class Backup{static void Postfix(ES3File __0)=>Instance?.Guard(()=>Instance.BackupCopied(__0));}
 [HarmonyPatch(typeof(ES3File),nameof(ES3File.Sync),new Type[]{})]static class Sync{
  static void Postfix(ES3File __instance)=>Instance?.Guard(()=>Instance.Synced(__instance));
  static Exception? Finalizer(Exception? __exception){if(__exception!=null&&Instance?.pending!=null){Instance.blocked=true;Instance.Close();Instance.Log.LogError("原版保存失败，藏经阁已锁定；恢复日志保留。");}return __exception;}
 }
}
