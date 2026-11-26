using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using WuLin;
namespace DaxiaMods;
[BepInPlugin("local.daxia.explorecamera","大侠·探索自由镜头","1.0.4")]
public sealed class ExploreCamera : BasePlugin
{
 private const string Owner="local.daxia.explorecamera";
 internal static ExploreCamera Instance=null!;
 private ConfigEntry<bool> enabled=null!;
 private ConfigEntry<KeyCode> key=null!;
 private ConfigEntry<int> edge=null!;
 private ConfigEntry<float> speed=null!;
 private ConfigEntry<float> smoothing=null!;
 private float velocityX,velocityY;
 private readonly PauseLease pause=new();
 private Harmony? harmony;
 private GameObject? token;
 private CameraFollowTarget? follower;
 private Vector3 cameraStart;
 private int map;
 private bool failed;
 internal static bool Active=>Instance!=null && Instance.pause.Active;
 public override void Load() {
  Instance=this;
  enabled=Config.Bind("General","Enabled",true,"启用探索自由镜头。");
  key=Config.Bind("Controls","ToggleKey",KeyCode.Y,"切换自由镜头的键。");
  edge=Config.Bind("Controls","EdgePixels",24,"屏幕边缘移动区域宽度。");
  speed=Config.Bind("Controls","ScreensPerSecond",1f,"每秒移动的屏幕宽度，范围0.1～3。");
  smoothing=Config.Bind("Controls","SmoothSeconds",0.04f,"起停和转向的平滑响应时间。默认0.04秒，约0.12秒达到95%目标速度；范围0～0.2，0关闭缓动。");
  if(!BuildGuard.Check(Log))return;
  try{
   harmony=new Harmony(Owner);harmony.PatchAll(typeof(ExploreCamera).Assembly);
   AddComponent<CameraWatcher>();
   Log.LogInfo("探索自由镜头已加载：Y切换，鼠标边缘移动；右键地面恢复游戏并寻路；世界地图与战斗禁用。");
  }catch(Exception e){Fail(e);}
 }
 public override bool Unload(){Exit();harmony?.UnpatchSelf();return true;}
 private bool Allowed() {
  // 先拒绝非探索状态，避免在主菜单或战斗访问尚未初始化的场景对象。
  if(!GameManager.Instance || !RoamingManager.IsRoamingMode || GameManager.Instance.IsOnWorldMap || GameManager.IsLoadingMap)return false;
  if(!StoryManager.Instance)return false;
  return GameManager.Instance && Rules.SceneAllowed(RoamingManager.IsRoamingMode,
   GameManager.Instance.IsOnWorldMap,GameManager.IsLoadingMap,
   RoamingManager.IsInStory || StoryManager.Instance.InStoryProgress || RoamingManager.IsTeleporting || RoamingManager.PrepareToBattle,
   ModalOpen());
 }
 private static bool ModalOpen() {
  var stack=UiFunctionGroup.displayingUiStack;
  if(stack==null || stack.Count==0)return false;
  // 提示可能盖在窗口上方；检查整栈，避免提示遮住真正的菜单。
  for(var i=stack.Count-1;i>=0;i--)if(IsBlockingUi(stack[i]))return true;
  return false;
 }
 private static bool IsBlockingUi(UiFunctionGroup ui) {
  if(!ui || ui.isAlwaysActived || ui.GetComponentInParent<RoamingUI>())return false;
  // 状态栏悬停通过UITipsManager打开UIItemTIps，不应取消自由镜头。
  if(ui.GetComponentInParent<UIItemTIps>())return false;
  var reference=ui.GetComponentInParent<UiRefHelper>();
  return !reference || reference.uiLayer!=UIRoot.GameUiLayer.ToolTip;
 }
 private void Enter() {
  if(!Allowed() || !RoamingManager.PlayerIsControlable || !Application.isFocused)return;
  follower=CameraFollowTarget.Instance;
  var border=CameraFollowBorder.Instance;
  if(!follower || !follower.followTarget || !border || !border.CachedTileMap)return;
  var sceneCamera=follower.GetComponentInChildren<Camera>();
  if(!sceneCamera || !sceneCamera.orthographic)throw new InvalidOperationException("当前场景镜头接口不符合适配版本，停止自由镜头。");
  token=new GameObject("DaxiaExploreCameraPause");
  UnityEngine.Object.DontDestroyOnLoad(token);
  cameraStart=follower.transform.position;
  velocityX=velocityY=0;
  map=MapManager.Instance.GetCurMapID();
  var timer=GameTimer.Instance;var pauseKey=token.Cast<Il2CppSystem.Object>();
  pause.Enter(()=>{
   RoamingManager.Instance.player.Stop();
   GameManager.RegisterGameFunctionMask(Owner,GameFunctionMask.PlayerControl|GameFunctionMask.InGameTimeElapse|GameFunctionMask.SaveGame|GameFunctionMask.PlayerTriggerNpcSight|GameFunctionMask.PlayerPet,false);
   timer.AddOrSetTimeScale(pauseKey,0);
  },()=>{
   try{GameManager.UnregisterGameFunctionMask(Owner);}finally{if(timer)timer.RemoveTimeScale(pauseKey);}
  });
  Log.LogInfo("自由镜头开启：场景"+map);
 }
 internal void Exit() {
  if(!pause.Active && !token)return;
  try{pause.Exit();}finally{
   try{
    if(follower && MapManager.Instance && MapManager.Instance.GetCurMapID()==map) {
     follower!.transform.position=cameraStart;follower.DoSnap();
    }
   }finally{
    if(token)UnityEngine.Object.Destroy(token);
    token=null;follower=null;velocityX=velocityY=0;
   }
  }
  Log.LogInfo("自由镜头关闭，暂停标识已释放。");
 }
 private void Fail(Exception e) {
  failed=true;
  try{Exit();}catch(Exception cleanup){Log.LogError(cleanup);}
  harmony?.UnpatchSelf();Log.LogError("自由镜头已停用："+e);
 }
 private void HotkeyTick() {
  if(failed || !enabled.Value){if(Active)Exit();return;}
  try {
   if(Active && (!Application.isFocused || !Allowed() || MapManager.Instance.GetCurMapID()!=map))Exit();
   if(Application.isFocused && Input.GetKeyDown(key.Value)) {
    if(Active)Exit();else if(Allowed())Enter();
    return;
   }
   if(Active && Input.GetKeyDown(KeyCode.Escape)){Exit();return;}
   if(Active && Input.GetMouseButtonDown(1))MoveFromCamera();
  }catch(Exception e){Fail(e);}
 }
 internal void Watch() {
  // 独立轮询切换键，避免接管原版快捷键更新或在输入回调内移除该回调的补丁。
  HotkeyTick();
 }
 private void MoveFromCamera() {
  var pointer=Input.mousePosition;
  if(!Application.isFocused || !Allowed() || !float.IsFinite(pointer.x) || !float.IsFinite(pointer.y)
     || pointer.x<0 || pointer.y<0 || pointer.x>=Screen.width || pointer.y>=Screen.height || InputManager.IsPointerOverUI())return;
  var gameCamera=GameCamera.Instance;
  if(!gameCamera || !gameCamera.MainCamera || !MapManager.Instance)
   throw new InvalidOperationException("右键寻路所需的场景镜头或地图缺失。");
  // 与原版InputManager一致；必须在Exit复位镜头前完成屏幕位置到地图格子的转换。
  var renderPosition=gameCamera.ScreenPosToRenderCameraPos(new Vector3(pointer.x,pointer.y,0));
  var worldPosition=gameCamera.MainCamera.ScreenToWorldPoint(renderPosition);
  if(!float.IsFinite(worldPosition.x) || !float.IsFinite(worldPosition.y) || !float.IsFinite(worldPosition.z))
   throw new InvalidOperationException("右键寻路坐标转换结果无效。");
  var destination=MapManager.Instance.WorldToCell(worldPosition);
  Exit();
  RoamingManager.Instance.PlayerMoveTo(destination,RoamingManager.Instance.GetKeyRunActive());
  Log.LogInfo("右键寻路："+destination+"；已恢复正常游戏。");
 }
 private void Pan(CameraFollowTarget current) {
  try {
   if(!follower || current.Pointer!=follower!.Pointer){Exit();return;}
   if(CameraFollowBorder.Instance.noScrolling){velocityX=velocityY=0;return;}
   var m=Input.mousePosition;
   // 界面和窗口外立即停止；仅在正常画面内离开边缘时允许短暂减速。
   if(!Application.isFocused || InputManager.IsPointerOverUI() || !float.IsFinite(m.x) || !float.IsFinite(m.y)
      || m.x<0 || m.y<0 || m.x>=Screen.width || m.y>=Screen.height) {velocityX=velocityY=0;return;}
   var dir=Rules.Edge(m.x,m.y,Screen.width,Screen.height,edge.Value,true,false);
   if(dir.X==0 && dir.Y==0 && velocityX==0 && velocityY==0)return;
   var camera=current.GetComponentInChildren<Camera>();if(!camera || !camera.orthographic)return;
   var rate=float.IsFinite(speed.Value)?Math.Clamp(speed.Value,0.1f,3f):1f;
   var x=CameraMotion.SmoothAxis(velocityX,dir.X*rate,Time.unscaledDeltaTime,smoothing.Value);
   var y=CameraMotion.SmoothAxis(velocityY,dir.Y*rate,Time.unscaledDeltaTime,smoothing.Value);
   velocityX=x.Velocity;velocityY=y.Velocity;
   var width=2*camera.orthographicSize*camera.aspect;
   var before=current.transform.position;
   var pos=before+current.transform.right*(x.Distance*width)+current.transform.up*(y.Distance*width);
   var limited=ClampTarget(pos);
   current.transform.position=limited;
   // 被边界完全挡住时清空速度，防止反向离开边界还要先消除旧惯性。
   if(limited.x==before.x && limited.y==before.y)velocityX=velocityY=0;
  }catch(Exception e){Fail(e);}
 }
 private static Vector3 ClampTarget(Vector3 target) {
  var border=CameraFollowBorder.Instance;
  if(border.noScrolling)return border.CachedTileMap.GetCellCenterWorld(border.anchorTile);
  var tile=border.CachedTileMap;
  var origin=tile.LocalToWorld(tile.CellToLocalInterpolated(Vector3.zero));
  var x=tile.LocalToWorld(tile.CellToLocalInterpolated(new Vector3(1,0,0)))-origin;
  var y=tile.LocalToWorld(tile.CellToLocalInterpolated(new Vector3(0,1,0)))-origin;
  var p=target-origin;float det=x.x*y.y-x.y*y.x;
  if(Math.Abs(det)<0.000001f)throw new InvalidOperationException("场景镜头边界坐标无法解析。");
  var cx=(p.x*y.y-p.y*y.x)/det;var cy=(x.x*p.y-x.y*p.x)/det;
  cx=Math.Clamp(cx,Math.Min(border.xMin,border.xMax),Math.Max(border.xMin,border.xMax));
  cy=Math.Clamp(cy,Math.Min(border.yMin,border.yMax),Math.Max(border.yMin,border.yMax));
  var result=origin+x*cx+y*cy;result.z=target.z;return result;
 }
 [HarmonyPatch(typeof(CameraFollowTarget),"LateUpdate")]
 private static class Follow {
  private static bool Prefix(CameraFollowTarget __instance) {if(!Active)return true;Instance.Pan(__instance);return !Active;}
 }
 [HarmonyPatch(typeof(RoamingManager),"Update")]
 private static class Roam {private static bool Prefix()=>!Active;}
 [HarmonyPatch(typeof(TimeDelayCall),"Update")]
 private static class Delay {private static bool Prefix()=>!Active;}
 [HarmonyPatch(typeof(GameManager),nameof(GameManager.LoadMap))]
 private static class LoadMap {private static void Prefix(){if(Active)Instance.Exit();}}
 [HarmonyPatch(typeof(GameManager),nameof(GameManager.SwitchInstantBattle))]
 private static class Battle {private static void Prefix(){if(Active)Instance.Exit();}}
 [HarmonyPatch(typeof(StoryManager),"set_InStoryProgress")]
 private static class Story {private static void Prefix(bool value){if(value && Active)Instance.Exit();}}
 [HarmonyPatch(typeof(UiFunctionGroup),"OnEnable")]
 private static class Ui {
  private static void Prefix(UiFunctionGroup __instance){if(Active && IsBlockingUi(__instance))Instance.Exit();}
 }
 public sealed class CameraWatcher : MonoBehaviour {
  public CameraWatcher(IntPtr ptr):base(ptr){}
  private void Update()=>Instance.Watch();
  private void OnApplicationFocus(bool focus){if(!focus)try{Instance.Exit();}catch(Exception e){Instance.Fail(e);}}
  private void OnDestroy(){try{Instance.Exit();}catch(Exception e){Instance.Fail(e);}}
 }
}
