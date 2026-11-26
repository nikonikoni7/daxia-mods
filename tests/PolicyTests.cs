using System;
using DaxiaMods;
class PolicyTests
{
 static int Main() {
  int failures=0;
  void Check(bool value,string name) { if(!value){Console.WriteLine("FAIL "+name);failures++;}else Console.WriteLine("PASS "+name); }
  var right=Rules.Edge(1919,540,1920,1080,24,true,false);
  Check(right.X==1 && right.Y==0,"right edge pans");
  var corner=Rules.Edge(0,1079,1920,1080,24,true,false);
  Check(Math.Abs(corner.X+0.70710678f)<0.0001 && Math.Abs(corner.Y-0.70710678f)<0.0001,"diagonal has same speed");
  Check(Rules.Edge(-1,540,1920,1080,24,true,false).X==0,"outside window does not pan");
  Check(Rules.Edge(0,540,1920,1080,24,false,false).X==0,"unfocused does not pan");
  Check(Rules.Edge(0,540,1920,1080,24,true,true).X==0,"UI hover does not pan");
  Check(Rules.SceneAllowed(true,false,false,false,false),"exploration allowed");
  Check(!Rules.SceneAllowed(true,true,false,false,false),"world map blocked");
  Check(!Rules.SceneAllowed(false,false,false,false,false),"battle and menu blocked");
  Check(!Rules.SceneAllowed(true,false,true,false,false),"loading blocked");
  Check(!Rules.SceneAllowed(true,false,false,true,false),"story blocked");
  Check(!Rules.SceneAllowed(true,false,false,false,true),"modal UI blocked");
  Check(Rules.CompressTimer(0.1f,0.2f,3)==1.5f,"slow motion compressed proportionally");
  Check(Rules.CompressTimer(0.21f,0.2f,3)==3,"finish deadline reaches original end");
  Check(Rules.CompressTimer(-1,0.2f,3)==0,"negative elapsed clamped");
  Check(Rules.Duration(float.NaN)==0.2f && Rules.Duration(0)==0.05f,"invalid duration safe");
  Check(Rules.ShouldCompleteBanner(true,true,true,false,0.2f,0.2f),"victory completes at deadline");
  Check(!Rules.ShouldCompleteBanner(true,true,true,false,0.19f,0.2f),"victory remains visible before deadline");
  Check(!Rules.ShouldCompleteBanner(true,false,true,false,1f,0.2f),"defeat confirmation remains original");
  Check(!Rules.ShouldCompleteBanner(true,true,true,true,1f,0.2f),"completed banner callback cannot repeat");
  var lease=new PauseLease();float speed=1.5f;int tokens=0;
  lease.Enter(()=>{tokens++;speed=0;},()=>{tokens--;speed=1.5f;});
  lease.Enter(()=>tokens++,()=>tokens--);
  Check(tokens==1 && speed==0,"pause acquired once");
  lease.Exit();lease.Exit();
  Check(tokens==0 && speed==1.5f,"exit restores exactly once");
  try {lease.Enter(()=>throw new Exception("acquire"),()=>tokens--);}catch{}
  Check(!lease.Active,"failed acquisition cannot leave active lease");
  Console.WriteLine($"Failures: {failures}");return failures==0?0:1;
 }
}
