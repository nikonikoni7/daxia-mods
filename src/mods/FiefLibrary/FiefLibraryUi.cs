using System;
using System.Linq;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using WuLin;
using WuLin.GameFrameworks;
using Object=UnityEngine.Object;
namespace DaxiaMods;
public sealed partial class FiefLibrary {
 ItemPackShower? left,right;GameItemInstance? selected;bool selectedDeposit;
 TMP_Text? selection;TMP_InputField? quantity;Button? transferButton;
 ObjectViewElemenetAdapator? boundView;InteractiveObject? boundShelf;bool originalInteractive,originalNativeInteractive;
 Collider2D[] ownedClickColliders=Array.Empty<Collider2D>();
 void ReleaseView(){
  if(boundView)boundView!.isInteractiveable=originalInteractive;
  if(boundShelf)boundShelf!.interactivable=originalNativeInteractive;
  // Only remove click colliders created by our native UpdateCollider call.
  // Immediate component removal also permits disable/re-enable in one frame.
  foreach(var collider in ownedClickColliders)if(collider){collider.enabled=false;Object.DestroyImmediate(collider);}
  ownedClickColliders=Array.Empty<Collider2D>();boundView=null;boundShelf=null;
 }
 void BindView(){
  if(!enabled.Value||PlayerTeamManager.Instance?.dataContainer?.initedObject?.mapId!=507||!MapManager.Instance){ReleaseView();return;}
  if(boundView&&Target(boundView!.InteractiveObject))return;
  ReleaseView();var list=MapManager.Instance.GetCurMapInfo()?.SaveBuildingList;if(list==null||!list.TryGetValue(77,out var obj)||!Target(obj))return;
  boundView=GameUtil.GetOrCreateViewAdapator<ObjectViewElemenetAdapator>(obj.Cast<IViewElemenet>(),true);
  if(boundView){
   boundShelf=obj;originalNativeInteractive=obj.interactivable;originalInteractive=boundView!.isInteractiveable;
   var existing=obj.GetComponentsInChildren<Collider2D>(true).Select(c=>c.Pointer).ToHashSet();
   // Native selection inlines InteractiveObject.interactivable (offset 0x4E)
   // and overwrites the view flag on every hover. UpdateCollider(1) enables
   // that source flag and creates the original sprite-shaped click polygons.
   try{obj.UpdateCollider(1);}
   finally{ownedClickColliders=obj.GetComponentsInChildren<Collider2D>(true).Where(c=>!existing.Contains(c.Pointer)).ToArray();}
   boundView.isInteractiveable=true;
   Log.LogInfo("已绑定正房藏经书架：SaveID=77，原生交互标志及点击区域已启用；原始外观及阻挡保持。");
  }
 }
 ItemPackShower CloneShower(ItemPackShower source,Transform parent,Vector2 pos,string name){
  var go=Object.Instantiate(source.gameObject,parent,false);go.name="FiefLibrary."+name;
  var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.localScale=Vector3.one;r.anchoredPosition=pos;r.sizeDelta=new Vector2(500,350);
  var shower=go.GetComponent<ItemPackShower>();shower.UnregisterPack();shower.navigationGroupName="FiefLibrary."+name;shower.reserveIconsRow=0;shower.showReservesWhenEmpty=false;shower.autoResetScrollPosWhenChanged=false;shower.showItemAsTemplete=false;
  go.SetActive(true);return shower;
 }
 void BuildStorageUi(RectTransform panel){
  var prefab=ResourceManager.Instance.GetAsset<GameObject>("UIPrefab/TradingWithNpcUI");
  var native=prefab?prefab.GetComponent<TradingWithNpcUI>():null;
  if(!native||!native!.playerBackPack)throw new InvalidOperationException("找不到原版双栏物品列表资源。");
  var frame=prefab.GetComponentsInChildren<Image>(true).FirstOrDefault(x=>x.sprite&&x.type==Image.Type.Sliced);
  if(frame){var bg=panel.GetComponent<Image>();bg.sprite=frame!.sprite;bg.type=Image.Type.Sliced;bg.color=Color.white;}
  Label(panel,"背包秘籍",new Vector2(-275,212),new Vector2(500,36));Label(panel,"藏经书架",new Vector2(275,212),new Vector2(500,36));
  left=CloneShower(native.playerBackPack,panel,new Vector2(-275,12),"Backpack");right=CloneShower(native.playerBackPack,panel,new Vector2(275,12),"Warehouse");
  selection=Label(panel,"选择秘籍查看详情，然后存入或取出",new Vector2(0,-196),new Vector2(1020,40),20);
  quantity=QuantityBox(panel);quantity.SetTextWithoutNotify("1");
  Button(panel,"全部",new Vector2(-50,-246),new Vector2(100,38),()=>{if(selected!=null&&quantity)quantity!.SetTextWithoutNotify(selected.Stack.ToString());});
  transferButton=Button(panel,"确认转移",new Vector2(195,-246),new Vector2(230,38),()=>{
   var item=selected;if(item==null||!quantity)return;int count=FiefLibraryPolicy.Quantity(quantity!.text,item.Stack);if(count==0){status!.text="请输入有效的转移数量。";return;}
   bool direction=selectedDeposit;selected=null;transferButton!.interactable=false;
   if(!Transfer(item,count,direction))status!.text="当前状态或物品数量变化，转移未执行，请重新选择。";
  });
  status=Label(panel,"默认不限容量；关闭窗口后请正常保存。悬停查看原版物品说明。",new Vector2(0,-289),new Vector2(1030,30),17);
 }
 TMP_InputField QuantityBox(Transform parent){
  Label(parent,"数量",new Vector2(-315,-246),new Vector2(80,38),20);
  var r=Rect("Quantity",parent,new Vector2(-200,-246),new Vector2(130,38));var image=r.gameObject.AddComponent<Image>();image.color=new Color(.1f,.075f,.05f);
  var viewport=Rect("Viewport",r,Vector2.zero,new Vector2(114,32));viewport.gameObject.AddComponent<RectMask2D>();
  var text=Label(viewport,"",Vector2.zero,new Vector2(114,32),20);
  var input=r.gameObject.AddComponent<TMP_InputField>();input.targetGraphic=image;input.textViewport=viewport;input.textComponent=text;input.contentType=TMP_InputField.ContentType.IntegerNumber;input.characterLimit=10;input.pointSize=20;return input;
 }
 void Select(ItemButton button,bool deposit){
  if(!visible||!SameContext())return;var item=button.Info?.GameItemInstance;if(!Eligible(item))return;
  selected=item;selectedDeposit=deposit;quantity!.SetTextWithoutNotify("1");
  selection!.text=(deposit?"存入：":"取出：")+item!.ItemName+"　"+Rarity(item.Templete.Rarity)+"　数量："+item.Stack;
  transferButton!.interactable=true;
 }
 static string Rarity(int n)=>GameUtil.GetItemDataRarity(n);
 void RefreshLists(){
  if(!left||!right)return;
  int token=uiGeneration;
  var a=DelegateSupport.ConvertDelegate<Il2CppSystem.Action<ItemButton>>((Action<ItemButton>)(b=>{if(visible&&token==uiGeneration)Guard(()=>Select(b,true));}));
  var b=DelegateSupport.ConvertDelegate<Il2CppSystem.Action<ItemButton>>((Action<ItemButton>)(x=>{if(visible&&token==uiGeneration)Guard(()=>Select(x,false));}));
  // Native additionMaskFunc returns true for items to HIDE. Keep native stack
  // instances: stack=false splits them into display-only template copies.
  var filter=DelegateSupport.ConvertDelegate<Il2CppSystem.Func<GameItemInstance,bool>>((Func<GameItemInstance,bool>)(item=>!Eligible(item)));
  left!.ShowPack(PlayerTeamManager.Instance.TeamInventory,false,WuLin.ItemType.KungfuBook,a,a,filter,null,true);
  right!.ShowPack(warehouse,false,WuLin.ItemType.KungfuBook,b,b,filter,null,true);
  selected=null;if(selection)selection!.text="选择秘籍查看名称、品级及数量";if(transferButton)transferButton!.interactable=false;
 }
}
