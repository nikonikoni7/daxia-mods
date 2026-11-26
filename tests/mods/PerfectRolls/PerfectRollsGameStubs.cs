using System;
using System.Collections.Generic;
using System.Reflection;
namespace Il2CppSystem {
 public class Object {static long next;public IntPtr Pointer {get;}=(IntPtr)(++next);public T? TryCast<T>() where T:class=>this as T;}
 public struct Decimal {public int flags,hi,lo,mid;}
}
namespace Il2CppSystem.Collections.Generic {
 public class List<T>:Il2CppSystem.Object,System.Collections.Generic.IEnumerable<T> {
  readonly System.Collections.Generic.List<T> items=new();public int Count=>items.Count;public T this[int i]{get=>items[i];set=>items[i]=value;}
  public void Add(T value)=>items.Add(value);public void Clear()=>items.Clear();public System.Collections.Generic.IEnumerator<T> GetEnumerator()=>items.GetEnumerator();System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
 }
 public class Dictionary<K,V>:System.Collections.Generic.Dictionary<K,V> where K:notnull {}
}
namespace BepInEx {[AttributeUsage(AttributeTargets.Class)]public class BepInPlugin:Attribute {public BepInPlugin(string id,string name,string version){}}}
namespace BepInEx.Configuration {
 public class ConfigEntry<T>{public T Value;public ConfigEntry(T value){Value=value;}}
 public class ConfigFile{public ConfigEntry<T> Bind<T>(string s,string k,T value,string description)=>new(value);}
}
namespace BepInEx.Unity.IL2CPP {
 public class BasePlugin{public BepInEx.Configuration.ConfigFile Config=new();public Log Log=new();public virtual void Load(){}public virtual bool Unload()=>true;}
 public class Log{public void LogInfo(object value){}public void LogWarning(object value)=>Console.WriteLine("DIAGNOSTIC "+value);public void LogError(object value)=>Console.WriteLine("DIAGNOSTIC "+value);}
}
namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.Class)]public class HarmonyPatch:Attribute{}
 public class Harmony{public Harmony(string id){}public void PatchAll(Assembly a){}public void UnpatchSelf(){}}
 public static class AccessTools{public static MethodInfo Method(Type t,string name,Type[] args)=>t.GetMethod(name,args)!;}
}
namespace Il2CppInterop.Runtime.InteropTypes.Arrays {public class Il2CppReferenceArray<T>:System.Collections.Generic.List<T>{public Il2CppReferenceArray(T[] values):base(values){}}}
namespace UnityEngine{public class Object:Il2CppSystem.Object{public static implicit operator bool([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] Object? value)=>value!=null;}}
namespace DaxiaMods{public static class BuildGuard{public static bool Check(object log)=>true;}}
public class KeyWithValues:Il2CppSystem.Object {
 public string key="";public bool isValid=true;
 public Il2CppSystem.Collections.Generic.List<int> values=new();public Il2CppSystem.Collections.Generic.List<Il2CppSystem.Decimal> dValues=new();
}
public class BaseDataClass {
 public static T GetGameData<T>() where T:new()=>Tables.Get<T>();
}
static class Tables{static readonly Dictionary<Type,object> values=new();public static T Get<T>() where T:new(){if(!values.TryGetValue(typeof(T),out var o))values[typeof(T)]=o=new T();return (T)o;}}
namespace GameData {
 public class ItemData:Il2CppSystem.Object{public int Uid;public bool IsStackable;}
 public class ItemForgeData{public KeyWithValues[] BonusExt=Array.Empty<KeyWithValues>();}
 public class ItemForgeEffectData{public int Uid;public string UName="";public KeyWithValues? BonusExt;}
 public class ItemForgeDataScriptObject{public Dictionary<int,ItemForgeData> Data=new();public ItemForgeData? GetData(int id)=>Data.GetValueOrDefault(id);}
 public class ItemForgeEffectDataScriptObject{public Dictionary<int,ItemForgeEffectData> Data=new();public ItemForgeEffectData? GetData(int id)=>Data.GetValueOrDefault(id);}
}
namespace WuLin {
 public class CountedItems:List<GameItemInstance>{
  public int Scans;
  public new List<GameItemInstance>.Enumerator GetEnumerator(){Scans++;return base.GetEnumerator();}
 }
 public class ItemModifyData:Il2CppSystem.Object {
  public int templeteUid,forgeLevel=-1;public Il2CppSystem.Collections.Generic.List<float>? rawRnd;
  public Il2CppSystem.Collections.Generic.List<KeyWithValues>? overrideBonus,overrideBonusExt,overrideEquipReq;
  public Il2CppSystem.Collections.Generic.Dictionary<int,KeyWithValues>? forgeEffect=new();
 }
 public class GameItemInstance:Il2CppSystem.Object {
  public int TempleteId,Stack=1;public bool IsValid=true,IsStackable;public GameData.ItemData Templete=new();public ItemModifyData? modifyData=new();public GameCharacterInstance? cachedEquipingCharacter;
  public int ModifyReads;
  public ItemModifyData? GetModifyData(){ModifyReads++;return modifyData;}
 }
 public class GameItemPack:Il2CppSystem.Object {
  public CountedItems Contents=new();
  public bool AddItem(GameItemInstance item,bool check=false)=>true;
  public bool AddItem(GameData.ItemData item,int count=1,bool check=false)=>true;
  public bool AddItem(string name,int count=1,bool check=false)=>true;
  public bool AddItem(int id,int count=1,bool check=false)=>true;
  public bool AddItemAsIndependent(GameItemInstance item)=>true;
 }
 public class GameCharacterInstance:Il2CppSystem.Object {public Dictionary<int,GameItemInstance> EquipSlot=new();public int Refreshes;public void ComputeEquipProp(bool dirty=true,bool force=false){Refreshes++;}}
 public class PlayerTeamManager:UnityEngine.Object{public static PlayerTeamManager Instance=new();public GameItemPack TeamInventory=new();public List<GameCharacterInstance> Members=new();public int TeamSize=>Members.Count;public GameCharacterInstance GetTeamMemberByIndex(int i)=>Members[i];public GameCharacterInstance PlayerDataInstance=new();public void PickupPack(GameItemPack pack,bool hasLog=true,bool important=false,bool ability=false,bool hide=false,bool currencies=true){}}
 public class GameManager:UnityEngine.Object{public static GameManager Instance=new();public static bool IsSlgMode;}
 public class SaveManager:UnityEngine.Object{public static SaveManager Instance=new();public bool IsSavingOrLoading;public void Load(){}}
 public enum GameLogType {Left,Center,Scroll,OnlyChatLog,Achv}
 public class GameLogManager {public void OnLogRaw(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Object> paras){}public void GameLogItem(GameData.ItemData item,int amount,GameLogType type,bool important=false,GameCharacterInstance? actor=null,string format="") {}}
 public class GameConfig:UnityEngine.Object {
  public static GameConfig Instance=new();public Dictionary<string,Il2CppSystem.Decimal> Values=new();
  public bool HasConfigValue(string key)=>Values.ContainsKey(key);public Il2CppSystem.Decimal GetConfigValue(string key)=>Values[key];
 }
}
