// Run the actual plugin against a small scene/input boundary, without starting Unity or touching saves.
using System;
using System.Linq;
using System.Reflection;
using DaxiaMods;
using UnityEngine;
using WuLin;

static class ExploreCameraInputTests
{
 static int Main() {
  int failures=0;
  void Check(bool ok,string name) {Console.WriteLine((ok?"PASS ":"FAIL ")+name);if(!ok)failures++;}
  var plugin=new ExploreCamera();plugin.Load();
  var patches=typeof(ExploreCamera).GetNestedTypes(BindingFlags.NonPublic)
   .SelectMany(t=>t.GetCustomAttributes<HarmonyLib.HarmonyPatch>());
  Check(!patches.Any(p=>p.Target==typeof(HotKeyManager)),"original hotkey update is never intercepted");
  try {typeof(ExploreCamera).GetMethod("Enter",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(plugin,null);}
  catch(TargetInvocationException e) {Console.WriteLine("DIAGNOSTIC "+e.InnerException!.Message);}
  Check(ExploreCamera.Active,"camera is resolved when attached to a child of the follower");
  plugin.Exit();
  var watcher=new ExploreCamera.CameraWatcher(IntPtr.Zero);
  var update=typeof(ExploreCamera.CameraWatcher).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance)!;
  void Tick(KeyCode pressed) {Input.Pressed=pressed;update.Invoke(watcher,null);Input.Pressed=KeyCode.None;}
  Tick(KeyCode.None);
  Check(!ExploreCamera.Active && GameManager.ControlsEnabled,"normal exploration leaves controls available");
  Tick(KeyCode.Y);
  Check(ExploreCamera.Active,"Y is polled by the independent watcher");
  Check(!GameManager.ControlsEnabled && GameTimer.Instance.Scale==0,"child camera scene acquires pause");
  Tick(KeyCode.Y);
  Check(!ExploreCamera.Active && GameManager.ControlsEnabled && GameTimer.Instance.Scale==1,"second Y releases controls and time");
  Tick(KeyCode.Y);Tick(KeyCode.Escape);
  Check(!ExploreCamera.Active && GameManager.ControlsEnabled,"Escape releases pause");
  Tick(KeyCode.Y);Application.isFocused=false;Tick(KeyCode.None);Application.isFocused=true;
  Check(!ExploreCamera.Active && GameManager.ControlsEnabled,"focus loss releases pause");
  var uiEnable=typeof(ExploreCamera).GetNestedType("Ui",BindingFlags.NonPublic)!
   .GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic)!;
  void EnableUi(UiFunctionGroup ui)=>uiEnable.Invoke(null,new object[]{ui});
  var hoverTip=new UiRefHelper{uiLayer=UIRoot.GameUiLayer.ToolTip};
  Tick(KeyCode.Y);EnableUi(hoverTip);
  Check(ExploreCamera.Active && GameTimer.Instance.Scale==0,"tooltip activation does not exit or release pause");
  UiFunctionGroup.displayingUiStack.Add(hoverTip);Tick(KeyCode.None);
  Check(ExploreCamera.Active,"tooltip in UI stack does not cancel free camera");
  Tick(KeyCode.Y);Tick(KeyCode.Y);
  Check(ExploreCamera.Active,"free camera can be entered with a visible tooltip");
  UiFunctionGroup.displayingUiStack.Clear();plugin.Exit();
  var itemTip=new UiFunctionGroup{ParentComponent=new UIItemTIps()};
  Tick(KeyCode.Y);EnableUi(itemTip);UiFunctionGroup.displayingUiStack.Add(itemTip);Tick(KeyCode.None);
  Check(ExploreCamera.Active,"status explanation panel remains nonmodal even without a layer tag");
  UiFunctionGroup.displayingUiStack.Clear();plugin.Exit();
  Tick(KeyCode.Y);EnableUi(new UiFunctionGroup{ParentComponent=new RoamingUI()});
  Check(ExploreCamera.Active,"normal exploration HUD activation preserves free camera");plugin.Exit();
  Tick(KeyCode.Y);EnableUi(new UiFunctionGroup());
  Check(!ExploreCamera.Active && GameManager.ControlsEnabled && GameTimer.Instance.Scale==1,"real panel activation still exits and releases pause");
  Tick(KeyCode.Y);UiFunctionGroup.displayingUiStack.Add(new());UiFunctionGroup.displayingUiStack.Add(hoverTip);Tick(KeyCode.None);
  Check(!ExploreCamera.Active && GameManager.ControlsEnabled,"tooltip cannot hide a real modal lower in the stack");
  UiFunctionGroup.displayingUiStack.Clear();
  var pan=typeof(ExploreCamera).GetMethod("Pan",BindingFlags.NonPublic|BindingFlags.Instance)!;
  void PanAt(float x,float y,float dt=1f/60) {
   Input.mousePosition=new Vector3(x,y,0);Time.unscaledDeltaTime=dt;
   pan.Invoke(plugin,new object[]{CameraFollowTarget.Instance});
  }
  Tick(KeyCode.Y);
  var camera=CameraFollowTarget.Instance.ChildCamera!;
  var screenWidth=2*camera.orthographicSize*camera.aspect;
  PanAt(1919,540);
  var firstDistance=CameraFollowTarget.Instance.transform.position.x;
  Check(firstDistance>0 && firstDistance<screenWidth/60*0.5f,"edge entry accelerates instead of jumping to full speed");
  for(int i=0;i<30;i++)PanAt(1919,540);
  var beforeStop=CameraFollowTarget.Instance.transform.position.x;
  PanAt(960,540);
  var coast=CameraFollowTarget.Instance.transform.position.x-beforeStop;
  Check(coast>0 && coast<screenWidth/60,"leaving edge decelerates smoothly");
  for(int i=0;i<40;i++)PanAt(960,540);
  beforeStop=CameraFollowTarget.Instance.transform.position.x;PanAt(960,540);
  Check(Math.Abs(CameraFollowTarget.Instance.transform.position.x-beforeStop)<0.00001,"deceleration settles without continuous drifting");
  plugin.Exit();Tick(KeyCode.Y);
  for(int i=0;i<20;i++)PanAt(1919,540);
  var beforeTurn=CameraFollowTarget.Instance.transform.position.x;PanAt(0,540);
  Check(CameraFollowTarget.Instance.transform.position.x>beforeTurn,"opposite edge changes velocity smoothly instead of snapping direction");
  beforeStop=CameraFollowTarget.Instance.transform.position.x;InputManager.OverUI=true;PanAt(0,540);InputManager.OverUI=false;
  Check(CameraFollowTarget.Instance.transform.position.x==beforeStop,"UI hover stops all momentum immediately");
  PanAt(960,540);
  Check(CameraFollowTarget.Instance.transform.position.x==beforeStop,"blocked input clears momentum before leaving UI");
  for(int i=0;i<20;i++)PanAt(1919,540);
  beforeStop=CameraFollowTarget.Instance.transform.position.x;PanAt(-1,540);PanAt(960,540);
  Check(CameraFollowTarget.Instance.transform.position.x==beforeStop,"outside window clears momentum immediately");
  plugin.Exit();Tick(KeyCode.Y);PanAt(960,540);
  Check(CameraFollowTarget.Instance.transform.position.x==0,"reentering starts with no old momentum");
  plugin.Exit();Tick(KeyCode.Y);
  for(int i=0;i<30;i++)PanAt(1919,540);
  var at60=CameraFollowTarget.Instance.transform.position.x;plugin.Exit();Tick(KeyCode.Y);
  for(int i=0;i<15;i++)PanAt(1919,540,1f/30);
  Check(Math.Abs(CameraFollowTarget.Instance.transform.position.x-at60)<0.0001,"30fps and 60fps integrate equal-time motion consistently");
  plugin.Exit();
  Tick(KeyCode.Y);CameraFollowTarget.Instance.transform.position=new Vector3(100,0,0);
  for(int i=0;i<10;i++)PanAt(1919,540);
  Check(CameraFollowTarget.Instance.transform.position.x==100,"smoothing does not exceed scene boundary");
  PanAt(0,540);
  Check(CameraFollowTarget.Instance.transform.position.x<100,"blocked boundary clears outward momentum before reversing");
  plugin.Exit();
  var smoothSetting=(BepInEx.Configuration.ConfigEntry<float>)typeof(ExploreCamera).GetField("smoothing",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(plugin)!;
  smoothSetting.Value=0;Tick(KeyCode.Y);PanAt(1919,540);
  Check(Math.Abs(CameraFollowTarget.Instance.transform.position.x-screenWidth/60)<0.00001,"zero smoothing setting restores immediate movement");
  plugin.Exit();smoothSetting.Value=0.04f;
  Check(CameraMotion.SmoothAxis(1,1,float.NaN,0.04f).Distance==0,"invalid frame time cannot move camera");
  void Click() {Input.RightDown=true;Tick(KeyCode.None);Input.RightDown=false;}
  Input.mousePosition=new Vector3(400,200,0);
  Tick(KeyCode.Y);CameraFollowTarget.Instance.transform.position=new Vector3(1000,500,0);
  RoamingManager.RunActive=true;Click();
  Check(RoamingManager.MoveCalls==1 && RoamingManager.LastTarget.x==120 && RoamingManager.LastTarget.y==60,
   "right click uses render coordinates and the panned camera before restoring follow");
  Check(!ExploreCamera.Active && GameManager.ControlsEnabled && GameTimer.Instance.Scale==1 && RoamingManager.UnpausedWhenMoved,
   "pause and controls are released before native pathfinding");
  Check(CameraFollowTarget.Instance.transform.position.x==0 && RoamingManager.LastRun,"camera follows again and native running setting is preserved");
  Tick(KeyCode.None);Click();
  Check(RoamingManager.MoveCalls==1,"one press sends one movement command and inactive clicks are untouched");
  RoamingManager.RunActive=false;Tick(KeyCode.Y);Click();
  Check(RoamingManager.MoveCalls==2 && !RoamingManager.LastRun,"native walking setting is preserved");
  Tick(KeyCode.Y);InputManager.OverUI=true;Click();InputManager.OverUI=false;
  Check(ExploreCamera.Active && RoamingManager.MoveCalls==2,"UI right click does not move or leave free camera");
  foreach(var pos in new[]{new Vector3(-1,100,0),new Vector3(Screen.width,100,0),new Vector3(100,-1,0),new Vector3(100,Screen.height,0),new Vector3(float.NaN,100,0)}) {
   Input.mousePosition=pos;Click();
  }
  Check(ExploreCamera.Active && RoamingManager.MoveCalls==2,"outside and invalid pointer coordinates are ignored");
  Input.mousePosition=new Vector3(400,200,0);Application.isFocused=false;Click();Application.isFocused=true;
  Check(!ExploreCamera.Active && RoamingManager.MoveCalls==2 && GameManager.ControlsEnabled,"unfocused click only releases pause");
  void Blocked(Action block,Action reset,string name) {
   Tick(KeyCode.Y);block();Click();
   Check(!ExploreCamera.Active && RoamingManager.MoveCalls==2 && GameManager.ControlsEnabled,name);reset();
  }
  Blocked(()=>GameManager.Instance.IsOnWorldMap=true,()=>GameManager.Instance.IsOnWorldMap=false,"world map transition blocks right-click movement");
  Blocked(()=>RoamingManager.IsRoamingMode=false,()=>RoamingManager.IsRoamingMode=true,"battle transition blocks right-click movement");
  Blocked(()=>GameManager.IsLoadingMap=true,()=>GameManager.IsLoadingMap=false,"loading transition blocks right-click movement");
  Blocked(()=>StoryManager.Instance.InStoryProgress=true,()=>StoryManager.Instance.InStoryProgress=false,"story transition blocks right-click movement");
  Blocked(()=>UiFunctionGroup.displayingUiStack.Add(new()),()=>UiFunctionGroup.displayingUiStack.Clear(),"modal transition blocks right-click movement");
  Tick(KeyCode.Y);RoamingManager.NoPath=true;Click();RoamingManager.NoPath=false;
  Check(!ExploreCamera.Active && RoamingManager.MoveCalls==3 && GameTimer.Instance.Scale==1,"unreachable target is left to native pathfinding without repausing");
  Tick(KeyCode.Y);GameCamera.ThrowConversion=true;Click();GameCamera.ThrowConversion=false;
  Check(!ExploreCamera.Active && RoamingManager.MoveCalls==3 && GameManager.ControlsEnabled && GameTimer.Instance.Scale==1,"coordinate exception releases pause without issuing movement");
  plugin=new ExploreCamera();plugin.Load();
  CameraFollowTarget.Instance.ChildCamera=null;
  Tick(KeyCode.Y);
  Check(!ExploreCamera.Active && GameManager.ControlsEnabled,"missing camera fails without locking original controls");
  Tick(KeyCode.Y);
  Check(!ExploreCamera.Active && GameManager.ControlsEnabled,"failed plugin leaves subsequent input available");
  Console.WriteLine($"Failures: {failures}");return failures==0?0:1;
 }
}

