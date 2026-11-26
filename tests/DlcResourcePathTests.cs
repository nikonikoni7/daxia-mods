using System;
using System.IO;
using DaxiaMods;
static class DlcResourcePathTests
{
 static int Main() {
  int failures=0;
  void Check(bool ok,string name){Console.WriteLine((ok?"PASS ":"FAIL ")+name);if(!ok)failures++;}
  var root=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"DaxiaDlcPathTest"));
  var wrong=Path.Combine(root,"Wulin_Data");
  var suffix=Path.Combine("Dlc3","dlc3_portrait_assets_all.bundle");
  string? replacement=null;
  bool Correct(string current,Func<string,bool> exists)=>DlcResourcePathPolicy.TryRepair(root,current,exists,p=>replacement=p);
  Check(Correct(wrong,p=>p==Path.Combine(root,suffix)) && replacement==root,"missing portrait path is repaired to the installed Steam root");
  replacement=null;
  Check(!Correct(root,p=>p==Path.Combine(root,suffix)) && replacement==null,"working native path is preserved");
  Check(!Correct(wrong,_=>false) && replacement==null,"missing or uninstalled DLC is never fabricated");
  Check(!Correct(wrong,p=>p==Path.Combine(wrong,suffix)) && replacement==null,"existing custom resource path is preserved");
  Check(!Correct(wrong,p=>p==Path.Combine(root,"Dlc3","other.bundle")) && replacement==null,"unrelated files cannot authorize a path repair");
  Check(Correct(wrong,p=>p==Path.Combine(root,"Dlc1","dlc1_config_assets_all.bundle")),"same DLC root correction covers installed conquest resources");
  return failures==0?0:1;
 }
}
