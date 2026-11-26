using System;
using System.Collections.Generic;
using System.Linq;
namespace UnityEngine.EventSystems {public class EventSystem {public static EventSystem? current;public UnityEngine.GameObject? currentSelectedGameObject;}}
namespace Il2CppSystem {public class Object {public IntPtr Pointer {get;}=(IntPtr)Guid.NewGuid().GetHashCode();}}
namespace Il2CppInterop.Runtime {public static class Il2CppType {public static Type Of<T>()=>typeof(T);}public static class DelegateSupport {public static T ConvertDelegate<T>(Delegate d) where T:class=>new UnityEngine.Events.UnityAction((Action)d) as T ?? throw new Exception();}}
namespace BepInEx {
 public static class Paths {public static string ConfigPath="";}
 [AttributeUsage(AttributeTargets.Class)]public class BepInPlugin:Attribute {public BepInPlugin(string id,string name,string version){}}
}
namespace BepInEx.Configuration {
 public class ConfigEntry<T> {public T Value;public ConfigEntry(T value){Value=value;}}
 public class ConfigFile {public ConfigEntry<T> Bind<T>(string section,string key,T value,string description)=>new(value);}
}
namespace BepInEx.Unity.IL2CPP {
 public class BasePlugin {public BepInEx.Configuration.ConfigFile Config=new();public Log Log=new();public virtual void Load(){}public virtual bool Unload()=>true;public T AddComponent<T>() where T:UnityEngine.Component=>new UnityEngine.GameObject("watcher").AddComponent<T>();}
 public class Log {public static int Errors,Warnings;public void LogInfo(object s){}public void LogWarning(object s){Warnings++;}public void LogError(object s){Errors++;Console.WriteLine("DIAGNOSTIC "+s);}}
}
namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.Class,AllowMultiple=true)]public class HarmonyPatch:Attribute {public HarmonyPatch(){}public HarmonyPatch(Type type,string method){}public HarmonyPatch(Type type,string method,Type[] args){}}
 public class Harmony {public Harmony(string id){}public void PatchAll(System.Reflection.Assembly a){}public void UnpatchSelf(){}}
}
namespace DaxiaMods {public static class BuildGuard {public static bool Check(object log)=>true;}}
// The Unity resource bridge is exercised by the native performance probe;
// these tests keep chest/UI/save boundaries independent of asset loading.
namespace DaxiaMods {internal sealed class PortraitWarmup {internal PortraitWarmup(Action<string> info,Action<string> warning){}internal void Tick(bool enabled){}internal void Stop(){}}}
namespace UnityEngine.Events {
 public class UnityAction {readonly Action action;public UnityAction(Action action){this.action=action;}public void Invoke()=>action();public static UnityAction? operator +(UnityAction? a,UnityAction? b)=>a==null?b:b==null?a:new(()=>{a.Invoke();b.Invoke();});public static UnityAction? operator -(UnityAction? a,UnityAction? b)=>ReferenceEquals(a,b)?null:a;}
}
namespace UnityEngine {
 public class Object:Il2CppSystem.Object {
  public bool destroyed;public static implicit operator bool([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] Object? o)=>o!=null&&!o.destroyed;
  public T Cast<T>() where T:Il2CppSystem.Object=>this as T??throw new Exception();
  public static void Destroy(Object o){o.destroyed=true;if(o is GameObject go){go.transform.parent?.Children.Remove(go.transform);}}
  public static void DontDestroyOnLoad(Object o){}
  public static int SceneScans;
  public static T[] FindObjectsOfType<T>() where T:Component{SceneScans++;return GameObject.All.Where(g=>!g.destroyed).SelectMany(g=>g.Components.OfType<T>()).ToArray();}
 }
 public class Component:Object {
  GameObject? owner;public GameObject gameObject=>owner??=new GameObject("orphan",this);internal void Attach(GameObject go){owner=go;}
  public Transform transform=>gameObject.transform;
  public T? GetComponent<T>() where T:Component=>gameObject.GetComponent<T>();
  public T? GetComponentInParent<T>() where T:Component {var node=transform;while(node!=null){var c=node.gameObject.GetComponent<T>();if(c!=null)return c;node=node.parent;}return null;}
 }
 public class MonoBehaviour:Component {public MonoBehaviour(){}public MonoBehaviour(IntPtr ptr){}}
 public class GameObject:Object {
  public static List<GameObject> All=new();public List<Component> Components=new();public string name;public Transform transform;public bool activeInHierarchy=true;
  public GameObject(string name,Component? component=null){this.name=name;transform=new Transform();transform.Attach(this);Components.Add(transform);if(component!=null){component.Attach(this);Components.Add(component);}All.Add(this);}
  public GameObject(string name,Type[] components):this(name){foreach(var type in components){if(type==typeof(RectTransform))AddComponent<RectTransform>();else throw new NotSupportedException();}}
  public T AddComponent<T>() where T:Component {var c=(T)(typeof(T).GetConstructor(Type.EmptyTypes)!=null?Activator.CreateInstance(typeof(T))!:Activator.CreateInstance(typeof(T),IntPtr.Zero)!);c.Attach(this);Components.Add(c);if(c is RectTransform r){r.parent=transform.parent;transform=r;}return c;}
  public T? GetComponent<T>() where T:Component=>Components.OfType<T>().LastOrDefault();
  public T? GetComponentInParent<T>() where T:Component=>transform.GetComponentInParent<T>();
  public T[] GetComponentsInChildren<T>() where T:Component=>Components.OfType<T>().Concat(transform.Children.SelectMany(t=>t.gameObject.GetComponentsInChildren<T>())).ToArray();
 }
 public class Transform:Component {public Vector3 localPosition;public Vector3 localScale;public Transform? parent;public List<Transform> Children=new();public void SetParent(Transform parent,bool worldPositionStays){this.parent=parent;parent.Children.Add(this);}public void SetAsLastSibling(){} }
 public class RectTransform:Transform {public Vector2 anchoredPosition,sizeDelta,anchorMin,anchorMax,offsetMin,offsetMax;}
 public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 one=>new(1,1,1);public float magnitude=>MathF.Sqrt(x*x+y*y+z*z);}
 public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static Vector2 zero=>new(0,0);public static Vector2 one=>new(1,1);}
 public struct Vector3Int {public int x,y,z;public Vector3Int(int x,int y,int z){this.x=x;this.y=y;this.z=z;}}
 public struct Vector2Int {public int x,y;public Vector2Int(int x,int y){this.x=x;this.y=y;}public static Vector2Int operator -(Vector2Int a,Vector2Int b)=>new(a.x-b.x,a.y-b.y);}
 public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color red=>new(1,0,0);public static Color white=>new(1,1,1);}
 public struct Rect {public Rect(float x,float y,float w,float h){}}
 public enum HideFlags {HideAndDontSave}
 public enum TextureFormat {RGBA32}
 public class Texture2D:Object {public HideFlags hideFlags;public Texture2D(int w,int h,TextureFormat f,bool mip){}public void SetPixel(int x,int y,Color color){}public void Apply(){}}
 public class Sprite:Object {public static Sprite Create(Texture2D texture,Rect rect,Vector2 pivot)=>new();}
 public enum RenderMode {ScreenSpaceOverlay}
 public class Canvas:Component {public RenderMode renderMode;public int sortingOrder;}
 public enum KeyCode {None,F1,L,Escape}
 public static class Input {public static KeyCode Pressed;public static bool GetKeyDown(KeyCode key)=>Pressed==key;}
 public static class Time {public static int frameCount;public static float unscaledTime;}
 public static class Application {public static bool isFocused=true;}
}
namespace UnityEngine.UI {
 public class InputField:UnityEngine.Component {public bool isFocused;}
 public class Image:UnityEngine.Component {public UnityEngine.Sprite? sprite;public UnityEngine.Color color;public bool raycastTarget=true;}
 public class Button:UnityEngine.Component {public Image? targetGraphic;public ClickEvent onClick=new();public class ClickEvent {public List<UnityEngine.Events.UnityAction> Listeners=new();public void AddListener(UnityEngine.Events.UnityAction a)=>Listeners.Add(a);}}
 public class GraphicRaycaster:UnityEngine.Component {}
 public class CanvasScaler:UnityEngine.Component {public enum ScaleMode {ScaleWithScreenSize}public ScaleMode uiScaleMode;public UnityEngine.Vector2 referenceResolution;public float matchWidthOrHeight;}
}
namespace TMPro {
 public static class TMP_Settings {public static TMP_FontAsset defaultFontAsset=new();}
 public class TMP_InputField:UnityEngine.Component {public bool isFocused;}
 public class TMP_FontAsset:UnityEngine.Object {}
 public enum TextAlignmentOptions {Center,TopLeft}
 public class TMP_Text:UnityEngine.Component {public string text="";public int fontSize;public UnityEngine.Color color;public bool raycastTarget,richText;public TMP_FontAsset font=new();public TextAlignmentOptions alignment;}
 public class TextMeshProUGUI:TMP_Text {}
}
namespace GameData {
 public class ItemData {public int Id;}
 public class ItemDataScriptObject {public ItemData GetData(int id)=>new(){Id=id};}
 public class ItemMapDataScriptObject {public Dictionary<string,int> mapToItemDict=new();}
 public static class BaseTables {public static ItemMapDataScriptObject Maps=new();}
}
public class BaseDataClass {public static T GetGameData<T>() where T:class=>typeof(T)==typeof(GameData.ItemMapDataScriptObject)?(BaseTablesMap() as T)!:Activator.CreateInstance<T>();static object BaseTablesMap()=>GameData.BaseTables.Maps;}
public class ES3File:Il2CppSystem.Object {public void Sync(){}}
namespace WuLin {
 using UnityEngine;
 public class GameCharacterManager {}
 public class GameManager:Object {public static GameManager Instance=new();public bool IsOnWorldMap;public static bool IsLoadingMap;public static bool ControlsEnabled=true;public static void StartNewGame(){}public static void QuitToMenu(){}public static void RegisterGameFunctionMask(string owner,GameFunctionMask mask,bool permanent){ControlsEnabled=false;}public static void UnregisterGameFunctionMask(string owner){ControlsEnabled=true;}}
 [Flags] public enum GameFunctionMask {PlayerControl=1,InGameTimeElapse=2,SaveGame=4,PlayerTriggerNpcSight=8,PlayerPet=16}
 public class RoamingManager:Object {public static RoamingManager Instance=new();public static bool IsRoamingMode=true,IsInStory,IsTeleporting,PrepareToBattle;public Player player=new();public class Player {public void Stop(){}}}
 public class StoryManager:Object {public static StoryManager Instance=new();public bool InStoryProgress;}
 public class UiFunctionGroup:Component {public static List<UiFunctionGroup> displayingUiStack=new();public bool isAlwaysActived;}
 public class UiRefHelper:UiFunctionGroup {public UIRoot.GameUiLayer uiLayer;}
 public class UIRoot {public enum GameUiLayer {Normal,ToolTip}}
 public class RoamingUI:Component {public static RoamingUI Instance=new();public TMPro.TMP_Text dayLabel=new();}
 public class UIItemTIps:Component {}
 public class SaveManager:Object {public static SaveManager Instance=new();public static string SavePath="",FullPathForAllSaveFolder="";public bool IsSaving,IsLoading;public bool IsSavingOrLoading=>IsSaving||IsLoading;public UnityEngine.Events.UnityAction? onLoadFinished,onReset,onSaveFinished,onAccountChange;public ES3File m_saveFile=new(),m_saveFileBackup=new();public void Load(){}}
 public class PlayerTeamManager:Object {public static PlayerTeamManager Instance=new();public GameItemPack TeamInventory=new();}
 public class GameItemPack {public Dictionary<int,int> Counts=new();public bool ThrowGetItem;public int GetItemCountById(int id)=>Counts.GetValueOrDefault(id);public object? GetItem(int id){if(ThrowGetItem)throw new InvalidOperationException("simulated unavailable map inventory");return GetItemCountById(id)>0?new object():null;}}
 public class MapManager:Object {public static MapManager Instance=new();public int Map=1;public int GetCurMapID()=>Map;public bool IsPicked(InteractiveObject obj)=>obj.Picked;public void PickHiddenItem(InteractiveObject obj){}public Vector3 CellToWorld(Vector3Int pos)=>new(pos.x,pos.y,pos.z);}
 public class InteractiveObject:Component {public int SaveID,mTempID;public Vector3Int m_pos;public bool Picked;public List<OpenConditionType> Conditions=new();public List<OpenConditionType> GetCondition()=>Conditions;public class OpenConditionType {public List<OpenConditionData> Data=new();}public class OpenConditionData {public MapConst.BuildingOpenCondition ConditionType;public int Param0,Param1;}}
 public static class MapConst {public enum BuildingOpenCondition {ITEM,PROPERTY}}
 public class GameLoc {public static GameLoc Instance=new();public string GetLocString(string key)=>key=="WMapName_1"?"迷踪林":"楚襄城";}
 public static class GameUtil {public static string GetName(GameData.ItemData item,bool rich)=>item.Id==101?"铜钥匙":"银钥匙";}
 public class UIThumbnail:Component {public GameObject? CurMap;public void Show(string mapID){}public void Hide(){}}
 public class UIThumbnailEditor:Component {public Vector2Int ScenePos1,ScenePos2;public RectTransform MapPos1=new(),MapPos2=new();}
 public class GameTimer:Object {public static GameTimer Instance=new();public float Scale=1;public void AddOrSetTimeScale(Il2CppSystem.Object token,float scale){Scale=scale;}public void RemoveTimeScale(Il2CppSystem.Object token){Scale=1;}}
 public class HotKeyManager {}
}
namespace WuLin.StateMachine {
 public enum StateMachineDataTypes {Interactive_OnObjectSet}
 public class StateMachineDataContainer {public InteractiveObjectStateMachineData State=new();public T GetData<T>(StateMachineDataTypes type) where T:class=>(State as T)!;}
 public class InteractiveObjectStateMachineData {public bool isChest;public WuLin.InteractiveObject interactiveObject=new();public Dictionary<int,int> openItemRequire=new();}
 public class StartInteractiveWithObjectWithCostActionNode {public void InvokeAction(StateMachineDataContainer data){}}
}
