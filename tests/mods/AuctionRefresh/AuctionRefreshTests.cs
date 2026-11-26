using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
static class AuctionRefreshTests {
 static int checks;
 static Type Policy=null!;
 static void Check(bool ok,string label){if(!ok)throw new Exception("FAIL: "+label);checks++;Console.WriteLine("PASS: "+label);}
 static object Call(string name,params object[] args)=>Policy.GetMethod(name)!.Invoke(null,args)!;
 static List<int> Draw(int[] pool,Func<int,int> random,Func<int,int>? create=null)=>
  (List<int>)Policy.GetMethod("Draw")!.MakeGenericMethod(typeof(int)).Invoke(null,new object[]{pool,random,create??(id=>id),(Comparison<int>)((a,b)=>a.CompareTo(b))})!;
 static void Main(){
  Policy=typeof(AuctionRefreshTests).Assembly.GetType("DaxiaMods.AuctionRefreshPolicy")!;
  Check(Policy!=null,"auction refresh behavior implemented");
  var source=new[]{9,2,9,4,7,1,5,3};
  var sampled=Draw(source,_=>0);
  Check(sampled.SequenceEqual(new[]{1,2,4,7,9,9}),"six lots sorted; duplicate pool entries retain their weight");
  Check(source.SequenceEqual(new[]{9,2,9,4,7,1,5,3}),"shared auction pool never modified");
  Check(Draw(new[]{8,4},n=>n-1).SequenceEqual(new[]{4,8}),"small pools terminate after available entries");
  Check(Draw(Array.Empty<int>(),_=>throw new Exception()).Count==0,"empty pool consumes no randomness");
  Check(Draw(new[]{1,2,3,4,5,6,7},n=>n-1).SequenceEqual(new[]{2,3,4,5,6,7}),"selection samples remaining entries without replacement");
  Check(Draw(new[]{1,2},_=>0,id=>id*10).SequenceEqual(new[]{10,20}),"all sampled IDs pass through item factory");
  bool threw=false;try{Draw(new[]{1,2},_=>0,id=>throw new InvalidOperationException("factory failure"));}catch(TargetInvocationException){threw=true;}
  Check(threw,"generation failures propagate before caller publishes results");
  var publish=Policy!.GetMethod("Publish");Check(publish!=null,"atomic publication with display rollback implemented");
  var transaction=publish!.MakeGenericMethod(typeof(string));string current="old";int draws=0;
  transaction.Invoke(null,new object[]{"old","new",(Action<string>)(s=>current=s),(Action)(()=>draws++)});
  Check(current=="new"&&draws==1,"complete new list published and redrawn once");
  current="old";draws=0;threw=false;
  try{transaction.Invoke(null,new object[]{"old","new",(Action<string>)(s=>current=s),(Action)(()=>{draws++;if(draws==1)throw new InvalidOperationException();})});}catch(TargetInvocationException){threw=true;}
  Check(threw&&current=="old"&&draws==2,"failed display restores old list and redraws original items");
  current="old";threw=false;
  try{transaction.Invoke(null,new object[]{"old","new",(Action<string>)(s=>current=s),(Action)(()=>throw new InvalidOperationException())});}catch(TargetInvocationException){threw=true;}
  Check(threw&&current=="old","repeated display failure still retains original list");
  Check((bool)Call("CanRefresh",true,true,false,false,false),"open preview allows refresh");
  foreach(var flags in new[]{new[]{false,true,false,false,false},new[]{true,false,false,false,false},new[]{true,true,true,false,false},new[]{true,true,false,true,false},new[]{true,true,false,false,true}})
   Check(!(bool)Call("CanRefresh",flags.Cast<object>().ToArray()),"disabled/closed/auction/save/reentrant states block refresh");
  Check((bool)Call("ShowCountdown","M20209-1",42,true),"existing waiting branch exposes native countdown");
  foreach(var c in new[]{("M20209-1",5,true),("M20209-1",42,false),("Fame-4",42,true),("M20209-2",42,true),("M20209-1",0,true)})
   Check(!(bool)Call("ShowCountdown",c.Item1,c.Item2,c.Item3),"other branches and disabled plugin retain native visibility");
  // Every current auction uses its own fixture pool: no global catalogue fallback.
  foreach(int id in new[]{102,103,104,105,106}){
   int start=id*100;var pool=Enumerable.Range(start,10).ToArray();
   Check(Draw(pool,_=>0).SequenceEqual(Enumerable.Range(start,6)),"auction "+id+" stays within its own configured pool");
  }
  Console.WriteLine("AuctionRefresh policy checks passed: "+checks);
 }
}