namespace BepInEx {
 [AttributeUsage(AttributeTargets.Class)] public sealed class BepInPlugin:Attribute {public BepInPlugin(string a,string b,string c){}}
}
namespace BepInEx.Configuration {
 public sealed class ConfigEntry<T> {public T Value;public ConfigEntry(T value){Value=value;}}
 public sealed class ConfigFile {public ConfigEntry<T> Bind<T>(string a,string b,T value,string description)=>new(value);}
}
namespace BepInEx.Unity.IL2CPP {
 public class TestLog {public void LogInfo(object s){}public void LogError(object s){Console.WriteLine("DIAGNOSTIC "+s);}}
 public abstract class BasePlugin {
  public BepInEx.Configuration.ConfigFile Config=new();public TestLog Log=new();
  public abstract void Load();public virtual bool Unload()=>true;public void AddComponent<T>(){}
 }
}
namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.Class,AllowMultiple=true)] public sealed class HarmonyPatch:Attribute {
  public Type Target;public HarmonyPatch(Type t,string method){Target=t;}
 }
 public sealed class Harmony {public Harmony(string id){}public void PatchAll(Assembly assembly){}public void UnpatchSelf(){}}
}
namespace Il2CppSystem {public class Object{}}
namespace UnityEngine {
 public class Object {
  public static implicit operator bool([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] Object? o)=>o!=null;
  public static void DontDestroyOnLoad(Object o){}public static void Destroy(Object o){}
  public T Cast<T>() where T:new()=>new();
 }
 public class Component:Object {
  public IntPtr Pointer=new(1);public Transform transform=new();public Camera? ChildCamera;public Component? ParentComponent;
  public T? GetComponent<T>() where T:Component=>null;
  public T? GetComponentInChildren<T>() where T:Component=>ChildCamera as T;
  public T? GetComponentInParent<T>() where T:Component=>this as T ?? ParentComponent as T;
 }
 public class MonoBehaviour:Component {public MonoBehaviour(){}public MonoBehaviour(IntPtr p){Pointer=p;}}
 public class GameObject:Object {public GameObject(string name){}}
 public class Transform:Object {public Vector3 position;public Vector3 right=new(1,0,0);public Vector3 up=new(0,1,0);}
 public class Camera:Component {
  public bool orthographic=true;public float orthographicSize=10,aspect=1.8f;
  public Vector3 ScreenToWorldPoint(Vector3 pos)=>WuLin.CameraFollowTarget.Instance.transform.position+pos;
 }
 public struct Vector3Int {public int x,y,z;public Vector3Int(int a,int b,int c){x=a;y=b;z=c;}}
 public struct Vector3 {
  public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}
  public static Vector3 zero=>new();
  public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
  public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
  public static Vector3 operator *(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b);
 }
 public enum KeyCode {None,Y,Escape,B}
 public static class Input {public static KeyCode Pressed;public static bool RightDown;public static bool GetMouseButtonDown(int b)=>b==1 && RightDown;public static bool GetKeyDown(KeyCode k)=>Pressed==k;public static Vector3 mousePosition;}
 public static class Application {public static bool isFocused=true;}
 public static class Screen {public static int width=1920,height=1080;}
 public static class Time {public static float unscaledDeltaTime=0.016f;}
}
namespace WuLin {
 [Flags] public enum GameFunctionMask {PlayerControl=1,InGameTimeElapse=2,SaveGame=4,PlayerTriggerNpcSight=8,PlayerPet=16}
 public class GameManager:UnityEngine.Object {
  public static GameManager Instance=new();public bool IsOnWorldMap;public static bool IsLoadingMap;
  public static bool ControlsEnabled=true;
  public static void RegisterGameFunctionMask(string o,GameFunctionMask m,bool b){ControlsEnabled=false;}
  public static void UnregisterGameFunctionMask(string o){ControlsEnabled=true;}
  public void LoadMap(){}public void SwitchInstantBattle(){}
 }
 public class StoryManager:UnityEngine.Object {public static StoryManager Instance=new();public bool InStoryProgress{get;set;}}
 public class RoamingUI:MonoBehaviour{}
 public class UIItemTIps:MonoBehaviour{}
 public class UiRefHelper:UiFunctionGroup {public UIRoot.GameUiLayer uiLayer;}
 public class UIRoot {public enum GameUiLayer {Normal,Window,ToolTip}}
 public class UiFunctionGroup:MonoBehaviour {public static System.Collections.Generic.List<UiFunctionGroup> displayingUiStack=new();public bool isAlwaysActived;}
 public class Player {public void Stop(){}}
 public class RoamingManager {
  public static RoamingManager Instance=new();public Player player=new();public static bool IsRoamingMode=true,PlayerIsControlable=true,IsInStory,IsTeleporting,PrepareToBattle;
  public static int MoveCalls;public static Vector3Int LastTarget;public static bool RunActive,LastRun,UnpausedWhenMoved,NoPath;
  public bool GetKeyRunActive()=>RunActive;
  public void PlayerMoveTo(Vector3Int pos,bool isRun=false) {
   MoveCalls++;LastTarget=pos;LastRun=isRun;UnpausedWhenMoved=GameManager.ControlsEnabled && GameTimer.Instance.Scale==1 && !ExploreCamera.Active;
   if(NoPath)return;
  }
 }
 public class CameraFollowTarget:Component {public static CameraFollowTarget Instance=new(){ChildCamera=new Camera()};public Transform followTarget=new();public void DoSnap(){}}
 public class Tilemap:UnityEngine.Object {public Vector3 GetCellCenterWorld(Vector3 v)=>v;public Vector3 LocalToWorld(Vector3 v)=>v;public Vector3 CellToLocalInterpolated(Vector3 v)=>v;}
 public class CameraFollowBorder:UnityEngine.Object {public static CameraFollowBorder Instance=new();public bool noScrolling;public Tilemap CachedTileMap=new();public Vector3 anchorTile;public int xMin=-100,xMax=100,yMin=-100,yMax=100;}
 public class MapManager:UnityEngine.Object {public static MapManager Instance=new();public int GetCurMapID()=>1;public Vector3Int WorldToCell(Vector3 p)=>new((int)(p.x/10),(int)(p.y/10),0);}
 public class GameCamera:UnityEngine.Object {
  public static GameCamera Instance=new();public Camera MainCamera=new();public static bool ThrowConversion;
  public Vector3 ScreenPosToRenderCameraPos(Vector3 p) {
   if(ThrowConversion)throw new InvalidOperationException("simulated render conversion failure");
   return p*0.5f;
  }
 }
 public class GameTimer:UnityEngine.Object {public static GameTimer Instance=new();public float Scale=1;public void AddOrSetTimeScale(Il2CppSystem.Object o,float f){Scale=f;}public void RemoveTimeScale(Il2CppSystem.Object o){Scale=1;}}
 public class HotKeyManager{}
 public class TimeDelayCall{}
 public static class InputManager {public static bool OverUI;public static bool IsPointerOverUI()=>OverUI;}
}
namespace DaxiaMods {internal static class BuildGuard {internal static bool Check(BepInEx.Unity.IL2CPP.TestLog log)=>true;}}
