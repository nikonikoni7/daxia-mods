using System;
using System.Collections.Generic;
namespace DaxiaMods;
internal static class FusionRenamePolicy {
 internal static string? Validate(string? name,int limit,Func<string,bool> forbidden){
  if(string.IsNullOrWhiteSpace(name))return "名称不能为空。";
  if(limit>0&&name.Length>limit)return "名称最多"+limit+"个字符。";
  foreach(char c in name)if(char.IsControl(c))return "名称不能包含换行或控制字符。";
  if(forbidden(name))return "名称未通过原版取名检查。";
  return null;
 }
 // Include the throwing write: native setters can change state before throwing.
 internal static void Publish(List<Action> writes,List<Action> undo,Action persist){
  int applied=0;
  try{for(int i=0;i<writes.Count;i++){applied=i+1;writes[i]();}persist();}
  catch(Exception original){
   var failures=new List<Exception>{original};
   for(int i=applied-1;i>=0;i--)try{undo[i]();}catch(Exception e){failures.Add(e);}
   if(failures.Count>1)throw new AggregateException("改名失败且回滚遇到错误。",failures);
   throw;
  }
 }
}
