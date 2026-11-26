using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WuLin;
using WuLin.StateMachine;
using Object=UnityEngine.Object;
using Skills=Il2CppSystem.Collections.Generic.List<WuLin.KungfuInstance>;
namespace DaxiaMods;

[BepInPlugin(Owner,"大侠·武功拖动换位","1.0.1")]
public sealed class KungfuOrder:BasePlugin {
 internal const string Owner="local.daxia.kungfuorder";
 internal static KungfuOrder? Instance;
 ConfigEntry<bool> enabled=null!;
 Harmony? harmony;
 UIKongfuPanel? panel;
 GameCharacterInstance? character;
 IntPtr characters,team,dynamicData,manager;
 string? path;
 readonly List<Row> rows=new();
 readonly KungfuOrderGesture gesture=new();
 Row? pressed,target;
 Skills? pressedList;
 long[]? pressedOrder;
 int suppressedThrough=-1;
 int revision;
 GameObject? ghost,highlight;
 RectTransform? dragRect;
 Image? previewBackground,previewIcon;
 TMP_Text? previewLabel;
 PointerEventData? pointerData;
 IntPtr pointerEventSystem;
 readonly Il2CppSystem.Collections.Generic.List<RaycastResult> hits=new();
 sealed class Row {
  internal Component component=null!;
  internal KungfuInstance skill=null!;
  internal int group;
  internal Image? bg,icon;
  internal TMP_Text? text;
 }
 public override void Load(){
  enabled=Config.Bind("General","Enabled",true,"武功页左侧普通武功按住0.15秒拖动换位；战斗绝招固定在前，普通武功及快捷键按左栏顺序排列。");
  if(!BuildGuard.Check(Log))return;
  Instance=this;
  try{harmony=new Harmony(Owner);harmony.PatchAll(typeof(KungfuOrder).Assembly);AddComponent<KungfuOrderWatcher>();Log.LogInfo("武功拖动换位1.0.1已加载：左栏长按0.15秒交换；绝招区域保留原版，战斗绝招在前，普通武功与快捷键按左栏顺序。");}
  catch(Exception e){Unload();Log.LogError("武功拖动换位接入失败："+e);}
 }
 public override bool Unload(){Cancel();ReleaseVisuals();panel=null;character=null;rows.Clear();pointerData=null;harmony?.UnpatchSelf();Instance=null;return true;}
 internal void Guard(Action action){try{action();}catch(Exception e){Fault(e);}}
 internal void Fault(Exception e){Cancel();Log.LogError("武功换位操作失败，已取消拖动："+e);}
 internal bool EnvironmentAvailable()=>enabled.Value&&GameManager.Instance&&RoamingManager.IsRoamingMode&&
  !GameManager.IsLoadingMap&&!RoamingManager.IsInStory&&!RoamingManager.IsTeleporting&&!RoamingManager.PrepareToBattle&&
  SaveManager.Instance&&!SaveManager.Instance.IsSavingOrLoading&&StoryManager.Instance&&!StoryManager.Instance.InStoryProgress;
 bool ContextValid()=>panel&&panel!.isActiveAndEnabled&&character!=null&&
  SaveManager.Instance?.Pointer==manager&&SaveManager.SavePath==path&&
  GameCharacterManager.Instance?.dataContainer?.initedObject?.Pointer==characters&&
  PlayerTeamManager.Instance?.dataContainer?.initedObject?.Pointer==team&&
  DynamicDataManager.Instance?.dataContainer?.initedObject?.Pointer==dynamicData&&
  panel.chara?.Pointer==character.Pointer&&
  PlayerTeamManager.Instance.GetTeamMemberByIndex(UIPackManager.Instance.PackSelectRoleID)?.Pointer==character.Pointer;
 internal void Bind(UIKongfuPanel value){
  Cancel();if(panel?.Pointer!=value.Pointer)ReleaseVisuals();panel=value;character=value.chara;rows.Clear();
  manager=SaveManager.Instance?.Pointer??IntPtr.Zero;path=SaveManager.SavePath;
  characters=GameCharacterManager.Instance?.dataContainer?.initedObject?.Pointer??IntPtr.Zero;
  team=PlayerTeamManager.Instance?.dataContainer?.initedObject?.Pointer??IntPtr.Zero;
  dynamicData=DynamicDataManager.Instance?.dataContainer?.initedObject?.Pointer??IntPtr.Zero;
  foreach(var card in value.LearnedSkillPanel.GetComponentsInChildren<UILearnedSkillPanel>(true))
   if(card.data!=null&&card.gameObject.activeInHierarchy&&!card.data.Templete.IsUnique&&card.data.Templete.KungfuType!=KungfuType.Bond)
    rows.Add(new Row{component=card,skill=card.data,group=0,bg=card.Bg,icon=card.icon,text=card.Name});
  if(rows.Count>0){CreateGhost(rows[0]);CreateHighlight(rows[0]);Cancel();}
  revision++;
 }
 static long[] Pointers(Skills skills){var ids=new long[skills.Count];for(int i=0;i<ids.Length;i++)ids[i]=skills[i]?.Pointer.ToInt64()??0;return ids;}
 static bool MatchesOrder(Skills list,long[] snapshot){if(list.Count!=snapshot.Length)return false;for(int i=0;i<snapshot.Length;i++)if(list[i].Pointer.ToInt64()!=snapshot[i])return false;return true;}
 Row? RowFor(KungfuInstance skill){foreach(var row in rows)if(row.skill.Pointer==skill.Pointer)return row;return null;}
 Row? Hit(){
  var events=EventSystem.current;if(!events)return null;
  if(pointerData==null||pointerEventSystem!=events!.Pointer){pointerData=new PointerEventData(events);pointerEventSystem=events.Pointer;}
  pointerData.position=new Vector2(Input.mousePosition.x,Input.mousePosition.y);
  hits.Clear();events.RaycastAll(pointerData,hits);
  if(hits.Count==0||!hits[0].gameObject)return null;
  var hit=hits[0].gameObject.transform;
  foreach(var row in rows){
   if(!row.component||!row.component.gameObject.activeInHierarchy)continue;
   var root=row.component.transform;
   if(hit.Pointer!=root.Pointer&&!hit.IsChildOf(root))continue;
   // Upgrade/forget controls own their original click and hold handling.
   var learned=row.component.TryCast<UILearnedSkillPanel>();
   if(learned&&learned!.uiObj_addExp){var upgrade=learned.uiObj_addExp.transform;if(hit.Pointer==upgrade.Pointer||hit.IsChildOf(upgrade))return null;}
   var button=hit.GetComponentInParent<Button>();
   if(button&&button!.transform.Pointer!=root.Pointer&&button.transform.IsChildOf(root))return null;
   return row;
  }
  return null;
 }
 internal bool SwapCards(UIKongfuPanel ui,KungfuInstance source,KungfuInstance destination){
  if(!enabled.Value||!EnvironmentAvailable()||!ContextValid()||panel?.Pointer!=ui.Pointer||source==null||destination==null)return false;
  var a=RowFor(source);var b=RowFor(destination);
  if(a==null||b==null||!KungfuOrderPolicy.CanSwapGroup(a.group,b.group))return false;
  var list=character!.KungfuInstances;if(list==null)return false;
  var order=new NativeOrder(list);int from=order.IndexOf(source.Pointer.ToInt64()),to=order.IndexOf(destination.Pointer.ToInt64());
  try{return KungfuOrderPolicy.Swap(order,from,to,()=>ContextValid()&&character.KungfuInstances.Pointer==list.Pointer,()=>RefreshForSwap(ui));}
  catch(Exception e){Log.LogError("武功换位刷新失败，已恢复原列表："+e);try{ui.InitLeftPanel();}catch(Exception redraw){Log.LogError("恢复原列表后的界面重绘失败，请重新打开武功页："+redraw);}return false;}
 }
 void RefreshForSwap(UIKongfuPanel ui){
  int before=revision;ui.InitLeftPanel();
  // A native->managed IL2CPP trampoline can log a native exception without
  // propagating it back to the wrapper. Bind is reached only after successful
  // native rendering, so a missing revision also means the swap must roll back.
  if(revision==before||panel?.Pointer!=ui.Pointer||!ContextValid())throw new InvalidOperationException("原版武功页刷新未完成或角色上下文已变化。");
 }
 internal void Tick(){
  if(!enabled.Value||!Application.isFocused||Input.GetKeyDown(KeyCode.Escape)){Cancel();return;}
  if(pressed==null&&!Input.GetMouseButtonDown(0))return;
  if(!ContextValid()||!EnvironmentAvailable()){Cancel();return;}
  var pos=Input.mousePosition;
  if(Input.GetMouseButtonDown(0)){
   Cancel();suppressedThrough=-1;pressed=Hit();
   if(pressed!=null){pressedList=character!.KungfuInstances;pressedOrder=Pointers(pressedList);CreateGhost(pressed);ghost!.SetActive(false);gesture.Begin(Time.unscaledTimeAsDouble,pos.x,pos.y);}
  }
  if(pressed==null)return;
  if(!pressed.component||character!.KungfuInstances.Pointer!=pressedList?.Pointer||!MatchesOrder(pressedList!,pressedOrder!)){Cancel();return;}
  bool wasDragging=gesture.Dragging;
  if(Input.GetMouseButton(0))gesture.Move(Time.unscaledTimeAsDouble,pos.x,pos.y,true);
  if(gesture.Dragging){
   suppressedThrough=Time.frameCount+1;
   if(!wasDragging){ghost!.SetActive(true);UITipsManager.Instance.HideTips();foreach(var scroll in panel!.GetComponentsInChildren<ScrollRect>(true))scroll.StopMovement();}
   if(dragRect)dragRect!.position=new Vector3(pos.x+28,pos.y-28,0);
   var next=Hit();if(next==pressed||next==null||!KungfuOrderPolicy.CanSwapGroup(pressed.group,next.group))next=null;
   if(next!=target){ClearHighlight();target=next;if(next!=null)CreateHighlight(next);}
  }
  if(!Input.GetMouseButton(0)){
   bool commit=gesture.Release();var source=pressed.skill;var destination=target?.skill;var current=panel;
   Cancel();
   if(commit&&current&&destination!=null)SwapCards(current!,source,destination);
  }
 }
 internal void Cancel(){
  if(gesture.Dragging)suppressedThrough=Time.frameCount+1;
  gesture.Cancel();pressed=null;pressedList=null;pressedOrder=null;target=null;
  if(ghost)ghost!.SetActive(false);ClearHighlight();
 }
 void ClearHighlight(){if(highlight)highlight!.SetActive(false);}
 void ReleaseVisuals(){if(ghost)Object.Destroy(ghost);if(highlight)Object.Destroy(highlight);ghost=null;highlight=null;dragRect=null;previewBackground=null;previewIcon=null;previewLabel=null;}
 static GameObject Ui(string name,Transform parent){var go=new GameObject(name,new[]{Il2CppType.Of<RectTransform>()});go.transform.SetParent(parent,false);return go;}
 void CreateHighlight(Row row){
  if(highlight){highlight!.transform.SetParent(row.component.transform,false);highlight.SetActive(true);return;}
  highlight=Ui("KungfuOrder.Target",row.component.transform);var rect=highlight.GetComponent<RectTransform>();
  rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;
  var image=highlight.AddComponent<Image>();image.color=new Color(1f,.78f,.15f,.3f);image.raycastTarget=false;
 }
 void CreateGhost(Row row){
  if(ghost){
   previewBackground!.sprite=row.bg?row.bg!.sprite:null;previewBackground.color=row.bg?row.bg!.color:Color.white;
   previewIcon!.sprite=row.icon?row.icon!.sprite:null;if(row.text)previewLabel!.font=row.text!.font;
   previewLabel!.text=row.skill.KungfuNameWithColor;ghost!.SetActive(true);return;
  }
  // Build an inert preview; cloning a native card would copy event handlers,
  // subscriptions and upgrade controls into the overlay.
  ghost=new GameObject("KungfuOrder.DragPreview");var canvas=ghost.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32760;
  var parentCanvas=row.component.GetComponentInParent<Canvas>();canvas.scaleFactor=parentCanvas?parentCanvas!.rootCanvas.scaleFactor:1f;
  var group=ghost.AddComponent<CanvasGroup>();group.alpha=.65f;group.blocksRaycasts=false;group.interactable=false;
  var preview=Ui("Card",ghost.transform);var rect=preview.GetComponent<RectTransform>();dragRect=rect;rect.sizeDelta=new Vector2(250,68);rect.pivot=new Vector2(0,1);
  var bg=preview.AddComponent<Image>();previewBackground=bg;bg.sprite=row.bg?row.bg!.sprite:null;bg.color=row.bg?row.bg!.color:new Color(.15f,.12f,.1f,1);bg.raycastTarget=false;
  var iconObject=Ui("Icon",preview.transform);var ir=iconObject.GetComponent<RectTransform>();ir.anchorMin=new Vector2(0,.5f);ir.anchorMax=ir.anchorMin;ir.anchoredPosition=new Vector2(34,0);ir.sizeDelta=new Vector2(52,52);
  var icon=iconObject.AddComponent<Image>();previewIcon=icon;icon.sprite=row.icon?row.icon!.sprite:null;icon.preserveAspect=true;icon.raycastTarget=false;
  var labelObject=Ui("Name",preview.transform);var tr=labelObject.GetComponent<RectTransform>();tr.anchorMin=new Vector2(0,0);tr.anchorMax=new Vector2(1,1);tr.offsetMin=new Vector2(66,0);tr.offsetMax=new Vector2(-8,0);
  var label=labelObject.AddComponent<TextMeshProUGUI>();previewLabel=label;if(row.text)label.font=row.text!.font;label.text=row.skill.KungfuNameWithColor;label.fontSize=20;label.alignment=TextAlignmentOptions.MidlineLeft;label.raycastTarget=false;label.ForceMeshUpdate(true,true);
 }
 bool Suppress(Component card)=>enabled.Value&&(gesture.Dragging||Time.frameCount<=suppressedThrough)&&panel&&card.transform.IsChildOf(panel!.transform);
 internal static Skills Ordered(Skills original,GameCharacterInstance owner){
  var normal=new List<long>();foreach(var skill in owner.KungfuInstances)if(skill!=null&&skill.IsValid&&!skill.Templete.IsUnique&&skill.Templete.KungfuType!=KungfuType.Bond)normal.Add(skill.Pointer.ToInt64());
  var unique=new bool[original.Count];for(int i=0;i<unique.Length;i++)unique[i]=original[i]!=null&&(original[i].Templete.IsUnique||original[i].Templete.KungfuType==KungfuType.Unique);
  var wanted=KungfuOrderPolicy.OrderBattle(KungfuOrderPolicy.LeftColumnFirst(normal.ToArray()),Pointers(original),unique);var map=new Dictionary<long,KungfuInstance>();foreach(var skill in original)if(skill!=null)map[skill.Pointer.ToInt64()]=skill;
  var copy=new Skills();foreach(long id in wanted)copy.Add(id==0?null!:map[id]);return copy;
 }
 void OrderSelector(UISelectKongfuInBattle ui){
  if(!enabled.Value||ui.displayKungfuType==KungfuType.Unique||ui.Chara==null||!PlayerTeamManager.Instance.HasCharacterAsMember(ui.Chara,false))return;
  var normal=new List<long>();foreach(var skill in ui.Chara.KungfuInstances)if(skill!=null&&skill.IsValid&&!skill.Templete.IsUnique&&skill.Templete.KungfuType!=KungfuType.Bond)normal.Add(skill.Pointer.ToInt64());
  var ordered=KungfuOrderPolicy.LeftColumnFirst(normal.ToArray());var ranks=new Dictionary<IntPtr,int>();for(int i=0;i<ordered.Length;i++)ranks[new IntPtr(ordered[i])]=i;
  var cards=new List<UIBattleSkillItem>();foreach(var card in ui.SKillPanel.GetComponentsInChildren<UIBattleSkillItem>(false))if(card.data!=null&&ranks.ContainsKey(card.data.Pointer))cards.Add(card);
  cards.Sort((a,b)=>ranks[a.data.Pointer].CompareTo(ranks[b.data.Pointer]));
  // Keep the native unique selector intact; only order the ordinary selector.
  // Restore saved order while keeping its original selected item/callback.
  foreach(var card in cards)card.transform.SetAsLastSibling();
 }
 [HarmonyPatch(typeof(UIKongfuPanel),nameof(UIKongfuPanel.InitLeftPanel))]
 static class PanelPatch {static void Postfix(UIKongfuPanel __instance){Instance?.Guard(()=>Instance.Bind(__instance));}}
 [HarmonyPatch]
 static class ClickPatch {
  static IEnumerable<MethodBase> TargetMethods(){foreach(var name in new[]{"OnPointerClick","CB_PointerClick","OnPointerEnter"})yield return AccessTools.Method(typeof(UILearnedSkillPanel),name);}
  static bool Prefix(Component __instance)=>Instance?.Suppress(__instance)!=true;
 }
 [HarmonyPatch]
 static class ScrollPatch {
  static IEnumerable<MethodBase> TargetMethods(){yield return AccessTools.Method(typeof(ScrollRect),"OnBeginDrag");yield return AccessTools.Method(typeof(ScrollRect),"OnDrag");}
  static bool Prefix(ScrollRect __instance)=>Instance?.gesture.Dragging!=true||!Instance.panel||!__instance.transform.IsChildOf(Instance.panel!.transform);
 }
 [HarmonyPatch(typeof(BattleControl),nameof(BattleControl.DrawActionButtons))]
 static class BattlePatch {
  static void Prefix(ActiveActorStateMachineData __0,out Skills? __state){
   __state=null;if(Instance?.enabled.Value!=true||__0?.actor?.info.characterInstance==null||__0.avalibleKungfuInstances==null)return;
   var owner=__0.actor.info.characterInstance;if(!PlayerTeamManager.Instance.HasCharacterAsMember(owner,false))return;
   try{var ordered=Ordered(__0.avalibleKungfuInstances,owner);__state=__0.avalibleKungfuInstances;__0.avalibleKungfuInstances=ordered;}
   catch(Exception e){Instance.Log.LogError("战斗武功顺序未应用，保留原版列表："+e);}
  }
  static Exception? Finalizer(ActiveActorStateMachineData __0,Skills? __state,Exception? __exception){if(__state!=null)__0.avalibleKungfuInstances=__state;return __exception;}
 }
 [HarmonyPatch(typeof(UISelectKongfuInBattle),nameof(UISelectKongfuInBattle.SkillInit))]
 static class SelectorPatch {static void Postfix(UISelectKongfuInBattle __instance){Instance?.Guard(()=>Instance.OrderSelector(__instance));}}
 // Expose only indexed replacement to the transaction policy. Every value is
 // mapped back to the exact native object, never recreated from template IDs.
 sealed class NativeOrder:IList<long> {
  readonly Skills list;readonly Dictionary<long,KungfuInstance> objects=new();
  internal NativeOrder(Skills skills){list=skills;foreach(var item in skills)objects[item.Pointer.ToInt64()]=item;}
  public long this[int i]{get=>list[i].Pointer.ToInt64();set=>list[i]=objects[value];}
  public int Count=>list.Count;public bool IsReadOnly=>false;
  public int IndexOf(long value){for(int i=0;i<Count;i++)if(this[i]==value)return i;return -1;}
  public bool Contains(long value)=>IndexOf(value)>=0;
  public IEnumerator<long> GetEnumerator(){for(int i=0;i<Count;i++)yield return this[i];}IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
  public void CopyTo(long[] array,int start){for(int i=0;i<Count;i++)array[start+i]=this[i];}
  public void Add(long value)=>throw new NotSupportedException();public void Clear()=>throw new NotSupportedException();public void Insert(int i,long value)=>throw new NotSupportedException();public bool Remove(long value)=>throw new NotSupportedException();public void RemoveAt(int i)=>throw new NotSupportedException();
 }
}
public sealed class KungfuOrderWatcher:MonoBehaviour {
 public KungfuOrderWatcher(IntPtr p):base(p){}
 public void LateUpdate(){try{KungfuOrder.Instance?.Tick();}catch(Exception e){KungfuOrder.Instance?.Fault(e);}}
 public void OnApplicationFocus(bool focus){if(!focus)KungfuOrder.Instance?.Cancel();}
}
