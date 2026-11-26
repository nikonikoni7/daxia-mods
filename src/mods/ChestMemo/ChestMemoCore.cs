using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace DaxiaMods;
public enum MemoGrouping { Map, Key, None }
public sealed class KeyNeed { public int Id {get;set;} public int Count {get;set;} public string Name {get;set;}=""; }
public sealed class OpenRoute {
 public List<KeyNeed> Keys {get;set;}=new();
 public bool HasOtherConditions {get;set;}
}
public sealed class ChestRecord {
 public int MapId {get;set;} public int SaveId {get;set;} public int TemplateId {get;set;}
 public int X {get;set;} public int Y {get;set;} public int Z {get;set;}
 public string MapName {get;set;}="";
 public List<OpenRoute> Routes {get;set;}=new();
}
public sealed class MemoGroup { public string Title {get;set;}=""; public List<ChestRecord> Items {get;set;}=new(); }
public sealed class ChestBook {
 public int Schema {get;set;}=1;
 public List<ChestRecord> Records {get;set;}=new();
 public ChestBook Clone()=>new(){Schema=Schema,Records=Records.Select(r=>new ChestRecord{MapId=r.MapId,SaveId=r.SaveId,TemplateId=r.TemplateId,X=r.X,Y=r.Y,Z=r.Z,MapName=r.MapName,
  Routes=r.Routes.Select(route=>new OpenRoute{HasOtherConditions=route.HasOtherConditions,Keys=route.Keys.Select(k=>new KeyNeed{Id=k.Id,Count=k.Count,Name=k.Name}).ToList()}).ToList()}).ToList()};
 public bool RecordAttempt(bool chest,bool picked,bool attempted,bool missing,ChestRecord record) {
  if(!chest || picked || !attempted || !missing || record.Routes.Count==0 || !record.Routes.Any(r=>r.Keys.Count>0))return false;
  var old=Records.Find(r=>Matches(r,record.MapId,record.SaveId,record.TemplateId,record.X,record.Y));
  if(old!=null){old.MapName=record.MapName;old.Routes=record.Routes;return false;}
  Records.Add(record);return true;
 }
 public static bool Matches(ChestRecord r,int map,int id,int template,int x,int y)=>r.MapId==map &&
  ((r.SaveId>0 || id>0)?r.SaveId==id:r.TemplateId==template && r.X==x && r.Y==y);
 public bool Remove(int map,int id,int template,int x,int y)=>Records.RemoveAll(r=>Matches(r,map,id,template,x,y))>0;
 public static bool Missing(IEnumerable<KeyNeed> keys,Func<int,int> count)=>keys.Any(k=>k.Count>0 && count(k.Id)<k.Count);
 public static string RequirementText(ChestRecord r) {
  var routes=r.Routes.Select(route=>string.Join("＋",route.Keys.Select(k=>k.Name+"×"+k.Count)))
   .Where(s=>s.Length>0).ToArray();
  return routes.Length>1?string.Join("或",routes.Select(s=>"（"+s+"）")):string.Join("",routes);
 }
 public List<MemoGroup> Groups(MemoGrouping grouping,int map,bool descending) {
  var compare=StringComparer.Create(CultureInfo.GetCultureInfo("zh-CN"),false);
  IOrderedEnumerable<ChestRecord> Sort(IEnumerable<ChestRecord> entries)=>entries.OrderBy(r=>r.MapId==map?0:1)
   .ThenBy(r=>r.MapName,descending?Comparer<string>.Create((a,b)=>compare.Compare(b,a)):compare)
   .ThenBy(r=>r.SaveId).ThenBy(r=>r.X).ThenBy(r=>r.Y);
  if(grouping==MemoGrouping.None)return new(){new(){Items=Sort(Records).ToList()}};
  if(grouping==MemoGrouping.Map)return Sort(Records).GroupBy(r=>r.MapId).Select(g=>new MemoGroup{Title=g.First().MapName,Items=g.ToList()}).ToList();
  return Records.SelectMany(r=>r.Routes.SelectMany(route=>route.Keys).GroupBy(k=>k.Id).Select(g=>(Key:g.First(),Record:r)))
   .GroupBy(x=>x.Key.Id).OrderBy(g=>g.First().Key.Name,compare)
   .Select(g=>new MemoGroup{Title=g.First().Key.Name,Items=Sort(g.Select(x=>x.Record)).ToList()}).ToList();
 }
 public static bool ShowDots(bool owned,bool open,bool roaming,int map,int shown)=>owned && open && roaming && map==shown;
 // Native UIThumbnail: anchor1 + CellToWorld(cell-anchor1) * (UI anchor distance / world anchor distance).
 public static bool TryProject(float dx,float dy,float ax,float ay,float bx,float by,float worldDistance,out float x,out float y) {
  x=0;y=0;var scale=MathF.Sqrt((bx-ax)*(bx-ax)+(by-ay)*(by-ay))/worldDistance;
  if(!float.IsFinite(scale) || worldDistance<=0.00001f || scale<=0)return false;
  x=ax+dx*scale;y=ay+dy*scale;return float.IsFinite(x)&&float.IsFinite(y);
 }
}
public sealed class ChestStore {
 readonly string root;
 readonly Action<string>? warning;
 static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
 public ChestStore(string path,Action<string>? warning=null){root=Path.GetFullPath(path);this.warning=warning;}
 public static string ResolveSlotPath(string slot,string saveRoot){
  if(string.IsNullOrWhiteSpace(slot))throw new IOException("原版尚未提供当前存档路径。");
  return Path.IsPathFullyQualified(slot)?Path.GetFullPath(slot):Path.GetFullPath(slot,saveRoot);
 }
 static string Hash(string s)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
 // A slot is a directory in this game. Hash .save payloads, never screenshots/backup files.
 static string Identity(string path) {
  path=Path.GetFullPath(path);
  var files=Directory.Exists(path)?Directory.GetFiles(path,"*.save").OrderBy(p=>Path.GetFileName(p),StringComparer.OrdinalIgnoreCase).ToArray():new[]{path};
  if(files.Length==0)throw new IOException("存档中没有可核对的.save文件。");
  using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
  foreach(var file in files){hash.AppendData(Encoding.UTF8.GetBytes(Path.GetFileName(file)));using var stream=File.OpenRead(file);var buffer=new byte[65536];int n;while((n=stream.Read(buffer,0,buffer.Length))>0)hash.AppendData(buffer,0,n);}
  return Hash(path.ToUpperInvariant())+"-"+Convert.ToHexString(hash.GetHashAndReset());
 }
 public string SnapshotPath(string path)=>Path.Combine(root,Identity(path)+".json");
 string WorkingPath(string path)=>Path.Combine(root,Identity(path)+".working.json");
 public void SaveSnapshot(string path,ChestBook book)=>Write(SnapshotPath(path),book);
 // Resolve content identity once for both sidecars; never cache across saves.
 public string SaveCheckpoint(string path,ChestBook book,ChestBook? working=null){
  var identity=Identity(path);SaveCheckpointIdentity(identity,book,working??book);return identity;
 }
 public string CaptureIdentity(string path)=>Identity(path);
 public void SaveCheckpointIdentity(string identity,ChestBook snapshot,ChestBook working){ValidateIdentity(identity);Write(Path.Combine(root,identity+".json"),snapshot);SaveWorkingIdentity(identity,working);}
 public ChestBook LoadSnapshot(string path)=>Read(SnapshotPath(path));
 public void SaveWorking(string path,ChestBook book)=>Write(WorkingPath(path),book);
 public void SaveWorkingIdentity(string identity,ChestBook book){
  ValidateIdentity(identity);
  Write(Path.Combine(root,identity+".working.json"),book);
 }
 static void ValidateIdentity(string identity){if(identity.Length!=129 || identity[64]!='-' || identity.Where((c,i)=>i!=64).Any(c=>!(c>='0'&&c<='9'||c>='A'&&c<='F')))throw new ArgumentException("无效存档身份。",nameof(identity));}
 public ChestBook LoadContext(string path,bool working,out string identity){
  identity=Identity(path);return working?LoadWorkingIdentity(identity):Read(Path.Combine(root,identity+".json"));
 }
 public ChestBook LoadWorking(string path){
  return LoadWorkingIdentity(Identity(path));
 }
 ChestBook LoadWorkingIdentity(string identity){
  var p=Path.Combine(root,identity+".working.json");var saved=Read(Path.Combine(root,identity+".json"));
  if(!File.Exists(p) && !File.Exists(p+".bak"))return saved;
  var working=Read(p);
  // A restart loads the game's last save, so an unsaved opening must not erase its saved marker.
  foreach(var r in saved.Records)if(!working.Records.Any(w=>ChestBook.Matches(w,r.MapId,r.SaveId,r.TemplateId,r.X,r.Y)))working.Records.Add(r);
  return working;
 }
 public void SavePending(string token,ChestBook book)=>Write(Path.Combine(root,"pending-"+token+".json"),book);
 static ChestBook Parse(string path) {
  var book=JsonSerializer.Deserialize<ChestBook>(File.ReadAllText(path)) ?? throw new InvalidDataException("记录为空。");
  if(book.Schema!=1 || book.Records==null || book.Records.Count>10000)throw new InvalidDataException("不支持或损坏的记录。");
  foreach(var r in book.Records){
   if(r==null || r.MapName==null || r.Routes==null || r.Routes.Count==0 || r.Routes.Count>100)throw new InvalidDataException("宝箱记录损坏。");
   foreach(var route in r.Routes)if(route==null || route.Keys==null || route.Keys.Any(k=>k==null || k.Id<=0 || k.Count<=0 || k.Name==null))throw new InvalidDataException("钥匙条件损坏。");
  }
  return book;
 }
 ChestBook Read(string path) {
  if(!File.Exists(path) && !File.Exists(path+".bak"))return new();
  try{return Parse(path);}catch(Exception e)when(e is JsonException || e is InvalidDataException || e is IOException){
   warning?.Invoke("宝箱记录读取失败，尝试备份："+e.Message);
   try{return Parse(path+".bak");}catch(Exception backup)when(backup is JsonException || backup is InvalidDataException || backup is IOException){throw new InvalidDataException("宝箱记录及备份无法读取，已保留文件。",backup);}
  }
 }
 void Write(string path,ChestBook book) {
  Directory.CreateDirectory(root);
  if(File.Exists(path)){
   bool valid=true;try{Parse(path);}catch(Exception e)when(e is JsonException || e is InvalidDataException){valid=false;}
   if(valid)File.Copy(path,path+".bak",true);
   else {File.Copy(path,path+".corrupt-"+Guid.NewGuid().ToString("N"));warning?.Invoke("损坏的宝箱记录已另行保留。");}
  }
  var tmp=path+".tmp";File.WriteAllText(tmp,JsonSerializer.Serialize(book,Json),new UTF8Encoding(false));
  File.Move(tmp,path,true);
 }
}
