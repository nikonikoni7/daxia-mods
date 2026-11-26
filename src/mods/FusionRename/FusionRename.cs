using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using GameData;
using HarmonyLib;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using WuLin;
using Object=UnityEngine.Object;
namespace DaxiaMods;
[BepInPlugin(Owner,"大侠·融合武功重新取名","1.0.1")]
public sealed class FusionRename:BasePlugin {
 internal const string Owner="local.daxia.fusionrename";
 internal static FusionRename? Instance;
 ConfigEntry<bool> enabled=null!;
 ConfigEntry<KeyCode> key=null!;
 Harmony? harmony;
 internal sealed class Entry {
  internal KungfuData Data=null!;
  internal KungfuInstance Skill=null!;
  internal string Members="";
 }
 readonly List<Entry> entries=new();
 readonly List<GameObject> rows=new();
 readonly List<TMP_Text> rowLabels=new();
 readonly List<Image> rowBackgrounds=new();
 GameObject? window;
 TMP_InputField? nameInput;
 TMP_Text? selectedText,status,pageLabel;
 Button? confirm;
 TMP_FontAsset? font;
 Entry? selected;
 SaveObjectDynamicData? context;
 DynamicDataManager? dynamicManager;
 SaveManager? saveManager;
 PlayerTeamManager? teamManager;
 string? savePath;
 GameTimer? timer;
 Il2CppSystem.Object? pauseToken;
 bool visible,masked,busy;
 int generation,lastKeyFrame=-1,page,nameLimit;
 const int PageSize=6;
 Dictionary<string,string>? stoneNames;

