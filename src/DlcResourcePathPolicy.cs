using System;
using System.IO;
namespace DaxiaMods;
internal static class DlcResourcePathPolicy
{
 private static readonly string[] Bundles={Path.Combine("Dlc3","dlc3_portrait_assets_all.bundle"),Path.Combine("Dlc1","dlc1_config_assets_all.bundle")};
 internal static bool TryRepair(string root,string current,Func<string,bool> exists,Action<string> replace) {
  root=Path.GetFullPath(root);
  foreach(var bundle in Bundles)if(exists(Path.Combine(current,bundle)))return false;
  foreach(var bundle in Bundles)if(exists(Path.Combine(root,bundle))){replace(root);return true;}
  return false;
 }
}
