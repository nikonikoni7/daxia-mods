using System;
using System.Collections.Generic;
namespace DaxiaMods;
internal static class CompanionTravelPolicy {
 internal static string? Build(int character,bool joined,bool finished,Func<string,bool> contains){
  if(!joined||finished||character!=10092&&character!=10084)return null;
  bool hua=character==10092;
  var names=new[]{"楚襄城英雄雕像","将军府·正堂","楚襄城脏街",hua?"珍品阁":"群芳馆","珠光宝气楼","太学馆","破庙",hua?"神捕门":"燕子窝","流民营","乱葬岗"};
  var remaining=new List<string>();
  for(int i=0;i<names.Length;i++){
   // The opening rescue choice permanently gates each companion's temple scene.
   // Keep the original indices so later visit markers retain their native mapping.
   if(i==6&&!contains(hua?"Role1-1救燕子窝弟子":"Role1-1救洛千雪"))continue;
   bool done=hua?(i==0?contains("R10092-3_1")||contains("R10092-3_11"):contains("R10092-3_"+(i+1))):
    (i==0?contains("洛千雪游历1")||contains("洛千雪游历2"):contains("洛千雪游历"+(i+2)));
   if(!done)remaining.Add(names[i]);
  }
  return remaining.Count==0?"地点剧情已触发，等待原版游历总结。":string.Join("\n",remaining);
 }
}
