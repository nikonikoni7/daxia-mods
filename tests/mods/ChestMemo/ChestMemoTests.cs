using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using DaxiaMods;
public static class ChestMemoTests
{
 static int checks;
 static void Check(bool condition,string label){if(!condition)throw new Exception("FAIL: "+label);checks++;Console.WriteLine("PASS: "+label);}
 static ChestRecord Chest(int map=1,int id=4,string name="迷踪林")=>new(){MapId=map,SaveId=id,TemplateId=9,X=10,Y=20,MapName=name,Routes=new(){new(){Keys=new(){new(){Id=101,Name="铜钥匙",Count=2}}},new(){Keys=new(){new(){Id=102,Name="银钥匙",Count=1}}}}};
 public static int Main(){try{Run();return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 static void Run(){
  var book=new ChestBook();var chest=Chest();
  Check(book.RecordAttempt(true,false,true,true,chest),"missing-key failed attempt recorded");
  Check(!book.RecordAttempt(true,false,true,true,Chest()) && book.Records.Count==1,"same chest deduplicated");
  Check(!book.RecordAttempt(false,false,true,true,Chest(1,5)),"door excluded");
  Check(!book.RecordAttempt(true,true,true,true,Chest(1,5)),"opened chest excluded");
  Check(!book.RecordAttempt(true,false,false,true,Chest(1,5)),"cancelled interaction excluded");
  Check(!book.RecordAttempt(true,false,true,false,Chest(1,5)),"ability failure and sufficient-key cancellation excluded");
  Check(ChestBook.Missing(new[]{new KeyNeed{Id=101,Count=2}},_=>1),"key quantity shortage");
  Check(!ChestBook.Missing(new[]{new KeyNeed{Id=101,Count=2}},_=>2),"enough keys is not failure");
  Check(ChestBook.RequirementText(chest)=="（铜钥匙×2）或（银钥匙×1）","alternative requirements retained");
  var both=Chest(1,6);both.Routes=new(){new(){Keys=new(){new(){Id=101,Name="铜钥匙",Count=2},new(){Id=102,Name="银钥匙",Count=1}}}};
  Check(ChestBook.RequirementText(both)=="铜钥匙×2＋银钥匙×1","AND requirements retained");
  var hidden=Chest();hidden.Routes=new(){new(){Keys=new(){new(){Id=101,Name="铜钥匙",Count=2}},HasOtherConditions=true},new(){HasOtherConditions=true}};
  Check(ChestBook.RequirementText(hidden)=="铜钥匙×2","memo hides non-key condition alternatives and suffixes");
  Check(hidden.Routes.Count==2 && hidden.Routes.All(r=>r.HasOtherConditions),"hiding condition text preserves stored native conditions");
  hidden.Routes.Add(new(){Keys=new(){new(){Id=102,Name="银钥匙",Count=1}}});
  Check(ChestBook.RequirementText(hidden)=="（铜钥匙×2）或（银钥匙×1）","hiding other conditions retains alternative key relationships");
  book.RecordAttempt(true,false,true,true,Chest(2,4,"楚襄城"));
  Check(book.Records.Count==2,"same save ID on other map independent");
  Check(book.Groups(MemoGrouping.Map,2,false)[0].Title=="楚襄城","current map first");
  Check(book.Groups(MemoGrouping.Key,1,false).Count==2,"key grouping");
  Check(book.Groups(MemoGrouping.Key,1,false).All(g=>g.Items.Count==2),"alternative chest appears in both key groups");
  Check(book.Groups(MemoGrouping.None,2,false).Count==1,"ungrouped list");
  var ascending=book.Groups(MemoGrouping.None,0,false)[0].Items.Select(x=>x.MapName).ToArray();
  var descending=book.Groups(MemoGrouping.None,0,true)[0].Items.Select(x=>x.MapName).ToArray();
  Check(ascending.SequenceEqual(descending.Reverse()),"map name sort reversible");
  Check(!ChestBook.ShowDots(false,true,true,1,1),"no owned map no dots");
  Check(!ChestBook.ShowDots(true,false,true,1,1),"closed scene map no dots");
  Check(!ChestBook.ShowDots(true,true,false,1,1),"world map or battle no dots");
  Check(!ChestBook.ShowDots(true,true,true,1,2),"other scene map no dots");
  Check(ChestBook.ShowDots(true,true,true,1,1),"owned current scene map dots");
  Check(ChestBook.TryProject(3,4,10,20,25,40,5,out var x,out var y) && x==25 && y==40,"native anchor scale projection");
  Check(!ChestBook.TryProject(3,4,10,20,30,50,0,out _,out _),"degenerate calibration rejected");
  Check(book.Remove(1,4,9,10,20) && book.Records.Count==1,"successful opening removes only matched chest");
  Check(!book.Remove(1,999,9,10,20),"positive save IDs cannot collide by coordinate fallback");
  var fallback=Chest(3,0);book.RecordAttempt(true,false,true,true,fallback);
  Check(book.Remove(3,0,9,10,20),"no save ID falls back to template and cell");
  var dir=Path.Combine(Path.GetTempPath(),"ChestMemoTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  try{
   var saveA=Path.Combine(dir,"manual0.es3");var saveB=Path.Combine(dir,"auto0.es3");File.WriteAllText(saveA,"old save");File.WriteAllText(saveB,"other save");
   var store=new ChestStore(Path.Combine(dir,"memo"));store.SaveSnapshot(saveA,book);
   var slot=Path.Combine(dir,"ManualSave0");Directory.CreateDirectory(slot);File.WriteAllText(Path.Combine(slot,"SaveObjectMapManager.save"),"map data");
   store.SaveSnapshot(slot,book);Check(store.LoadSnapshot(slot).Records.Count==1,"native .save directory fingerprint");
   File.WriteAllText(Path.Combine(slot,"SaveInfo.bak"),"backup changed");Check(store.LoadSnapshot(slot).Records.Count==1,"backup changes do not change slot identity");
   Check(new ChestStore(Path.Combine(dir,"memo")).LoadSnapshot(saveA).Records.Count==1,"restart restores saved snapshot");
   Check(store.LoadSnapshot(saveB).Records.Count==0,"other save isolated");
   var loaded=store.LoadSnapshot(saveA);loaded.RecordAttempt(true,false,true,true,Chest());store.SaveSnapshot(saveB,loaded);
   Check(store.LoadSnapshot(saveB).Records.Count==2,"save-as copies current journal");
   Check(store.LoadSnapshot(saveA).Records.Count==1,"old snapshot not modified by save-as");
   File.WriteAllText(saveA,"new game in same slot");Check(store.LoadSnapshot(saveA).Records.Count==0,"slot overwrite cannot inherit old memo");
   File.WriteAllText(saveA,"old save");Check(store.LoadSnapshot(saveA).Records.Count==1,"restored old save gets matching snapshot");
   File.WriteAllText(saveB,"rotated autosave");store.SaveSnapshot(saveB,book);Check(store.LoadSnapshot(saveB).Records.Count==1,"autosave rotation binds new contents");
   store.SaveWorking(saveA,loaded);Check(store.LoadWorking(saveA).Records.Count==2,"failed attempts persist before next game save");
   loaded.Remove(1,4,9,10,20);store.SaveWorking(saveA,loaded);
   Check(store.LoadWorking(saveA).Records.Count==1 && store.LoadSnapshot(saveA).Records.Count==1,"working state independent from rollback snapshot");
   var removed=store.LoadSnapshot(saveA);removed.Records.Clear();store.SaveWorking(saveA,removed);
   Check(store.LoadWorking(saveA).Records.Count==1,"restart retains saved chest removed only after an unsaved opening");
   store.SaveWorking(saveA,book);store.SaveWorking(saveA,book);File.Delete(store.SnapshotPath(saveA).Replace(".json",".working.json"));
   Check(store.LoadWorking(saveA).Records.Count==1,"missing working primary recovers working backup");
   var path=store.SnapshotPath(saveA);store.SaveSnapshot(saveA,book);File.WriteAllText(path,"broken json");
   Check(store.LoadSnapshot(saveA).Records.Count==1,"corrupt primary recovers backup");
   Check(File.ReadAllText(path)=="broken json","corrupt primary preserved during recovery");
   store.SaveSnapshot(saveA,book);Check(Directory.GetFiles(Path.GetDirectoryName(path)!,"*.corrupt-*").Length>0,"corrupt file preserved before rewrite");
   File.WriteAllText(path,"broken");File.WriteAllText(path+".bak","broken");bool failed=false;try{store.LoadSnapshot(saveA);}catch(InvalidDataException){failed=true;}
   Check(failed,"double corruption reported instead of empty overwrite");
   File.WriteAllText(saveA,"other contents");Check(store.LoadWorking(saveA).Records.Count==0,"working memo fingerprint guards slot replacement");
  }finally{Directory.Delete(dir,true);}
  Console.WriteLine("ChestMemo checks passed: "+checks);
 }
}
