// Exercise the real plugin against scene/UI/save boundaries without launching Unity or writing game saves.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DaxiaMods;
using UnityEngine;
using WuLin;
using WuLin.StateMachine;
static class ChestMemoGameTests {
 static int checks;
 static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);checks++;Console.WriteLine("PASS: "+name);}
 static object? Call(object o,string name,params object?[] args)=>o.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(o,args);
 static object? Patch(string type,string name,params object?[] args)=>typeof(ChestMemo).GetNestedType(type,BindingFlags.NonPublic)!.GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,args);
 static T Field<T>(object obj,string field)=>(T)obj.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(obj)!;
 static int Main(){var dir=Path.Combine(Path.GetTempPath(),"ChestMemoGameTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  try{Run(dir);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{ChestMemo.Instance?.Unload();Directory.Delete(dir,true);}}
 static void Run(string dir){
  IsolationRegression(dir);
  BepInEx.Paths.ConfigPath=Path.Combine(dir,"config");var slot=Path.Combine(dir,"ManualSave0");Directory.CreateDirectory(slot);File.WriteAllText(Path.Combine(slot,"SaveObjectGame.save"),"save version one");SaveManager.SavePath=slot;
  var plugin=new ChestMemo();plugin.Load();Patch("LoadSave","Prefix");SaveManager.Instance.onLoadFinished?.Invoke();plugin.Tick();
  Check(UnityEngine.Object.SceneScans==0,"empty memo never enumerates every scene object");
  var diagnostics=typeof(ChestMemo).GetMethod("ReportTiming",BindingFlags.NonPublic|BindingFlags.Instance);
  Check(diagnostics!=null,"slow native operations expose bounded timing diagnostics");
  var logged=BepInEx.Unity.IL2CPP.Log.Warnings;
  diagnostics!.Invoke(plugin,new object[]{"NPC立绘",0.5});
  Check(BepInEx.Unity.IL2CPP.Log.Warnings==logged,"fast native operation does not write a timing log");
  diagnostics.Invoke(plugin,new object[]{"NPC立绘",1500.0});diagnostics.Invoke(plugin,new object[]{"NPC立绘",1500.0});
  Check(BepInEx.Unity.IL2CPP.Log.Warnings==logged+1,"repeated slow operation logs are rate limited");
  var chest=new InteractiveObject{SaveID=44,mTempID=9,m_pos=new(1,2,0)};
  chest.Conditions.Add(new(){Data=new(){new(){ConditionType=MapConst.BuildingOpenCondition.ITEM,Param0=101,Param1=2}}});
  chest.Conditions.Add(new(){Data=new(){new(){ConditionType=MapConst.BuildingOpenCondition.ITEM,Param0=102,Param1=1}}});
  var state=new InteractiveObjectStateMachineData{isChest=true,interactiveObject=chest,openItemRequire=new(){{101,2}}};
  var data=new StateMachineDataContainer{State=state};
  object?[] capture={data,null};Patch("CostAttempt","Prefix",capture);Patch("CostAttempt","Postfix",capture[1]);
  var book=Field<ChestBook>(plugin,"book");Check(book.Records.Count==1,"actual cost-action hook records failed attempt");
  var scans=UnityEngine.Object.SceneScans;
  for(int i=0;i<120;i++){Time.unscaledTime+=3;Time.frameCount++;plugin.Tick();}
  Check(UnityEngine.Object.SceneScans==scans,"closed memo does not repeatedly scan scene during six minutes of exploration");
  RoamingManager.IsInStory=true;MapManager.Instance.Map=2;Time.unscaledTime+=3;plugin.Tick();
  Check(UnityEngine.Object.SceneScans==scans,"dialogue does not trigger memo scene scans");
  RoamingManager.IsInStory=false;MapManager.Instance.Map=1;
  Check(ChestBook.RequirementText(book.Records[0])=="（铜钥匙×2）或（银钥匙×1）","adapter keeps OR key routes and names");
  Patch("CostAttempt","Postfix",capture[1]);Check(book.Records.Count==1,"hook duplicate stays one record");
  PlayerTeamManager.Instance.TeamInventory.Counts[101]=2;capture=new object?[]{data,null};Patch("CostAttempt","Prefix",capture);Check(capture[1]==null,"sufficient-key confirmation cancellation has no failure candidate");
  PlayerTeamManager.Instance.TeamInventory.Counts.Clear();state.isChest=false;capture=new object?[]{data,null};Patch("CostAttempt","Prefix",capture);Check(capture[1]==null,"game adapter excludes non-chest objects");state.isChest=true;
  var ui=new UIThumbnail();var prefab=new GameObject("SceneMap");var calibration=prefab.AddComponent<UIThumbnailEditor>();calibration.ScenePos1=new(0,0);calibration.ScenePos2=new(3,4);
  calibration.MapPos1=new RectTransform{localPosition=new(10,20,0)};calibration.MapPos2=new RectTransform{localPosition=new(25,40,0)};ui.CurMap=prefab;
  GameData.BaseTables.Maps.mapToItemDict["1"]=201;
  Patch("ShowMap","Postfix",ui,"1");Check(Field<GameObject?>(plugin,"dotLayer")==null,"map opening alone cannot bypass ownership");
  PlayerTeamManager.Instance.TeamInventory.Counts[201]=1;plugin.Tick();
  var layer=Field<GameObject>(plugin,"dotLayer");var dot=layer.transform.Children.Single().gameObject;var image=dot.GetComponent<UnityEngine.UI.Image>();
  Check(image!=null && image.color.r==1 && image.color.g==0 && image.color.b==0 && !image.raycastTarget,"dot is red and cannot intercept map clicks");
  Check(dot.GetComponent<TMPro.TMP_Text>()==null && dot.transform.Children.Count==0,"map dot carries no text or tooltip children");
  Check(layer.transform.parent==prefab.transform,"dot layer inherits native map transform and clipping ancestors");
  Check(dot.transform.localPosition.x==15 && dot.transform.localPosition.y==30,"actual scene calibration positions the dot");
  PlayerTeamManager.Instance.TeamInventory.ThrowGetItem=true;plugin.Tick();Check(Field<GameObject?>(plugin,"dotLayer")==null,"unverifiable ownership hides previously displayed dots");PlayerTeamManager.Instance.TeamInventory.ThrowGetItem=false;
  PlayerTeamManager.Instance.TeamInventory.Counts.Remove(201);plugin.Tick();Check(Field<GameObject?>(plugin,"dotLayer")==null,"losing map hides dots");
  PlayerTeamManager.Instance.TeamInventory.Counts[201]=1;Patch("ShowMap","Postfix",ui,"2");Check(Field<GameObject?>(plugin,"dotLayer")==null,"viewing a different map hides dots");
  Patch("ShowMap","Postfix",ui,"1");GameManager.Instance.IsOnWorldMap=true;plugin.Tick();Check(Field<GameObject?>(plugin,"dotLayer")==null,"world map transition clears dots");GameManager.Instance.IsOnWorldMap=false;
  Patch("ShowMap","Postfix",ui,"1");Patch("HideMap","Prefix");Check(Field<GameObject?>(plugin,"dotLayer")==null,"closing scene map clears markers");
  Input.Pressed=KeyCode.L;Time.frameCount++;Check(!plugin.Keys(),"L opens memo and consumes native hotkey");
  Check(!GameManager.ControlsEnabled && GameTimer.Instance.Scale==0,"memo blocks world controls and pauses time");
  var memo=Field<GameObject>(plugin,"memoRoot");var labels=memo.GetComponentsInChildren<TMPro.TMP_Text>();
  Check(labels.Any(t=>t.text.Contains("迷踪林：") && t.text.Contains("铜钥匙×2")),"memo shows map and complete key requirements");
  Check(!labels.Any(t=>t.text.Contains("齐备") || t.text.Contains("坐标") || t.text.Contains("距离")),"memo omits removed data fields");
  Input.Pressed=KeyCode.None;Time.frameCount++;Call(plugin,"ChangeGrouping",MemoGrouping.Key);
  Check(Field<BepInEx.Configuration.ConfigEntry<MemoGrouping>>(plugin,"grouping").Value==MemoGrouping.Key,"classification preference changes through actual UI handler");
  Input.Pressed=KeyCode.L;Time.frameCount++;Check(!plugin.Keys(),"L closes memo and consumes native hotkey");
  Check(!plugin.Keys(),"watcher and native hotkey handler both consume the same closing key frame");
  Check(GameManager.ControlsEnabled && GameTimer.Instance.Scale==1,"closing restores original control and time");
  Input.Pressed=KeyCode.None;Time.frameCount++;Input.Pressed=KeyCode.L;plugin.Keys();Application.isFocused=false;plugin.Tick();Check(!Field<bool>(plugin,"visible") && GameManager.ControlsEnabled,"focus loss releases memo pause");Application.isFocused=true;Input.Pressed=KeyCode.None;
  chest.Picked=true;Patch("Pick","Postfix",chest);Check(book.Records.Count==0,"native picked confirmation removes record");
  chest.Picked=false;capture=new object?[]{data,null};Patch("CostAttempt","Prefix",capture);Patch("CostAttempt","Postfix",capture[1]);
  NativeSaveComplete();plugin.Tick();
  Check(new ChestStore(Path.Combine(BepInEx.Paths.ConfigPath,"ChestMemo")).LoadSnapshot(slot).Records.Count==1,"successful native sync attaches snapshot");
  book.RecordAttempt(true,false,true,true,new ChestRecord{MapId=1,SaveId=46,TemplateId=9,MapName="迷踪林",Routes=new(){new(){Keys=new(){new(){Id=101,Name="铜钥匙",Count=2}}}}});Call(plugin,"Persist");
  Call(plugin,"BeforeLoad");SaveManager.Instance.onLoadFinished?.Invoke();Check(Field<ChestBook>(plugin,"book").Records.Count==1,"explicit load restores matching snapshot");
  var loadedBook=Field<ChestBook>(plugin,"book");var additional=new ChestRecord{MapId=1,SaveId=45,TemplateId=9,MapName="迷踪林",Routes=new(){new(){Keys=new(){new(){Id=101,Name="铜钥匙",Count=2}}}}};
  loadedBook.RecordAttempt(true,false,true,true,additional);Call(plugin,"Persist");Call(plugin,"BeforeLoad");SaveManager.Instance.onLoadFinished?.Invoke();
  Check(Field<ChestBook>(plugin,"book").Records.Count==1,"manual rollback loads snapshot instead of later working discoveries");
  var missing=Path.Combine(dir,"ManualSave1");Directory.CreateDirectory(missing);File.WriteAllText(Path.Combine(missing,"SaveObjectGame.save"),"different save");
  Call(plugin,"BeforeLoad");SaveManager.SavePath=missing;SaveManager.Instance.onLoadFinished?.Invoke();Check(Field<ChestBook>(plugin,"book").Records.Count==0,"another slot has no inherited records");
  SaveManager.Instance.onReset?.Invoke();Check(Field<string?>(plugin,"activePath")==null && Field<ChestBook>(plugin,"book").Records.Count==0,"new game starts detached and empty");
  plugin.Unload();Check(SaveManager.Instance.onLoadFinished==null && SaveManager.Instance.onReset==null,"unload removes event callbacks");
  var newSlot=Path.Combine(dir,"new-game-slot");Directory.CreateDirectory(newSlot);File.WriteAllText(Path.Combine(newSlot,"SaveObjectGame.save"),"new game");SaveManager.SavePath=newSlot;
  RoamingManager.IsRoamingMode=false;var fresh=new ChestMemo();fresh.Load();fresh.Tick();SaveManager.Instance.onReset?.Invoke();RoamingManager.IsRoamingMode=true;
  capture=new object?[]{data,null};Patch("CostAttempt","Prefix",capture);Patch("CostAttempt","Postfix",capture[1]);
  NativeSaveComplete();fresh.Tick();
  Field<ChestBook>(fresh,"book").RecordAttempt(true,false,true,true,additional);Call(fresh,"Persist");
  Call(fresh,"BeforeLoad");SaveManager.Instance.onLoadFinished?.Invoke();
  Check(Field<ChestBook>(fresh,"book").Records.Count==1,"first manual rollback after starting a new game restores saved snapshot");fresh.Unload();
  RelativeSaveLifecycle(dir);
  RetryBudget(dir);
  Console.WriteLine("ChestMemo game-boundary checks passed: "+checks);
 }
 static void OptionalPatch(string type,string name)=>typeof(ChestMemo).GetNestedType(type,BindingFlags.NonPublic)?.GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static)?.Invoke(null,null);
 static ChestRecord KnownChest(int id,string map)=>new(){MapId=id,SaveId=id,X=1,Y=2,MapName=map,Routes=new(){new(){Keys=new(){new(){Id=101,Name="钥匙",Count=1}}}}};
 static void NativeSaveReady(){SaveManager.Instance.IsSaving=true;SaveManager.Instance.onSaveFinished?.Invoke();SaveManager.Instance.IsSaving=false;}
 static void NativeSaveComplete(){NativeSaveReady();Patch("Saved","Postfix",SaveManager.Instance.m_saveFile);Patch("Saved","Postfix",SaveManager.Instance.m_saveFileBackup);}
 static void IsolationRegression(string dir){
  var context=Path.Combine(dir,"isolation");var old=Path.Combine(context,"old-slot");var fresh=Path.Combine(context,"new-slot");Directory.CreateDirectory(old);Directory.CreateDirectory(fresh);
  File.WriteAllText(Path.Combine(old,"SaveObjectGame.save"),"old character");File.WriteAllText(Path.Combine(fresh,"SaveObjectGame.save"),"new character");
  BepInEx.Paths.ConfigPath=Path.Combine(context,"config");SaveManager.Instance=new();SaveManager.SavePath=old;RoamingManager.IsRoamingMode=false;
  var disk=new ChestStore(Path.Combine(BepInEx.Paths.ConfigPath,"ChestMemo"));var oldBook=new ChestBook();oldBook.Records.Add(KnownChest(2140,"燕子窝密室"));disk.SaveCheckpoint(old,oldBook);
  var p=new ChestMemo();p.Load();p.Tick();RoamingManager.IsRoamingMode=true;p.Tick();
  Check(!Field<bool>(p,"ready") && Field<ChestBook>(p,"book").Records.Count==0,"exploration flag alone cannot restore previous slot before a verified load");
  Patch("LoadSave","Prefix");SaveManager.Instance.onLoadFinished?.Invoke();
  Check(Field<ChestBook>(p,"book").Records.Count==1,"isolation fixture loads legitimate old-character chest");
  // Native StartNewGame does not promise to fire SaveManager.onReset; SavePath may still point at the old slot.
  OptionalPatch("StartGame","Prefix");RoamingManager.IsRoamingMode=true;p.Tick();
  Check(Field<ChestBook>(p,"book").Records.Count==0 && Field<string?>(p,"activePath")==null,"native new game detaches old slot even without SaveManager reset event");
  Field<ChestBook>(p,"book").Records.Add(KnownChest(1102,"村长家密室"));Call(p,"Persist");
  Check(disk.LoadWorking(old).Records.Count==1 && disk.LoadWorking(old).Records[0].MapId==2140,"unsaved new-game discovery never writes to stale old SavePath");
  SaveManager.SavePath=fresh;NativeSaveComplete();
  File.WriteAllText(Path.Combine(fresh,"SaveObjectGame.save"),"slot replaced after completed Sync before watcher");p.Tick();
  Check(disk.LoadSnapshot(fresh).Records.Count==0,"deferred checkpoint cannot bind captured book to later overwritten slot contents");
  File.WriteAllText(Path.Combine(fresh,"SaveObjectGame.save"),"new character");
  Check(disk.LoadSnapshot(fresh).Records.Count==1 && disk.LoadSnapshot(fresh).Records[0].MapId==1102,"new-game save contains only its own chest after IsSaving clears before Sync");
  // Simulate native overwrite before the loading callback: ongoing discoveries must retain the loaded identity.
  File.WriteAllText(Path.Combine(fresh,"SaveObjectGame.save"),"overwritten unrelated character before load");Call(p,"Persist");
  Check(disk.LoadWorking(fresh).Records.Count==0,"working writes remain pinned to loaded checkpoint when slot bytes change");
  File.WriteAllText(Path.Combine(fresh,"SaveObjectGame.save"),"new character");
  NativeSaveReady();Field<ChestBook>(p,"book").Records[0].Routes[0].Keys[0].Count=7;
  Field<ChestBook>(p,"book").Records.Add(KnownChest(9,"后续未保存发现"));Call(p,"Persist");
  Patch("Saved","Postfix",SaveManager.Instance.m_saveFile);Patch("Saved","Postfix",SaveManager.Instance.m_saveFileBackup);p.Tick();
  Check(disk.LoadSnapshot(fresh).Records.Count==1 && disk.LoadWorking(fresh).Records.Count==2,"native save freezes checkpoint while later discoveries stay working-only");
  Check(disk.LoadSnapshot(fresh).Records[0].Routes[0].Keys[0].Count==1 && disk.LoadWorking(fresh).Records[0].Routes[0].Keys[0].Count==7,"save checkpoint deeply freezes key conditions rather than sharing lists");
  // A delayed callback from A must not snapshot B's live book into A.
  NativeSaveReady();Patch("LoadSave","Prefix");SaveManager.SavePath=old;SaveManager.Instance.onLoadFinished?.Invoke();
  Patch("Saved","Postfix",SaveManager.Instance.m_saveFile);Patch("Saved","Postfix",SaveManager.Instance.m_saveFileBackup);p.Tick();
  Check(disk.LoadSnapshot(fresh).Records.Count==1 && Field<ChestBook>(p,"book").Records[0].MapId==2140,"old save callbacks cannot cross a load generation");
  OptionalPatch("LeaveGame","Prefix");RoamingManager.IsRoamingMode=false;p.Tick();RoamingManager.IsRoamingMode=true;p.Tick();
  Check(!Field<bool>(p,"ready") && Field<ChestBook>(p,"book").Records.Count==0,"menu transition cannot blindly restore stale slot while creating a new character");
  OptionalPatch("StartGame","Prefix");SaveManager.Instance.onAccountChange?.Invoke();p.Tick();
  Check(!Field<bool>(p,"ready") && Field<ChestBook>(p,"book").Records.Count==0,"account transition detaches records and pending writes");
  OptionalPatch("StartGame","Prefix");SaveManager.Instance.destroyed=true;SaveManager.Instance=new();SaveManager.SavePath=old;p.Tick();
  Check(!Field<bool>(p,"ready") && Field<ChestBook>(p,"book").Records.Count==0,"replacement save manager cannot inherit previous runtime context");
  p.Unload();SaveManager.Instance=new();RoamingManager.IsRoamingMode=true;
 }
 static void RetryBudget(string dir){
  BepInEx.Paths.ConfigPath=Path.Combine(dir,"retry-config");SaveManager.Instance=new();
  SaveManager.SavePath=Path.Combine(dir,"missing-retry-slot");RoamingManager.IsRoamingMode=false;
  var plugin=new ChestMemo();plugin.Load();plugin.Tick();SaveManager.Instance.onReset?.Invoke();
  NativeSaveComplete();
  plugin.Tick();var errors=BepInEx.Unity.IL2CPP.Log.Errors;
  for(int i=0;i<120;i++){Time.frameCount++;Time.unscaledTime+=.01f;plugin.Tick();}
  Check(BepInEx.Unity.IL2CPP.Log.Errors==errors,"unreadable snapshot does not retry disk IO and log exceptions every frame");
  plugin.Unload();RoamingManager.IsRoamingMode=true;
 }
 static void RelativeSaveLifecycle(string dir){
  // The native SavePath is account/slot, relative to FullPathForAllSaveFolder, not the process directory.
  var root=Path.Combine(dir,"persistent");var relative=Path.Combine("account","QuickSave0");var slot=Path.Combine(root,relative);
  Directory.CreateDirectory(slot);var gameFile=Path.Combine(slot,"SaveObjectGame.save");File.WriteAllText(gameFile,"relative save v1");
  SaveManager.FullPathForAllSaveFolder=root;SaveManager.SavePath=relative;SaveManager.Instance=new();
  var memoPath=Path.Combine(dir,"relative-memo");BepInEx.Paths.ConfigPath=memoPath;
  var expected=new ChestBook();expected.RecordAttempt(true,false,true,true,new(){MapId=1,SaveId=88,MapName="迷踪林",Routes=new(){new(){Keys=new(){new(){Id=101,Name="铜钥匙",Count=2}}}}});
  var disk=new ChestStore(Path.Combine(memoPath,"ChestMemo"));disk.SaveSnapshot(slot,expected);
  RoamingManager.IsRoamingMode=false;var plugin=new ChestMemo();plugin.Load();plugin.Tick();
  Patch("LoadSave","Prefix");SaveManager.Instance.onReset?.Invoke();RoamingManager.IsRoamingMode=true;SaveManager.Instance.onLoadFinished?.Invoke();
  Check(Field<ChestBook>(plugin,"book").Records.Count==1,"native relative SavePath restores fingerprinted records from persistent root");
  Check(Field<string?>(plugin,"activePath")==slot,"active journal binds to absolute native slot");
  File.WriteAllText(gameFile,"relative save v2");NativeSaveComplete();plugin.Tick();
  Check(disk.LoadSnapshot(slot).Records.Count==1,"quick save with changed payload rebinds record snapshot under native root");
  Patch("LoadSave","Prefix");SaveManager.Instance.onReset?.Invoke();SaveManager.Instance.onLoadFinished?.Invoke();
  Check(Field<ChestBook>(plugin,"book").Records.Count==1,"in-session read of native relative slot retains saved records");plugin.Unload();
  var restarted=new ChestMemo();restarted.Load();Patch("LoadSave","Prefix");SaveManager.Instance.onLoadFinished?.Invoke();restarted.Tick();
  Check(Field<ChestBook>(restarted,"book").Records.Count==1,"plugin restart restores native relative slot records");
  File.WriteAllText(gameFile,"relative save v3");NativeSaveComplete();
  Patch("LoadSave","Prefix");SaveManager.Instance.onReset?.Invoke();SaveManager.Instance.onLoadFinished?.Invoke();
  Check(Field<ChestBook>(restarted,"book").Records.Count==1,"immediate load before watcher tick commits successful save snapshot");
  var delayed=Path.Combine("account","AutoSave0");SaveManager.SavePath=delayed;NativeSaveComplete();restarted.Tick();
  Check(Field<string?>(restarted,"saveCompletedPath")!=null,"transient snapshot path failure preserves retry and in-memory records");
  var auto=Path.Combine(root,delayed);Directory.CreateDirectory(auto);File.WriteAllText(Path.Combine(auto,"SaveObjectGame.save"),"relative auto");Time.unscaledTime+=5;restarted.Tick();
  Check(disk.LoadSnapshot(auto).Records.Count==1,"failed snapshot retries once native files become readable");
  File.WriteAllText(Path.Combine(auto,"SaveObjectGame.save"),"different run overwrote auto slot");Patch("LoadSave","Prefix");SaveManager.Instance.onLoadFinished?.Invoke();
  Check(Field<ChestBook>(restarted,"book").Records.Count==0,"relative slot overwrite still cannot inherit unrelated records");restarted.Unload();
 }
}
