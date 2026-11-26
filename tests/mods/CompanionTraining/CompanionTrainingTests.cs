using System;
using System.IO;
using DaxiaMods;

static class CompanionTrainingTests {
 static int checks;
 static void Check(bool ok,string label){if(!ok)throw new Exception("FAIL: "+label);checks++;}
 static TrainingBook Leaving(){var b=new TrainingBook();b.RememberLeave(true,true,false,11,101,501);return b;}
 static void Main(){
  var b=Leaving();Check(b.Records.Count==1,"successful departure remembers book");
  b.RememberLeave(false,true,true,11,101,502);Check(b.Records[11].BookId==501,"failed departure preserves record");
  b.RememberLeave(true,false,false,12,102,503);Check(b.Records.Count==1,"nonmember departure ignored");
  b.RememberLeave(true,true,true,12,102,503);Check(b.Records.Count==1,"still-member departure ignored");
  Check(b.TakeOnReturn(false,false,false,11,101)==null && b.Records.Count==1,"failed invitation retains record");
  Check(b.TakeOnReturn(true,true,true,11,101)==null && b.Records.Count==1,"duplicate invitation cannot consume record");
  var r=b.TakeOnReturn(true,false,true,11,101);Check(r?.BookId==501 && b.Records.Count==0,"successful return consumes record once");
  Check(b.TakeOnReturn(true,false,true,11,101)==null,"duplicate return has no delayed restoration");
  b=Leaving();Check(b.TakeOnReturn(true,false,true,11,999)==null && b.Records.Count==0,"reused character ID cannot inherit record");
  b=Leaving();b.RememberLeave(true,true,false,11,101,0);Check(b.Records.Count==0,"departure with no book clears stale record");
  foreach(var id in new[]{-1,-2,-104,int.MinValue+100}){
   b=new();b.RememberLeave(true,true,false,11,101,id);
   Check(b.Records.TryGetValue(11,out var fused) && fused.BookId==id,"negative fused-book ID is retained: "+id);
   Check(b.Clone().TakeOnReturn(true,false,true,11,101)?.BookId==id,"fused-book clone and return preserve exact ID: "+id);
  }
  b=Leaving();var clone=b.Clone();b.Records.Clear();Check(clone.Records.Count==1,"save snapshot is independent of subsequent mutations");
  var dir=Path.Combine(Path.GetTempPath(),"DaxiaTrainingTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  try{
   var slot=Path.Combine(dir,"account","ManualSave0");Directory.CreateDirectory(slot);
   var save=Path.Combine(slot,"SaveObjectGame.save");File.WriteAllText(save,"before departure");
   var store=new TrainingStore(Path.Combine(dir,"records"));var emptyPath=store.SnapshotPath(slot);store.Save(slot,new());
   foreach(var id in new[]{-1,-2,-104,int.MinValue+100}){
    var fused=new TrainingBook();fused.RememberLeave(true,true,false,11,101,id);store.Save(slot,fused);
    Check(new TrainingStore(Path.Combine(dir,"records")).Load(slot).Records[11].BookId==id,"saved fused-book ID survives restart: "+id);
   }
   store.Save(slot,new());
   File.WriteAllText(save,"after departure");var departedPath=store.SnapshotPath(slot);store.Save(slot,Leaving());
   Check(emptyPath!=departedPath,"overwrite of same slot has distinct identity");
   Check(new TrainingStore(Path.Combine(dir,"records")).Load(slot).Records.Count==1,"restart restores saved departure");
   File.WriteAllText(Path.Combine(slot,"screenshot.png"),"new image");Check(store.SnapshotPath(slot)==departedPath,"screenshot does not change identity");
   File.WriteAllText(save,"before departure");Check(store.Load(slot).Records.Count==0,"old save restores old snapshot");
   var other=Path.Combine(dir,"other-account","ManualSave0");Directory.CreateDirectory(other);File.WriteAllText(Path.Combine(other,"SaveObjectGame.save"),"after departure");
   Check(store.Load(other).Records.Count==0,"other account cannot inherit identical save payload");store.Save(other,Leaving());Check(store.Load(other).Records.Count==1,"save-as receives its own snapshot");
   Check(TrainingStore.ResolveSlotPath("account/ManualSave0",dir)==Path.GetFullPath(slot),"relative native path resolves under save root");
   var snapshot=store.SnapshotPath(other);store.Save(other,Leaving());Check(File.Exists(snapshot+".bak"),"valid snapshot has backup on replacement");
   File.WriteAllText(snapshot,"broken");bool blocked=false;try{store.Load(other);}catch(InvalidDataException){blocked=true;}Check(blocked && File.ReadAllText(snapshot)=="broken","corruption is preserved and blocks restoration");
   blocked=false;try{store.Save(other,Leaving());}catch(InvalidDataException){blocked=true;}Check(blocked && File.ReadAllText(snapshot)=="broken","corrupt file is not overwritten");
   var obstruction=Path.Combine(dir,"not-directory");File.WriteAllText(obstruction,"keep");blocked=false;try{new TrainingStore(obstruction).Save(slot,Leaving());}catch(IOException){blocked=true;}Check(blocked && File.ReadAllText(obstruction)=="keep","write failure protects existing files");
  }finally{Directory.Delete(dir,true);}
  Console.WriteLine("CompanionTraining core/storage: "+checks+" checks passed.");
 }
}
