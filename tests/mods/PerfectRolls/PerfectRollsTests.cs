using System;
using DaxiaMods;
static class PerfectRollsTests {
 static int checks;
 static void Check(bool value,string label){if(!value)throw new Exception("FAIL: "+label);checks++;}
 static void Best(string key,decimal a,decimal b,float min,float max,RollDirection direction,decimal expected,string label){
  Check(PerfectRollPolicy.TryBest(key,a,b,min,max,direction,out var actual)&&actual==expected,label+" expected="+expected+" actual="+actual);
 }
 static void Main(){
  Best("攻击",10,25,0,1,RollDirection.Higher,25,"beneficial attack reaches legal maximum");
  Best("攻击",10,25,0.2f,0.6f,RollDirection.Higher,19,"craft level limits maximum");
  Best("攻击",25,10,0.2f,0.6f,RollDirection.Higher,22,"reversed endpoints use attainable beneficial end");
  Best("攻击",-30,-10,0,1,RollDirection.Higher,-10,"negative beneficial stat minimizes penalty");
  Best("消耗",10,30,0.2f,0.6f,RollDirection.Lower,14,"harmful magnitude chooses minimum");
  Best("暴击%",0.06m,0.15m,0,1,RollDirection.Higher,0.15m,"percent retains decimals");
  Best("特殊效果",0.1m,0.25m,0,1,RollDirection.Higher,0.25m,"fractional upper endpoint retains decimals");
  Best("特殊效果",1,2.5m,0,1,RollDirection.Higher,2.5m,"noninteger upper endpoint avoids integer rounding");
  Best("攻击",10,25,0,0.5f,RollDirection.Higher,18,"native integer midpoint rounds to even");
  Best("效果@",1,2,0,0.125f,RollDirection.Higher,1.12m,"native two decimal midpoint rounds to even");
  Check(!PerfectRollPolicy.TryBest("固定",10,10,0,1,RollDirection.Higher,out _),"fixed effects remain untouched");
  Check(!PerfectRollPolicy.TryBest("未知",10,20,0,1,RollDirection.Unknown,out _),"unknown semantics remain untouched");
  Check(!PerfectRollPolicy.TryBest("攻击",10,20,float.NaN,1,RollDirection.Higher,out _),"invalid factor rejected");
  Check(!PerfectRollPolicy.TryBest("攻击",10,20,1,0,RollDirection.Higher,out _),"inverted factor bounds rejected");
  Console.WriteLine("PerfectRolls numeric tests passed: "+checks);
 }
}
