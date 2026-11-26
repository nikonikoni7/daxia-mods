using System;
using System.Reflection;
namespace BepInEx {
 [AttributeUsage(AttributeTargets.Class)]public class BepInPlugin:Attribute {public BepInPlugin(string id,string name,string version){}}
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
 [AttributeUsage(AttributeTargets.Class)]public class HarmonyPatch:Attribute {public Type Target;public string Method;public HarmonyPatch(Type target,string method){Target=target;Method=method;}}
 public class Harmony {
  public static bool ThrowOnPatch;public static MethodInfo? Prefix;
  public Harmony(string id){}
  public void PatchAll(Assembly assembly){
   foreach(var t in assembly.GetTypes()){
    var attr=t.GetCustomAttribute<HarmonyPatch>();if(attr==null)continue;
    if(attr.Target!=typeof(WuLin.GameUtil)||attr.Method!="ContainsForbiddenWord")throw new Exception("Unexpected patch target");
    Prefix=t.GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic);
   }
   if(ThrowOnPatch)throw new Exception("patch failure");
  }
  public void UnpatchSelf()=>Prefix=null;
 }
}
namespace DaxiaMods {public static class BuildGuard{public static bool Allowed=true;public static bool Check(object log)=>Allowed;}}
namespace WuLin {
 public static class GameUtil {
  public static int OriginalCalls;
  // This simulates the external game's call boundary, not its input-window behavior.
  public static bool ContainsForbiddenWord(string text){
   object[] args={true};
   if(HarmonyLib.Harmony.Prefix!=null && !(bool)HarmonyLib.Harmony.Prefix.Invoke(null,args)!)return (bool)args[0];
   OriginalCalls++;return text.Contains("测试禁词",StringComparison.Ordinal);
  }
 }
}
