using System;
using System.Collections.Generic;
namespace DaxiaMods;
internal static class KungfuOrderPolicy {
 internal static bool CanSwapGroup(int a,int b)=>a==0&&b==0;
 internal static bool Swap(IList<long> list,int a,int b,Func<bool> valid,Action refresh){
  if(a<0||b<0||a>=list.Count||b>=list.Count||a==b||!valid())return false;
  long source=list[a],target=list[b];
  try{list[a]=target;list[b]=source;refresh();return true;}
  catch{list[a]=source;list[b]=target;throw;}
 }
 // Only reorder members present in the owner's list; native extra actions
 // retain their slots. Never mutate the AI/state-machine list passed in.
 internal static long[] OrderSubset(long[] owned,long[] subset){
  var rank=new Dictionary<long,int>();for(int i=0;i<owned.Length;i++)if(!rank.ContainsKey(owned[i]))rank.Add(owned[i],i);
  var known=new List<(long value,int rank,int index)>();
  for(int i=0;i<subset.Length;i++)if(rank.TryGetValue(subset[i],out int r))known.Add((subset[i],r,i));
  known.Sort((a,b)=>{int r=a.rank.CompareTo(b.rank);return r!=0?r:a.index.CompareTo(b.index);});
  var result=(long[])subset.Clone();int cursor=0;
  for(int i=0;i<result.Length;i++)if(rank.ContainsKey(subset[i]))result[i]=known[cursor++].value;
  return result;
 }
 internal static long[] OrderBattle(long[] normalOrder,long[] subset,bool[] unique){
  if(subset.Length!=unique.Length)throw new ArgumentException("武功分类与列表不匹配。");
  var first=new List<long>();var remaining=new List<long>();
  for(int i=0;i<subset.Length;i++){if(unique[i])first.Add(subset[i]);else remaining.Add(subset[i]);}
  first.AddRange(OrderSubset(normalOrder,remaining.ToArray()));return first.ToArray();
 }
 // UIKongfuPanel fills a native two-column grid in row-major order. Combat
 // follows the visible left column top-to-bottom, then the right column.
 internal static long[] LeftColumnFirst(long[] rows){
  var result=new long[rows.Length];int next=0;
  for(int column=0;column<2;column++)for(int i=column;i<rows.Length;i+=2)result[next++]=rows[i];
  return result;
 }
}
internal sealed class KungfuOrderGesture {
 double started;float x,y;bool pending;
 internal bool Dragging{get;private set;}
 internal void Begin(double now,float px,float py){Cancel();started=now;x=px;y=py;pending=true;}
 internal void Move(double now,float px,float py,bool valid){
  if(!valid){Cancel();return;}if(!pending||Dragging)return;
  if(now-started>=.15-1e-9){Dragging=true;return;}
  if((px-x)*(px-x)+(py-y)*(py-y)>64)Cancel();
 }
 internal bool Release(){bool drag=Dragging;Cancel();return drag;}
 internal void Cancel(){pending=false;Dragging=false;}
}
