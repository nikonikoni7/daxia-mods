using System;
using System.Collections.Generic;
using System.Reflection;
static class CompanionTravelTests {
 static int checks;
 static string? Build(int id,bool joined,bool finished,HashSet<string> tags){
  var type=typeof(CompanionTravelTests).Assembly.GetType("DaxiaMods.CompanionTravelPolicy")??throw new Exception("FAIL: companion travel progress policy is missing");
  return (string?)type.GetMethod("Build",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{id,joined,finished,(Func<string,bool>)tags.Contains});
 }
 static void Check(bool ok,string label){if(!ok)throw new Exception("FAIL: "+label);checks++;Console.WriteLine("PASS: "+label);}
 static void Main(){
  var eligible=new HashSet<string>{"Role1-1救燕子窝弟子","Role1-1救洛千雪"};
  var tags=new HashSet<string>(eligible);
  Check(Build(10092,false,false,tags)==null,"never recruited companion has no task");
  var hua=Build(10092,true,false,tags)!;var luo=Build(10084,true,false,tags)!;
  Check(hua.Split('\n').Length==10&&hua.Contains("神捕门")&&hua.Contains("珍品阁")&&!hua.Contains("群芳馆"),"Hua starts with her ten places");
  Check(luo.Split('\n').Length==10&&luo.Contains("燕子窝")&&luo.Contains("群芳馆")&&!luo.Contains("珍品阁"),"Luo starts with her ten places");
  var helpedLuo=new HashSet<string>{"Role1-1救洛千雪"};
  var helpedHua=new HashSet<string>{"Role1-1救燕子窝弟子"};
  Check(!Build(10092,true,false,helpedLuo)!.Contains("破庙")&&Build(10092,true,false,helpedLuo)!.Split('\n').Length==9,"helping Luo excludes Hua temple from the old-save task");
  Check(Build(10084,true,false,helpedLuo)!.Contains("破庙")&&Build(10084,true,false,helpedLuo)!.Split('\n').Length==10,"helping Luo retains her unvisited temple");
  Check(Build(10092,true,false,helpedHua)!.Contains("破庙")&&Build(10092,true,false,helpedHua)!.Split('\n').Length==10,"helping Hua retains her unvisited temple");
  Check(!Build(10084,true,false,helpedHua)!.Contains("破庙")&&Build(10084,true,false,helpedHua)!.Split('\n').Length==9,"helping Hua excludes Luo temple");
  Check(!Build(10092,true,false,new())!.Contains("破庙")&&!Build(10084,true,false,new())!.Contains("破庙"),"recruitment without either rescue does not imply temple eligibility");
  helpedLuo.Add("洛千雪游历8");helpedHua.Add("R10092-3_7");
  Check(!Build(10092,true,false,helpedHua)!.Contains("破庙")&&!Build(10084,true,false,helpedLuo)!.Contains("破庙"),"eligible temple disappears after its original completion marker");
  Check(Build(10092,true,false,helpedLuo)!.Contains("神捕门")&&Build(10084,true,false,helpedHua)!.Contains("燕子窝"),"excluding temple preserves the following place marker mapping");
  Check(Build(10092,true,false,eligible)!.Contains("破庙")&&!Build(10092,true,false,new(){"Role1-1救洛千雪"})!.Contains("破庙")&&Build(10092,true,false,eligible)!.Contains("破庙"),"switching rescue checkpoints rebuilds eligibility without cached progress");
  tags.Add("R10092-3_2");tags.Add("洛千雪游历6");
  Check(!Build(10092,true,false,tags)!.Contains("将军府")&&Build(10092,true,false,tags)!.Contains("珠光宝气楼"),"old Hua save hides only her completed place");
  Check(!Build(10084,true,false,tags)!.Contains("珠光宝气楼")&&Build(10084,true,false,tags)!.Contains("将军府"),"companions do not share progress");
  foreach(var tag in new[]{"R10092-3_1","R10092-3_11"}){
   Check(!Build(10092,true,false,new(){tag})!.Contains("雕像"),"either Hua statue scene completes the single place: "+tag);
  }
  foreach(var tag in new[]{"洛千雪游历1","洛千雪游历2"}){
   Check(!Build(10084,true,false,new(){tag})!.Contains("雕像"),"either Luo statue scene completes the single place: "+tag);
  }
  Check(Build(10092,true,false,new(){"Role1-1救燕子窝弟子","R10092-3_1","R10092-3_11"})!.Split('\n').Length==9,"both statue branches count once");
  Check(Build(10092,true,false,tags)!=null,"recruitment history keeps task after departure");
  Check(Build(10092,true,true,tags)==null,"confirmed native final reward removes task even with unvisited places");
  var all=new HashSet<string>();for(int i=1;i<=11;i++)all.Add("R10092-3_"+i);
  Check(Build(10092,true,false,all)=="地点剧情已触发，等待原版游历总结。","all places alone do not finish the task or award skill");
  Check(Build(10092,true,true,all)==null,"native completion hides the final summary");
  Check(Build(10092,true,false,eligible)==hua,"older checkpoint restores remaining places without cached progress");
  Check(Build(10092,false,false,new())==null,"new game does not inherit prior recruitment");
  Check(Build(10084,true,false,new(){"Role1-1救洛千雪","洛千雪游历11"})!.Split('\n').Length==9,"Luo eleventh tag is burial ground, not an extra place");
  Check(Build(12345,true,false,tags)==null,"unrelated character is ignored");
  Console.WriteLine("CompanionTravel logic: "+checks+" checks passed; native UI/save acceptance separate.");
 }
}
