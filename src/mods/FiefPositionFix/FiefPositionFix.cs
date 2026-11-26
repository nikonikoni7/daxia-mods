using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using WuLin;
using WuLin.StateMachine;
namespace DaxiaMods;
[BepInPlugin("local.daxia.fiefpositionfix","大侠·大地图位置修复","1.0.2")]
public sealed class FiefPositionFix:BasePlugin
{
 static FiefPositionFix instance=null!;
 ConfigEntry<bool> enabled=null!;
 Harmony? harmony;
 [ThreadStatic] static int positioningHome;
 [ThreadStatic] static int restoringLoadedMap;
 public override void Load(){
  instance=this;
  enabled=Config.Bind("General","Enabled",true,"修复家园读档锚点和清风遗痕渡口连战收尾定位；正常快捷进出家园保持进入前位置。");
  if(!BuildGuard.Check(Log))return;
  try{harmony=new Harmony("local.daxia.fiefpositionfix");harmony.PatchAll(typeof(FiefPositionFix).Assembly);Log.LogInfo("大地图位置修复1.0.2已加载：家园读档锚点、清风遗痕渡口连战收尾；快捷进出家园保持原位置。");}
  catch(Exception e){harmony?.UnpatchSelf();Log.LogError("家园返回位置修复已停用："+e);}
 }
 public override bool Unload(){harmony?.UnpatchSelf();positioningHome=0;restoringLoadedMap=0;return true;}
 [HarmonyPatch(typeof(DllScenarioDelegate),"EnterMapNodeStartTeleport")]
 static class FerryEnterPatch {
  static void Prefix(EnterMapNode __0,out bool __state){
   __state=false;
   if(!instance.enabled.Value||!__0||!__0.graph||__0.mapID!=1002||__0.moveWorldTag||__0.TagID!=0||__0.pos.x!=0||__0.pos.y!=0)return;
   var name=__0.graph?.name;
   if(name!="EJ-003-38"&&name!="EJ-003-40")return;
   // Both original endings omit the tag update. Let the native transition
   // select the real ferry anchor; restore the shared node even on failure.
   __state=true;__0.TagID=5025;__0.moveWorldTag=true;
  }
  static void Finalizer(EnterMapNode __0,bool __state){if(__state){__0.moveWorldTag=false;__0.TagID=0;}}
 }
 [HarmonyPatch(typeof(UIWorldMapManager),nameof(UIWorldMapManager.OnGameLoaded))]
 static class LoadPatch {
  static void Prefix(out int __state){__state=restoringLoadedMap;restoringLoadedMap++;}
  static void Finalizer(int __state)=>restoringLoadedMap=__state;
 }
 [HarmonyPatch(typeof(UIWorldMapManager),nameof(UIWorldMapManager.initTarget))]
 static class LoadedTargetPatch {
  static void Prefix(UIWorldMapManager __instance){
   if(restoringLoadedMap<=0||!instance.enabled.Value)return;
   try{
    var record=Path.Combine(Paths.ConfigPath,"FiefPositionFix","dlc-ferry-recovery.json");
    if(!File.Exists(record)||string.IsNullOrWhiteSpace(SaveManager.SavePath)||PlayerTeamManager.Instance.CurMapId!=1002)return;
    var marks=__instance.GetSaveMark();
    if(marks==null||!marks.TryGetValue(1002,out var mark)||mark==null||mark.markMapID!=5001||!mark.isStandOn||mark.markOffsetPos.x!=0||mark.markOffsetPos.y!=0)return;
    var maps=__instance.listWorldMap;
    if(maps==null||!maps.TryGetValue(1002,out var map)||map==null||!map)return;
    var icons=map.CityIcon;if(icons==null||!icons||!icons.Find("5025/imgType"))return;
    // Optional, locally prepared evidence pins the entire original checkpoint.
    // No story-state heuristic may move arbitrary later visits to the graveyard.
    var slot=Path.IsPathFullyQualified(SaveManager.SavePath)?Path.GetFullPath(SaveManager.SavePath):Path.GetFullPath(SaveManager.SavePath,SaveManager.FullPathForAllSaveFolder);
    using var json=JsonDocument.Parse(File.ReadAllText(record));var root=json.RootElement;
    if(root.GetProperty("Schema").GetInt32()!=1||!string.Equals(Path.GetFullPath(root.GetProperty("SlotPath").GetString()!),slot,StringComparison.OrdinalIgnoreCase))return;
    var entries=root.GetProperty("Files");var files=Directory.GetFiles(slot,"*.save");
    if(files.Length==0||entries.GetArrayLength()!=files.Length)return;
    var checkedNames=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach(var entry in entries.EnumerateArray()){
     var name=entry.GetProperty("Name").GetString();
     if(string.IsNullOrEmpty(name)||name!=Path.GetFileName(name)||!name.EndsWith(".save",StringComparison.OrdinalIgnoreCase)||!checkedNames.Add(name))return;
     using var stream=File.OpenRead(Path.Combine(slot,name));using var sha=SHA256.Create();
     if(!string.Equals(Convert.ToHexString(sha.ComputeHash(stream)),entry.GetProperty("Sha256").GetString(),StringComparison.OrdinalIgnoreCase))return;
    }
    // Use native mark setters before native initTarget consumes the loaded marks.
    // This changes runtime state only. Only the player's normal save persists it.
    __instance.SetMarkOffsetPos(1002,5025,UnityEngine.Vector2.zero);__instance.SetMarkStand(1002,true);
    instance.Log.LogInfo("已按完整存档指纹恢复本次清风遗痕异常检查点：乱葬岗→中州渡口；未写入原版存档文件。");
   }catch(Exception e){instance.Log.LogError("清风遗痕检查点恢复未执行，保留原版位置："+e.Message);}
  }
 }
 [HarmonyPatch(typeof(UIWorldMap),nameof(UIWorldMap.SetPos))]
 static class PositionPatch {
  static void Prefix(UIWorldMap __instance,int __0,out int __state){
   __state=positioningHome;
   try{
    if(restoringLoadedMap<=0||!instance.enabled.Value||__0<=0||!MapManager.Instance)return;
    // Ask the native classifier before entering the narrowly scoped override.
    // A real imgType anchor is required; never invent a destination if absent.
    if(positioningHome==__0||MapManager.Instance.IsFiefMainMapID(__0)){
     var cities=__instance.CityIcon;
     var icon=cities!=null&&cities?cities.Find(__0.ToString(System.Globalization.CultureInfo.InvariantCulture)):null;
     if(icon!=null&&icon&&icon.Find("imgType"))positioningHome=__0;
    }
   }catch(Exception e){positioningHome=__state;instance.Log.LogError("家园位置锚点检查失败，保留原版流程："+e.Message);}
  }
  // Harmony finalizers execute for both success and exception paths. Restoring
  // the previous value also keeps nested SetPos calls and unrelated maps safe.
  static void Finalizer(int __state)=>positioningHome=__state;
 }
 [HarmonyPatch(typeof(MapManager),nameof(MapManager.IsFiefMainMapID))]
 static class HomeCheckPatch {
  static bool Prefix(int __0,ref bool __result){
   if(positioningHome<=0||positioningHome!=__0)return true;
   // Only UIWorldMap.SetPos's special-case branch is bypassed. All coordinate,
   // offset, marker, movement and save bookkeeping still run in native code.
   __result=false;return false;
  }
 }
}
