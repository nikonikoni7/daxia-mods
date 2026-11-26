using System;
using System.Collections.Generic;
namespace DaxiaMods;
public static class PortraitWarmupCapture {
 [ThreadStatic]static List<string>? current;
 public static List<string> Run(Action enumerate){
  var previous=current;var paths=new List<string>();current=paths;
  try{enumerate();return paths;}finally{current=previous;}
 }
 public static bool TryCapture(string path){
  if(current==null)return false;
  if(!string.IsNullOrWhiteSpace(path)&&!current.Contains(path))current.Add(path);
  return true;
 }
}
public sealed class PortraitWarmupRequest {
 public string Path {get;}
 internal long Generation {get;}
 internal PortraitWarmupRequest(string path,long generation){Path=path;Generation=generation;}
}
// Contains only resource keys. Native sprites remain owned by the game's cache.
public sealed class PortraitWarmupQueue {
 readonly int limit;
 readonly Queue<string> pending=new();
 readonly HashSet<string> seen=new(StringComparer.Ordinal);
 long generation;
 PortraitWarmupRequest? active;
 public PortraitWarmupQueue(int limit){if(limit<1)throw new ArgumentOutOfRangeException(nameof(limit));this.limit=limit;}
 public bool Add(string path){
  if(string.IsNullOrWhiteSpace(path)||seen.Count>=limit||!seen.Add(path))return false;
  pending.Enqueue(path);return true;
 }
 public bool TryStart(out PortraitWarmupRequest? request){
  request=null;if(active!=null||pending.Count==0)return false;
  request=active=new(pending.Dequeue(),generation);return true;
 }
 public bool Complete(PortraitWarmupRequest request){
  if(!ReferenceEquals(active,request))return false;
  active=null;return request.Generation==generation;
 }
 public void Reset(){generation++;pending.Clear();seen.Clear();}
}
