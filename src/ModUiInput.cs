using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WuLin;
namespace DaxiaMods;

// Only visible, modal game panels block the tools. Hover tips are not menus.
internal static class ModUiInput {
 internal static string? BlockingMenu(bool allowMap){
  var stack=UiFunctionGroup.displayingUiStack;
  if(stack==null)return null;
  for(int i=stack.Count-1;i>=0;i--){
   var ui=stack[i];
   if(!ui||!ui.gameObject.activeInHierarchy||ui.isAlwaysActived||ui.GetComponentInParent<RoamingUI>()||ui.GetComponentInParent<UIItemTIps>())continue;
   if(allowMap&&ui.GetComponentInParent<UIThumbnail>())continue;
   var reference=ui.GetComponentInParent<UiRefHelper>();
   if(reference&&reference.uiLayer==UIRoot.GameUiLayer.ToolTip)continue;
   return ui.gameObject.name;
  }
  return null;
 }
 internal static bool Typing(){
  var selected=EventSystem.current?.currentSelectedGameObject;
  if(selected==null||!selected||!selected.activeInHierarchy)return false;
  var tmp=selected.GetComponentInParent<TMP_InputField>();
  if(tmp&&tmp.isFocused)return true;
  var legacy=selected.GetComponentInParent<InputField>();
  return legacy&&legacy.isFocused;
 }
}
