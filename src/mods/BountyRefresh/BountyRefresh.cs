using System;
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
using Object=UnityEngine.Object;
using TaskList=Il2CppSystem.Collections.Generic.List<WuLin.SaveObjectBountyHunter.TaskData>;
namespace DaxiaMods;

[BepInPlugin(Owner,"大侠·公告栏刷新","1.0.0")]
public sealed class BountyRefresh:BasePlugin {
 internal const string Owner="local.daxia.bountyrefresh";
 internal static BountyRefresh? Instance;
 ConfigEntry<bool> enabled=null!;
 Harmony? harmony;
 BountyHunterUI? panel;
 Button? refreshButton;
 TMP_Text? status;
 IntPtr dataIdentity;
 readonly BountyRefreshOperation operation=new();
 string? failure;
 public override void Load(){
  enabled=Config.Bind("General","Enabled",true,"公告栏三个未接任务可免费整批刷新；接取后等待原版跨天刷新。");
  if(!BuildGuard.Check(Log))return;
  Instance=this;
  try{harmony=new Harmony(Owner);harmony.PatchAll(typeof(BountyRefresh).Assembly);AddComponent<BountyRefreshWatcher>();Log.LogInfo("公告栏刷新已加载：仅完整三个未接任务可刷新，不改变原版刷新日期。");}
  catch(Exception e){Unload();Log.LogError("公告栏刷新接入失败："+e);}
 }
 public override bool Unload(){DestroyButton();harmony?.UnpatchSelf();Instance=null;return true;}
 static SaveObjectBountyHunter? Data()=>BountyHunterManager.Instance?.dataContainer?.initedObject;
 internal SaveObjectBountyHunter.FactionTaskBoardData? Board(BountyHunterUI ui){
  var data=Data();var dict=data?.listFactionTaskBoard;
  if(data==null||data.Pointer!=dataIdentity||dict==null||!dict.ContainsKey(ui.BountyHunterFactionID))return null;
  return dict[ui.BountyHunterFactionID];
 }
 internal static bool Valid(TaskList? tasks)=>tasks!=null&&BountyRefreshPolicy.AllOpen(tasks.Count,
  i=>tasks[i]?.taskID??0,i=>(int)(tasks[i]?.taskState??WuLin.TaskState.Close));
 internal bool EnvironmentAvailable(BountyHunterUI ui)=>BountyRefreshPolicy.CanRefresh(enabled.Value,
  ui&&panel&&panel!.Pointer==ui.Pointer&&ui.gameObject.activeInHierarchy,
  !SaveManager.Instance||SaveManager.Instance.IsSavingOrLoading,GameManager.IsLoadingMap,
  !RoamingManager.IsRoamingMode||RoamingManager.IsInStory||RoamingManager.IsTeleporting||RoamingManager.PrepareToBattle||!StoryManager.Instance||StoryManager.Instance.InStoryProgress,operation.Busy);
 internal bool Available(BountyHunterUI ui)=>enabled.Value&&EnvironmentAvailable(ui)&&ui.factionData!=null&&Valid(Board(ui)?.listTaskData);
 internal void Refresh(BountyHunterUI ui){
  try{
   if(!Available(ui))return;
   var board=Board(ui)!;failure=null;
   operation.TryRefresh(()=>Available(ui),()=>board.listTaskData,value=>board.listTaskData=value,
    ()=>new TaskList(),()=>BountyHunterManager.Instance.TryCreateTask(ui.BountyHunterFactionID,ui.factionData),
    Valid,ui.UpdateTaskUI);
  }catch(Exception e){failure="刷新失败，已保留原任务";Log.LogError("公告栏刷新失败，原任务列表已恢复："+e);}
  finally{UpdateButton();}
 }
 internal void DestroyButton(){
  if(refreshButton)Object.Destroy(refreshButton!.gameObject);
  if(status)Object.Destroy(status!.gameObject);
  panel=null;refreshButton=null;status=null;dataIdentity=IntPtr.Zero;failure=null;
 }
 static TMP_Text CloneText(TMP_Text source,Transform parent,string name){
  var label=Object.Instantiate(source,parent,false);label.name=name;
  foreach(var loc in label.GetComponentsInChildren<LocalizationComponent>(true)){loc.enabled=false;Object.Destroy(loc);}
  label.raycastTarget=false;label.enableAutoSizing=true;label.fontSizeMin=16;label.fontSizeMax=24;
  label.alignment=TextAlignmentOptions.Center;label.text=string.Empty;return label;
 }
 internal void EnsureButton(BountyHunterUI ui){
  if(!enabled.Value){DestroyButton();return;}
  try{
   if(panel&&panel!.Pointer!=ui.Pointer)DestroyButton();
   panel=ui;dataIdentity=Data()?.Pointer??IntPtr.Zero;failure=null;
   if(!refreshButton){
    var countdown=ui.txtUpdateTime;if(!countdown)throw new InvalidOperationException("找不到原版刷新倒计时。");
    var root=new GameObject("BountyRefresh.RefreshTasks",new[]{Il2CppType.Of<RectTransform>()});
    var rect=root.GetComponent<RectTransform>();rect.SetParent(countdown.transform.parent,false);
    var timeRect=countdown.rectTransform;rect.anchorMin=timeRect.anchorMin;rect.anchorMax=timeRect.anchorMax;rect.pivot=new Vector2(0.5f,0.5f);
    rect.sizeDelta=new Vector2(180,46);rect.anchoredPosition=timeRect.anchoredPosition+new Vector2(300,0);
    refreshButton=root.AddComponent<Button>();var image=root.AddComponent<Image>();
    image.color=new Color(0.30f,0.23f,0.15f,0.95f);refreshButton.targetGraphic=image;
    var label=CloneText(countdown,rect,"Label");var labelRect=label.rectTransform;
    labelRect.anchorMin=Vector2.zero;labelRect.anchorMax=Vector2.one;labelRect.offsetMin=new Vector2(6,2);labelRect.offsetMax=new Vector2(-6,-2);label.color=new Color(1,0.91f,0.70f,1);label.text="刷新任务";
    refreshButton.onClick=new Button.ButtonClickedEvent();refreshButton.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>((Action)(()=>Refresh(ui))));
    status=CloneText(countdown,countdown.transform.parent,"BountyRefresh.Status");var statusRect=status.rectTransform;
    statusRect.anchorMin=timeRect.anchorMin;statusRect.anchorMax=timeRect.anchorMax;statusRect.pivot=new Vector2(0.5f,0.5f);
    statusRect.sizeDelta=new Vector2(440,28);statusRect.anchoredPosition=timeRect.anchoredPosition+new Vector2(150,-42);
    status.fontSizeMax=18;status.color=countdown.color;
   }
   UpdateButton();
  }catch(Exception e){DestroyButton();Log.LogError("无法创建公告栏刷新按钮："+e);}
 }
 internal void UpdateButton(){
  if(!refreshButton||!panel)return;
  if(!enabled.Value){DestroyButton();return;}
  refreshButton!.interactable=Available(panel!);
  if(status){
   var board=Board(panel!);
   status!.text=failure??(board==null?"公告栏暂不可刷新":!Valid(board.listTaskData)?"本轮已接取任务，等待原版刷新":!EnvironmentAvailable(panel!)?"当前状态暂不可刷新":"免费刷新三个任务，不消耗时间");
  }
 }
 internal void Tick(){try{if(panel&&panel!.gameObject.activeInHierarchy)UpdateButton();}catch(Exception e){DestroyButton();Log.LogError("公告栏状态检查失败，已撤销按钮："+e);}}
 [HarmonyPatch(typeof(BountyHunterUI),nameof(BountyHunterUI.Show),new[]{typeof(InteractiveObject)})]
 static class ShowPatch{static void Postfix(BountyHunterUI __instance)=>Instance?.EnsureButton(__instance);}
}
public sealed class BountyRefreshWatcher:MonoBehaviour {
 public BountyRefreshWatcher(IntPtr p):base(p){}
 public void Update()=>BountyRefresh.Instance?.Tick();
}
