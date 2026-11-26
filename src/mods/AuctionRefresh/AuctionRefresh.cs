using System;
using System.Collections.Generic;
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

[BepInPlugin(Owner,"大侠·拍卖刷新","1.0.0")]
public sealed class AuctionRefresh:BasePlugin {
 internal const string Owner="local.daxia.auctionrefresh";
 internal static AuctionRefresh? Instance;
 ConfigEntry<bool> enabled=null!;
 Harmony? harmony;
 Button? refreshButton;
 UIAuctionEntrance? entrance;
 bool busy;
 readonly Dictionary<IntPtr,TimeLimitNode> countdowns=new();
 public override void Load(){
  Instance=this;enabled=Config.Bind("General","Enabled",true,"入场预览免费刷新普通拍品，并显示珠光宝气楼下次开场倒计时；保留珍品与寄售。");
  if(!BuildGuard.Check(Log))return;
  try{harmony=new Harmony(Owner);harmony.PatchAll(typeof(AuctionRefresh).Assembly);Log.LogInfo("拍卖刷新已加载：入场预览刷新普通拍品；珠光宝气楼等待阶段显示原版倒计时。");}
  catch(Exception e){harmony?.UnpatchSelf();Instance=null;Log.LogError("拍卖刷新接入失败："+e);}
 }
 public override bool Unload(){DestroyButton();RestoreCountdowns();harmony?.UnpatchSelf();Instance=null;return true;}
 void RestoreCountdowns(){foreach(var node in countdowns.Values)if(node)node.needToNote=false;countdowns.Clear();}
 internal void EnableCountdown(StoryGraph graph,int saveId,TimeLimitNode? node){
  if(!enabled.Value){RestoreCountdowns();return;}
  if(node&&AuctionRefreshPolicy.ShowCountdown(graph.name,saveId,true)&&!node!.needToNote){countdowns[node.Pointer]=node;node.needToNote=true;}
 }
 void DestroyButton(){if(refreshButton)Object.Destroy(refreshButton!.gameObject);refreshButton=null;entrance=null;}
 internal bool Available(UIAuctionEntrance ui){
  var manager=AuctionManager.Instance;
  return AuctionRefreshPolicy.CanRefresh(enabled.Value,ui&&ui.gameObject.activeInHierarchy,manager&&manager.IsInAuction,
   !SaveManager.Instance||SaveManager.Instance.IsSavingOrLoading||GameManager.IsLoadingMap,busy)&&manager&&manager.Config!=null&&manager.history!=null;
 }
 internal void EnsureButton(UIAuctionEntrance ui){
  if(!enabled.Value)return;
  try{
   if(entrance&&entrance!.Pointer!=ui.Pointer)DestroyButton();
   entrance=ui;
   if(!refreshButton){
    var template=ui.ExitButton;
    if(!template)throw new InvalidOperationException("找不到原版退出按钮。");
    var clone=Object.Instantiate(template.gameObject,template.transform.parent,false);
    clone.name="AuctionRefresh.RefreshLots";refreshButton=clone.GetComponent<Button>();
    // Replace the whole event, including persistent inspector callbacks.
    refreshButton.onClick=new Button.ButtonClickedEvent();
    foreach(var loc in clone.GetComponentsInChildren<LocalizationComponent>(true)){loc.enabled=false;Object.Destroy(loc);}
    foreach(var text in clone.GetComponentsInChildren<TMP_Text>(true))text.text="刷新拍品";
    var rect=clone.GetComponent<RectTransform>();
    var enter=ui.EnterButton.GetComponent<RectTransform>();var exit=template.GetComponent<RectTransform>();
    // The native buttons are 207px wide with only 113.8px between them.
    // Extend the row to the left; inserting into that gap would overlap both.
    rect.anchoredPosition=new Vector2(exit.anchoredPosition.x-Math.Abs(enter.anchoredPosition.x-exit.anchoredPosition.x),exit.anchoredPosition.y);
    refreshButton.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>((Action)(()=>Refresh(ui))));
   }
   refreshButton!.gameObject.SetActive(true);refreshButton.interactable=Available(ui);
  }catch(Exception e){DestroyButton();Log.LogError("无法创建拍卖刷新按钮："+e);}
 }
 internal void Refresh(UIAuctionEntrance ui){
  if(!Available(ui))return;
  busy=true;if(refreshButton)refreshButton!.interactable=false;
  var manager=AuctionManager.Instance;var history=manager.history;var before=history.LastActivateItems;
  try{
   var pool=manager.Config.Items;
   if(pool==null||pool.Length==0)throw new InvalidOperationException("当前拍卖会没有可刷新的普通拍品池。");
   var ids=new int[pool.Length];for(int i=0;i<ids.Length;i++)ids[i]=pool[i];
   var generated=AuctionRefreshPolicy.Draw(ids,n=>UnityEngine.Random.Range(0,n),id=>new GameItemInstance(id,1,-2),manager.AuctionItemInstanceComparer);
   var next=new Il2CppSystem.Collections.Generic.List<GameItemInstance>();foreach(var item in generated)next.Add(item);
   // Publish only a complete list. Never reset history, entrance, treasure or
   // PlayerItemInstance: native Show/StartAuctionEntrance clear sell selection.
   AuctionRefreshPolicy.Publish(before,next,value=>history.LastActivateItems=value,ui.RefreshNormalItems);
  }catch(Exception e){Log.LogError("刷新失败，保留刷新前拍品："+e);}
  finally{busy=false;if(refreshButton)refreshButton!.interactable=Available(ui);}
 }
 [HarmonyPatch(typeof(UIAuctionEntrance),nameof(UIAuctionEntrance.Show))]
 static class ShowPatch{static void Postfix(UIAuctionEntrance __instance)=>Instance?.EnsureButton(__instance);}
 [HarmonyPatch(typeof(UIAuctionEntrance),nameof(UIAuctionEntrance.Hide))]
 static class HidePatch{static void Prefix(){var button=Instance?.refreshButton;if(button)button!.gameObject.SetActive(false);}}
 // IsNeedToNote is a tiny branch-heavy native method. Its trampoline reaches
 // an INT3 on this Build; patch the graph lookup instead and let all native
 // callers read their own display flag and compute time normally.
 [HarmonyPatch(typeof(StoryGraph),nameof(StoryGraph.GetActiveTimeLimitNode))]
 static class CountdownPatch{
  static void Postfix(StoryGraph __instance,int __0,TimeLimitNode __result)=>Instance?.EnableCountdown(__instance,__0,__result);
 }
}
