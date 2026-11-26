using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace DaxiaMods;
public sealed class TrainingRecord {
 public int CharacterId {get;set;}
 public int TemplateId {get;set;}
 public int BookId {get;set;}
}
public sealed class TrainingBook {
 public int Schema {get;set;}=1;
 public Dictionary<int,TrainingRecord> Records {get;set;}=new();
 public void RememberLeave(bool success,bool wasMember,bool isMember,int characterId,int templateId,int bookId){
  if(!success || !wasMember || isMember || characterId<=0 || templateId<=0)return;
  // Native fused kungfu books use negative dynamic IDs; only zero means no book.
  if(bookId==0){Records.Remove(characterId);return;}
  Records[characterId]=new(){CharacterId=characterId,TemplateId=templateId,BookId=bookId};
 }
 public TrainingRecord? TakeOnReturn(bool success,bool wasMember,bool isMember,int characterId,int templateId){
  if(!success || wasMember || !isMember || !Records.Remove(characterId,out var record))return null;
  return record.TemplateId==templateId?record:null;
 }
 public TrainingBook Clone()=>new(){Records=Records.ToDictionary(p=>p.Key,p=>new TrainingRecord{CharacterId=p.Value.CharacterId,TemplateId=p.Value.TemplateId,BookId=p.Value.BookId})};
}
public sealed class TrainingStore {
 readonly string root;
 static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
 public TrainingStore(string root){this.root=Path.GetFullPath(root);}
 public static string ResolveSlotPath(string slot,string saveRoot){
  if(string.IsNullOrWhiteSpace(slot))throw new IOException("原版尚未提供当前存档路径。");
  return Path.IsPathFullyQualified(slot)?Path.GetFullPath(slot):Path.GetFullPath(slot,saveRoot);
 }
 public string SnapshotPath(string path){
  path=Path.GetFullPath(path);
  // Native slots are directories; screenshots and backups are not part of game state.
  var files=Directory.Exists(path)?Directory.GetFiles(path,"*.save").OrderBy(p=>Path.GetFileName(p),StringComparer.OrdinalIgnoreCase).ToArray():new[]{path};
  if(files.Length==0)throw new IOException("存档中没有可核对的.save文件。");
  using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
  foreach(var file in files){hash.AppendData(Encoding.UTF8.GetBytes(Path.GetFileName(file)));using var stream=File.OpenRead(file);var buffer=new byte[65536];int n;while((n=stream.Read(buffer,0,buffer.Length))>0)hash.AppendData(buffer,0,n);}
  var pathHash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path.ToUpperInvariant())));
  return Path.Combine(root,pathHash+"-"+Convert.ToHexString(hash.GetHashAndReset())+".json");
 }
 static void Validate(TrainingBook book){
  if(book.Schema!=1 || book.Records==null || book.Records.Count>10000)throw new InvalidDataException("不支持或损坏的队友修炼记录。");
  foreach(var p in book.Records)if(p.Value==null || p.Key<=0 || p.Key!=p.Value.CharacterId || p.Value.TemplateId<=0 || p.Value.BookId==0)throw new InvalidDataException("队友修炼记录字段无效。");
 }
 static TrainingBook Read(string file){
  try{var b=JsonSerializer.Deserialize<TrainingBook>(File.ReadAllText(file,Encoding.UTF8))??throw new InvalidDataException("队友修炼记录为空。");Validate(b);return b;}
  catch(JsonException e){throw new InvalidDataException("队友修炼记录损坏，已保留原文件。",e);}
 }
 public void Save(string path,TrainingBook book){
  Validate(book);var file=SnapshotPath(path);Directory.CreateDirectory(root);
  if(File.Exists(file)){Read(file);File.Copy(file,file+".bak",true);}
  var temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{File.WriteAllText(temp,JsonSerializer.Serialize(book,Json),new UTF8Encoding(false));File.Move(temp,file,true);}
  finally{if(File.Exists(temp))File.Delete(temp);}
 }
 public TrainingBook Load(string path){var file=SnapshotPath(path);return File.Exists(file)?Read(file):new();}
}
