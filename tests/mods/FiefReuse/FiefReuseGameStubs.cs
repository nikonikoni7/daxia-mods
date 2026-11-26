// Only external BepInEx/Harmony/game call boundaries are substituted here.
using System;
using System.Collections.Generic;
using System.Reflection;
namespace BepInEx {
 [AttributeUsage(AttributeTargets.Class)]public class BepInPlugin:Attribute{public BepInPlugin(string id,string name,string version){}}
}
namespace BepInEx.Configuration {
 public class ConfigEntry<T>{public T Value;public ConfigEntry(T value){Value=value;}}
 public class ConfigFile{public ConfigEntry<bool> Enabled=new(true);public ConfigEntry<bool> Bind(string s,string k,bool value,string description)=>Enabled;}
}
namespace BepInEx.Unity.IL2CPP {
 public class BasePlugin{public BepInEx.Configuration.ConfigFile Config=new();public Log Log=new();public virtual void Load(){}public virtual bool Unload()=>true;}
 public class Log{public void LogInfo(object value){}public void LogError(object value){}}
}
namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.Class)]public class HarmonyPatch:Attribute{public Type Target;public string Method;public HarmonyPatch(Type target,string method){Target=target;Method=method;}}
 public class Harmony {
  public static bool ThrowOnPatch;
  public static readonly Dictionary<string,MethodInfo> Prefixes=new();
  public Harmony(string id){}
  public void PatchAll(Assembly assembly){
   foreach(var type in assembly.GetTypes()){
    var attr=type.GetCustomAttribute<HarmonyPatch>();if(attr==null)continue;
    if(attr.Target!=typeof(WuLin.FiefUnsharedData)||(attr.Method!="IsFiefItemTaken"&&attr.Method!="IsFiefKungfuBookTaken"))throw new Exception("Unexpected patch target");
    Prefixes.Add(attr.Method,type.GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic)!);
    if(ThrowOnPatch)throw new Exception("partial patch failure");
   }
  }
  public void UnpatchSelf()=>Prefixes.Clear();
 }
}
namespace DaxiaMods {public static class BuildGuard{public static bool Allowed=true;public static bool Check(object log)=>Allowed;}}
namespace WuLin {
 public class FiefUnsharedData {
  public bool isPrivateFief;
  public List<int> takenItems=new(),takenKungfuBook=new();
  bool Taken(string method,int index,List<int> history){
   object[] args={this,true};
   if(HarmonyLib.Harmony.Prefixes.TryGetValue(method,out var prefix)&&!(bool)prefix.Invoke(null,args)!)return (bool)args[1];
   return history.Contains(index);
  }
  public bool IsFiefItemTaken(int index)=>Taken(nameof(IsFiefItemTaken),index,takenItems);
  public bool IsFiefKungfuBookTaken(int index)=>Taken(nameof(IsFiefKungfuBookTaken),index,takenKungfuBook);
 }
}
