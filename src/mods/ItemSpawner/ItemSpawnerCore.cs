using System;
using System.Globalization;
using System.Collections.Generic;
namespace DaxiaMods;
internal static class ItemSpawnerPolicy {
 internal static int Count(string text,bool stackable)=>int.TryParse(text,NumberStyles.Integer,CultureInfo.InvariantCulture,out var n)&&n>=1&&n<=999?(stackable?n:1):0;
 internal static bool Matches(int id,string name,string query){query=query.Trim();return query.Length==0||name.Contains(query,StringComparison.OrdinalIgnoreCase)||id.ToString(CultureInfo.InvariantCulture).Contains(query,StringComparison.Ordinal);}
 internal static string Category(string type)=>type.StartsWith("Equip",StringComparison.Ordinal)?"装备":type.StartsWith("KungfuBook",StringComparison.Ordinal)?"秘籍":type.StartsWith("Consumeable_Edible",StringComparison.Ordinal)?"食药":type.StartsWith("Consumeable_Material",StringComparison.Ordinal)?"材料":type=="Consumeable_Recipe"?"配方":"其他";
 internal static int Page(int page,int total,int size)=>Math.Clamp(page,0,Math.Max(0,(total-1)/size));
 internal static bool CanAdd(bool inventory,bool exploring,bool savingOrLoading,bool blocked)=>inventory&&exploring&&!savingOrLoading&&!blocked;
}
internal sealed class ItemSpawnerCart {
 readonly Dictionary<int,int> items=new();
 internal bool Stage(int id,string amount,bool stackable){
  int count=ItemSpawnerPolicy.Count(amount,stackable);if(id<=0||count==0)return false;
  if(!items.ContainsKey(id)&&items.Count>=50)return false;
  int next=stackable?(items.TryGetValue(id,out var old)?old:0)+count:1;
  if(next>999)return false;items[id]=next;return true;
 }
 internal Dictionary<int,int> Snapshot()=>new(items);
 internal void Remove(int id)=>items.Remove(id);
 internal void Clear()=>items.Clear();
}
