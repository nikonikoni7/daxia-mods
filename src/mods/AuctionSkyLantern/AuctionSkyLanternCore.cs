namespace DaxiaMods;
public sealed class AuctionSkyLanternState {
 long manager,item;int round;bool consumed;
 public bool Matches(long managerId,long itemId,int roundId)=>manager!=0&&manager==managerId&&item==itemId&&round==roundId;
 public bool Start(long managerId,long itemId,int roundId){
  if(manager!=0||managerId==0||itemId==0||roundId<=0)return false;
  manager=managerId;item=itemId;round=roundId;consumed=false;return true;
 }
 public bool Consume(long managerId,long itemId,int roundId){
  if(!Matches(managerId,itemId,roundId)||consumed)return false;
  consumed=true;return true;
 }
 public void Reset(){manager=0;item=0;round=0;consumed=false;}
}
