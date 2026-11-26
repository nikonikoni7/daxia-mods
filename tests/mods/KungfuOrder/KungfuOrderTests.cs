using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
class KungfuOrderTests {
 static int checks;
 static Type Policy=null!,Gesture=null!;
 static void Check(bool ok,string message){if(!ok)throw new Exception("FAIL: "+message);checks++;}
 static object? Call(string method,params object[] args)=>Policy.GetMethod(method,BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(null,args);
 static bool Swap(List<long> list,int a,int b,bool valid,Action refresh){try{return (bool)Call("Swap",list,a,b,(Func<bool>)(()=>valid),refresh)!;}catch(TargetInvocationException e){throw e.InnerException!;}}
 static object NewGesture()=>Activator.CreateInstance(Gesture,true)!;
 static object? G(object g,string method,params object[] args)=>Gesture.GetMethod(method,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(g,args);
 static bool Dragging(object g)=>(bool)Gesture.GetProperty("Dragging",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.GetValue(g)!;
 static void Main(){
  var assembly=Assembly.GetExecutingAssembly();Policy=assembly.GetType("DaxiaMods.KungfuOrderPolicy")!;Gesture=assembly.GetType("DaxiaMods.KungfuOrderGesture")!;
  Check(Policy!=null&&Gesture!=null,"KungfuOrder implementation exists");
  var a=new List<long>{10,20,-99,40};var other=new List<long>{10,20,-99,40};int redraws=0;
  Check(Swap(a,0,2,true,()=>redraws++),"swap commits");Check(a.SequenceEqual(new long[]{-99,20,10,40}),"swap endpoints only, preserve negative fused identity");Check(other.SequenceEqual(new long[]{10,20,-99,40}),"another character unchanged");Check(redraws==1,"refresh once after swap");
  Check(!Swap(a,0,1,false,()=>throw new Exception()),"stale context/cross group rejected");Check(!Swap(a,0,0,true,()=>throw new Exception()),"same position rejected");Check(!Swap(a,-1,2,true,()=>throw new Exception()),"missing source rejected");
  try{Swap(a,0,3,true,()=>throw new InvalidOperationException("render failed"));throw new Exception("expected render failure");}catch(InvalidOperationException){Check(a.SequenceEqual(new long[]{-99,20,10,40}),"render failure restores order");}
  var order=new long[]{30,10,-9,20};var subset=new long[]{20,777,-9,10};
  var result=(long[])Call("OrderSubset",order,subset)!;Check(result.SequenceEqual(new long[]{10,777,-9,20}),"battle subset follows owned order, unknown native extra stays in place");Check(subset.SequenceEqual(new long[]{20,777,-9,10}),"battle source cache not modified");
  Check((bool)Call("CanSwapGroup",0,0)!&&!(bool)Call("CanSwapGroup",1,1)!&&!(bool)Call("CanSwapGroup",2,2)!,"only the ordinary left column can be reordered; unique and bond regions excluded");
  var g=NewGesture();G(g,"Begin",0d,0f,0f);G(g,"Move",.149d,0f,0f,true);Check(!Dragging(g),"short press not dragging");Check(!(bool)G(g,"Release")!,"short click not committed");
  G(g,"Begin",1d,0f,0f);G(g,"Move",1.15d,0f,0f,true);Check(Dragging(g),"hold activates at 0.15 seconds");G(g,"Move",1.2d,100f,10f,true);Check(Dragging(g),"moving after hold stays dragging");Check((bool)G(g,"Release")!,"drag release permits commit");
  Check(Policy!.GetMethod("OrderBattle",BindingFlags.Static|BindingFlags.NonPublic)!=null,"battle unique-first ordering exists");
  Check(Policy!.GetMethod("LeftColumnFirst",BindingFlags.Static|BindingFlags.NonPublic)!=null,"native two-column conversion exists");
  Check(((long[])Call("LeftColumnFirst",new long[]{10,20,30,40,50})!).SequenceEqual(new long[]{10,30,50,20,40}),"odd-sized native row-major grid becomes left column then right column");
  Check(((long[])Call("LeftColumnFirst",new long[]{10,20,30,40})!).SequenceEqual(new long[]{10,30,20,40}),"even grid remains column major");
  var mixed=new long[]{20,900,-9,901,10};var flags=new bool[]{false,true,false,true,false};
  var battle=(long[])Call("OrderBattle",order,mixed,flags)!;
  Check(battle.SequenceEqual(new long[]{900,901,10,-9,20}),"native unique order stays first, ordinary skills follow left column top to bottom");
  Check(mixed.SequenceEqual(new long[]{20,900,-9,901,10}),"battle sorting does not mutate the original cache");
  G(g,"Begin",2d,0f,0f);G(g,"Move",2.1d,9f,0f,true);G(g,"Move",3d,9f,0f,true);Check(!Dragging(g)&&!(bool)G(g,"Release")!,"early movement remains native scroll and cannot later activate");
  G(g,"Begin",4d,0f,0f);G(g,"Move",4.5d,0f,0f,true);G(g,"Cancel");Check(!Dragging(g)&&!(bool)G(g,"Release")!,"escape/focus/context loss cancels release");
  G(g,"Begin",5d,0f,0f);G(g,"Move",6d,0f,0f,false);Check(!Dragging(g),"invalid context cancels pending hold");
  Console.WriteLine("KungfuOrder tests PASS: "+checks);
 }
}
