using System;
using System.Collections.Generic;
using System.Reflection;
static class QuestTrackingTests {
 static int checks;
 static Type Policy=>typeof(QuestTrackingTests).Assembly.GetType("DaxiaMods.QuestTrackingPolicy")??throw new Exception("FAIL: quest tracking extension is missing");
 static void Check(bool value,string label){if(!value)throw new Exception("FAIL: "+label);checks++;Console.WriteLine("PASS: "+label);}
 static bool Add(int story,int bounty,Action add,Action reject)=>(bool)Policy.GetMethod("TryAdd",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{story,bounty,add,reject})!;
 static void Draw(IReadOnlyList<string> notes,string?[] rows){
  Policy.GetMethod("Render",BindingFlags.Static|BindingFlags.NonPublic)!.MakeGenericMethod(typeof(string)).Invoke(null,new object[]{notes,(Action<int,string>)((i,n)=>rows[i]=n),(Action<int>)(i=>rows[i]=null)});
 }
 static void Main(){
  foreach(var pair in new[]{(0,0),(2,1),(0,3),(3,1),(1,3),(0,4),(4,0)}){
   int added=0,rejected=0;bool result=Add(pair.Item1,pair.Item2,()=>added++,()=>rejected++);
   Check(result&&added==1&&rejected==0,$"tracking may grow below five: story={pair.Item1}, bounty={pair.Item2}");
  }
  foreach(var pair in new[]{(5,0),(2,3),(0,5),(4,2)}){
   int added=0,rejected=0;bool result=Add(pair.Item1,pair.Item2,()=>added++,()=>rejected++);
   Check(!result&&added==0&&rejected==1,$"sixth task rejected without adding: story={pair.Item1}, bounty={pair.Item2}");
  }
  var story=new HashSet<string>{"a","b","c"};var bounty=new HashSet<int>{41};
  Check(Add(story.Count,bounty.Count,()=>story.Add("d"),()=>throw new Exception("unexpected rejection")),"fifth task added");
  Check(!Add(story.Count,bounty.Count,()=>bounty.Add(42),()=>{}),"full mixed list blocks extra task");
  story.Remove("a");Check(Add(story.Count,bounty.Count,()=>bounty.Add(42),()=>{}),"cancelling one frees one shared slot");
  Check(story.SetEquals(new[]{"b","c","d"})&&bounty.SetEquals(new[]{41,42}),"existing choices preserved");
  var rows=new string?[5];Draw(new[]{"main","side","hunter","four","five"},rows);
  Check(string.Join("|",rows)=="main|side|hunter|four|five","all five rows retain original order");
  Draw(new[]{"new-slot"},rows);Check(rows[0]=="new-slot"&&rows[1]==null&&rows[4]==null,"switching to fewer tasks clears stale rows");
  Draw(Array.Empty<string>(),rows);Check(Array.TrueForAll(rows,x=>x==null),"empty new game clears every row");
  Draw(new[]{"1","2","3","4","5","6"},rows);Check(string.Join("|",rows)=="1|2|3|4|5","oversized preexisting list displays only five without deleting records");
  Console.WriteLine($"QuestTracking logic: {checks} checks passed; native UI and save acceptance are separate.");
 }
}
