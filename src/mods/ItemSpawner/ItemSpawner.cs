using System;
using System.Collections.Generic;
using System.Linq;
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

[BepInPlugin(Owner,"大侠·物品添加器","1.0.2")]
public sealed class ItemSpawner:BasePlugin {
 internal const string Owner="local.daxia.itemspawner";
 internal static ItemSpawner? Instance;
 ConfigEntry<KeyCode> key=null!;
 Harmony? harmony;
 internal sealed class Entry {internal ItemData Data=null!;internal int Id;internal string Name="",Category="";}
 readonly List<Entry> catalogue=new();
 List<Entry> matches=new();
 readonly ItemSpawnerCart cart=new();
 Dictionary<int,int> pending=new();
 GameObject? confirmation;
 bool committing;
 readonly List<GameObject> rows=new();
 readonly List<TMP_Text> labels=new();
 TMP_FontAsset? font;
 TMP_InputField? search,quantity;
 TMP_Text? status,pageLabel;
 GameObject? window;
 GameTimer? timer;
 Il2CppSystem.Object? token;
 bool visible,masked;
 int page,lastFrame=-1;
 string category="全部";
 const int PageSize=7;

 public override void Load(){
  Instance=this;
  key=Config.Bind("Controls","ToggleKey",KeyCode.K,"打开或关闭物品添加器；输入搜索词时不会触发此键。");
  if(!BuildGuard.Check(Log))return;
  try{harmony=new Harmony(Owner);harmony.PatchAll(typeof(ItemSpawner).Assembly);AddComponent<ItemWatcher>();Log.LogInfo("物品添加器已加载："+key.Value+"打开；仅添加物品，无人物、武学、加速或恢复功能。");}
  catch(Exception e){harmony?.UnpatchSelf();Log.LogError("物品添加器加载失败："+e);}
 }
 public override bool Unload(){Close();harmony?.UnpatchSelf();Instance=null;return true;}
 internal void Guard(Action action){try{action();}catch(Exception e){Log.LogError("物品添加器："+e);if(status)status!.text="操作失败，请查看日志；未自动重试。";}}
 internal bool Available(){
  bool exploring=GameManager.Instance&&RoamingManager.IsRoamingMode&&!GameManager.Instance.IsOnWorldMap&&!GameManager.IsLoadingMap;
  return ItemSpawnerPolicy.CanAdd(PlayerTeamManager.Instance?.TeamInventory!=null,exploring,
   !SaveManager.Instance||SaveManager.Instance.IsSavingOrLoading,
   RoamingManager.IsInStory||RoamingManager.IsTeleporting||RoamingManager.PrepareToBattle||!StoryManager.Instance||StoryManager.Instance.InStoryProgress);
 }
 static bool MenuOpen()=>ModUiInput.BlockingMenu(false)!=null;
 internal bool Keys(){
  if(!Application.isFocused){Close();return true;}
  if(lastFrame==Time.frameCount)return false;
  if(visible&&Input.GetKeyDown(KeyCode.Escape)){lastFrame=Time.frameCount;if(confirmation)HideConfirmation();else Close();return false;}
  if(Input.GetKeyDown(key.Value)&&!ModUiInput.Typing()){
   lastFrame=Time.frameCount;
   if(visible)Close();else {
    if(MenuOpen())Log.LogInfo("物品添加器未打开：请先关闭菜单 "+ModUiInput.BlockingMenu(false));
    else if(Available())Open();
    else Log.LogInfo($"物品添加器未打开：需自由探索且非剧情/读写存档；roaming={RoamingManager.IsRoamingMode}, worldMap={GameManager.Instance&&GameManager.Instance.IsOnWorldMap}, loadingMap={GameManager.IsLoadingMap}, story={RoamingManager.IsInStory||StoryManager.Instance&&StoryManager.Instance.InStoryProgress}, teleport={RoamingManager.IsTeleporting}, battle={RoamingManager.PrepareToBattle}, saving/loading={!SaveManager.Instance||SaveManager.Instance.IsSavingOrLoading}");
   }
   return false;
  }
  return !visible;
 }
 internal void Tick(){Guard(()=>{if(visible&&(!Available()||!Application.isFocused))Close();if(Application.isFocused)Keys();});}
 internal int LoadCatalogue(){
  catalogue.Clear();var table=BaseDataClass.GetGameData<ItemDataScriptObject>();
  if(table?.ItemData==null)throw new InvalidOperationException("物品数据尚未加载。");
  var ids=new HashSet<int>();
  foreach(var data in table.ItemData){if(data==null||data.Uid<=0||!ids.Add(data.Uid))continue;var name=GameUtil.GetName(data,false);if(string.IsNullOrWhiteSpace(name))name=data.UName;catalogue.Add(new Entry{Data=data,Id=data.Uid,Name=name??("物品"+data.Uid),Category=ItemSpawnerPolicy.Category(data.Type.ToString())});}
  catalogue.Sort((a,b)=>a.Id.CompareTo(b.Id));return catalogue.Count;
 }
 static RectTransform Rect(string name,Transform parent,Vector2 at,Vector2 size){var go=new GameObject(name,new[]{Il2CppType.Of<RectTransform>()});var r=go.GetComponent<RectTransform>();r.SetParent(parent,false);r.localScale=Vector3.one;r.anchoredPosition=at;r.sizeDelta=size;return r;}
 TMP_Text Label(Transform parent,string text,Vector2 at,Vector2 size,int sizeText=20){
  var r=Rect("物品添加器.文字",parent,at,size);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.text=text;t.fontSize=sizeText;t.color=Color.white;t.richText=false;t.raycastTarget=false;t.enableWordWrapping=false;t.overflowMode=TextOverflowModes.Ellipsis;t.alignment=TextAlignmentOptions.MidlineLeft;if(font)t.font=font!;return t;
 }
 GameObject Button(Transform parent,string text,Vector2 at,Vector2 size,Action action){
  var r=Rect("物品添加器."+text,parent,at,size);var image=r.gameObject.AddComponent<Image>();image.color=new Color(.23f,.28f,.28f,1);var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=image;b.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>((Action)(()=>Guard(action))));
  Label(r,text,Vector2.zero,size,19).alignment=TextAlignmentOptions.Center;return r.gameObject;
 }
 TMP_InputField InputBox(Transform parent,string hint,Vector2 at,Vector2 size,bool numeric,Action<string> changed){
  var r=Rect("物品添加器.输入",parent,at,size);var image=r.gameObject.AddComponent<Image>();image.color=new Color(.05f,.07f,.08f,1);
  var viewport=Rect("文本区域",r,Vector2.zero,new Vector2(size.x-18,size.y-6));viewport.gameObject.AddComponent<RectMask2D>();
  var text=Label(viewport,"",Vector2.zero,viewport.sizeDelta);var placeholder=Label(viewport,hint,Vector2.zero,viewport.sizeDelta);placeholder.color=new Color(.65f,.7f,.7f,1);
  var field=r.gameObject.AddComponent<TMP_InputField>();field.targetGraphic=image;field.textViewport=viewport;field.textComponent=text;field.placeholder=placeholder;field.caretColor=Color.white;field.customCaretColor=true;field.pointSize=20;field.lineType=TMP_InputField.LineType.SingleLine;field.contentType=numeric?TMP_InputField.ContentType.IntegerNumber:TMP_InputField.ContentType.Standard;field.characterLimit=numeric?4:80;
  field.onValueChanged.AddListener(DelegateSupport.ConvertDelegate<UnityAction<string>>((Action<string>)(value=>Guard(()=>changed(value)))));return field;
 }
 internal void Open(){
  if(visible||!Available())return;
  try{
   LoadCatalogue();font=RoamingUI.Instance?.dayLabel?.font??TMP_Settings.defaultFontAsset;
   window=new GameObject("物品添加器.窗口",new[]{Il2CppType.Of<RectTransform>()});Object.DontDestroyOnLoad(window);
   var canvas=window.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30100;window.AddComponent<GraphicRaycaster>();var scaler=window.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
   var shade=Rect("遮罩",window.transform,Vector2.zero,Vector2.zero);shade.anchorMin=Vector2.zero;shade.anchorMax=Vector2.one;shade.offsetMin=Vector2.zero;shade.offsetMax=Vector2.zero;shade.gameObject.AddComponent<Image>().color=new Color(0,0,0,.7f);
   var panel=Rect("面板",shade,Vector2.zero,new Vector2(920,640));panel.gameObject.AddComponent<Image>().color=new Color(.1f,.13f,.14f,1);
   Label(panel,"物品添加器",new Vector2(-100,278),new Vector2(650,42),28);Button(panel,"关闭",new Vector2(385,278),new Vector2(96,38),Close);
   search=InputBox(panel,"输入中文名称或物品编号",new Vector2(-180,221),new Vector2(480,40),false,_=>{page=0;Filter();});
   Label(panel,"数量",new Vector2(116,221),new Vector2(60,38));quantity=InputBox(panel,"1～999",new Vector2(245,221),new Vector2(150,40),true,_=>{});quantity.SetTextWithoutNotify("1");
   var categories=new[]{"全部","装备","秘籍","食药","材料","配方","其他"};for(int i=0;i<categories.Length;i++){var choice=categories[i];Button(panel,choice,new Vector2(-366+i*122,165),new Vector2(112,36),()=>{category=choice;page=0;Filter();});}
   rows.Clear();labels.Clear();for(int i=0;i<PageSize;i++){
    int row=i;var r=Rect("物品行",panel,new Vector2(0,110-i*43),new Vector2(850,40));r.gameObject.AddComponent<Image>().color=new Color(.14f,.18f,.19f,1);rows.Add(r.gameObject);
    labels.Add(Label(r,"",new Vector2(-65,0),new Vector2(680,38),19));Button(r,"加入清单",new Vector2(361,0),new Vector2(110,34),()=>{int index=page*PageSize+row;if(index<matches.Count)Stage(matches[index]);});
   }
   pageLabel=Label(panel,"",new Vector2(0,-232),new Vector2(330,36));pageLabel.alignment=TextAlignmentOptions.Center;
   Button(panel,"上一页",new Vector2(-310,-232),new Vector2(160,36),()=>{page--;Render();});Button(panel,"下一页",new Vector2(310,-232),new Vector2(160,36),()=>{page++;Render();});
   Button(panel,"清空清单",new Vector2(-305,-279),new Vector2(170,38),()=>{cart.Clear();if(status)status!.text="清单已清空。";});
   Button(panel,"查看清单并确认",new Vector2(260,-279),new Vector2(290,38),()=>ShowConfirmation(0));
   status=Label(panel,"尚未添加物品；关闭窗口会清空待添加清单。",new Vector2(0,-196),new Vector2(850,30),17);
   token=window.Cast<Il2CppSystem.Object>();timer=GameTimer.Instance;
   GameManager.RegisterGameFunctionMask(Owner,GameFunctionMask.PlayerControl|GameFunctionMask.InGameTimeElapse|GameFunctionMask.SaveGame|GameFunctionMask.PlayerTriggerNpcSight|GameFunctionMask.PlayerPet,false);masked=true;timer.AddOrSetTimeScale(token,0);if(RoamingManager.Instance&&RoamingManager.Instance.player)RoamingManager.Instance.player.Stop();visible=true;page=0;category="全部";Filter();
  }catch{Close();throw;}
 }
 void Filter(){matches=catalogue.Where(x=>(category=="全部"||x.Category==category)&&ItemSpawnerPolicy.Matches(x.Id,x.Name,search?.text??"")).ToList();Render();}
 void Render(){page=ItemSpawnerPolicy.Page(page,matches.Count,PageSize);for(int i=0;i<rows.Count;i++){int index=page*PageSize+i;rows[i].SetActive(index<matches.Count);if(index<matches.Count){var e=matches[index];labels[i].text=e.Name+"  ·  编号"+e.Id+"  ·  "+e.Category;}}if(pageLabel)pageLabel!.text=category+" · "+matches.Count+"项 · 第"+(page+1)+"/"+Math.Max(1,(matches.Count+PageSize-1)/PageSize)+"页";}
 void Stage(Entry entry){
  if(!Available()){if(status)status!.text="当前无法添加，请在自由探索时操作。";return;}
  if(!cart.Stage(entry.Id,quantity?.text??"1",entry.Data.IsStackable)){if(status)status!.text="未加入：数量须为1～999；每种累计不超过999；每批最多50种。";return;}
  var items=cart.Snapshot();if(status)status!.text="已加入清单："+entry.Name+" ×"+items[entry.Id]+"；共"+items.Count+"种，等待确认。";
 }
 void HideConfirmation(){if(confirmation){confirmation!.SetActive(false);Object.Destroy(confirmation);}confirmation=null;pending.Clear();}
 void ShowConfirmation(int reviewPage){
  HideConfirmation();pending=cart.Snapshot();if(pending.Count==0){if(status)status!.text="清单为空，请先选择物品。";return;}
  var shade=Rect("物品添加器.确认遮罩",window!.transform,Vector2.zero,Vector2.zero);confirmation=shade.gameObject;shade.anchorMin=Vector2.zero;shade.anchorMax=Vector2.one;shade.offsetMin=Vector2.zero;shade.offsetMax=Vector2.zero;shade.gameObject.AddComponent<Image>().color=new Color(0,0,0,.87f);
  var panel=Rect("确认添加",shade,Vector2.zero,new Vector2(860,590));panel.gameObject.AddComponent<Image>().color=new Color(.12f,.16f,.17f,1);
  Label(panel,"确认添加以下物品？",new Vector2(0,244),new Vector2(760,42),26);
  Label(panel,"确认前不会往背包添加任何物品；不可堆叠物品每种1件。",new Vector2(0,197),new Vector2(760,34),17);
  var list=pending.OrderBy(x=>x.Key).ToList();reviewPage=ItemSpawnerPolicy.Page(reviewPage,list.Count,PageSize);
  for(int i=0;i<PageSize;i++){int index=reviewPage*PageSize+i;if(index>=list.Count)break;var item=list[index];var entry=catalogue.First(x=>x.Id==item.Key);
   Label(panel,entry.Name+"  ·  编号"+item.Key+"  ×"+item.Value,new Vector2(-70,140-i*42),new Vector2(620,38),19);
   Button(panel,"移除",new Vector2(337,140-i*42),new Vector2(90,32),()=>{cart.Remove(item.Key);ShowConfirmation(reviewPage);});
  }
  int current=reviewPage;Label(panel,"共"+list.Count+"种 · 第"+(current+1)+"/"+Math.Max(1,(list.Count+PageSize-1)/PageSize)+"页",new Vector2(0,-182),new Vector2(290,36)).alignment=TextAlignmentOptions.Center;
  Button(panel,"上一页",new Vector2(-280,-182),new Vector2(130,36),()=>ShowConfirmation(current-1));Button(panel,"下一页",new Vector2(280,-182),new Vector2(130,36),()=>ShowConfirmation(current+1));
  Button(panel,"返回选择",new Vector2(-210,-243),new Vector2(230,42),HideConfirmation);Button(panel,"确认添加整批物品",new Vector2(190,-243),new Vector2(300,42),ConfirmBatch);
 }
 void ConfirmBatch(){
  if(committing||!confirmation||pending.Count==0)return;
  if(!Available()){HideConfirmation();if(status)status!.text="状态已变化，本批未添加，请重新确认。";return;}
  committing=true;
  try{
   var snapshot=new Dictionary<int,int>(pending);var pack=new GameItemPack();
   foreach(var request in snapshot){var entry=catalogue.FirstOrDefault(x=>x.Id==request.Key);if(entry==null||ItemSpawnerPolicy.Count(request.Value.ToString(),entry.Data.IsStackable)!=request.Value||!pack.AddItem(entry.Data,request.Value,false))throw new InvalidOperationException("整批生成失败，尚未提交领取："+request.Key);}
   // Consume the confirmation before delivery so a double click cannot submit it twice.
   cart.Clear();HideConfirmation();PlayerTeamManager.Instance.PickupPack(pack,true,false,false,false,true);
   if(status)status!.text="已提交"+snapshot.Count+"种物品的领取，请检查背包；清单已清空。";
   Log.LogInfo("确认批量添加："+string.Join("，",snapshot.Select(x=>x.Key+"×"+x.Value)));
  }catch{HideConfirmation();throw;}finally{committing=false;}
 }
 internal void Close(){visible=false;try{if(masked)GameManager.UnregisterGameFunctionMask(Owner);}finally{masked=false;if(timer&&token!=null)timer!.RemoveTimeScale(token);timer=null;token=null;HideConfirmation();cart.Clear();if(window)Object.Destroy(window);window=null;search=null;quantity=null;status=null;pageLabel=null;rows.Clear();labels.Clear();catalogue.Clear();matches.Clear();}}
 [HarmonyPatch(typeof(HotKeyManager),"Update")]
 static class Hotkeys {static bool Prefix(){bool original=true;Instance?.Guard(()=>original=Instance.Keys());return original;}}
 public sealed class ItemWatcher:MonoBehaviour {public ItemWatcher(IntPtr pointer):base(pointer){}public void Update()=>Instance?.Tick();}
}
