using System;
namespace DaxiaMods;
public static class BountyRefreshPolicy {
 // Native state: Open=0, Accept=1, Done=2, Cancel=3, Close=4.
 // CloseByStoryHunter may remove a slot entirely; missing slots must also lock.
 public static bool AllOpen(int count,Func<int,int> id,Func<int,int> state){
  if(count!=3)return false;
  for(int i=0;i<3;i++){
   if(id(i)<=0||state(i)!=0)return false;
   for(int j=0;j<i;j++)if(id(i)==id(j))return false;
  }
  return true;
 }
 public static bool CanRefresh(bool enabled,bool visible,bool saving,bool loadingMap,bool story,bool busy)=>enabled&&visible&&!saving&&!loadingMap&&!story&&!busy;
}
public sealed class BountyRefreshOperation {
 public bool Busy{get;private set;}
 public bool TryRefresh<T>(Func<bool> available,Func<T> read,Action<T> write,Func<T> create,Action generate,Predicate<T> valid,Action redraw){
  if(Busy||!available())return false;
  var before=read();Busy=true;bool assigned=false;
  try{
   var next=create();write(next);assigned=true;
   generate();
   if(!valid(read()))throw new InvalidOperationException("原版任务池未生成完整三个有效任务。");
   redraw();return true;
  }catch{
   if(assigned){write(before);try{redraw();}catch{/* Original data is already restored. */}}
   throw;
  }finally{Busy=false;}
 }
}
