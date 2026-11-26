using System;
using System.Reflection;
static class AnswerHintsTests {
 static int checks;
 static Type Policy=>typeof(AnswerHintsTests).Assembly.GetType("DaxiaMods.AnswerHintsPolicy")??throw new Exception("FAIL: scoped answer hints are missing");
 static int Find(string? group,int npc,int answer,int count,int[] indices)=>(int)Policy.GetMethod("FindAnswer",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object?[]{group,npc,answer,count,indices})!;
 static void Check(bool ok,string label){if(!ok)throw new Exception("FAIL: "+label);checks++;Console.WriteLine("PASS: "+label);}
 static void Main(){
  foreach(var p in new[]{("隆中书院老学究",20130),("太学馆对对子",20231),("蔡攸之吟诗作对",10018)})
   Check(Find(p.Item1,p.Item2,2,4,new[]{0,1,2,3})==2,"supported venue: "+p.Item1);
  foreach(var p in new[]{("院试",5),("乡试",10),("会试",10)})for(int i=1;i<=p.Item2;i++)
   Check(Find(p.Item1+i,100401,3,4,new[]{0,1,2,3})==3,"all examination stages: "+p.Item1+i);
  for(int npc=31245;npc<=31256;npc++)
   Check(Find("儒圣馆答疑解惑",npc,2,4,new[]{3,2,0,1})==1,"tutor quest maps the student's correct answer: "+npc);
  foreach(int npc in new[]{31244,31257})
   Check(Find("儒圣馆答疑解惑",npc,2,4,new[]{0,1,2,3})==-1,"outside student roster is rejected: "+npc);
  Check(Find("儒圣馆文人闹事",31245,2,4,new[]{0,1,2,3})==-1,"another Ru Sheng quest with a student NPC is excluded");
  foreach(var group in new string?[]{null,"","中文测试题库","儒圣馆看门对对子","儒圣馆文人闹事","儒圣馆答疑解惑","哑巴老太太猜字","端午节太白楼","端午节忘忧楼","端午节繁华似锦楼","院试0","院试6","乡试11","会试11","院试01","乡试1额外","太学馆对对子额外"})
   Check(Find(group,100401,0,4,new[]{0,1,2,3})==-1,"unrequested or invalid group rejected: "+group);
  Check(Find("隆中书院老学究",10018,0,4,new[]{0,1,2,3})==-1,"wrong question owner cannot activate hints");
  Check(Find("院试1",100401,0,4,new[]{3,1,0,2})==2,"map original answer index after display reorder");
  foreach(var indices in new[]{new[]{0,0,2,3},new[]{0,1,2},new[]{0,1,2,4},new[]{-1,1,2,3}})
   Check(Find("院试1",100401,0,4,indices)==-1,"malformed selection mapping is untouched");
  Check(Find("院试1",100401,-1,4,new[]{0,1,2,3})==-1,"negative answer leaves UI unchanged");
  Check(Find("院试1",100401,4,4,new[]{0,1,2,3})==-1,"out-of-range answer leaves UI unchanged");
  Check(Find("院试1",100401,0,0,Array.Empty<int>())==-1,"empty question leaves UI unchanged");
  Console.WriteLine($"AnswerHints logic: {checks} checks passed.");
 }
}