 public override void Load(){
  Instance=this;
  enabled=Config.Bind("General","Enabled",true,"启用融合武功重新取名窗口；同步当前档武功、秘籍与悟道碑，保留其他档已拓印的名称。");
  key=Config.Bind("Controls","ToggleKey",KeyCode.F6,"打开或关闭融合武功重新取名；输入文字时不触发。");
  if(!BuildGuard.Check(Log)){Instance=null;return;}
  try{harmony=new Harmony(Owner);harmony.PatchAll(typeof(FusionRename).Assembly);AddComponent<RenameWatcher>();Log.LogInfo("融合武功重新取名已加载："+key.Value+"打开，Esc关闭。");}
  catch(Exception e){harmony?.UnpatchSelf();Instance=null;Log.LogError("融合武功重新取名加载失败："+e);}
 }
 public override bool Unload(){Close();harmony?.UnpatchSelf();Instance=null;return true;}
 internal void Guard(Action action){try{action();}catch(Exception e){Log.LogError("融合武功重新取名："+e);if(status)status!.text="操作失败，请查看日志；未自动重试。";}}
 internal bool Available()=>enabled.Value&&GameManager.Instance&&RoamingManager.IsRoamingMode&&
  !GameManager.Instance.IsOnWorldMap&&!GameManager.IsLoadingMap&&SaveManager.Instance&&!SaveManager.Instance.IsSavingOrLoading&&
  !RoamingManager.IsInStory&&!RoamingManager.IsTeleporting&&!RoamingManager.PrepareToBattle&&
  StoryManager.Instance&&!StoryManager.Instance.InStoryProgress&&PlayerTeamManager.Instance?.TeamInventory!=null;
 internal bool SameContext()=>context&&dynamicManager&&saveManager&&teamManager&&
  DynamicDataManager.Instance?.Pointer==dynamicManager!.Pointer&&SaveManager.Instance?.Pointer==saveManager!.Pointer&&
  PlayerTeamManager.Instance?.Pointer==teamManager!.Pointer&&dynamicManager.dataContainer.initedObject?.Pointer==context!.Pointer&&
  savePath==SaveManager.SavePath;
 internal bool Keys(){
  if(!Application.isFocused){Close();return true;}
  if(lastKeyFrame==Time.frameCount)return !visible;
  if(visible&&Input.GetKeyDown(KeyCode.Escape)){lastKeyFrame=Time.frameCount;Close();return false;}
  if(Input.GetKeyDown(key.Value)&&!ModUiInput.Typing()){
   lastKeyFrame=Time.frameCount;
   if(visible)Close();
   else if(!enabled.Value)return true;
   else if(ModUiInput.BlockingMenu(false)!=null)Log.LogInfo("改名窗口未打开：请先关闭当前菜单。");
   else if(Available())Open();
   else Log.LogInfo("改名窗口未打开：需处于自由探索，且不在剧情、战斗、传送或读写存档中。");
   return false;
  }
  return !visible;
 }
 internal void Tick(){Guard(()=>{
  if(visible&&(!Available()||!SameContext()||!Application.isFocused||ModUiInput.BlockingMenu(false)!=null))Close();
  if(Application.isFocused)Keys();
 });}
 internal void LoadCatalogue(){
  entries.Clear();selected=null;
  if(!context||!teamManager)throw new InvalidOperationException("当前存档数据尚未加载。");
  var found=new Dictionary<int,Entry>();
  for(int i=0;i<teamManager!.TeamSize;i++){
   var member=teamManager.GetTeamMemberByIndex(i);if(member?.KungfuInstances==null)continue;
   foreach(var skill in member.KungfuInstances){
    var data=skill?.Templete;if(data==null||!FusionRenameData.CanRename(context!,data))continue;
    if(!found.TryGetValue(data.Uid,out var entry)){
     entry=new Entry{Data=data,Skill=skill!};found.Add(data.Uid,entry);entries.Add(entry);
    }else if(entry.Data.UName!=data.UName)throw new InvalidOperationException("融合武功编号冲突，未打开改名列表。");
    if(entry.Members.Length>0)entry.Members+="、";
    entry.Members+=member.FullName;
   }
  }
  var inventory=teamManager.TeamInventory?.Contents;
  if(inventory!=null)foreach(var book in inventory){
   if(book==null||book.Stack<=0||book.TempleteId>=0)continue;
   foreach(var item in context!.storedDatas){
    var data=item?.TryCast<KungfuData>();
    if(data==null||!FusionRenameData.MatchesBook(book,data)||!FusionRenameData.CanRename(context,data))continue;
    if(!found.TryGetValue(data.Uid,out var entry)){
     // Display instance only; never add it to a character or call learning.
     entry=new Entry{Data=data,Skill=new KungfuInstance(data,new KungfuLevelData())};found.Add(data.Uid,entry);entries.Add(entry);
    }else if(entry.Data.UName!=data.UName)throw new InvalidOperationException("融合武功编号冲突，未打开改名列表。");
    if(!entry.Members.Contains("背包秘籍",StringComparison.Ordinal)){
     if(entry.Members.Length>0)entry.Members+="、";
     entry.Members+="背包秘籍";
    }
    break;
   }
  }
  entries.Sort((a,b)=>string.Compare(a.Skill.KungfuName,b.Skill.KungfuName,StringComparison.CurrentCulture));
 }
 internal bool HasSource(KungfuData data){
  if(!context||!teamManager||!FusionRenameData.CanRename(context!,data))return false;
  for(int i=0;i<teamManager!.TeamSize;i++){
   var member=teamManager.GetTeamMemberByIndex(i);if(member?.KungfuInstances==null)continue;
   foreach(var skill in member.KungfuInstances)if(skill?.Templete?.Pointer==data.Pointer)return true;
  }
  var inventory=teamManager.TeamInventory?.Contents;
  if(inventory!=null)foreach(var book in inventory)if(book!=null&&book.Stack>0&&FusionRenameData.MatchesBook(book,data))return true;
  return false;
 }
 internal void Open(){
  if(visible||!Available()||ModUiInput.BlockingMenu(false)!=null)return;
  try{
   dynamicManager=DynamicDataManager.Instance;saveManager=SaveManager.Instance;teamManager=PlayerTeamManager.Instance;
   context=dynamicManager?.dataContainer.initedObject;savePath=SaveManager.SavePath;
   LoadCatalogue();BuildWindow();
   pauseToken=window!.Cast<Il2CppSystem.Object>();timer=GameTimer.Instance;
   GameManager.RegisterGameFunctionMask(Owner,GameFunctionMask.PlayerControl|GameFunctionMask.InGameTimeElapse|GameFunctionMask.SaveGame|GameFunctionMask.PlayerTriggerNpcSight|GameFunctionMask.PlayerPet,false);masked=true;
   timer.AddOrSetTimeScale(pauseToken,0);if(RoamingManager.Instance&&RoamingManager.Instance.player)RoamingManager.Instance.player.Stop();
   visible=true;page=0;Render();
  }catch{Close();throw;}
 }
 static RectTransform Rect(string name,Transform parent,Vector2 at,Vector2 size){
  var go=new GameObject(name,new[]{Il2CppType.Of<RectTransform>()});var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);rect.localScale=Vector3.one;rect.anchoredPosition=at;rect.sizeDelta=size;return rect;
 }
 TMP_Text Label(Transform parent,string text,Vector2 at,Vector2 size,int pointSize=20){
  var rect=Rect("FusionRename.Text",parent,at,size);var label=rect.gameObject.AddComponent<TextMeshProUGUI>();
  label.text=text;label.font=font!;label.fontSize=pointSize;label.color=new Color(.91f,.88f,.79f,1);label.richText=false;label.raycastTarget=false;
  label.alignment=TextAlignmentOptions.MidlineLeft;label.enableWordWrapping=false;label.overflowMode=TextOverflowModes.Ellipsis;return label;
 }
 Button AddButton(Transform parent,string text,Vector2 at,Vector2 size,Action action){
  var rect=Rect("FusionRename."+text,parent,at,size);var image=rect.gameObject.AddComponent<Image>();image.color=new Color(.24f,.27f,.24f,1);
  var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;int owner=generation;
  button.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>((Action)(()=>{if(owner==generation&&visible)Guard(action);})));
  Label(rect,text,Vector2.zero,size,20).alignment=TextAlignmentOptions.Center;return button;
 }
 internal void BuildWindow(){
  font=RoamingUI.Instance?.dayLabel?.font??TMP_Settings.defaultFontAsset;
  if(!font)throw new InvalidOperationException("未找到游戏中文字体。");
  var native=UIFiefDeveConfirm.Instance.CountInputField;if(!native)throw new InvalidOperationException("未找到原版融合命名输入框。");
  nameLimit=native.characterLimit;
  window=new GameObject("融合武功重新取名.窗口",new[]{Il2CppType.Of<RectTransform>()});Object.DontDestroyOnLoad(window);
  var canvas=window.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30100;window.AddComponent<GraphicRaycaster>();
  var scaler=window.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
  var shade=Rect("FusionRename.Shade",window.transform,Vector2.zero,Vector2.zero);shade.anchorMin=Vector2.zero;shade.anchorMax=Vector2.one;shade.offsetMin=Vector2.zero;shade.offsetMax=Vector2.zero;shade.gameObject.AddComponent<Image>().color=new Color(0,0,0,.72f);
  var panel=Rect("FusionRename.Panel",shade,Vector2.zero,new Vector2(940,570));panel.gameObject.AddComponent<Image>().color=new Color(.095f,.12f,.12f,1);
  Label(panel,"融合武功重新取名",new Vector2(-155,237),new Vector2(550,46),28);
  AddButton(panel,"关闭",new Vector2(385,237),new Vector2(100,38),Close);
  Label(panel,"选择融合武功",new Vector2(-215,180),new Vector2(420,34),19);
  Label(panel,"修改名称",new Vector2(253,180),new Vector2(340,34),19);
  rows.Clear();rowLabels.Clear();rowBackgrounds.Clear();
  for(int i=0;i<PageSize;i++){
   int row=i;var button=AddButton(panel,"",new Vector2(-215,124-i*54),new Vector2(420,46),()=>{int n=page*PageSize+row;if(n<entries.Count)Select(entries[n]);});
   rows.Add(button.gameObject);rowBackgrounds.Add(button.GetComponent<Image>());rowLabels.Add(button.GetComponentInChildren<TMP_Text>());
  }
  selectedText=Label(panel,"请从左侧选择武功",new Vector2(255,120),new Vector2(330,52),23);
  var input=Rect("FusionRename.NameInput",panel,new Vector2(253,44),new Vector2(340,48));var bg=input.gameObject.AddComponent<Image>();bg.color=new Color(.045f,.065f,.065f,1);
  var viewport=Rect("TextArea",input,Vector2.zero,new Vector2(318,42));viewport.gameObject.AddComponent<RectMask2D>();
  var text=Label(viewport,"",Vector2.zero,viewport.sizeDelta,23);var placeholder=Label(viewport,"输入新名称",Vector2.zero,viewport.sizeDelta);placeholder.color=new Color(.5f,.55f,.52f,1);
  nameInput=input.gameObject.AddComponent<TMP_InputField>();nameInput.targetGraphic=bg;nameInput.textViewport=viewport;nameInput.textComponent=text;nameInput.placeholder=placeholder;nameInput.pointSize=23;
  nameInput.caretColor=Color.white;nameInput.customCaretColor=true;nameInput.lineType=TMP_InputField.LineType.SingleLine;nameInput.contentType=native.contentType;nameInput.characterValidation=native.characterValidation;nameInput.characterLimit=nameLimit;nameInput.interactable=false;
  confirm=AddButton(panel,"确认改名",new Vector2(253,-28),new Vector2(340,46),Commit);confirm.interactable=false;
  var hint=Label(panel,"同步武功、秘籍及碑上名称\n其他档已拓印版本保留原名",new Vector2(253,-124),new Vector2(340,75),17);hint.enableWordWrapping=true;
  pageLabel=Label(panel,"",new Vector2(-215,-217),new Vector2(190,34),17);pageLabel.alignment=TextAlignmentOptions.Center;
  AddButton(panel,"上一页",new Vector2(-370,-217),new Vector2(100,34),()=>{page--;Render();});
  AddButton(panel,"下一页",new Vector2(-60,-217),new Vector2(100,34),()=>{page++;Render();});
  status=Label(panel,"改名后请正常保存游戏。",new Vector2(0,-258),new Vector2(850,30),17);
 }
 internal void Render(){
  int pages=Math.Max(1,(entries.Count+PageSize-1)/PageSize);page=Math.Clamp(page,0,pages-1);
  for(int i=0;i<rows.Count;i++){
   int index=page*PageSize+i;rows[i].SetActive(index<entries.Count);if(index>=entries.Count)continue;
   var entry=entries[index];rowLabels[i].text=entry.Skill.KungfuName;rowBackgrounds[i].color=entry==selected?new Color(.38f,.32f,.18f,1):new Color(.2f,.24f,.22f,1);
  }
  if(pageLabel)pageLabel!.text="第"+(page+1)+"/"+pages+"页 · "+entries.Count+"门";
  if(entries.Count==0&&status)status!.text="当前队伍未学会融合武功，背包中也没有融合秘籍。";
 }
 internal void Select(Entry entry){
  if(!entries.Contains(entry))return;selected=entry;
  selectedText!.text=entry.Skill.KungfuName;nameInput!.interactable=true;nameInput.SetTextWithoutNotify(entry.Skill.KungfuName);
  confirm!.interactable=true;status!.text="已选择："+entry.Members;Render();nameInput.ActivateInputField();
 }
 internal void Commit(){
  if(busy||!visible||selected==null)return;
  if(!Available()||!SameContext()){Close();return;}
  var error=FusionRenamePolicy.Validate(nameInput?.text,nameLimit,GameUtil.ContainsForbiddenWord);
  if(error!=null){status!.text=error;return;}
  if(!HasSource(selected.Data)){Close();return;}
  busy=true;confirm!.interactable=false;
  try{
   string text=nameInput!.text;
   var fief=GameManager.Instance.GetFiefSharedData();bool privateFief=GameManager.Instance.GetFiefUnsharedData().isPrivateFief;
   bool privateStone=false;if(privateFief&&fief?.kungfuBooks!=null)foreach(var book in fief.kungfuBooks)if(FusionRenameData.MatchesBook(book,selected.Data)){privateStone=true;break;}
   bool stone=FusionRenameData.Rename(context!,selected.Data,privateFief?null:fief,text,()=>PersistShared(selected.Data))||privateStone;
   selectedText!.text=text;Render();status!.text=stone?"已改名，悟道碑已同步；请正常保存当前游戏。":"已改名；请正常保存当前游戏。";
   Log.LogInfo("已重命名融合武功编号"+selected.Data.Uid+"；悟道碑同步="+stone);
  }finally{busy=false;if(confirm)confirm!.interactable=true;}
 }
 internal void PersistShared(KungfuData kungfu){
  var container=dynamicManager!.globalDataContainer;var global=container.initedObject;var expected=context!.GlobalStoredData;
  bool dlc=global.storedDatasDlc1!=null&&expected.Pointer==global.storedDatasDlc1.Pointer;
  container.Save(true);
  // Native Save/Sync may log a failed write instead of throwing. Read the file
  // afresh so the confirmation cannot report success based on its RAM cache.
  if(global.m_saveFile?.settings==null)throw new InvalidOperationException("共享武功名称未生成保存文件。");
  var disk=new ES3File(global.m_saveFile.settings,true).Load<SaveObjectDynamicDataGlobal>("Data");
  try{FusionRenameData.VerifySavedNames(expected,dlc?disk.storedDatasDlc1:disk.storedDatasMainGame,kungfu);}
  finally{if(disk)Object.Destroy(disk);}
 }
 // Owned imprints use the slot's local names. Only the stone's left-hand
 // renderer and tooltip resolve the independently saved shared names.
 internal Dictionary<string,string>? BeginStoneNames(SaveObjectDynamicData save,FiefSharedData fief){
  var previous=stoneNames;var next=new Dictionary<string,string>();
  if(enabled.Value&&save&&save.GlobalStoredData!=null&&fief?.kungfuBooks!=null){
   foreach(var book in fief.kungfuBooks){
    if(book==null||book.TempleteId>=0)continue;
    foreach(var data in save.GlobalStoredData){
     var kungfu=data?.TryCast<KungfuData>();if(kungfu==null||!FusionRenameData.MatchesBook(book,kungfu))continue;
     foreach(var prefix in new[]{"KunfuName_","ItemName_"}){
      string name=prefix+kungfu.UName;var loc=FusionRenameData.Name(save.GlobalStoredData,name);
      // Native fusion naming, like this plugin, writes one literal in all languages.
      if(!string.IsNullOrEmpty(loc.SChinese))next[name]=loc.SChinese;
     }
     break;
    }
   }
  }
  stoneNames=next;return previous;
 }
 internal void EndStoneNames(Dictionary<string,string>? previous)=>stoneNames=previous;
 internal void Close(){
  generation++;visible=false;busy=false;
  try{if(masked)GameManager.UnregisterGameFunctionMask(Owner);}
  finally{
   masked=false;if(timer&&pauseToken!=null)timer!.RemoveTimeScale(pauseToken);timer=null;pauseToken=null;
   if(window){window!.SetActive(false);Object.Destroy(window);}window=null;nameInput=null;selectedText=null;confirm=null;status=null;pageLabel=null;
   entries.Clear();rows.Clear();rowLabels.Clear();rowBackgrounds.Clear();selected=null;context=null;dynamicManager=null;saveManager=null;teamManager=null;savePath=null;
  }
 }
 [HarmonyPatch(typeof(HotKeyManager),"Update")]
 static class Hotkeys{static bool Prefix(){bool original=true;Instance?.Guard(()=>original=Instance.Keys());return original;}}
 [HarmonyPatch(typeof(SaveManager),nameof(SaveManager.Load),new Type[]{})]
 static class Loading{static void Prefix()=>Instance?.Guard(()=>Instance.Close());}
 [HarmonyPatch(typeof(GameManager),nameof(GameManager.StartNewGame),new Type[]{})]
 static class NewGame{static void Prefix()=>Instance?.Guard(()=>Instance.Close());}
 [HarmonyPatch(typeof(GameManager),nameof(GameManager.QuitToMenu),new Type[]{})]
 static class Quit{static void Prefix()=>Instance?.Guard(()=>Instance.Close());}
 [HarmonyPatch]
 static class StoneDisplay{
  static IEnumerable<MethodBase> TargetMethods(){yield return AccessTools.Method(typeof(UIFiefKungfu),"UpdateLeft");yield return AccessTools.Method(typeof(UIFiefKungfu),nameof(UIFiefKungfu.ShowLeftTip));}
  static void Prefix(out Dictionary<string,string>? __state){
   var instance=Instance;__state=instance?.stoneNames;
   if(instance!=null&&instance.enabled.Value)instance.Guard(()=>{
    var save=DynamicDataManager.Instance?.dataContainer.initedObject;
    if(save&&GameManager.Instance&&!GameManager.Instance.GetFiefUnsharedData().isPrivateFief)instance.BeginStoneNames(save!,GameManager.Instance.GetFiefSharedData());
   });
  }
  static Exception? Finalizer(Exception? __exception,Dictionary<string,string>? __state){Instance?.EndStoneNames(__state);return __exception;}
 }
 [HarmonyPatch(typeof(GameLoc),nameof(GameLoc.GetLocString))]
 static class StoneName{
  static void Postfix(string __0,bool __1,ref string __result){
   var instance=Instance;if(instance?.enabled.Value!=true||__0==null||instance.stoneNames==null||!instance.stoneNames.TryGetValue(__0,out var text))return;
   if(__1&&text.Contains('|')){var parts=text.Split('|');__result=parts[UnityEngine.Random.Range(0,parts.Length)];}else __result=text;
  }
 }
 public sealed class RenameWatcher:MonoBehaviour{public RenameWatcher(IntPtr pointer):base(pointer){}public void Update()=>Instance?.Tick();}
}
