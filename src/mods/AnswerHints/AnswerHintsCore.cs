using System;
namespace DaxiaMods;
internal static class AnswerHintsPolicy {
 internal static int FindAnswer(string? group,int npc,int answer,int count,int[] indices){
  bool supported=(group=="隆中书院老学究"&&npc==20130)||(group=="太学馆对对子"&&npc==20231)||(group=="蔡攸之吟诗作对"&&npc==10018)
   ||(group=="儒圣馆答疑解惑"&&npc>=31245&&npc<=31256);
  if(!supported&&npc==100401){
   foreach(var stage in new[]{("院试",5),("乡试",10),("会试",10)}){
    for(int n=1;n<=stage.Item2;n++)if(group==stage.Item1+n){supported=true;break;}
    if(supported)break;
   }
  }
  if(!supported||count<=0||answer<0||answer>=count||indices.Length!=count)return -1;
  var seen=new bool[count];int position=-1;
  for(int i=0;i<count;i++){
   int index=indices[i];if(index<0||index>=count||seen[index])return -1;
   seen[index]=true;if(index==answer)position=i;
  }
  return position;
 }
}
