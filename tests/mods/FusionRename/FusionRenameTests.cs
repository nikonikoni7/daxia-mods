using System;
using System.Collections.Generic;
using System.Reflection;
static class FusionRenameTests {
 static Type Policy=>typeof(FusionRenameTests).Assembly.GetType("DaxiaMods.FusionRenamePolicy")??throw new Exception("FAIL: fusion rename validation and atomic publication are missing");
 static object? Call(string name,params object?[] args)=>Policy.GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args);
 static int checks;
 static void Check(bool value,string label){if(!value)throw new Exception("FAIL: "+label);checks++;Console.WriteLine("PASS: "+label);}
 static int Main(){try{Run();return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 static void Run(){
  Func<string,bool> blocked=s=>s=="禁词";
  Check((string?)Call("Validate","新剑法",8,blocked)==null,"ordinary new name accepted");
  foreach(var bad in new string?[]{null,"","  ","一二三四五六七八九","a\nb","禁词"})Check(Call("Validate",bad,8,blocked)!=null,"invalid name rejected without mutation");
  Check(Call("Validate","禁词",8,(Func<string,bool>)(_=>false))==null,"native detector bypass remains compatible");
  Check(Call("Validate","一二三四五六七八",8,blocked)==null,"native length boundary accepted");
  Check(Call("Validate","新剑法",0,blocked)==null,"zero native limit means unlimited");
  string local="旧名",book="旧名",stone="旧名";
  var writes=new List<Action>();var undo=new List<Action>();
  writes.Add(()=>local="新名");undo.Add(()=>local="旧名");
  writes.Add(()=>book="新名");undo.Add(()=>book="旧名");
  writes.Add(()=>stone="新名");undo.Add(()=>stone="旧名");
  Call("Publish",writes,undo,(Action)(()=>{}));
  Check(local=="新名"&&book=="新名"&&stone=="新名","publish all prepared name changes together");
  local=book=stone="旧名";
  try{Call("Publish",writes,undo,(Action)(()=>throw new InvalidOperationException("save failed")));throw new Exception("FAIL: failure was swallowed");}
  catch(TargetInvocationException e){Check(e.InnerException is InvalidOperationException,"persistence error is surfaced");}
  Check(local=="旧名"&&book=="旧名"&&stone=="旧名","failed publication rolls all names back");
  var partial=new List<Action>{()=>local="坏名",()=>{book="坏名";throw new InvalidOperationException("write failed");}};
  try{Call("Publish",partial,new List<Action>{()=>local="旧名",()=>book="旧名"},(Action)(()=>{}));throw new Exception("FAIL: partial failure was swallowed");}
  catch(TargetInvocationException e){Check(e.InnerException is InvalidOperationException,"partial write failure is surfaced");}
  Check(local=="旧名"&&book=="旧名","throwing write itself is rolled back");
  Console.WriteLine($"FusionRename logic: {checks} checks passed.");
 }
}
