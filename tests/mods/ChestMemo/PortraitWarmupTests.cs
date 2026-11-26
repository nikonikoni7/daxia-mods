using System;
using System.Reflection;
static class PortraitWarmupTests {
 static int checks;
 static Type queueType=null!;
 static void Check(bool ok,string label){if(!ok)throw new Exception("FAIL: "+label);checks++;Console.WriteLine("PASS: "+label);}
 static object Make(int limit)=>Activator.CreateInstance(queueType,new object[]{limit})!;
 static object? Call(object queue,string method,params object?[] args)=>queueType.GetMethod(method)!.Invoke(queue,args);
 static bool Add(object queue,string path)=>(bool)Call(queue,"Add",path)!;
 static object? Start(object queue){var args=new object?[]{null};var success=(bool)queueType.GetMethod("TryStart")!.Invoke(queue,args)!;return success?args[0]:null;}
 static bool Finish(object queue,object request)=>(bool)Call(queue,"Complete",request)!;
 static string Path(object request)=>(string)request.GetType().GetProperty("Path")!.GetValue(request)!;
 static void Main(){
  queueType=typeof(PortraitWarmupTests).Assembly.GetType("DaxiaMods.PortraitWarmupQueue")!;
  Check(queueType!=null,"bounded portrait warmup queue is available");
  var q=Make(2);Check(!Add(q,"")&&!Add(q," "),"empty resource keys never schedule IO");
  Check(Add(q,"Portrait/A")&&Add(q,"Portrait/B")&&!Add(q,"Portrait/C"),"one context accepts only its bounded resource budget");
  Check(!Add(q,"Portrait/A"),"duplicate NPC portraits share a request");
  var a=Start(q)!;Check(Path(a)=="Portrait/A"&&Start(q)==null,"only one native request may be in flight");
  Check(Finish(q,a),"current completion releases the in-flight request");
  Check(!Add(q,"Portrait/A"),"completed or failed keys are not retried every frame");
  var b=Start(q)!;Check(Path(b)=="Portrait/B","completion advances the remaining queue");
  Check(!Finish(q,a)&&Start(q)==null,"late duplicate callbacks cannot release a newer request");
  Call(q,"Reset");Check(Add(q,"Portrait/A"),"map/load reset allows a fresh bounded plan");
  Check(Start(q)==null,"map changes do not start a second request while old IO is pending");
  Check(!Finish(q,b),"completion from a previous map is stale but releases old IO");
  var fresh=Start(q)!;Check(Path(fresh)=="Portrait/A","old completion permits current-map preparation");
  Check(!Finish(q,b)&&Start(q)==null,"stale callback cannot release current-map IO");
  Check(Finish(q,fresh)&&Start(q)==null,"queue becomes idle after all scheduled resources");
  Call(q,"Reset");Check(Add(q,"Portrait/A"),"reset does not keep historical scene keys forever");
  var capture=typeof(PortraitWarmupTests).Assembly.GetType("DaxiaMods.PortraitWarmupCapture");
  Check(capture!=null,"native path enumeration has a scoped capture boundary");
  bool Intercept(string path)=>(bool)capture!.GetMethod("TryCapture")!.Invoke(null,new object[]{path})!;
  Check(!Intercept("normal request"),"normal game resource reads are never intercepted");
  var run=capture!.GetMethod("Run")!;
  var paths=(System.Collections.Generic.List<string>)run.Invoke(null,new object[]{(Action)(()=>{Intercept("first");Intercept("second");Intercept("first");})})!;
  Check(paths.Count==2&&paths[0]=="first"&&paths[1]=="second","native fallback order and duplicate identity are retained");
  try{run.Invoke(null,new object[]{(Action)(()=>throw new InvalidOperationException("enumeration failure"))});}catch(TargetInvocationException){}
  Check(!Intercept("after failure"),"path capture is released after exceptions");
  var outer=(System.Collections.Generic.List<string>)run.Invoke(null,new object[]{(Action)(()=>{Intercept("outer one");run.Invoke(null,new object[]{(Action)(()=>Intercept("inner"))});Intercept("outer two");})})!;
  Check(outer.Count==2&&outer[1]=="outer two","nested enumerations restore their parent's scope");
  bool otherThread=false;
  run.Invoke(null,new object[]{(Action)(()=>{System.Threading.Tasks.Task.Run(()=>otherThread=Intercept("background request")).GetAwaiter().GetResult();})});
  Check(!otherThread,"capture cannot change background-thread resource requests");
  Console.WriteLine("Portrait warmup checks passed: "+checks);
 }
}
