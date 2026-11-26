using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using GameData;
using HarmonyLib;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using WuLin;
using WuLin.StateMachine;
using Object=UnityEngine.Object;
namespace DaxiaMods;
[BepInPlugin("local.daxia.chestmemo","大侠·宝箱备忘录","1.0.6")]
public sealed class ChestMemo : BasePlugin {
 const string Owner="local.daxia.chestmemo";
 internal static ChestMemo? Instance;
 ConfigEntry<bool> enabled=null!;
 ConfigEntry<KeyCode> key=null!;
 ConfigEntry<MemoGrouping> grouping=null!;
 ConfigEntry<bool> descending=null!;
 ConfigEntry<bool> timingLogs=null!;
 ConfigEntry<bool> preloadPortraits=null!;
 PortraitWarmup? portraitWarmup;
 readonly Dictionary<string,long> timingLastLog=new();
 long nativeLoadStarted;
 Harmony? harmony;
 ChestBook book=new();
 ChestStore store=null!;
 SaveManager? subscribed;
 UnityAction? loadFinished,reset,saveReady,accountChanged;
 string? activePath,activeIdentity;
 long generation;
 ChestBook? pendingSnapshot,saveCompletedBook;
 string? pendingSavePath;
 IntPtr pendingPrimary,pendingBackup;
 bool primarySynced;
 string pendingToken=Guid.NewGuid().ToString("N");
 bool explicitLoad,ready,storageBlocked,maskRegistered,firstLoad=true;
 string? saveCompletedPath,saveCompletedIdentity;
 int lastKeyFrame=-1,lastMap=int.MinValue;
 float nextReconcile;
 float nextSnapshotRetry;
 GameObject? memoRoot,dotLayer;
 UIThumbnail? mapUi;
 int shownMap=int.MinValue;
 TMP_Text? body;
 int page;
 bool visible;
 GameTimer? pausedTimer;
 Il2CppSystem.Object? pauseToken;
 public override void Load(){
  Instance=this;
  enabled=Config.Bind("General","Enabled",true,"启用宝箱备忘录和场景地图红点。");
  key=Config.Bind("Controls","ToggleKey",KeyCode.L,"打开或关闭备忘录。");
  grouping=Config.Bind("Memo","Grouping",MemoGrouping.Map,"分类：Map地图、Key钥匙、None不分类。");
  descending=Config.Bind("Memo","Descending",false,"地图名称降序排列；当前地图始终优先。");
  timingLogs=Config.Bind("Diagnostics","TimingLogs",true,"记录超过100毫秒的立绘加载、存档写盘和宝箱记录处理；同类慢操作最多每5秒记录一次，不记录对话内容。");
  preloadPortraits=Config.Bind("Performance","PreloadDialogueAtlas",true,"仅在主菜单提前准备原版共用对话立绘图集，将首次资源等待移到启动准备阶段；保留原版资源选择与释放。");
  if(!BuildGuard.Check(Log))return;
  store=new ChestStore(Path.Combine(Paths.ConfigPath,"ChestMemo"),s=>Log.LogWarning(s));
  portraitWarmup=new(s=>Log.LogInfo(s),s=>Log.LogWarning(s));
  try{harmony=new Harmony(Owner);harmony.PatchAll(typeof(ChestMemo).Assembly);AddComponent<MemoWatcher>();BindSaveEvents();Log.LogInfo("宝箱备忘录已加载："+key.Value+"查看；缺钥匙开箱失败自动记录，持有当前场景地图时显示纯红点。");}
  catch(Exception e){Log.LogError("宝箱备忘录加载失败："+e);harmony?.UnpatchSelf();}
 }
 public override bool Unload(){portraitWarmup?.Stop();Close();ClearDots();if(dotSprite)Object.Destroy(dotSprite);if(dotTexture)Object.Destroy(dotTexture);dotSprite=null;dotTexture=null;Unsubscribe();harmony?.UnpatchSelf();Instance=null;return true;}
 void Guard(Action action){try{action();}catch(Exception e){Log.LogError("宝箱备忘录："+e);}}
 internal long BeginTiming()=>timingLogs.Value?System.Diagnostics.Stopwatch.GetTimestamp():0;
 internal void EndTiming(string phase,long start){
  if(start==0)return;
  try{ReportTiming(phase,(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency);}catch{/* Diagnostics must never affect the original operation. */}
 }
 void ReportTiming(string phase,double milliseconds){
  if(!timingLogs.Value || milliseconds<100)return;
  var now=System.Diagnostics.Stopwatch.GetTimestamp();
  if(timingLastLog.TryGetValue(phase,out var last) && now-last<System.Diagnostics.Stopwatch.Frequency*5)return;
  timingLastLog[phase]=now;Log.LogWarning($"性能计时：{phase} {milliseconds:F1} ms（仅该阶段，不等于整段黑屏）。");
 }
 bool Exploring()=>GameManager.Instance && RoamingManager.IsRoamingMode && !GameManager.Instance.IsOnWorldMap && !GameManager.IsLoadingMap;
 bool Available()=>enabled.Value && ready && Exploring() && !RoamingManager.IsInStory && !RoamingManager.IsTeleporting && !RoamingManager.PrepareToBattle
  && StoryManager.Instance && !StoryManager.Instance.InStoryProgress && SaveManager.Instance && !SaveManager.Instance.IsSavingOrLoading;
 static bool MenuOpen()=>ModUiInput.BlockingMenu(true)!=null;
 internal bool Keys(){
  if(!enabled.Value){Close();return true;}
  if(!Application.isFocused){Close();return true;}
  if(lastKeyFrame==Time.frameCount)return false;
  if(visible && Input.GetKeyDown(KeyCode.Escape)){lastKeyFrame=Time.frameCount;Close();return false;}
  if(Input.GetKeyDown(key.Value) && !ModUiInput.Typing()){
   lastKeyFrame=Time.frameCount;
   if(visible)Close();else {
    var blocking=ModUiInput.BlockingMenu(true);
    if(blocking!=null)Log.LogInfo("备忘录未打开：请先关闭菜单 "+blocking);
    else if(Available())Open();
    else Log.LogInfo($"备忘录未打开：需自由探索且非剧情/读写存档；ready={ready}, exploring={Exploring()}, story={RoamingManager.IsInStory||StoryManager.Instance&&StoryManager.Instance.InStoryProgress}, teleport={RoamingManager.IsTeleporting}, battle={RoamingManager.PrepareToBattle}, saving/loading={!SaveManager.Instance||SaveManager.Instance.IsSavingOrLoading}");
   }
   return false;
  }
  return !visible;
 }
 internal void Tick(){Guard(()=>{
  portraitWarmup?.Tick(preloadPortraits.Value);
  BindSaveEvents();
  // A stale SavePath is not evidence of loading that slot. Restore only after a real Load.
  if(nativeLoadStarted!=0 && ready && Exploring() && SaveManager.Instance && !SaveManager.Instance.IsSavingOrLoading){var started=nativeLoadStarted;nativeLoadStarted=0;EndTiming("读档请求至恢复探索",started);}
  if(SaveManager.Instance && !SaveManager.Instance.IsSavingOrLoading && Time.unscaledTime>=nextSnapshotRetry)FlushSnapshot();
  Keys();
  if(visible && (!Available() || MenuOpen()))Close();
  if(!Available()){ClearDots();return;}
  var current=MapManager.Instance.GetCurMapID();
  if(lastMap!=current || ((visible || (mapUi && mapUi!.gameObject.activeInHierarchy)) && Time.unscaledTime>=nextReconcile)){lastMap=current;nextReconcile=Time.unscaledTime+2;Reconcile();}
  RefreshDots();
 });}
 void BindSaveEvents(){
  var manager=SaveManager.Instance;if(!manager || (subscribed && subscribed!.Pointer==manager.Pointer))return;
  if(subscribed is not null)Detach();Unsubscribe();subscribed=manager;
  loadFinished=DelegateSupport.ConvertDelegate<UnityAction>((Action)(()=>Guard(()=>{if(explicitLoad)Restore(firstLoad);firstLoad=false;})));
  reset=DelegateSupport.ConvertDelegate<UnityAction>((Action)(()=>Guard(NewGame)));
  saveReady=DelegateSupport.ConvertDelegate<UnityAction>((Action)(()=>Guard(SavedDataReady)));
  accountChanged=DelegateSupport.ConvertDelegate<UnityAction>((Action)(()=>Guard(Detach)));
  manager.onLoadFinished+=loadFinished;manager.onReset+=reset;manager.onSaveFinished+=saveReady;manager.onAccountChange+=accountChanged;
 }
 void Unsubscribe(){if(subscribed){if(loadFinished!=null)subscribed!.onLoadFinished-=loadFinished;if(reset!=null)subscribed!.onReset-=reset;if(saveReady!=null)subscribed!.onSaveFinished-=saveReady;if(accountChanged!=null)subscribed!.onAccountChange-=accountChanged;}subscribed=null;}
 void ClearSave(){pendingSnapshot=null;pendingSavePath=null;pendingPrimary=IntPtr.Zero;pendingBackup=IntPtr.Zero;primarySynced=false;saveCompletedBook=null;saveCompletedPath=null;saveCompletedIdentity=null;nextSnapshotRetry=0;}
 void Detach(){generation++;Close();ClearDots();book=new();activePath=null;activeIdentity=null;ClearSave();ready=false;explicitLoad=false;storageBlocked=false;lastMap=int.MinValue;pendingToken=Guid.NewGuid().ToString("N");}
 void NewGame(){var loading=explicitLoad;Detach();explicitLoad=loading;ready=!loading;if(!loading)firstLoad=false;}
 void StartNewGame(){Detach();ready=true;firstLoad=false;Log.LogInfo("宝箱备忘录：新游戏已独立初始化，不继承之前槽位。");}
 static string NativeSlotPath()=>ChestStore.ResolveSlotPath(SaveManager.SavePath,SaveManager.FullPathForAllSaveFolder);
 void FlushSnapshot(){
  if(saveCompletedPath==null || saveCompletedBook==null || storageBlocked || !ready)return;
  var path=saveCompletedPath;
  // Keep the pending association on I/O failure; the next watcher tick can retry.
  var started=BeginTiming();
  try{saveCompletedIdentity??=store.CaptureIdentity(path);store.SaveCheckpointIdentity(saveCompletedIdentity,saveCompletedBook,book);activeIdentity=saveCompletedIdentity;}catch{nextSnapshotRetry=Time.unscaledTime+5;throw;}finally{EndTiming("宝箱记录保存",started);}
  activePath=path;saveCompletedPath=null;saveCompletedBook=null;saveCompletedIdentity=null;nextSnapshotRetry=0;
  if(visible)RenderMemo();RebuildDots();
  Log.LogInfo("宝箱备忘录已关联保存快照："+book.Records.Count+"条。");
 }
 void BeforeLoad(){
  nativeLoadStarted=BeginTiming();
  if(SaveManager.Instance && !SaveManager.Instance.IsSaving)Guard(FlushSnapshot);
  Detach();explicitLoad=true;
 }
 void Restore(bool restart){
  Close();ClearDots();var path=NativeSlotPath();
  var started=BeginTiming();
  try{book=store.LoadContext(path,restart,out var identity);activeIdentity=identity;activePath=path;storageBlocked=false;}
  catch(IOException e){
   if(!Directory.Exists(path) && !File.Exists(path)){book=new();activePath=null;}
   else {storageBlocked=true;book=new();Log.LogError("宝箱记录无法恢复，已停止写入并保留原文件："+e);}
  }
  finally{EndTiming("宝箱记录读取",started);}
  ready=true;explicitLoad=false;firstLoad=false;lastMap=int.MinValue;
  if(!storageBlocked)Log.LogInfo("宝箱备忘录已恢复："+book.Records.Count+"条（"+(restart?"重启":"读档")+"）。");
 }
 void Persist(){if(storageBlocked || !ready)return;if(activeIdentity!=null)store.SaveWorkingIdentity(activeIdentity,book);else store.SavePending(pendingToken,book);if(visible)RenderMemo();RebuildDots();}
 void SavedDataReady(){
  var manager=SaveManager.Instance;
  if(!ready || storageBlocked || !enabled.Value || !manager || !manager.IsSaving || manager.m_saveFile==null || manager.m_saveFileBackup==null)return;
  ClearSave();pendingSnapshot=book.Clone();pendingSavePath=NativeSlotPath();pendingPrimary=manager.m_saveFile.Pointer;pendingBackup=manager.m_saveFileBackup.Pointer;
 }
 void Synced(ES3File file){
  if(!ready || pendingSnapshot==null || pendingSavePath==null)return;
  if(file.Pointer==pendingPrimary){primarySynced=true;return;}
  if(file.Pointer!=pendingBackup || !primarySynced)return;
  saveCompletedBook=pendingSnapshot;saveCompletedPath=pendingSavePath;pendingSnapshot=null;pendingSavePath=null;primarySynced=false;nextSnapshotRetry=0;
  // Pin the contents at actual Sync completion, not at a later watcher tick or I/O retry.
  saveCompletedIdentity=store.CaptureIdentity(saveCompletedPath);
 }
 static string ItemName(int id){var data=BaseDataClass.GetGameData<ItemDataScriptObject>().GetData(id);return data!=null?GameUtil.GetName(data,false):"物品"+id;}
 static string MapName(int id){var loc=GameLoc.Instance;var name=loc.GetLocString("WMapName_"+id);return string.IsNullOrEmpty(name) || name=="WMapName_"+id?"地图"+id:name;}
 static List<OpenRoute> Routes(InteractiveObject obj){
  var routes=new List<OpenRoute>();var conditions=obj.GetCondition();if(conditions==null)return routes;
  for(int i=0;i<conditions.Count;i++){
   var source=conditions[i];if(source==null || source.Data==null)continue;var route=new OpenRoute();
   for(int j=0;j<source.Data.Count;j++){var c=source.Data[j];if(c.ConditionType==MapConst.BuildingOpenCondition.ITEM && c.Param0>0 && c.Param1>0)route.Keys.Add(new(){Id=c.Param0,Count=c.Param1,Name=ItemName(c.Param0)});else route.HasOtherConditions=true;}
   routes.Add(route);
  }
  return routes;
 }
 sealed class Attempt {public ChestRecord Record=null!;public InteractiveObject Object=null!;public bool Missing;public long Generation;}
 Attempt? Capture(StateMachineDataContainer data){
  if(!enabled.Value || !ready || storageBlocked || !Exploring())return null;
  var state=data.GetData<InteractiveObjectStateMachineData>(StateMachineDataTypes.Interactive_OnObjectSet);
  if(state==null || !state.isChest || !state.interactiveObject || state.openItemRequire==null)return null;
  var obj=state.interactiveObject;var missing=false;var inventory=PlayerTeamManager.Instance.TeamInventory;
  foreach(var entry in state.openItemRequire)if(entry.Value>0 && inventory.GetItemCountById(entry.Key)<entry.Value)missing=true;
  if(!missing || MapManager.Instance.IsPicked(obj))return null;
  var pos=obj.m_pos;var map=MapManager.Instance.GetCurMapID();
  return new(){Generation=generation,Object=obj,Missing=missing,Record=new(){MapId=map,SaveId=obj.SaveID,TemplateId=obj.mTempID,X=pos.x,Y=pos.y,Z=pos.z,MapName=MapName(map),Routes=Routes(obj)}};
 }
 void FailedAttempt(Attempt? attempt){
  if(attempt==null || attempt.Generation!=generation || !ready || !attempt.Object || !Exploring() || MapManager.Instance.GetCurMapID()!=attempt.Record.MapId)return;
  if(book.RecordAttempt(true,MapManager.Instance.IsPicked(attempt.Object),true,attempt.Missing,attempt.Record))Log.LogInfo("已记录未开宝箱："+attempt.Record.MapName+" #"+attempt.Record.SaveId);
  Persist();
 }
 void Picked(InteractiveObject obj){if(!ready || !Exploring())return;var p=obj.m_pos;if(MapManager.Instance.IsPicked(obj) && book.Remove(MapManager.Instance.GetCurMapID(),obj.SaveID,obj.mTempID,p.x,p.y))Persist();}
 void Reconcile(){
  var map=MapManager.Instance.GetCurMapID();var changed=false;
  if(!book.Records.Any(r=>r.MapId==map))return;
  // Read only objects in the loaded scene; absent objects do not imply opened chests.
  var objects=Object.FindObjectsOfType<InteractiveObject>();
  foreach(var obj in objects){var p=obj.m_pos;if(book.Records.Any(r=>ChestBook.Matches(r,map,obj.SaveID,obj.mTempID,p.x,p.y)) && MapManager.Instance.IsPicked(obj))changed|=book.Remove(map,obj.SaveID,obj.mTempID,p.x,p.y);}
  if(changed)Persist();
 }
 static bool OwnsMap(int map){
  try{
  var table=BaseDataClass.GetGameData<ItemMapDataScriptObject>();var inventory=PlayerTeamManager.Instance?.TeamInventory;
  var id=map.ToString(CultureInfo.InvariantCulture);
  return table!=null && table.mapToItemDict!=null && inventory!=null && table.mapToItemDict.ContainsKey(id) && inventory.GetItem(table.mapToItemDict[id])!=null;
  }catch(Exception){return false;}
 }
 void MapShown(UIThumbnail ui,string id){ClearDots();mapUi=ui;shownMap=int.TryParse(id,out var value)?value:int.MinValue;if(Available())Reconcile();RebuildDots();}
 void ClearDots(){if(dotLayer)Object.Destroy(dotLayer);dotLayer=null;}
 void RebuildDots(){ClearDots();RefreshDots();}
 void RefreshDots(){
  var map=MapManager.Instance.GetCurMapID();
  var allowed=mapUi && mapUi!.gameObject.activeInHierarchy && ChestBook.ShowDots(OwnsMap(map),true,Exploring(),map,shownMap);
  if(!allowed){ClearDots();return;}
  var ui=mapUi!;var current=ui.CurMap;if(!current){ClearDots();return;}
  var calibration=current.GetComponent<UIThumbnailEditor>();
  if(!calibration || !calibration.MapPos1 || !calibration.MapPos2){ClearDots();return;}
  if(dotLayer)return;
  var a=calibration.MapPos1.localPosition;var b=calibration.MapPos2.localPosition;
  var delta=calibration.ScenePos2-calibration.ScenePos1;
  var distance=MapManager.Instance.CellToWorld(new Vector3Int(delta.x,delta.y,0)).magnitude;
  dotLayer=RectObject("ChestMemo.RedDots",current.transform).gameObject;
  dotLayer.transform.SetAsLastSibling();
  foreach(var r in book.Records.Where(r=>r.MapId==map)){
   var cellDelta=new Vector3Int(r.X-calibration.ScenePos1.x,r.Y-calibration.ScenePos1.y,r.Z);
   var world=MapManager.Instance.CellToWorld(cellDelta);
   if(!ChestBook.TryProject(world.x,world.y,a.x,a.y,b.x,b.y,distance,out var x,out var y))continue;
   var dot=RectObject("ChestMemo.RedDot",dotLayer.transform);dot.localPosition=new Vector3(x,y,0);dot.sizeDelta=new Vector2(10,10);
   var image=dot.gameObject.AddComponent<Image>();image.sprite=DotSprite();image.color=Color.red;image.raycastTarget=false;
  }
 }
 Sprite? dotSprite;
 Texture2D? dotTexture;
 Sprite DotSprite(){
  if(dotSprite)return dotSprite!;
  var texture=new Texture2D(24,24,TextureFormat.RGBA32,false);dotTexture=texture;texture.hideFlags=HideFlags.HideAndDontSave;
  for(int y=0;y<24;y++)for(int x=0;x<24;x++){var dx=x-11.5f;var dy=y-11.5f;texture.SetPixel(x,y,new Color(1,1,1,Math.Clamp(12-MathF.Sqrt(dx*dx+dy*dy),0,1)));}
  texture.Apply();dotSprite=Sprite.Create(texture,new Rect(0,0,24,24),new Vector2(.5f,.5f));return dotSprite;
 }
 static RectTransform RectObject(string name,Transform parent){var go=new GameObject(name,new[]{Il2CppType.Of<RectTransform>()});var rect=go.GetComponent<RectTransform>()!;rect.SetParent(parent,false);rect.localScale=Vector3.one;return rect;}
 TMP_FontAsset? font;
 TMP_Text Label(Transform parent,string text,Vector2 at,Vector2 size,int fontSize=22){
  var rect=RectObject("ChestMemo.Text",parent);rect.anchoredPosition=at;rect.sizeDelta=size;
  var label=rect.gameObject.AddComponent<TextMeshProUGUI>();label.text=text;label.fontSize=fontSize;label.color=Color.white;label.raycastTarget=false;label.richText=false;
  if(font)label.font=font!;return label;
 }
 void ButtonAt(Transform parent,string text,float x,float y,float width,Action click){
  var rect=RectObject("ChestMemo.Button",parent);rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(width,40);
  var image=rect.gameObject.AddComponent<Image>();image.color=new Color(.22f,.24f,.27f,.98f);
  var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(click));
  var label=Label(rect,text,Vector2.zero,rect.sizeDelta,20);label.alignment=TextAlignmentOptions.Center;
 }
 void Open(){
  if(visible)return;
  try{
   font=RoamingUI.Instance?.dayLabel?.font??TMP_Settings.defaultFontAsset;
   memoRoot=new GameObject("ChestMemo.Window",new[]{Il2CppType.Of<RectTransform>()});Object.DontDestroyOnLoad(memoRoot);
   var canvas=memoRoot.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30000;
   memoRoot.AddComponent<GraphicRaycaster>();var scaler=memoRoot.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
   var shade=RectObject("ChestMemo.InputBlocker",memoRoot.transform);shade.anchorMin=Vector2.zero;shade.anchorMax=Vector2.one;shade.offsetMin=Vector2.zero;shade.offsetMax=Vector2.zero;
   var shadeImage=shade.gameObject.AddComponent<Image>();shadeImage.color=new Color(0,0,0,.62f);shadeImage.raycastTarget=true;
   var panel=RectObject("ChestMemo.Panel",shade);panel.sizeDelta=new Vector2(820,620);panel.gameObject.AddComponent<Image>().color=new Color(.1f,.11f,.12f,1);
   Label(panel,"宝箱备忘录",new Vector2(-80,265),new Vector2(600,42),28);
   ButtonAt(panel,"关闭",340,265,90,()=>Guard(Close));
   ButtonAt(panel,"按地图",-285,205,130,()=>ChangeGrouping(MemoGrouping.Map));
   ButtonAt(panel,"按钥匙",-140,205,130,()=>ChangeGrouping(MemoGrouping.Key));
   ButtonAt(panel,"不分类",5,205,130,()=>ChangeGrouping(MemoGrouping.None));
   ButtonAt(panel,"地图名称 ↑↓",225,205,230,()=>Guard(()=>{descending.Value=!descending.Value;page=0;RenderMemo();}));
   body=Label(panel,"",new Vector2(0,-5),new Vector2(740,350),22);body.alignment=TextAlignmentOptions.TopLeft;
   ButtonAt(panel,"上一页",-240,-260,150,()=>Guard(()=>{page=Math.Max(0,page-1);RenderMemo();}));
   ButtonAt(panel,"下一页",240,-260,150,()=>Guard(()=>{page++;RenderMemo();}));
   pauseToken=memoRoot.Cast<Il2CppSystem.Object>();pausedTimer=GameTimer.Instance;
   GameManager.RegisterGameFunctionMask(Owner,GameFunctionMask.PlayerControl|GameFunctionMask.InGameTimeElapse|GameFunctionMask.SaveGame|GameFunctionMask.PlayerTriggerNpcSight|GameFunctionMask.PlayerPet,false);
   maskRegistered=true;
   pausedTimer.AddOrSetTimeScale(pauseToken,0);RoamingManager.Instance.player.Stop();visible=true;page=0;RenderMemo();
  }catch{Close();throw;}
 }
 void ChangeGrouping(MemoGrouping choice)=>Guard(()=>{grouping.Value=choice;page=0;RenderMemo();});
 void RenderMemo(){
  if(!body)return;
  if(storageBlocked){body!.text="备忘录文件读取失败，已保留原文件并停止写入。";return;}
  var current=MapManager.Instance.GetCurMapID();var groups=book.Groups(grouping.Value,current,descending.Value);
  var lines=new List<string>();
  foreach(var g in groups){if(g.Title.Length>0)lines.Add("【"+g.Title+"】");foreach(var r in g.Items)lines.Add(r.MapName+"："+ChestBook.RequirementText(r));}
  if(lines.Count==0){body!.text="尚无缺钥匙开启失败的宝箱记录。";return;}
  const int perPage=7;var pages=(lines.Count+perPage-1)/perPage;page=Math.Clamp(page,0,pages-1);
  body!.text=string.Join("\n",lines.Skip(page*perPage).Take(perPage))+"\n\n第"+(page+1)+"/"+pages+"页 · "+(descending.Value?"地图名称降序":"地图名称升序");
 }
 void Close(){
  visible=false;
  try{if(maskRegistered)GameManager.UnregisterGameFunctionMask(Owner);}finally{
   maskRegistered=false;
   if(pausedTimer && pauseToken!=null)pausedTimer!.RemoveTimeScale(pauseToken);
   pausedTimer=null;pauseToken=null;if(memoRoot)Object.Destroy(memoRoot);memoRoot=null;body=null;
  }
 }
 [HarmonyPatch(typeof(StartInteractiveWithObjectWithCostActionNode),nameof(StartInteractiveWithObjectWithCostActionNode.InvokeAction))]
 static class CostAttempt {
  static void Prefix(StateMachineDataContainer data,out Attempt? __state){Attempt? value=null;Instance?.Guard(()=>value=Instance.Capture(data));__state=value;}
  static void Postfix(Attempt? __state)=>Instance?.Guard(()=>Instance.FailedAttempt(__state));
 }
 [HarmonyPatch(typeof(MapManager),nameof(MapManager.PickHiddenItem))]
 static class Pick {static void Postfix(InteractiveObject iObj)=>Instance?.Guard(()=>Instance.Picked(iObj));}
 [HarmonyPatch(typeof(UIThumbnail),nameof(UIThumbnail.Show),new Type[]{typeof(string)})]
 static class ShowMap {static void Postfix(UIThumbnail __instance,string mapID)=>Instance?.Guard(()=>Instance.MapShown(__instance,mapID));}
 [HarmonyPatch(typeof(UIThumbnail),nameof(UIThumbnail.Hide))]
 static class HideMap {static void Prefix()=>Instance?.Guard(()=>{Instance.ClearDots();Instance.mapUi=null;});}
 [HarmonyPatch(typeof(HotKeyManager),"Update")]
 static class Hotkeys {static bool Prefix(){bool original=true;Instance?.Guard(()=>original=Instance.Keys());return original;}}
 [HarmonyPatch(typeof(SaveManager),nameof(SaveManager.Load),new Type[]{})]
 static class LoadSave {static void Prefix()=>Instance?.Guard(()=>Instance.BeforeLoad());}
 [HarmonyPatch(typeof(GameManager),nameof(GameManager.StartNewGame),new Type[]{})]
 static class StartGame {static void Prefix()=>Instance?.Guard(()=>Instance.StartNewGame());}
 [HarmonyPatch(typeof(GameManager),nameof(GameManager.QuitToMenu),new Type[]{})]
 static class LeaveGame {static void Prefix()=>Instance?.Guard(()=>Instance.Detach());}
 [HarmonyPatch(typeof(ES3File),nameof(ES3File.Sync),new Type[]{})]
 static class Saved {
  static void Prefix(out long __state){__state=Instance?.BeginTiming()??0;}
  static void Postfix(ES3File __instance)=>Instance?.Guard(()=>Instance.Synced(__instance));
  static Exception? Finalizer(Exception? __exception,long __state){Instance?.EndTiming("原版存档Sync",__state);return __exception;}
 }
 [HarmonyPatch]
 static class PortraitTiming {
  static IEnumerable<System.Reflection.MethodBase> TargetMethods()=>typeof(GameCharacterManager).GetMethods(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static).Where(m=>m.Name=="GetPortrait");
  static void Prefix(out long __state){__state=Instance?.BeginTiming()??0;}
  static Exception? Finalizer(Exception? __exception,long __state){Instance?.EndTiming("NPC立绘",__state);return __exception;}
 }
 public sealed class MemoWatcher : MonoBehaviour {
  public MemoWatcher(IntPtr pointer):base(pointer){}
  void Update()=>Instance?.Tick();
  void OnApplicationFocus(bool focus){if(!focus)Instance?.Guard(()=>Instance.Close());}
  void OnDestroy()=>Instance?.Guard(()=>Instance.Close());
 }
}
