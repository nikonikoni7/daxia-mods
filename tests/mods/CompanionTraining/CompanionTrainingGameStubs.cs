using System;
using System.Collections.Generic;
namespace Il2CppSystem {public class Object {static long next;public IntPtr Pointer {get;}=(IntPtr)(++next);}}
namespace Il2CppInterop.Runtime {public static class DelegateSupport {public static T ConvertDelegate<T>(Delegate d) where T:Delegate=>(T)Delegate.CreateDelegate(typeof(T),d.Target,d.Method);}}
namespace BepInEx {
 public static class Paths {public static string ConfigPath="";}
 [AttributeUsage(AttributeTargets.Class)]public class BepInPlugin:Attribute {public BepInPlugin(string id,string name,string version){}}
}
namespace BepInEx.Configuration {
 public class ConfigEntry<T> {public T Value;public ConfigEntry(T value){Value=value;}}
 public class ConfigFile {public ConfigEntry<T> Bind<T>(string section,string key,T value,string description)=>new(value);}
}
namespace BepInEx.Unity.IL2CPP {
 public class BasePlugin {public BepInEx.Configuration.ConfigFile Config=new();public Log Log=new();public virtual void Load(){}public virtual bool Unload()=>true;public void AddComponent<T>(){}}
 public class Log {public void LogInfo(object s){}public void LogWarning(object s){}public void LogError(object s)=>Console.WriteLine("DIAGNOSTIC "+s);}
}
namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.Class,AllowMultiple=true)]public class HarmonyPatch:Attribute {public HarmonyPatch(Type type,string method){}public HarmonyPatch(Type type,string method,Type[] args){}}
 public class Harmony {public Harmony(string id){}public void PatchAll(System.Reflection.Assembly a){}public void UnpatchSelf(){}}
}
namespace UnityEngine {public class Object:Il2CppSystem.Object {public static implicit operator bool([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] Object? o)=>o!=null;}public class MonoBehaviour:Object {public MonoBehaviour(IntPtr ptr){}}}
namespace UnityEngine.Events {public delegate void UnityAction();}
namespace DaxiaMods {public static class BuildGuard {public static bool Check(object log)=>true;}}
public class ES3File:Il2CppSystem.Object {public void Sync(){}}
namespace WuLin {
 [Flags]public enum ItemType:ulong {KungfuBook=133677056,Equip_Armor=4096}
 public class GameItemInstance:Il2CppSystem.Object {public int TempleteId;public int Stack=1;public bool IsValid=true;public GameCharacterInstance? cachedEquipingCharacter;public bool IsType(ItemType type)=>type==ItemType.KungfuBook;}
 public class GameItemPack {public Dictionary<int,GameItemInstance> Items=new();public GameItemInstance? GetItem(int id)=>Items.GetValueOrDefault(id);}
 public class GameCharacterInstance:Il2CppSystem.Object {
  public enum EquipingCheckResult {Ok,ItemInvalid,PropNotMatch,KungfuMaxLevel,CharacterMaxKungfuCount}
  public int InstanceId,TempleteUid;public bool IsValid=true,IsAlive=true;public GameItemInstance? Book;
  public EquipingCheckResult Eligibility=EquipingCheckResult.Ok;public int EquipCalls,ConditionCalls;public bool EquipSucceeds=true;public bool ThrowEquip;
  public GameItemInstance? GetEquipingItem(ItemType type)=>Book;
  public EquipingCheckResult CouldEquiping(GameItemInstance item){ConditionCalls++;return Eligibility;}
  public bool EquipItem(GameItemInstance item,bool needRecomputeProp=true,bool returnItemToPack=true,GameItemPack? pack=null){EquipCalls++;if(ThrowEquip)throw new Exception("native equip failure");if(!EquipSucceeds)return false;Book=item;item.cachedEquipingCharacter=this;return true;}
 }
 public class PlayerTeamManager:UnityEngine.Object {
  public static PlayerTeamManager Instance=new();public GameItemPack TeamInventory=new();public List<GameCharacterInstance> Members=new();public GameCharacterInstance PlayerDataInstance=new(){InstanceId=1,TempleteUid=1};
  public int TeamSize=>Members.Count;public bool HasCharacterAsMember(GameCharacterInstance c,bool includePet=true)=>Members.Contains(c);public GameCharacterInstance GetTeamMemberByIndex(int i)=>Members[i];
  public bool RemoveCharacter(GameCharacterInstance c,ItemType unequipItemType=ItemType.KungfuBook,bool returnToFief=false)=>false;
  public bool AddCharacter(GameCharacterInstance c)=>false;
 }
 public class SaveManager:UnityEngine.Object {
  public static SaveManager Instance=new();public static string SavePath="",FullPathForAllSaveFolder="";
  public bool IsSaving,IsLoading;public bool IsSavingOrLoading=>IsSaving||IsLoading;
  public ES3File m_saveFile=new(),m_saveFileBackup=new();public UnityEngine.Events.UnityAction? onSaveFinished,onLoadFinished,onReset,onAccountChange;
  public void Load(){}
 }
 public class GameManager:UnityEngine.Object {public static GameManager Instance=new();public static bool IsSlgMode;}
 public static class RoamingManager {public static bool IsRoamingMode=true;}
}
