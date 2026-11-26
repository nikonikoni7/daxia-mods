using System;
using System.Reflection;
using BepInEx.Unity.IL2CPP;
using DaxiaMods;
using HarmonyLib;
using WuLin;
static class FiefReuseTests {
 static int checks;
 static void Check(bool value,string label){if(!value)throw new Exception("FAIL: "+label);checks++;Console.WriteLine("PASS: "+label);}
 static BasePlugin NewPlugin(){var t=typeof(FiefReuseTests).Assembly.GetType("DaxiaMods.FiefReuse");Check(t!=null,"FiefReuse plugin exists");return (BasePlugin)Activator.CreateInstance(t!)!;}
 static void Main(){
  var plugin=NewPlugin();plugin.Load();
  var shared=new FiefUnsharedData();shared.takenItems.Add(0);shared.takenKungfuBook.Add(0);
  Check(!shared.IsFiefItemTaken(0),"shared equipment can be taken again");
  Check(!shared.IsFiefKungfuBookTaken(0),"shared inscription can be rubbed again");
  var another=new FiefUnsharedData();another.takenItems.Add(7);another.takenKungfuBook.Add(4);
  Check(!another.IsFiefItemTaken(7)&&!another.IsFiefKungfuBookTaken(4),"another destination save also unlocks last slots");
  var privateHome=new FiefUnsharedData{isPrivateFief=true};privateHome.takenItems.Add(0);privateHome.takenKungfuBook.Add(0);
  Check(privateHome.IsFiefItemTaken(0)&&privateHome.IsFiefKungfuBookTaken(0),"private home retains both native limits");
  Check(!privateHome.IsFiefItemTaken(1)&&!privateHome.IsFiefKungfuBookTaken(1),"unused private slots retain native behavior");
  Check(shared.takenItems.Count==1&&shared.takenKungfuBook.Count==1,"unlock does not erase saved claim history");
  plugin.Config.Enabled.Value=false;
  Check(shared.IsFiefItemTaken(0)&&shared.IsFiefKungfuBookTaken(0),"disabled restores both original limits");
  Check(!shared.IsFiefItemTaken(1)&&!shared.IsFiefKungfuBookTaken(1),"disabled retains unused slots");
  plugin.Config.Enabled.Value=true;
  Check(!shared.IsFiefItemTaken(0)&&!shared.IsFiefKungfuBookTaken(0),"reenabling unlocks existing claims");
  plugin.Unload();
  Check(shared.IsFiefItemTaken(0)&&shared.IsFiefKungfuBookTaken(0),"unload restores native history checks");
  BuildGuard.Allowed=false;plugin=NewPlugin();plugin.Load();
  Check(shared.IsFiefItemTaken(0)&&shared.IsFiefKungfuBookTaken(0),"wrong build leaves both original limits");
  BuildGuard.Allowed=true;Harmony.ThrowOnPatch=true;plugin=NewPlugin();plugin.Load();
  Check(shared.IsFiefItemTaken(0)&&shared.IsFiefKungfuBookTaken(0),"partial patch failure rolls back both patches");
  Harmony.ThrowOnPatch=false;
  Console.WriteLine("FiefReuse interface checks passed: "+checks+". Native transfers and cross-save UI acceptance are separate.");
 }
}
