using System;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using WuLin;
using WuLin.StateMachine;
using Object=UnityEngine.Object;
namespace DaxiaMods;
[BepInPlugin(Owner,"家园藏经阁","1.0.4")]
public sealed partial class FiefLibrary:BasePlugin {
 public const string Owner="local.daxia.fieflibrary";
 internal static FiefLibrary? Instance;
 ConfigEntry<bool> enabled=null!;
 Harmony? harmony;
 GameObject? window; TMP_FontAsset? font; TMP_Text? status;
 InteractiveObject? shelf; GameTimer? timer; Il2CppSystem.Object? pauseToken;
 IntPtr originalShelfPointer;bool originalShelfInteractable;
 bool visible,masked; int uiGeneration;
 public override void Load(){
  enabled=Config.Bind("General","Enabled",true,"正房指定藏经书架；停用保留当前档仓库内容。");
  if(!BuildGuard.Check(Log))return;
  Instance=this;
  try{harmony=new Harmony(Owner);harmony.PatchAll(typeof(FiefLibrary).Assembly);AddComponent<FiefLibraryWatcher>();Log.LogInfo("家园藏经阁已加载：Map_507 / SaveID 77，指定原版书架交互。");}
  catch(Exception e){Log.LogError("藏经阁加载失败："+e);Unload();}
 }
 public override bool Unload(){Close();Unbind();ReleaseView();harmony?.UnpatchSelf();Instance=null;return true;}
 internal static bool Matches(InteractiveObject? obj,int map){
  if(!obj||obj!.transform.parent?.name!="Building"||obj.transform.parent.parent?.name.Replace("(Clone)","")!="Map_507")return false;
  var view=obj.GetView();if(!view)view=obj.transform.Find("View");
  var core=view?view!.Find("guizi31_C_0"):null;
  return FiefLibraryPolicy.Target(map,obj.SaveID,obj.gameObject.name,core?core!.name:"");
 }
 internal bool Target(InteractiveObject? obj)=>enabled.Value&&Matches(obj,PlayerTeamManager.Instance?.dataContainer?.initedObject?.mapId??0);
 internal bool Available()=>enabled.Value&&GameManager.Instance&&RoamingManager.IsRoamingMode&&!GameManager.IsLoadingMap&&
  SaveManager.Instance&&!SaveManager.Instance.IsSavingOrLoading&&!RoamingManager.IsInStory&&!RoamingManager.IsTeleporting&&!RoamingManager.PrepareToBattle&&
  StoryManager.Instance&&!StoryManager.Instance.InStoryProgress&&PlayerTeamManager.Instance?.dataContainer?.initedObject?.playerTeamInventory!=null;
 internal void Guard(Action action){try{action();}catch(Exception e){Log.LogError("藏经阁："+e);if(operation.Blocked){blocked=true;Close();LogDisplayer.Instance.ShowCenterLog("藏经阁还原失败，已锁定，请重新读档。");}else if(status)status!.text=e.Message;}}
 internal void Interact(InteractiveObject obj){
  if(!Target(obj)||!Available())return;
  if(!LiveContext())Restore();if(!ready||blocked){LogDisplayer.Instance.ShowCenterLog("藏经阁数据暂不可用，请查看日志并重新读档。");return;}
  Open(obj);
 }
 internal void Open(InteractiveObject obj){
  if(visible||!Target(obj)||!Available())return;
  if(!LiveContext())Restore();if(!ready||blocked)throw new InvalidOperationException("仓库数据暂不可用，请查看日志并重新读档。");
  windowGame=GameManager.SaveData.Pointer;windowData=liveData!.Pointer;windowInventory=PlayerTeamManager.Instance.TeamInventory.Pointer;windowSlot=SaveManager.SavePath;windowGeneration=generation;
  try{
  shelf=obj;BuildWindow();
  pauseToken=window!.Cast<Il2CppSystem.Object>();timer=GameTimer.Instance;
  GameManager.RegisterGameFunctionMask(Owner,GameFunctionMask.PlayerControl|GameFunctionMask.InGameTimeElapse|GameFunctionMask.SaveGame|GameFunctionMask.PlayerTriggerNpcSight,false);masked=true;
  timer.AddOrSetTimeScale(pauseToken,0);var roaming=RoamingManager.Instance;if(roaming&&roaming!.player)roaming.player.Stop();
  visible=true;RefreshLists();
  }catch{Close();throw;}
 }
 internal void Close(){
  uiGeneration++;visible=false;
  if(left)left!.UnregisterPack();if(right)right!.UnregisterPack();left=null;right=null;selected=null;selection=null;quantity=null;transferButton=null;
  if(timer&&pauseToken!=null)timer!.RemoveTimeScale(pauseToken);timer=null;pauseToken=null;
  if(masked){GameManager.UnregisterGameFunctionMask(Owner);masked=false;}
  if(window){window!.SetActive(false);Object.Destroy(window);}window=null;status=null;
 }
 internal void Tick(){
  DataTick();BindView();
  if(visible&&(!Available()||!SameContext()||!Target(shelf)||!Application.isFocused||Input.GetKeyDown(KeyCode.Escape)))Close();
 }
 static RectTransform Rect(string name,Transform parent,Vector2 at,Vector2 size){var o=new GameObject("FiefLibrary."+name,new[]{Il2CppType.Of<RectTransform>()});o.layer=parent.gameObject.layer;var r=o.GetComponent<RectTransform>();r.SetParent(parent,false);r.anchoredPosition=at;r.sizeDelta=size;return r;}
 TMP_Text Label(Transform parent,string text,Vector2 at,Vector2 size,int points=22){var r=Rect("Text",parent,at,size);var l=r.gameObject.AddComponent<TextMeshProUGUI>();l.font=font!;l.fontSize=points;l.text=text;l.raycastTarget=false;l.color=new Color(.94f,.89f,.76f);l.alignment=TextAlignmentOptions.Center;return l;}
 Button Button(Transform parent,string text,Vector2 at,Vector2 size,Action action){var r=Rect(text,parent,at,size);var i=r.gameObject.AddComponent<Image>();i.color=new Color(.29f,.20f,.12f);var b=r.gameObject.AddComponent<Button>();b.targetGraphic=i;int token=uiGeneration;b.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>((Action)(()=>{if(visible&&token==uiGeneration)Guard(action);})));
  Label(r,text,Vector2.zero,size);return b;}
 internal void BuildWindow(){
  font=RoamingUI.Instance?.dayLabel?.font??TMP_Settings.defaultFontAsset;
  if(!font)throw new InvalidOperationException("原版中文字体尚未加载。");
  // Keep the vanilla tooltip above the modal window, using its native UI layer.
  var tipsCanvas=UIItemTIps.Instance.GetComponentInParent<Canvas>();
  if(!tipsCanvas)throw new InvalidOperationException("原版物品提示画布尚未加载。");
  // Nested UI canvases retain an unused default plane distance of 100, exactly
  // on this build's UI camera far clip. Geometry comes from the root canvas.
  var tipsRoot=tipsCanvas.rootCanvas;
  window=new GameObject("藏经阁",new[]{Il2CppType.Of<RectTransform>()});window.layer=tipsRoot.gameObject.layer;var c=window.AddComponent<Canvas>();c.renderMode=tipsRoot.renderMode;c.worldCamera=tipsRoot.worldCamera;c.planeDistance=tipsRoot.planeDistance;c.sortingLayerID=tipsCanvas.sortingLayerID;c.sortingOrder=tipsCanvas.sortingOrder-1;
  window.AddComponent<GraphicRaycaster>();var scale=window.AddComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1280,720);scale.matchWidthOrHeight=.5f;
  var shade=Rect("Shade",window.transform,Vector2.zero,Vector2.zero);shade.anchorMin=Vector2.zero;shade.anchorMax=Vector2.one;shade.offsetMin=Vector2.zero;shade.offsetMax=Vector2.zero;shade.gameObject.AddComponent<Image>().color=new Color(0,0,0,.7f);
  var panel=Rect("Panel",shade,Vector2.zero,new Vector2(1120,620));panel.gameObject.AddComponent<Image>().color=new Color(.12f,.09f,.065f);
  Label(panel,"藏经阁",new Vector2(0,268),new Vector2(500,42),30);Button(panel,"关闭",new Vector2(475,268),new Vector2(100,38),Close);
  BuildStorageUi(panel);
 }
 [HarmonyPatch(typeof(InteractiveObject),nameof(InteractiveObject.Interactivable))]
 static class InteractablePatch {static void Postfix(InteractiveObject __instance,ref bool __result){if(Instance?.Target(__instance)==true){if(Instance.originalShelfPointer!=__instance.Pointer){Instance.originalShelfPointer=__instance.Pointer;Instance.originalShelfInteractable=__result;}__result=true;}}}
 [HarmonyPatch(typeof(ObjectViewElemenetAdapator),nameof(ObjectViewElemenetAdapator.GetInteractiveDelegateAdapator))]
 static class DelegatePatch {static bool Prefix(ObjectViewElemenetAdapator __instance,ref ViewElemenetAdapator __result){if(Instance?.Target(__instance.InteractiveObject)!=true)return true;__result=__instance;return false;}}
 [HarmonyPatch(typeof(RoamingManager),nameof(RoamingManager.PlayerInteractiveWithSceneObject))]
 static class InteractPatch {static bool Prefix(RoamingManager __instance){var view=__instance.CurrentInteractiveTarget?.TryCast<ObjectViewElemenetAdapator>();if(view&&Instance?.Target(view!.InteractiveObject)==true){Instance.Guard(()=>Instance.Interact(view.InteractiveObject));return false;}return true;}}
 [HarmonyPatch(typeof(HotKeyManager),"Update")]static class Hotkeys{static bool Prefix()=>Instance?.visible!=true;}
}
public sealed class FiefLibraryWatcher:MonoBehaviour {public FiefLibraryWatcher(IntPtr p):base(p){}public void Update()=>FiefLibrary.Instance?.Guard(FiefLibrary.Instance.Tick);}
