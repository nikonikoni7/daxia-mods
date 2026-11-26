using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace DaxiaMods;
internal sealed class LibraryLocationRecovery {
 public int Schema {get;set;}=1;
 public string Slot {get;set;}="";
 public int SourceMap {get;set;}
 public int TargetMap {get;set;}
 public System.Collections.Generic.Dictionary<string,string> Files {get;set;}=new(StringComparer.OrdinalIgnoreCase);
 internal bool Matches(string slot){
  if(Schema!=1||SourceMap<=0||TargetMap<=0||SourceMap==TargetMap||Files==null||Files.Count==0||!Path.IsPathFullyQualified(Slot)||!string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(slot)),Path.TrimEndingDirectorySeparator(Path.GetFullPath(Slot)),StringComparison.OrdinalIgnoreCase)||!Directory.Exists(slot))return false;
  var actual=Directory.GetFiles(slot,"*.save");if(actual.Length!=Files.Count)return false;
  var names=new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach(var entry in Files){
   if(entry.Key!=Path.GetFileName(entry.Key)||!entry.Key.EndsWith(".save",StringComparison.OrdinalIgnoreCase)||!names.Add(entry.Key))return false;
   string path=Path.Combine(slot,entry.Key);
   if(!File.Exists(path)||!string.Equals(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),entry.Value,StringComparison.OrdinalIgnoreCase))return false;
  }
  return true;
 }
}
// Read only the required ES3 value. Deserializing a saved ScriptableObject can
// resolve its _ES3Ref to the live game object and overwrite current progress.
internal static class LibraryCheckpoint {
 internal static string Member(string text,params string[] path){
  foreach(string name in path){
   int p=0;Space(text,ref p);if(p>=text.Length||text[p++]!='{')throw new InvalidDataException("保存检查点不是对象。");
   string? found=null;
   while(true){
    Space(text,ref p);if(p>=text.Length)throw new InvalidDataException("保存检查点不完整。");
    if(text[p]=='}')break;
    int keyStart=p;Skip(text,ref p);string key=text[keyStart]=='"'?JsonSerializer.Deserialize<string>(text.Substring(keyStart,p-keyStart))!:text.Substring(keyStart,p-keyStart);
    Space(text,ref p);if(p>=text.Length||text[p++]!=':')throw new InvalidDataException("保存检查点缺少分隔符。");
    Space(text,ref p);int start=p;Skip(text,ref p);
    if(key==name){if(found!=null)throw new InvalidDataException("保存检查点包含重复字段。");found=text.Substring(start,p-start);}
    Space(text,ref p);if(p>=text.Length)throw new InvalidDataException("保存检查点不完整。");
    if(text[p]=='}')break;if(text[p++]!=',')throw new InvalidDataException("保存检查点分隔符无效。");
   }
   text=found??throw new InvalidDataException("保存检查点缺少字段："+name);
  }
  return text;
 }
 static void Space(string s,ref int p){while(p<s.Length&&char.IsWhiteSpace(s[p]))p++;}
 static void Skip(string s,ref int p){
  if(p>=s.Length)throw new InvalidDataException("保存检查点值缺失。");
  if(s[p]=='"'){
   int start=p++;while(p<s.Length){char c=s[p++];if(c=='"'){_=JsonSerializer.Deserialize<string>(s.Substring(start,p-start));return;}if(c=='\\'){if(p>=s.Length)break;p++;}}
   throw new InvalidDataException("保存检查点字符串不完整。");
  }
  if(s[p]=='{'||s[p]=='['){
   var close=new System.Collections.Generic.Stack<char>();close.Push(s[p++]=='{'?'}':']');
   while(p<s.Length){char c=s[p];if(c=='"'){Skip(s,ref p);continue;}p++;
    if(c=='{'||c=='[')close.Push(c=='{'?'}':']');
    else if(c=='}'||c==']'){if(close.Pop()!=c)throw new InvalidDataException("保存检查点括号不匹配。");if(close.Count==0)return;}
   }
   throw new InvalidDataException("保存检查点对象不完整。");
  }
  int begin=p;while(p<s.Length&&!char.IsWhiteSpace(s[p])&&s[p]!=','&&s[p]!=':'&&s[p]!='}'&&s[p]!=']')p++;
  if(begin==p)throw new InvalidDataException("保存检查点值无效。");
 }
 internal static string Payload(string text,string key){
  using var strings=JsonDocument.Parse(Member(text,"Data","value","miscStringDict"));
  return strings.RootElement.TryGetProperty(key,out var value)?value.GetString()??throw new InvalidDataException("仓库字段不是字符串。"):"";
 }
}
internal sealed class LibraryDeferredEvents {
 readonly System.Collections.Generic.List<Action> queue=new();int state;
 internal void Enqueue(Action action){if(state==0)queue.Add(action);else if(state==1)action();}
 internal void Commit(){state=1;var pending=queue.ToArray();queue.Clear();System.Collections.Generic.List<Exception>? errors=null;foreach(var action in pending)try{action();}catch(Exception e){(errors??=new()).Add(e);}if(errors!=null)throw new AggregateException("物品已转移，原版通知处理失败。",errors);}
 internal void Discard(){state=2;queue.Clear();}
}
internal static class FiefLibraryPolicy {
 internal static bool Target(int map,int id,string name,string sprite)=>map==507&&id==77&&name=="guizi31_4_26"&&sprite=="guizi31_C_0";
 internal static int Quantity(string text,int maximum)=>int.TryParse(text,out var n)&&n>0&&n<=maximum?n:0;
}
internal sealed class FiefLibraryTransaction {
 internal bool Busy=false,Blocked=false;
 internal bool Execute(Func<bool> valid,Action move,Action commit,Action restore,Action verify){
  if(Busy||Blocked||!valid())return false;
  Busy=true;
  try{move();verify();if(!valid())throw new InvalidOperationException("转移期间上下文变化。");commit();return true;}
  catch(Exception e){try{restore();}catch(Exception rollback){Blocked=true;throw new AggregateException("秘籍转移还原失败，已锁定仓库；请重新读档。",e,rollback);}throw;}
  finally{Busy=false;}
 }
}
internal sealed class LibraryEnvelope {
 public int Schema {get;set;}=1;
 public long Revision {get;set;}
 public string Pack {get;set;}="";
 public string Digest {get;set;}="";
 internal static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
 internal static string Encode(string pack,long revision){
  if(revision<0)throw new InvalidDataException("仓库修订号无效。");
  _=Convert.FromBase64String(pack);
  return JsonSerializer.Serialize(new LibraryEnvelope{Pack=pack,Revision=revision,Digest=Hash("1\n"+revision+"\n"+pack)});
 }
 internal static LibraryEnvelope Decode(string value){
  try{var e=JsonSerializer.Deserialize<LibraryEnvelope>(value);if(e==null||e.Schema!=1||e.Revision<0||e.Digest!=Hash("1\n"+e.Revision+"\n"+e.Pack))throw new InvalidDataException("仓库版本或校验不符。");_=Convert.FromBase64String(e.Pack);return e;}
  catch(Exception e) when(e is JsonException||e is FormatException){throw new InvalidDataException("仓库内容损坏，原始数据已保留。",e);}
 }
}
internal sealed class LibrarySaveJournal {
 public int Schema {get;set;}=1;
 public string Slot {get;set;}="";
 public string Generation {get;set;}=Guid.NewGuid().ToString("N");
 public string Phase {get;set;}="Pending";
 public string BeforeInventoryHash {get;set;}="";
 public string BeforePayload {get;set;}="";
 public string AfterInventoryDigest {get;set;}="";
 public string AfterPayload {get;set;}="";
 public string GameHash {get;set;}="";
 public string InventoryHash {get;set;}="";
 internal string Resolve(string fileHash,string digest){
  bool before=BeforeInventoryHash.Length>0&&fileHash==BeforeInventoryHash,after=digest==AfterInventoryDigest;
  if(before&&after&&BeforePayload!=AfterPayload)throw new InvalidDataException("保存恢复证据冲突。");
  if(after)return AfterPayload;if(before)return BeforePayload;
  throw new InvalidDataException("存档未匹配保存事务，已停止仓库操作；请使用原版完整备份恢复。");
 }
 internal static void Write(string path,LibrarySaveJournal record){
  Directory.CreateDirectory(Path.GetDirectoryName(path)!);string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){var bytes=JsonSerializer.SerializeToUtf8Bytes(record);f.Write(bytes);f.Flush(true);}File.Move(temp,path,true);}finally{if(File.Exists(temp))File.Delete(temp);}
 }
 internal static LibrarySaveJournal? Read(string path){
  if(!File.Exists(path))return null;
  try{var x=JsonSerializer.Deserialize<LibrarySaveJournal>(File.ReadAllText(path));if(x==null||x.Schema!=1||x.Slot.Length==0||x.Generation.Length!=32||(x.Phase!="Pending"&&x.Phase!="Committed"))throw new InvalidDataException("保存日志损坏。");return x;}
  catch(JsonException e){throw new InvalidDataException("保存日志损坏，已保留原文件。",e);}
 }
}
