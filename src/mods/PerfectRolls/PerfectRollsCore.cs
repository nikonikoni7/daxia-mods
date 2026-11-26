using System;
namespace DaxiaMods;
internal enum RollDirection { Unknown, Higher, Lower }
internal static class PerfectRollPolicy {
 internal static bool TryBest(string key,decimal first,decimal second,float factorMin,float factorMax,RollDirection direction,out decimal best){
  best=0;
  if(string.IsNullOrEmpty(key)||direction==RollDirection.Unknown||first==second||!float.IsFinite(factorMin)||!float.IsFinite(factorMax)||factorMin>factorMax)return false;
  try{
   // Build 24794582 converts the single-precision Range result to Decimal before interpolation.
   decimal a=first+(second-first)*(decimal)factorMin,b=first+(second-first)*(decimal)factorMax;
   best=direction==RollDirection.Higher?Math.Max(a,b):Math.Min(a,b);
   bool integer=decimal.Truncate(second)==second&&!key.EndsWith("%",StringComparison.Ordinal)&&!key.EndsWith("@",StringComparison.Ordinal)&&(Math.Abs(first)>1||Math.Abs(second)>1);
   best=Math.Round(best,integer?0:2,MidpointRounding.ToEven);
   return best>=int.MinValue&&best<=int.MaxValue;
  }catch(OverflowException){return false;}
 }
}
