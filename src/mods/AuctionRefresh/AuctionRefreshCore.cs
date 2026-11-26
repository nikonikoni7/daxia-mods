using System;
using System.Collections.Generic;
namespace DaxiaMods;
public static class AuctionRefreshPolicy {
 // Native StartAuctionEntrance samples pool entries, not distinct IDs. Repeated
 // entries are intentional weights and may produce repeated item IDs.
 public static List<T> Draw<T>(int[] pool,Func<int,int> random,Func<int,T> create,Comparison<T> compare){
  var remaining=new List<int>(pool);var result=new List<T>();
  while(result.Count<6&&remaining.Count>0){
   int index=random(remaining.Count);
   result.Add(create(remaining[index]));remaining.RemoveAt(index);
  }
  result.Sort(compare);return result;
 }
 public static void Publish<T>(T before,T next,Action<T> assign,Action redraw){
  assign(next);
  try{redraw();}
  catch{
   assign(before);
   try{redraw();}catch{/* The data is already restored even if the view is unavailable. */}
   throw;
  }
 }
 public static bool CanRefresh(bool enabled,bool previewOpen,bool inAuction,bool saving,bool busy)=>enabled&&previewOpen&&!inAuction&&!saving&&!busy;
 public static bool ShowCountdown(string graph,int saveId,bool enabled)=>enabled&&graph=="M20209-1"&&saveId==42;
}
