using System;
using System.IO;
using System.Reflection;
using DaxiaMods;
using WuLin;

static class CompanionTrainingGameTests {
 static int checks;
 static void Check(bool ok,string label){if(!ok)throw new Exception("FAIL: "+label);checks++;}
 static T Field<T>(CompanionTraining p,string name)=>(T)typeof(CompanionTraining).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(p)!;
 static void Patch(string name,string method,params object?[] args)=>typeof(CompanionTraining).GetNestedType(name,BindingFlags.NonPublic)!.GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args);
 static GameCharacterInstance Actor(int id=11)=>new(){InstanceId=id,TempleteUid=100+id};
 static void Leave(GameCharacterInstance c,bool success=true){
  var team=PlayerTeamManager.Instance;object?[] prefix={team,c,null};Patch("Leave","Prefix",prefix);
  if(success){team.Members.Remove(c);if(c.Book!=null){c.Book.cachedEquipingCharacter=null;team.TeamInventory.Items[c.Book.TempleteId]=c.Book;c.Book=null;}}
  Patch("Leave","Postfix",team,c,success,prefix[2]);
 }
 static void Join(GameCharacterInstance c,bool success=true){
  var team=PlayerTeamManager.Instance;object?[] prefix={team,c,null};Patch("Join","Prefix",prefix);
  if(success && !team.Members.Contains(c))team.Members.Add(c);
  Patch("Join","Postfix",team,c,success,prefix[2]);
 }
 static void SaveNative(CompanionTraining p){
  var sm=SaveManager.Instance;sm.IsSaving=true;sm.onSaveFinished?.Invoke();
  // Build 24794582 clears IsSaving BEFORE synchronizing the real files.
  sm.IsSaving=false;Patch("Saved","Postfix",sm.m_saveFile);Patch("Saved","Postfix",sm.m_saveFileBackup);p.Tick();
 }
 static void Main(){
  var dir=Path.Combine(Path.GetTempPath(),"DaxiaTrainingGame-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  BepInEx.Paths.ConfigPath=Path.Combine(dir,"config");var slot=Path.Combine(dir,"account","ManualSave0");Directory.CreateDirectory(slot);
  File.WriteAllText(Path.Combine(slot,"SaveObjectGame.save"),"native-state");SaveManager.SavePath="account/ManualSave0";SaveManager.FullPathForAllSaveFolder=dir;
  var p=new CompanionTraining();p.Load();p.Tick();var team=PlayerTeamManager.Instance;
  try{
   foreach(var id in new[]{-1,-2}){
    var fusedActor=Actor(30-id);var fusedBook=new GameItemInstance{TempleteId=id,cachedEquipingCharacter=fusedActor};fusedActor.Book=fusedBook;team.Members.Add(fusedActor);
    Leave(fusedActor);Check(Field<TrainingBook>(p,"book").Records.TryGetValue(fusedActor.InstanceId,out var memory) && memory.BookId==id,"leave hook retains dynamic fused-book ID: "+id);
    Check(fusedActor.Book==null && fusedBook.cachedEquipingCharacter==null,"fused book is released while actor is absent");
    SaveNative(p);p.Unload();p=new CompanionTraining();p.Load();p.Tick();
    Join(fusedActor);Check(fusedActor.Book==fusedBook && fusedActor.EquipCalls==1,"fused book restores through native equip after save/restart: "+id);
    Leave(fusedActor);var holder=Actor(70-id);holder.Book=fusedBook;fusedBook.cachedEquipingCharacter=holder;team.Members.Add(holder);Join(fusedActor);
    Check(fusedActor.Book==null && holder.Book==fusedBook && !Field<TrainingBook>(p,"book").Records.ContainsKey(fusedActor.InstanceId),"occupied fused book stays with current holder");
    holder.Book=null;fusedBook.cachedEquipingCharacter=null;team.Members.Remove(holder);p.Tick();Check(fusedActor.Book==null,"fused book is not restored later when released");
    fusedActor.Book=fusedBook;Leave(fusedActor);fusedActor.Eligibility=GameCharacterInstance.EquipingCheckResult.KungfuMaxLevel;Join(fusedActor);Check(fusedActor.Book==null,"fused book still obeys native max-level restriction");fusedActor.Eligibility=GameCharacterInstance.EquipingCheckResult.Ok;
    fusedActor.Book=fusedBook;Leave(fusedActor);team.TeamInventory.Items.Remove(id);Join(fusedActor);Check(fusedActor.Book==null,"missing fused book is not recreated");team.Members.Remove(fusedActor);
   }
   var c=Actor();var book=new GameItemInstance{TempleteId=501,cachedEquipingCharacter=c};c.Book=book;team.Members.Add(c);
   Leave(c,false);Check(Field<TrainingBook>(p,"book").Records.Count==0 && c.Book==book,"failed original removal has no record or equipment changes");
   Leave(c);Check(c.Book==null && book.cachedEquipingCharacter==null && Field<TrainingBook>(p,"book").Records.Count==1,"actual leave hook keeps native unequip and remembers book");
   Check(c.EquipCalls==0,"departed actor has no re-equip or cultivation callback");
   Join(c,false);Check(Field<TrainingBook>(p,"book").Records.Count==1,"failed original invitation retains record");Join(c);
   Check(c.Book==book && c.EquipCalls==1 && c.ConditionCalls==1,"actual join hook checks native eligibility then equips once");Join(c);Check(c.EquipCalls==1,"duplicate native add cannot re-equip");
   Leave(c);var other=Actor(12);other.Book=book;book.cachedEquipingCharacter=other;team.Members.Add(other);Join(c);
   Check(c.Book==null && other.Book==book && Field<TrainingBook>(p,"book").Records.Count==0,"occupied book is not stolen and record is consumed");
   other.Book=null;book.cachedEquipingCharacter=null;p.Tick();Check(c.Book==null,"later freeing book cannot trigger deferred restore");
   c.Book=book;Leave(c);team.TeamInventory.Items.Remove(501);Join(c);Check(c.Book==null,"lost book is not recreated");team.TeamInventory.Items[501]=book;
   foreach(var condition in new[]{GameCharacterInstance.EquipingCheckResult.PropNotMatch,GameCharacterInstance.EquipingCheckResult.KungfuMaxLevel,GameCharacterInstance.EquipingCheckResult.CharacterMaxKungfuCount}){
    c.Book=book;Leave(c);c.Eligibility=condition;var calls=c.EquipCalls;Join(c);Check(c.Book==null && c.EquipCalls==calls,"native training restriction blocks restoration: "+condition);
   }
   c.Eligibility=GameCharacterInstance.EquipingCheckResult.Ok;c.Book=book;Leave(c);var replacement=new GameItemInstance{TempleteId=502};c.Book=replacement;Join(c);Check(c.Book==replacement,"existing newly assigned book is preserved");
   c.Book=book;Leave(c);c.EquipSucceeds=false;Join(c);Check(c.Book==null && Field<TrainingBook>(p,"book").Records.Count==0,"failed native equip consumes record without retry");c.EquipSucceeds=true;
   c.Book=book;Leave(c);c.ThrowEquip=true;Join(c);Check(c.Book==null && Field<TrainingBook>(p,"book").Records.Count==0,"native exception is contained and cannot retry");c.ThrowEquip=false;
   c.Book=book;Leave(c);SaveNative(p);
   var store=new TrainingStore(Path.Combine(BepInEx.Paths.ConfigPath,"CompanionTraining"));Check(store.Load(slot).Records.Count==1,"snapshot persists after native sync even when IsSaving is false");
   Join(c);Patch("LoadSave","Prefix");SaveManager.Instance.onLoadFinished?.Invoke();Check(Field<TrainingBook>(p,"book").Records.Count==1,"manual load rolls back unsaved record consumption");
   p.Unload();p=new CompanionTraining();p.Load();p.Tick();Check(Field<TrainingBook>(p,"book").Records.Count==1,"restart loads only saved snapshot");
   // A finished serialization event without Sync must not create a snapshot.
   var unsynced=Path.Combine(dir,"account","ManualSave1");Directory.CreateDirectory(unsynced);File.WriteAllText(Path.Combine(unsynced,"SaveObjectGame.save"),"unsynced");SaveManager.SavePath=unsynced;
   SaveManager.Instance.onSaveFinished?.Invoke();Patch("Saved","Postfix",SaveManager.Instance.m_saveFile);Patch("Saved","Postfix",SaveManager.Instance.m_saveFileBackup);
   Check(!File.Exists(store.SnapshotPath(unsynced)),"idle save-finished notification cannot masquerade as a real save");
   SaveManager.Instance.IsSaving=true;SaveManager.Instance.onSaveFinished?.Invoke();SaveManager.Instance.IsSaving=false;Patch("Saved","Postfix",SaveManager.Instance.m_saveFileBackup);p.Tick();Check(store.Load(unsynced).Records.Count==0,"backup sync alone cannot commit an incomplete save");
   Patch("LoadSave","Prefix");SaveManager.Instance.onLoadFinished?.Invoke();Check(Field<TrainingBook>(p,"book").Records.Count==0,"different slot clears previous records");
   SaveManager.Instance.onReset?.Invoke();Check(Field<TrainingBook>(p,"book").Records.Count==0 && Field<bool>(p,"ready"),"new game starts empty and ready");
   team.Members.Remove(c);c.Book=book;team.Members.Add(c);Leave(c);
   SaveManager.Instance.IsSaving=true;SaveManager.Instance.onSaveFinished?.Invoke();SaveManager.Instance.IsSaving=false;Join(c);
   Patch("Saved","Postfix",new ES3File());Check(store.Load(unsynced).Records.Count==0,"unrelated file sync cannot commit snapshot");
   Patch("Saved","Postfix",SaveManager.Instance.m_saveFile);Patch("Saved","Postfix",SaveManager.Instance.m_saveFileBackup);
   Check(store.Load(unsynced).Records.Count==1 && Field<TrainingBook>(p,"book").Records.Count==0,"serialization snapshot cannot capture later unsaved return");
   team.Members.Remove(c);c.Book=book;team.Members.Add(c);Leave(c);SaveManager.Instance.onAccountChange?.Invoke();Check(!Field<bool>(p,"ready") && Field<TrainingBook>(p,"book").Records.Count==0,"account change immediately clears old account state");
   SaveManager.SavePath=slot;SaveManager.Instance.onLoadFinished?.Invoke();File.WriteAllText(store.SnapshotPath(slot),"broken");Patch("LoadSave","Prefix");SaveManager.Instance.onLoadFinished?.Invoke();
   Check(Field<bool>(p,"storageBlocked") && Field<TrainingBook>(p,"book").Records.Count==0,"corrupt snapshot disables restoration without overwriting file");SaveNative(p);Check(File.ReadAllText(store.SnapshotPath(slot))=="broken","corrupt snapshot remains intact on save");
   p.Unload();Check(SaveManager.Instance.onSaveFinished==null && SaveManager.Instance.onLoadFinished==null && SaveManager.Instance.onReset==null && SaveManager.Instance.onAccountChange==null,"unload removes all event subscriptions");
   BepInEx.Paths.ConfigPath=Path.Combine(dir,"failed-config");Directory.CreateDirectory(BepInEx.Paths.ConfigPath);File.WriteAllText(Path.Combine(BepInEx.Paths.ConfigPath,"CompanionTraining"),"keep user file");
   p=new CompanionTraining();p.Load();p.Tick();team.Members.Remove(c);c.Book=book;team.Members.Add(c);Leave(c);SaveNative(p);
   Check(Field<bool>(p,"storageBlocked") && File.ReadAllText(Path.Combine(BepInEx.Paths.ConfigPath,"CompanionTraining"))=="keep user file","native save continues when sidecar write fails and preserves obstruction");
   var callsAfterFailure=c.EquipCalls;Join(c);Check(c.Book==null && c.EquipCalls==callsAfterFailure,"write failure disables automatic restoration for current context");
   Console.WriteLine("CompanionTraining game boundary: "+checks+" checks passed.");
  }finally{p.Unload();Directory.Delete(dir,true);}
 }
}
