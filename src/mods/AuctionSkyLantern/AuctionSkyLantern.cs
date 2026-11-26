using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using ET;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using WuLin;
using Object=UnityEngine.Object;
namespace DaxiaMods;

[BepInPlugin(Owner,"大侠·拍卖点天灯","1.0.1")]
public sealed class AuctionSkyLantern:BasePlugin {
 internal const string Owner="local.daxia.auctionskylantern";
 internal static AuctionSkyLantern? Instance;
 ConfigEntry<bool> enabled=null!;
 Harmony? harmony;
 readonly AuctionSkyLanternState state=new();
 readonly Il2CppSystem.Object speedKey=new();
 GameTimer? timer;
 AuctionManager? boundManager;
 IntPtr saveIdentity,saveManagerIdentity;
 UIAuctionProcessing? panel;
 Button? lantern;
 Button[] originals=Array.Empty<Button>();
 readonly List<(Button Button,bool Enabled,Sprite Sprite)> lockedControls=new();
 bool submitting;
 IntPtr reminderToken;
 Navigation lastNavigation=null!;
 Button? lastButton;

 public override void Load(){
  enabled=Config.Bind("General","Enabled",true,"为当前拍品点天灯：按原版放弃后的速度叫价，停价后自动加一成；本件不能放弃。");
  if(!BuildGuard.Check(Log))return;
  Instance=this;
  try{harmony=new Harmony(Owner);harmony.PatchAll(typeof(AuctionSkyLantern).Assembly);AddComponent<LanternWatcher>();Log.LogInfo("拍卖点天灯已加载：仅当前拍品生效，NPC停价后原版加一成。");}
  catch(Exception e){Unload();Log.LogError("点天灯接入失败："+e);}
 }
 public override bool Unload(){Clear();DestroyButton();harmony?.UnpatchSelf();Instance=null;return true;}
 void Guard(Action action){try{action();}catch(Exception e){Clear();Log.LogError("点天灯操作失败，已解除本件自动竞价："+e);}}
 bool Same(AuctionManager manager)=>manager&&manager.CurItem!=null&&state.Matches(manager.Pointer.ToInt64(),manager.CurItem.Pointer.ToInt64(),manager.CurRound);
 bool ContextValid(AuctionManager manager)=>enabled.Value&&manager&&manager.IsInAuction&&!manager.Breaked&&manager.IsRoundProcessing&&
  !GameManager.IsLoadingMap&&SaveManager.Instance&&!SaveManager.Instance.IsSavingOrLoading;
 bool Locked(AuctionManager manager){
  if(boundManager==null)return false;
  if(!ContextValid(manager)||!Same(manager)||SaveManager.Instance.Pointer!=saveManagerIdentity||manager.dataContainer?.initedObject?.Pointer!=saveIdentity){Clear();return false;}
  return true;
 }
 AuctionBidStrategy? Strategy(AuctionManager manager){
  var strategies=manager.Player?.Strategies;if(strategies==null)return null;
  foreach(var strategy in strategies)if(strategy!=null&&Math.Abs(strategy.IncreaseRate-.1f)<.00001f)return strategy;
  return null;
 }
 internal bool CanLight(UIAuctionProcessing ui){
  var manager=AuctionManager.Instance;
  return ui&&panel&&panel!.Pointer==ui.Pointer&&ui.gameObject.activeInHierarchy&&ui.BidButtonGroupGameObject&&ui.BidButtonGroupGameObject.activeInHierarchy&&
   originals.Length==5&&ContextValid(manager)&&manager.Config!=null&&manager.CurItem!=null&&!manager.CurItem.Saled&&manager.CurItem.Type!=ActionItemType.FromPlayer&&
   !manager.IsGaveUp&&manager.IsBidTime()&&manager.dataContainer?.initedObject!=null&&boundManager==null&&Strategy(manager)!=null;
 }
 internal void Light(UIAuctionProcessing ui){
  if(!CanLight(ui))return;
  var manager=AuctionManager.Instance;
  if(!state.Start(manager.Pointer.ToInt64(),manager.CurItem.Pointer.ToInt64(),manager.CurRound))return;
  boundManager=manager;saveIdentity=manager.dataContainer.initedObject.Pointer;saveManagerIdentity=SaveManager.Instance.Pointer;
  try{timer=GameTimer.Instance;timer.AddOrSetTimeScale(speedKey,5f);ApplyControls();Log.LogInfo("当前拍品已点天灯，原版轮次："+manager.CurRound);}
  catch{Clear();throw;}
 }
 internal void ObserveWait(float seconds,ETCancellationToken token){
  var manager=AuctionManager.Instance;
  // Build 24794582: initial wait and first reminder wait are 5 seconds;
  // after the first spoken reminder the next bid window is 3 seconds.
  // Match the native auction token, never unrelated three-second timers.
  if(!ContextValid(manager)||token==null||manager.waitBidCancelToken?.Pointer!=token.Pointer||Math.Abs(seconds-3f)>.00001f)return;
  reminderToken=token.Pointer;
  TryAutoBid(manager);
 }
 internal void TryAutoBid(AuctionManager manager){
  if(submitting||!Locked(manager)||!manager.IsBidTime())return;
  var queue=manager.bidInfoQueue;
  bool noBids=queue.Count==0&&manager.bidNpcQueue.Count==0;
  if(!noBids&&(reminderToken==IntPtr.Zero||manager.waitBidCancelToken?.Pointer!=reminderToken))return;
  if(queue.Count>0&&queue[queue.Count-1]?.Bidder?.Pointer==manager.Player?.Pointer)return;
  var strategy=Strategy(manager);
  if(strategy==null||manager.Player==null)throw new InvalidOperationException("原版玩家竞拍角色或加一成策略不可用。");
  // CanBid also checks native cooldown. Wait for it instead of reporting
  // insufficient money just after an earlier lot's successful bid.
  if(Time.time<strategy.LastUseTime+3f)return;
  try{
   if(!state.Consume(manager.Pointer.ToInt64(),manager.CurItem.Pointer.ToInt64(),manager.CurRound))return;
   if(!manager.Player.CanBid(strategy)){LogDisplayer.Instance.ShowCenterLog("点天灯：金钱不足，无法加一成，本件自动竞价结束。");return;}
   int before=queue.Count;
   submitting=true;
   try{manager.PlayerBid(strategy);}
   finally{submitting=false;}
   if(queue.Count<=before||queue[queue.Count-1]?.Bidder?.Pointer!=manager.Player.Pointer)throw new InvalidOperationException("原版没有确认玩家出价。");
   Log.LogInfo("点天灯已在原版等待报价阶段加一成，金额："+queue[queue.Count-1].Money);
  }catch(Exception e){Log.LogError("点天灯自动出价失败："+e);}
 }
 void SetLabel(string value){
  if(!lantern)return;
  foreach(var text in lantern!.GetComponentsInChildren<TMP_Text>(true))text.text=value;
  foreach(var text in lantern.GetComponentsInChildren<Text>(true))text.text=value;
 }
 void ApplyControls(){
  if(!lantern)return;
  bool active=boundManager!=null;
  SetLabel(active?"已点天灯":"点天灯");lantern!.interactable=!active&&panel&&CanLight(panel!);
  if(!active)return;
  foreach(var button in originals){
   if(!button)continue;
   if(lockedControls.Count<originals.Length)lockedControls.Add((button,button.interactable,button.image?button.image.sprite:null!));
   button.interactable=false;
   if(button.image&&panel)button.image.sprite=button.Pointer==originals[0].Pointer?panel!.GiveUpLockSprite:panel!.BidLockSprite;
  }
 }
 void Clear(){
  state.Reset();boundManager=null;saveIdentity=IntPtr.Zero;saveManagerIdentity=IntPtr.Zero;submitting=false;reminderToken=IntPtr.Zero;
  try{if(timer)timer!.RemoveTimeScale(speedKey);}finally{timer=null;}
  foreach(var saved in lockedControls)if(saved.Button){saved.Button.interactable=saved.Enabled;if(saved.Button.image)saved.Button.image.sprite=saved.Sprite;}
  lockedControls.Clear();SetLabel("点天灯");
 }
 void DestroyButton(){
  if(lastButton)lastButton!.navigation=lastNavigation;lastButton=null;
  if(lantern){lantern!.gameObject.SetActive(false);Object.Destroy(lantern.gameObject);}lantern=null;panel=null;originals=Array.Empty<Button>();
 }
 internal void EnsureButton(UIAuctionProcessing ui){
  if(!enabled.Value)return;
  if(panel&&panel!.Pointer==ui.Pointer&&lantern)return;
  Clear();DestroyButton();
  var group=ui.BidButtonGroupGameObject;if(!group||group.transform.childCount<5)throw new InvalidOperationException("找不到原版五个拍卖按钮。");
  originals=new Button[5];
  for(int i=0;i<5;i++){originals[i]=group.transform.GetChild(i).GetComponent<Button>();if(!originals[i])throw new InvalidOperationException("原版拍卖按钮结构不匹配。");}
  panel=ui;
  try{
   // Clone while inactive so copied hotkeys cannot register their callbacks.
   var holder=new GameObject("AuctionSkyLantern.CloneHolder");holder.SetActive(false);
   GameObject clone;
   try{clone=Object.Instantiate(originals[1].gameObject,holder.transform,false);
    foreach(var hotkey in clone.GetComponentsInChildren<JoystickHotkeyButton>(true)){hotkey.enabled=false;Object.Destroy(hotkey);}
    foreach(var hotkey in clone.GetComponentsInChildren<JoystickHotkeyEvent>(true)){hotkey.enabled=false;Object.Destroy(hotkey);}
    foreach(var hotkey in clone.GetComponentsInChildren<HotkeyListener>(true)){hotkey.enabled=false;Object.Destroy(hotkey);}
    foreach(var loc in clone.GetComponentsInChildren<LocalizationComponent>(true)){loc.enabled=false;Object.Destroy(loc);}
    lantern=clone.GetComponent<Button>();lantern.onClick=new Button.ButtonClickedEvent();
    clone.name="AuctionSkyLantern.Light";
    var rect=clone.GetComponent<RectTransform>();var last=originals[4].GetComponent<RectTransform>();var previous=originals[3].GetComponent<RectTransform>();
    rect.anchoredPosition=last.anchoredPosition+(last.anchoredPosition-previous.anchoredPosition);
    clone.transform.SetParent(group.transform,false);
   }finally{Object.Destroy(holder);}
   lantern!.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>((Action)(()=>Guard(()=>Light(ui)))));
   lastButton=originals[4];lastNavigation=lastButton.navigation;var nav=lastButton.navigation;nav.mode=Navigation.Mode.Explicit;nav.selectOnLeft=originals[3];nav.selectOnRight=lantern;lastButton.navigation=nav;
   nav=lantern.navigation;nav.mode=Navigation.Mode.Explicit;nav.selectOnLeft=lastButton;nav.selectOnRight=originals[0];lantern.navigation=nav;
   SetLabel("点天灯");lantern.gameObject.SetActive(true);ApplyControls();
  }catch{DestroyButton();throw;}
 }
 internal void Tick(){
  var manager=AuctionManager.Instance;
  if(boundManager!=null)TryAutoBid(manager);
  if(!enabled.Value){Clear();DestroyButton();return;}
  if(panel&&panel!.gameObject.activeInHierarchy){EnsureButton(panel!);ApplyControls();}
 }
 [HarmonyPatch(typeof(UIAuctionProcessing),nameof(UIAuctionProcessing.Show))]
 static class Show{static void Postfix(UIAuctionProcessing __instance)=>Instance?.Guard(()=>Instance.EnsureButton(__instance));}
 [HarmonyPatch(typeof(UIAuctionProcessing),"Update")]
 static class UpdateUi{static void Postfix()=>Instance?.Guard(()=>Instance.Tick());}
 [HarmonyPatch(typeof(UIAuctionProcessing),nameof(UIAuctionProcessing.Hide))]
 static class Hide{static void Prefix()=>Instance?.Guard(()=>{Instance.Clear();Instance.DestroyButton();});}
 [HarmonyPatch(typeof(AuctionManager),nameof(AuctionManager.PlayerGiveup))]
 static class Giveup{static bool Prefix(AuctionManager __instance)=>Instance==null||!Instance.Locked(__instance);}
 [HarmonyPatch(typeof(AuctionManager),nameof(AuctionManager.PlayerBid))]
 static class Bid{static bool Prefix(AuctionManager __instance)=>Instance==null||!Instance.Locked(__instance)||Instance.submitting;}
 [HarmonyPatch(typeof(AuctionManager),"EndOneRound")]
 static class EndRound{static void Prefix()=>Instance?.Guard(()=>Instance.Clear());}
 [HarmonyPatch(typeof(GameTimer),nameof(GameTimer.WaitSecondsAsync))]
 static class Wait{static void Postfix(float __0,ETCancellationToken __1)=>Instance?.Guard(()=>Instance.ObserveWait(__0,__1));}
 [HarmonyPatch(typeof(AuctionManager),"StartOneRound")]
 static class StartRound{static void Prefix()=>Instance?.Guard(()=>Instance.Clear());}
 [HarmonyPatch(typeof(AuctionManager),"EndAuction")]
 static class EndAuction{static void Prefix()=>Instance?.Guard(()=>Instance.Clear());}
 [HarmonyPatch(typeof(SaveManager),nameof(SaveManager.Load),new Type[]{})]
 static class Loading{static void Prefix()=>Instance?.Guard(()=>Instance.Clear());}
 [HarmonyPatch(typeof(GameManager),nameof(GameManager.StartNewGame),new Type[]{})]
 static class NewGame{static void Prefix()=>Instance?.Guard(()=>Instance.Clear());}
 [HarmonyPatch(typeof(GameManager),nameof(GameManager.QuitToMenu),new Type[]{})]
 static class Quit{static void Prefix()=>Instance?.Guard(()=>Instance.Clear());}
 public sealed class LanternWatcher:MonoBehaviour{public LanternWatcher(IntPtr p):base(p){}public void Update()=>Instance?.Guard(()=>Instance.Tick());}
}
