using System;
using System.IO;
using System.Text.Json;
using BepInEx;
using HarmonyLib;
using WuLin;
namespace DaxiaMods;
public sealed partial class FiefLibrary {
 // Optional local evidence is prepared only after inspecting a damaged slot.
 // It never ships with the plugin and never writes the player's save files.
 void RestoreIncidentLocation(SaveObjectGame game){
  if(!enabled.Value||!SaveManager.Instance||!SaveManager.Instance.IsLoading||!game||!game.isOnWorldMap)return;
  string record=Path.Combine(Paths.ConfigPath,"FiefLibrary","location-recovery.json");
  if(!File.Exists(record)||GameManager.dataContainer?.initedObject?.Pointer!=game.Pointer)return;
  try{
   var pin=JsonSerializer.Deserialize<LibraryLocationRecovery>(File.ReadAllText(record))??throw new InvalidDataException("位置恢复记录为空。");
   string slot=Slot();if(!pin.Matches(slot)||!game.miscIntDict.TryGetValue("PlayerMapID",out var old)||old!=pin.SourceMap)return;
   var mapText=LibraryCheckpoint.Member(CheckpointText(TeamFile(slot)),"Data","value","mapId");
   if(!int.TryParse(mapText,out var teamMap)||teamMap!=pin.TargetMap)throw new InvalidDataException("目标场景与指纹绑定的队伍检查点不符。");
   game.miscIntDict["PlayerMapID"]=teamMap;game.isOnWorldMap=false;
   Log.LogInfo("已按完整存档指纹在内存中恢复位置："+old+"→"+teamMap+"；原始存档未改写，请确认场景后正常保存。");
  }catch(Exception e){Log.LogError("位置恢复未执行，保留原版读档："+e.Message);}
 }
 [HarmonyPatch(typeof(SaveObjectGame),nameof(SaveObjectGame.LoadInto))]
 static class LocationRecovery {static void Postfix(SaveObjectGame __instance)=>Instance?.RestoreIncidentLocation(__instance);}
}
