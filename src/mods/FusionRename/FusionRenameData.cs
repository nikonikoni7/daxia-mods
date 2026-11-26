using System;
using System.Collections.Generic;
using System.Globalization;
using GameData;
using WuLin;
using DataList=Il2CppSystem.Collections.Generic.List<BaseDataStorageClass>;
namespace DaxiaMods;
internal static class FusionRenameData {
 internal static bool CanRename(SaveObjectDynamicData save,KungfuData kungfu){
  if(!save||kungfu==null||kungfu.Uid>=0||string.IsNullOrEmpty(kungfu.UName)||save.storedDatas==null)return false;
  foreach(var item in save.storedDatas){
   var candidate=item?.TryCast<KungfuData>();
   if(candidate!=null&&candidate.Uid==kungfu.Uid&&candidate.UName==kungfu.UName&&candidate.Pointer==kungfu.Pointer)return true;
  }
  return false;
 }
 internal static LocData Name(DataList list,string key){
  LocData? found=null;
  foreach(var item in list){var loc=item?.TryCast<LocData>();if(loc==null||loc.UName!=key)continue;if(found!=null)throw new InvalidOperationException("融合名称记录重复。请先重新读档。");found=loc;}
  return found??throw new InvalidOperationException("未找到当前融合武功的名称记录。");
 }
 static void CopyText(LocData from,LocData to){
  to.SChinese=from.SChinese;to.TChinese=from.TChinese;to.English=from.English;to.VI=from.VI;to.ES=from.ES;to.PT=from.PT;to.TH=from.TH;
 }
 internal static void VerifySavedNames(DataList expected,DataList actual,KungfuData kungfu){
  foreach(var prefix in new[]{"KunfuName_","ItemName_"}){
   var a=Name(expected,prefix+kungfu.UName);var b=Name(actual,prefix+kungfu.UName);
   if(a.SChinese!=b.SChinese||a.TChinese!=b.TChinese||a.English!=b.English||a.VI!=b.VI||a.ES!=b.ES||a.PT!=b.PT||a.TH!=b.TH)
    throw new InvalidOperationException("共享武功名称写入后核验失败，已撤销本次内存改名。");
  }
 }
 internal static bool MatchesBook(GameItemInstance? item,KungfuData kungfu){
  if(item==null||item.TempleteId!=kungfu.Uid)return false;
  var book=item.Templete;
  return book!=null&&book.Uid==kungfu.Uid&&book.UName==kungfu.UName&&
   (book.KungfuLearn==kungfu.Uid.ToString(CultureInfo.InvariantCulture)||book.KungfuLearn==kungfu.UName);
 }
 internal static bool Rename(SaveObjectDynamicData save,KungfuData kungfu,FiefSharedData? fief,string text,Action persistShared){
  if(!CanRename(save,kungfu))throw new InvalidOperationException("当前存档或融合武功已变化，请重新打开窗口。");
  bool onStone=false;
  if(fief?.kungfuBooks!=null)foreach(var item in fief.kungfuBooks)if(MatchesBook(item,kungfu)){onStone=true;break;}
  var names=new[]{"KunfuName_"+kungfu.UName,"ItemName_"+kungfu.UName};
  var writes=new List<Action>();var undo=new List<Action>();
  // Prepare complete local and global changes before publishing anything.
  // Replace global LocData with detached clones. Native imprints can put the
  // same object in both lists; mutating that object would couple both scopes.
  if(onStone){
   var global=save.GlobalStoredData??throw new InvalidOperationException("共享武功数据尚未加载。");
   bool matching=false;foreach(var item in global){var k=item?.TryCast<KungfuData>();if(k!=null&&k.Uid==kungfu.Uid&&k.UName==kungfu.UName){matching=true;break;}}
   if(!matching)throw new InvalidOperationException("悟道碑武功与共享动态数据不匹配，未执行改名。");
   foreach(var key in names){
    var original=Name(global,key);int index=global.IndexOf(original);var copy=GameUtil.SafeClone(original);
    GameUtil.ApplyLocTextForAllLanguage(copy,text);
    writes.Add(()=>global[index]=copy);undo.Add(()=>global[index]=original);
   }
  }
  foreach(var key in names){
   var loc=Name(save.storedDatas,key);var before=GameUtil.SafeClone(loc);
   writes.Add(()=>GameUtil.ApplyLocTextForAllLanguage(loc,text));undo.Add(()=>CopyText(before,loc));
  }
  FusionRenamePolicy.Publish(writes,undo,()=>{if(onStone)persistShared();});
  return onStone;
 }
}
