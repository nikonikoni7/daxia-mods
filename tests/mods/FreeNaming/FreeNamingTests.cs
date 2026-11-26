using System;
using BepInEx.Unity.IL2CPP;
using DaxiaMods;
using HarmonyLib;
using WuLin;
static class FreeNamingTests {
 static void Check(bool value,string label){if(!value)throw new Exception("FAIL: "+label);Console.WriteLine("PASS: "+label);}
 static BasePlugin NewPlugin(){var type=typeof(FreeNamingTests).Assembly.GetType("DaxiaMods.FreeNaming");Check(type!=null,"FreeNaming plugin exists");return (BasePlugin)Activator.CreateInstance(type!)!;}
 static void Main(){
  var plugin=NewPlugin();plugin.Load();
  Check(Harmony.Prefix!=null,"sensitive-word detector patched");
  foreach(var text in new[]{"测试禁词","正常姓名","前测试禁词后","", "姓测试禁词名"})
   Check(!GameUtil.ContainsForbiddenWord(text),"enabled accepts input: "+text);
  Check(GameUtil.OriginalCalls==0,"enabled skips original detector");
  plugin.Config.Enabled.Value=false;
  Check(GameUtil.ContainsForbiddenWord("测试禁词"),"disabled restores original rejection");
  Check(!GameUtil.ContainsForbiddenWord("正常姓名"),"disabled retains normal names");
  Check(GameUtil.OriginalCalls==2,"disabled invokes original detector");
  plugin.Config.Enabled.Value=true;
  Check(!GameUtil.ContainsForbiddenWord("测试禁词"),"can enable again");
  Check(plugin.Unload(),"unload succeeds");
  Check(Harmony.Prefix==null && GameUtil.ContainsForbiddenWord("测试禁词"),"unload restores original detector");
  BuildGuard.Allowed=false;plugin=NewPlugin();plugin.Load();
  Check(Harmony.Prefix==null && GameUtil.ContainsForbiddenWord("测试禁词"),"version mismatch leaves original behavior");
  BuildGuard.Allowed=true;Harmony.ThrowOnPatch=true;plugin=NewPlugin();plugin.Load();
  Check(Harmony.Prefix==null && GameUtil.ContainsForbiddenWord("测试禁词"),"partial patch failure rolls back");
  Harmony.ThrowOnPatch=false;
  Console.WriteLine("FreeNaming interface simulation passed; real IL2CPP and UI acceptance are separate.");
 }
}
