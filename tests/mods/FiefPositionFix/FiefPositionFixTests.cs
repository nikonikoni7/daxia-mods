using System;
using System.Reflection;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using WuLin;
using WuLin.StateMachine;
using System.IO;
using System.Text.Json;
static class FiefPositionFixTests {
 static void Check(bool ok,string label){if(!ok)throw new Exception("FAIL: "+label);Console.WriteLine("PASS: "+label);}
 static void Main(){
  var type=Assembly.GetExecutingAssembly().GetType("DaxiaMods.FiefPositionFix");
  Check(type!=null,"FiefPositionFix exists");
  var plugin=(BasePlugin)Activator.CreateInstance(type!)!;plugin.Load();
  var map=new UIWorldMap();
  foreach(var graph in new[]{"EJ-003-38","EJ-003-40"}){
   var node=new EnterMapNode{graph=new(){name=graph},mapID=1002};
   DllScenarioDelegate.EnterMapNodeStartTeleport(node,n=>Check(n.moveWorldTag&&n.TagID==5025&&n.mapID==1002,"DLC ending returns to Zhongzhou ferry: "+graph));
   Check(!node.moveWorldTag&&node.TagID==0,"scenario metadata restored after native teleport: "+graph);
   try{DllScenarioDelegate.EnterMapNodeStartTeleport(node,n=>throw new InvalidOperationException());}catch(InvalidOperationException){}
   Check(!node.moveWorldTag&&node.TagID==0,"failed teleport restores original node metadata");
   DllScenarioDelegate.EnterMapNodeStartTeleport(node,n=>{
    DllScenarioDelegate.EnterMapNodeStartTeleport(n,inner=>Check(inner.moveWorldTag&&inner.TagID==5025,"nested native call keeps ferry destination"));
    Check(n.moveWorldTag&&n.TagID==5025,"nested return keeps outer repair until completed");
   });
  }
  foreach(var node in new[]{
   new EnterMapNode{graph=new(){name="EJ-003-37"},mapID=1002},
   new EnterMapNode{graph=new(){name="EJ-003-38"},mapID=5002},
   new EnterMapNode{graph=new(){name="EJ-003-38"},mapID=1002,TagID=5001},
   new EnterMapNode{graph=new(){name="EJ-003-38"},mapID=1002,moveWorldTag=true},
   new EnterMapNode{graph=new(){name="EJ-003-38"},mapID=1002,pos=new(){x=1}},
   new EnterMapNode{mapID=1002}}){
   var old=(node.moveWorldTag,node.TagID,node.mapID);
   DllScenarioDelegate.EnterMapNodeStartTeleport(node,n=>Check((n.moveWorldTag,n.TagID,n.mapID)==old,"unrelated or explicitly configured teleport retained"));
  }
  var disabledNode=new EnterMapNode{graph=new(){name="EJ-003-38"},mapID=1002};
  plugin.Config.Enabled.Value=false;
  DllScenarioDelegate.EnterMapNodeStartTeleport(disabledNode,n=>Check(!n.moveWorldTag&&n.TagID==0,"disabled DLC fix keeps native destination policy"));
  plugin.Config.Enabled.Value=true;
  var recoveryRoot=Path.Combine(Path.GetTempPath(),"DaxiaPositionTests-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(recoveryRoot);
  try{
   BepInEx.Paths.ConfigPath=recoveryRoot;SaveManager.FullPathForAllSaveFolder=recoveryRoot;
   SaveManager.SavePath="Save0";var slot=Path.Combine(recoveryRoot,"Save0");Directory.CreateDirectory(slot);
   var save=Path.Combine(slot,"Map.save");File.WriteAllText(save,"checkpoint");
   var record=Path.Combine(recoveryRoot,"FiefPositionFix","dlc-ferry-recovery.json");Directory.CreateDirectory(Path.GetDirectoryName(record)!);
   File.WriteAllText(record,JsonSerializer.Serialize(new{Schema=1,SlotPath=slot,Files=new[]{new{Name="Map.save",Sha256="47320987F9A49D5B00119B960F247A956773F57543982B8BFCB6DA5BB3AFD9EF"}}}));
   var recoveryManager=new UIWorldMapManager();var ferryMap=new UIWorldMap();recoveryManager.listWorldMap[1002]=ferryMap;
   void ResetMark()=>recoveryManager.Marks[1002]=new(){markMapID=5001,isStandOn=true};
   void Load()=>recoveryManager.OnGameLoaded(()=>recoveryManager.initTarget());
   ResetMark();Load();Check(ferryMap.Position==5025&&recoveryManager.Marks[1002].markMapID==5025,"exact bad checkpoint restores ferry in memory during real load only");
   Check(File.ReadAllText(save)=="checkpoint","checkpoint recovery never writes original save bytes");
   ResetMark();recoveryManager.initTarget();Check(ferryMap.Position==5001,"new game or ordinary UI refresh cannot invoke recovery");
   foreach(var which in new[]{"changed","extra","wrong-slot","disabled","missing-icon","other-map","other-anchor"}){
    ResetMark();
    if(which=="changed")File.WriteAllText(save,"subsequent progress");
    if(which=="extra")File.WriteAllText(Path.Combine(slot,"New.save"),"extra");
    if(which=="wrong-slot")SaveManager.SavePath="Save1";
    if(which=="disabled")plugin.Config.Enabled.Value=false;
    if(which=="missing-icon")ferryMap.HasIcon=false;
    if(which=="other-map")PlayerTeamManager.Instance.CurMapId=20100;
    if(which=="other-anchor")recoveryManager.Marks[1002].markMapID=200;
    Load();Check(recoveryManager.Marks[1002].markMapID==(which=="other-anchor"?200:5001),"recovery refuses "+which);
    File.WriteAllText(save,"checkpoint");File.Delete(Path.Combine(slot,"New.save"));SaveManager.SavePath="Save0";plugin.Config.Enabled.Value=true;ferryMap.HasIcon=true;PlayerTeamManager.Instance.CurMapId=1002;
   }
   File.WriteAllText(record,"{broken");ResetMark();Load();Check(ferryMap.Position==5001,"corrupt recovery record leaves original loaded position intact");
  }finally{Directory.Delete(recoveryRoot,true);}
  // Three native home positioning calls model menu open, entry and exit.
  foreach(var origin in new[]{-99,1234,777}){
   map.Position=origin;map.SetPos(500);map.SetPos(500);map.SetPos(500);
   Check(map.Position==origin,"world-map shortcut entry/exit preserves origin "+origin);
  }
  var manager=new UIWorldMapManager();
  void Restore(Action action)=>manager.OnGameLoaded(action);
  map.Position=-99;Restore(()=>map.SetPos(500));
  Check(map.Position==500,"saved home anchor restores coordinate after UI reset");
  Check(MapManager.Instance.IsFiefMainMapID(500),"home classification remains native outside SetPos");
  map.Position=-99;map.SetPos(200);
  Check(map.Position==200,"ordinary city placement preserved");
  map.Position=-99;map.HasIcon=false;Restore(()=>map.SetPos(500));
  Check(map.Position==-99,"missing icon leaves native branch intact");map.HasIcon=true;
  plugin.Config.Enabled.Value=false;Restore(()=>map.SetPos(500));
  Check(map.Position==-99,"disabled preserves native behavior");plugin.Config.Enabled.Value=true;
  map.Throw=true;try{Restore(()=>map.SetPos(500));}catch(InvalidOperationException){}
  Check(MapManager.Instance.IsFiefMainMapID(500),"native exception clears scoped override");map.Throw=false;
  map.Position=456;map.SetPos(500);Check(map.Position==456,"failed load cannot leak into next shortcut");
  map.Nested=true;Restore(()=>map.SetPos(500));
  Check(map.OuterScopeRestored,"nested ordinary SetPos preserves outer home scope");
  Check(MapManager.Instance.IsFiefMainMapID(500),"nested exit clears scope");
  map.Nested=false;
  map.Position=321;map.SetPos(500);Check(map.Position==321,"successful load cannot leak into next shortcut");
  Restore(()=>Restore(()=>map.SetPos(500)));Check(map.Position==500,"nested load restores home");
  map.Position=654;map.SetPos(500);Check(map.Position==654,"nested load restores outer scope on exit");
  Restore(()=>map.SetPos(200));Check(map.Position==200,"ordinary anchors also restore on load");
  plugin.Unload();map.Position=-99;Restore(()=>map.SetPos(500));
  Check(map.Position==-99,"unload restores native behavior");
  Console.WriteLine("FiefPositionFix interface regression passed; native positioning verified separately.");
 }
}
