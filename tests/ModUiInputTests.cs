using System;
using System.Reflection;
using DaxiaMods;
using UnityEngine;
using WuLin;
static class ModUiInputTests {
 static int Main(){
  int failed=0,checks=0;
  void Check(bool ok,string name){checks++;Console.WriteLine((ok?"PASS: ":"FAIL: ")+name);if(!ok)failed++;}
  var menu=typeof(ChestMemo).GetMethod("MenuOpen",BindingFlags.Static|BindingFlags.NonPublic)!;
  bool Blocked()=>(bool)menu.Invoke(null,null)!;
  var hidden=new GameObject("hidden menu");var hiddenUi=hidden.AddComponent<UiFunctionGroup>();hidden.activeInHierarchy=false;
  UiFunctionGroup.displayingUiStack.Add(hiddenUi);Check(!Blocked(),"inactive menu left in native stack does not block L");
  UiFunctionGroup.displayingUiStack.Clear();
  var map=new GameObject("scene map");map.AddComponent<UIThumbnail>();UiFunctionGroup.displayingUiStack.Add(map.AddComponent<UiFunctionGroup>());
  Check(ModUiInput.BlockingMenu(true)==null,"scene map remains allowed for chest memo");
  Check(ModUiInput.BlockingMenu(false)=="scene map","item spawner still blocks scene map");
  UiFunctionGroup.displayingUiStack.Clear();
  var tip=new GameObject("generic tooltip");tip.AddComponent<UiRefHelper>().uiLayer=UIRoot.GameUiLayer.ToolTip;UiFunctionGroup.displayingUiStack.Add(tip.AddComponent<UiFunctionGroup>());
  Check(!Blocked(),"generic tooltip layer does not block L");
  var real=new GameObject("inventory").AddComponent<UiFunctionGroup>();UiFunctionGroup.displayingUiStack.Insert(0,real);
  Check(Blocked(),"real menu under tooltip still blocks L");
  UiFunctionGroup.displayingUiStack.Clear();
  var plugin=new ChestMemo();BepInEx.Paths.ConfigPath=System.IO.Path.GetTempPath();plugin.Load();
  typeof(ChestMemo).GetField("ready",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(plugin,true);
  var selected=new GameObject("search text");var field=selected.AddComponent<TMPro.TMP_InputField>();field.isFocused=true;
  UnityEngine.EventSystems.EventSystem.current=new(){currentSelectedGameObject=selected};
  Input.Pressed=KeyCode.L;Time.frameCount++;plugin.Keys();
  bool Visible()=>(bool)typeof(ChestMemo).GetField("visible",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(plugin)!;
  Check(!Visible(),"typing L in another TMP input does not open memo");
  field.isFocused=false;Time.frameCount++;plugin.Keys();Check(Visible(),"unfocused selected field does not swallow L");
  Input.Pressed=KeyCode.None;Time.frameCount++;UiFunctionGroup.displayingUiStack.Add(tip.GetComponent<UiFunctionGroup>()!);plugin.Tick();
  Check(Visible()&&GameTimer.Instance.Scale==0,"tooltip on watcher tick does not immediately close memo");
  UiFunctionGroup.displayingUiStack.Insert(0,real);Time.frameCount++;plugin.Tick();
  Check(!Visible()&&GameManager.ControlsEnabled&&GameTimer.Instance.Scale==1,"real menu on watcher tick closes memo and releases pause");
  UiFunctionGroup.displayingUiStack.Clear();Input.Pressed=KeyCode.L;Time.frameCount++;plugin.Keys();
  Input.Pressed=KeyCode.Escape;Time.frameCount++;plugin.Keys();Check(!Visible(),"Escape closes memo");
  Check(!plugin.Keys(),"second input callback consumes same closing Escape frame");
  var legacy=new GameObject("legacy input");var legacyField=legacy.AddComponent<UnityEngine.UI.InputField>();legacyField.isFocused=true;
  var child=new GameObject("caret");child.transform.SetParent(legacy.transform,false);
  UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject=child;
  Check(ModUiInput.Typing(),"focused legacy input parent protects text entry for both tools");
  legacy.activeInHierarchy=false;child.activeInHierarchy=false;
  Check(!ModUiInput.Typing(),"inactive selected input does not keep tools blocked");
  UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject=null;
  var roaming=RoamingUI.Instance;RoamingUI.Instance=null!;
  Input.Pressed=KeyCode.L;Time.frameCount++;plugin.Keys();Check(Visible(),"missing roaming HUD font falls back without losing L");
  RoamingUI.Instance=roaming;
  Application.isFocused=false;plugin.Keys();Check(!Visible()&&GameManager.ControlsEnabled&&GameTimer.Instance.Scale==1,"focus loss closes memo and releases pause");Application.isFocused=true;
  plugin.Unload();UnityEngine.EventSystems.EventSystem.current=null;Input.Pressed=KeyCode.None;
  Console.WriteLine($"Mod input checks: {checks}, failures: {failed}");return failed==0?0:1;
 }
}
