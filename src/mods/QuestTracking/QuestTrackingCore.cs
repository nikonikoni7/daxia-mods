using System;
using System.Collections.Generic;
namespace DaxiaMods;
internal static class QuestTrackingPolicy {
 internal const int Limit=5;
 internal static bool TryAdd(int storyCount,int bountyCount,Action add,Action reject){
  if((long)storyCount+bountyCount>=Limit){reject();return false;}
  add();return true;
 }
 internal static void Render<T>(IReadOnlyList<T> notes,Action<int,T> draw,Action<int> clear){
  for(int i=0;i<Limit;i++){if(i<notes.Count)draw(i,notes[i]);else clear(i);}
 }
}
