using System;
namespace DaxiaMods;
internal static class CameraMotion
{
 // 对指数速度响应做精确积分，避免逐帧Lerp在不同帧率下产生不同位移。
 public static (float Velocity,float Distance) SmoothAxis(float velocity,float target,float delta,float response) {
  if(!float.IsFinite(delta) || delta<=0)return (velocity,0);
  delta=Math.Min(delta,0.05f);
  response=float.IsFinite(response)?Math.Clamp(response,0,0.2f):0.04f;
  if(response==0)return (target,target*delta);
  var blend=1-MathF.Exp(-delta/response);
  var next=velocity+(target-velocity)*blend;
  var distance=target*delta+(velocity-target)*response*blend;
  if(target==0 && Math.Abs(next)<0.001f)next=0;
  return (next,distance);
 }
}
