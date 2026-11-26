using System;
using System.Reflection;
static class AuctionSkyLanternTests {
 static int checks;
 static void Check(bool ok,string label){if(!ok)throw new Exception("FAIL: "+label);checks++;Console.WriteLine("PASS: "+label);}
 static void Main(){
  var type=typeof(AuctionSkyLanternTests).Assembly.GetType("DaxiaMods.AuctionSkyLanternState");
  Check(type!=null,"single-lot lantern state exists");
  var state=Activator.CreateInstance(type!)!;
  bool Call(string method,params object[] args)=>(bool)type!.GetMethod(method)!.Invoke(state,args)!;
  void Reset()=>type!.GetMethod("Reset")!.Invoke(state,null);
  Check(!Call("Matches",1L,2L,1),"inactive state never locks manual controls");
  Check(Call("Start",1L,2L,1),"light current lot");
  Check(Call("Matches",1L,2L,1),"same manager and lot are locked");
  Check(!Call("Start",1L,2L,1),"repeat click cannot rearm lantern");
  Check(!Call("Matches",9L,2L,1)&&!Call("Matches",1L,9L,1)&&!Call("Matches",1L,2L,2),"manager/lot/round changes invalidate old controls");
  Check(!Call("Consume",9L,2L,1),"stale settlement cannot consume current lot");
  Check(Call("Consume",1L,2L,1),"settlement claims one automatic bid");
  Check(!Call("Consume",1L,2L,1),"duplicate settlement never submits another bid");
  Check(Call("Matches",1L,2L,1),"controls stay locked while settlement executes");
  Reset();Check(!Call("Matches",1L,2L,1),"round end releases lock");
  Check(Call("Start",1L,3L,2)&&Call("Consume",1L,3L,2),"next lot may light independently");
  Reset();Check(!Call("Consume",1L,3L,2),"disabled/closed state rejects delayed settlement");
  foreach(var input in new[]{new object[]{0L,2L,1},new object[]{1L,0L,1},new object[]{1L,2L,0}})
   Check(!Call("Start",input),"missing native identity cannot light lantern");
  Console.WriteLine("AuctionSkyLantern policy checks passed: "+checks);
 }
}
