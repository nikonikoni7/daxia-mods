using System;
using System.Reflection;
using System.Collections.Generic;
namespace BepInEx {public static class Paths {public static string ConfigPath="";}}
namespace BepInEx {[AttributeUsage(AttributeTargets.Class)]public class BepInPlugin:Attribute {public BepInPlugin(string a,string b,string c){}}}
namespace BepInEx.Configuration {
 public class ConfigEntry<T>{public T Value;public ConfigEntry(T value){Value=value;}}
 public class ConfigFile{public ConfigEntry<bool> Enabled=new(true);public ConfigEntry<bool> Bind(string a,string b,bool c,string d)=>Enabled;}
}
namespace BepInEx.Unity.IL2CPP {
 public class BasePlugin{public BepInEx.Configuration.ConfigFile Config=new();public Log Log=new();public virtual void Load(){}public virtual bool Unload()=>true;}
 public class Log{public void LogInfo(object v){}public void LogError(object v){}}
}
namespace UnityEngine {
 public struct Vector2 {public float x,y;public static Vector2 zero=>new();public Vector2(float a,float b){x=a;y=b;}}
 public struct Vector2Int {public int x,y;}
 public class Object {public static implicit operator bool(Object? o)=>o!=null;}
 public class Transform:Object {public Transform? Find(string name)=>new Transform();}
}
namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.Class)]public class HarmonyPatch:Attribute {public HarmonyPatch(Type t,string m){}}
 public class Harmony {
  public static Type? Plugin;public Harmony(string id){}
  public void PatchAll(Assembly a)=>Plugin=a.GetType("DaxiaMods.FiefPositionFix");public void UnpatchSelf()=>Plugin=null;
  public static object? Invoke(string patch,string method,object[] args)=>Plugin?.GetNestedType(patch,BindingFlags.NonPublic)?.GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)?.Invoke(null,args);
 }
}
namespace DaxiaMods {public static class BuildGuard{public static bool Check(object log)=>true;}}
namespace WuLin {
 public static class SaveManager {public static string SavePath="",FullPathForAllSaveFolder="";}
 public static class PlayerTeamManager {public static Team Instance=new();public class Team{public int CurMapId=1002;}}
 public class SaveObjectMapManager {public class MarkOffsetPos {public int markMapID;public UnityEngine.Vector2 markOffsetPos;public bool isStandOn;}}
 public class UIWorldMapManager {
  public Dictionary<int,UIWorldMap> listWorldMap=new();
  public Dictionary<int,SaveObjectMapManager.MarkOffsetPos> Marks=new();
  public Dictionary<int,SaveObjectMapManager.MarkOffsetPos> GetSaveMark()=>Marks;
  public void SetMarkOffsetPos(int world,int map,UnityEngine.Vector2 offset){Marks[world].markMapID=map;Marks[world].markOffsetPos=offset;}
  public void SetMarkStand(int world,bool stand)=>Marks[world].isStandOn=stand;
  public void initTarget(){HarmonyLib.Harmony.Invoke("LoadedTargetPatch","Prefix",new object[]{this});foreach(var pair in Marks)if(listWorldMap.TryGetValue(pair.Key,out var map))map.SetPos(pair.Value.markMapID);}
  public void OnGameLoaded(Action restore){
   object[] state={0};HarmonyLib.Harmony.Invoke("LoadPatch","Prefix",state);
   try{restore();}finally{HarmonyLib.Harmony.Invoke("LoadPatch","Finalizer",state);}
  }
 }
 public class MapManager:UnityEngine.Object {
  public static MapManager Instance=new();
  public bool IsFiefMainMapID(int id){object[] a={id,false};var run=HarmonyLib.Harmony.Invoke("HomeCheckPatch","Prefix",a);return run is false?(bool)a[1]:id==500;}
 }
 public class UIWorldMap:UnityEngine.Object {
  public bool HasIcon=true,Throw,Nested,OuterScopeRestored;public int Position=-99;
  public UnityEngine.Transform? CityIcon=>HasIcon?new():null;
  public UnityEngine.Transform? FindTargetIcon(int id)=>HasIcon?new():null;
  // Model only the observed external branch: normal anchors position the marker;
  // home anchors skip placement. This is not a replacement for the native probe.
  public void SetPos(int id){
   object[] a={this,id,0};HarmonyLib.Harmony.Invoke("PositionPatch","Prefix",a);
   try{
    if(Throw)throw new InvalidOperationException();
    if(Nested&&id==500){SetPos(200);OuterScopeRestored=!MapManager.Instance.IsFiefMainMapID(500);}
    if(!MapManager.Instance.IsFiefMainMapID(id))Position=id;
   }finally{HarmonyLib.Harmony.Invoke("PositionPatch","Finalizer",new[]{a[2]});}
  }
 }
}
namespace XNode {public class NodeGraph:UnityEngine.Object {public string name="";}}
namespace WuLin.StateMachine {
 public class EnterMapNode:UnityEngine.Object {
  public XNode.NodeGraph? graph;public int mapID,TagID;public bool moveWorldTag;public UnityEngine.Vector2Int pos;
 }
 public static class DllScenarioDelegate {
  public static void EnterMapNodeStartTeleport(EnterMapNode node,Action<EnterMapNode> native){
   object[] a={node,false};HarmonyLib.Harmony.Invoke("FerryEnterPatch","Prefix",a);
   try{native(node);}finally{HarmonyLib.Harmony.Invoke("FerryEnterPatch","Finalizer",new object[]{node,a[1]});}
  }
 }
}
