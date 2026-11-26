using System;
namespace DaxiaMods;
internal static class Rules
{
 public static (float X,float Y) Edge(float x,float y,int w,int h,int edge,bool focus,bool ui) {
  if(!focus || ui || w<=0 || h<=0 || x<0 || y<0 || x>=w || y>=h) return (0,0);
  edge=Math.Clamp(edge,1,Math.Max(1,Math.Min(w,h)/4));
  float dx=x<edge?-1:x>=w-edge?1:0;
  float dy=y<edge?-1:y>=h-edge?1:0;
  if(dx!=0 && dy!=0){dx*=0.70710678f;dy*=0.70710678f;}
  return (dx,dy);
 }
 public static bool SceneAllowed(bool roam,bool world,bool loading,bool story,bool modal)
  =>roam && !world && !loading && !story && !modal;
 public static float Duration(float value)=>float.IsFinite(value)?Math.Clamp(value,0.05f,2f):0.2f;
 public static float CompressTimer(float elapsed,float duration,int length)
  =>Math.Clamp(elapsed/Duration(duration),0,1)*Math.Max(0,length);
 public static bool ShouldCompleteBanner(bool enabled,bool win,bool waiting,bool finished,float elapsed,float duration)
  =>enabled && win && waiting && !finished && elapsed>=Duration(duration);
}
internal sealed class PauseLease
{
 private Action? release;
 public bool Active=>release!=null;
 public void Enter(Action acquire,Action cleanup) {
  if(Active)return;
  release=cleanup;
  try{acquire();}catch{Exit();throw;}
 }
 public void Exit() {
  var action=release;release=null;action?.Invoke();
 }
}
