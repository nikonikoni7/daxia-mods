using System;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Reflection;
using DaxiaMods;
using WuLin;
using GameData;
static class PerfectRollsGameTests {
 static int checks;
 static void Check(bool ok,string label){if(!ok)throw new Exception("FAIL: "+label);checks++;}
 static Il2CppSystem.Decimal N(decimal d){var b=decimal.GetBits(d);return new(){lo=b[0],mid=b[1],hi=b[2],flags=b[3]};}
 static decimal D(Il2CppSystem.Decimal d)=>new(d.lo,d.mid,d.hi,d.flags<0,(byte)((d.flags>>16)&255));
 static KeyWithValues KV(string key,params decimal[] v){var x=new KeyWithValues{key=key};foreach(var d in v){x.dValues.Add(N(d));x.values.Add((int)d);}return x;}
 static GameItemInstance Item(int level=-1){
  var item=new GameItemInstance{TempleteId=12066};item.Templete.Uid=12066;item.modifyData!.templeteUid=12066;item.modifyData.forgeLevel=level;
  item.modifyData.forgeEffect!.Add(1207,KV("格挡",0.13m));item.modifyData.forgeEffect.Add(1120661,KV("真武1多效果1",0.04m));item.modifyData.forgeEffect.Add(1120662,KV("真武2多效果1",12));
  return item;
 }
 static object? Capture(GameItemPack bag)=>typeof(PerfectRolls).GetMethod("Capture",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(plugin,new object[]{bag});
 static PerfectRolls plugin=null!;
 static void Complete(GameItemPack bag,object? state,bool success=true){var type=typeof(PerfectRolls);type.GetMethod("Acquired",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(plugin,new object?[]{bag,success,state});type.GetMethod("Release",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(plugin,new[]{state});}
 static decimal Value(GameItemInstance item,int id)=>D(item.modifyData!.forgeEffect![id].dValues[0]);
 static object? Call(string name,params object?[] args)=>typeof(PerfectRolls).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(plugin,args);
 static object? LogSource(params Il2CppSystem.Object[] values)=>Call("CaptureLog",new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Object>(values));
 static void Notify(GameItemInstance item,int amount=1,GameLogType type=GameLogType.Left,GameCharacterInstance? actor=null)=>Call("Notified",item.Templete,amount,type,actor);
 static void Main(){
  var effects=BaseDataClass.GetGameData<ItemForgeEffectDataScriptObject>().Data;
  effects[1207]=new(){Uid=1207,UName="格挡增加7",BonusExt=KV("格挡",0.1m,0.2m)};
  effects[1120661]=new(){Uid=1120661,UName="真武1多效果1",BonusExt=KV("真武1多效果1",0.03m,0.08m)};
  effects[1120662]=new(){Uid=1120662,UName="真武2多效果1",BonusExt=KV("真武2多效果1",10,20)};
  BaseDataClass.GetGameData<ItemForgeDataScriptObject>().Data[12066]=new(){BonusExt=new[]{KV("格挡增加7"),KV("真武1多效果1"),KV("真武2多效果1")}};
  GameConfig.Instance.Values["能力_制造_L-1_ModMin"]=N(0);GameConfig.Instance.Values["能力_制造_L-1_ModMax"]=N(1);
  GameConfig.Instance.Values["能力_制造_L-2_ModMin"]=N(0);GameConfig.Instance.Values["能力_制造_L-2_ModMax"]=N(0.3m);
  GameConfig.Instance.Values["能力_制造_L-3_ModMin"]=N(1);GameConfig.Instance.Values["能力_制造_L-3_ModMax"]=N(1);
  plugin=new();plugin.Load();var bag=PlayerTeamManager.Instance.TeamInventory;
  var old=Item();bag.Contents.Add(old);var state=Capture(bag);var fresh=Item();var original=fresh.modifyData;bag.Contents.Add(fresh);Complete(bag,state);
  Check(Value(fresh,1207)==0.2m&&Value(fresh,1120661)==0.08m&&Value(fresh,1120662)==20,"newly obtained Zhenwu gets all existing legal best values");
  Check(Value(old,1120662)==12,"existing inventory is unchanged");
  Check(D(original!.forgeEffect![1120662].dValues[0])==12&&fresh.modifyData!.Pointer!=original.Pointer,"commit isolates original/shared instance data");
  Check(fresh.modifyData!.forgeLevel==-1&&fresh.modifyData.forgeEffect!.Count==3&&fresh.modifyData.forgeEffect[1120662].values[0]==20,"level effect count and integer cache preserved");
  state=Capture(bag);Complete(bag,state);Check(Value(old,1120662)==12,"duplicate admission cannot optimize old inventory");
  state=Capture(bag);var failed=Item();Complete(bag,state,false);Check(Value(failed,1120662)==12,"failed purchase or full bag changes nothing");
  var npc=new GameItemPack();state=Capture(npc);var npcItem=Item();npc.Contents.Add(npcItem);Complete(npc,state);Check(Value(npcItem,1120662)==12,"NPC inventory remains original");
  SaveManager.Instance.IsSavingOrLoading=true;state=Capture(bag);var loading=Item();bag.Contents.Add(loading);Complete(bag,state);SaveManager.Instance.IsSavingOrLoading=false;Check(Value(loading,1120662)==12,"save loading never upgrades existing equipment");
  state=Capture(bag);var crafted=Item(3);bag.Contents.Add(crafted);Complete(bag,state);Check(Value(crafted,1120662)==20&&crafted.modifyData!.forgeLevel==3,"crafted acquisition reaches full endpoint without changing craft metadata");
  state=Capture(bag);var limited=Item(-2);bag.Contents.Add(limited);Complete(bag,state);Check(Value(limited,1120662)==20&&limited.modifyData!.forgeLevel==-2,"robbed auction equipment ignores the native 30 percent coefficient");
  var stored=Item(2);state=Capture(bag);bag.Contents.Add(stored);Complete(bag,state);stored.modifyData!.forgeEffect![1120662].dValues[0]=N(11);bag.Contents.Remove(stored);
  state=Capture(bag);bag.Contents.Add(stored);Complete(bag,state);Check(Value(stored,1120662)==20&&bag.Contents.Count(x=>x.Pointer==stored.Pointer)==1,"Treasure Pavilion withdrawal upgrades the same old crafted instance without duplication");
  foreach(int level in new[]{-3,-2,-1,1,2,3,4,5}){state=Capture(bag);var tier=Item(level);bag.Contents.Add(tier);Complete(bag,state);Check(Value(tier,1120662)==20&&tier.modifyData!.forgeLevel==level,"full endpoint for source/craft mode "+level);}
  state=Capture(bag);var guaranteed=Item(-3);bag.Contents.Add(guaranteed);Complete(bag,state);Check(Value(guaranteed,1120662)==20,"noncrafted guaranteed source keeps its native fixed coefficient");
  var equipped=Item();PlayerTeamManager.Instance.PlayerDataInstance.EquipSlot.Add(4096,equipped);state=Capture(bag);bag.Contents.Add(equipped);PlayerTeamManager.Instance.PlayerDataInstance.EquipSlot.Clear();Complete(bag,state);Check(Value(equipped,1120662)==12,"unequipping old equipment does not count as acquisition");
  bag.Contents.Remove(old);state=Capture(bag);bag.Contents.Add(old);Complete(bag,state);Check(Value(old,1120662)==20,"sold or transferred equipment can be optimized on reacquisition");
  state=Capture(bag);var nested=Capture(bag);var reward=Item();bag.Contents.Add(reward);Complete(bag,nested);Check(Value(reward,1120662)==12,"nested overload waits for outer success");Complete(bag,state);Check(Value(reward,1120662)==20,"outer acquisition processes nested results once");
  state=Capture(bag);var swapped=Item();bag.Contents.Add(swapped);PlayerTeamManager.Instance.TeamInventory=new();Complete(bag,state);Check(Value(swapped,1120662)==12,"manager or inventory replacement invalidates transaction");PlayerTeamManager.Instance.TeamInventory=bag;
  var enabled=(BepInEx.Configuration.ConfigEntry<bool>)typeof(PerfectRolls).GetField("enabled",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(plugin)!;enabled.Value=false;state=Capture(bag);var disabled=Item();bag.Contents.Add(disabled);Complete(bag,state);Check(Value(disabled,1120662)==12,"configuration switch disables processing");enabled.Value=true;
  state=Capture(bag);var unknown=Item();unknown.modifyData!.forgeEffect![999999]=KV("新效果",3);bag.Contents.Add(unknown);Complete(bag,state);Check(Value(unknown,999999)==3&&Value(unknown,1120662)==20,"unknown effects preserved without blocking known effects");
  effects[1120662].BonusExt!.dValues[1]=N(200);state=Capture(bag);var changed=Item();bag.Contents.Add(changed);Complete(bag,state);Check(Value(changed,1120662)==12,"modified third-party template does not use stale semantic approval");effects[1120662].BonusExt!.dValues[1]=N(20);
  state=Capture(bag);var malformed=Item();malformed.modifyData!.forgeEffect![1120662].dValues.Clear();bag.Contents.Add(malformed);Complete(bag,state);Check(malformed.modifyData.forgeEffect[1120662].dValues.Count==0,"malformed native value preserved");
  Check(PerfectRollCatalog.Direction(1120081,"百衲衣1多效果1",0.25m,0.75m)==RollDirection.Lower,"gift reduction uses least harmful magnitude");
  effects[1120081]=new(){Uid=1120081,UName="百衲衣1多效果1",BonusExt=KV("百衲衣1多效果1",0.25m,0.75m)};
  var recipe=BaseDataClass.GetGameData<ItemForgeDataScriptObject>().Data[12066];recipe.BonusExt=recipe.BonusExt.Concat(new[]{KV("百衲衣1多效果1")}).ToArray();
  state=Capture(bag);var penalty=Item();penalty.modifyData!.forgeEffect![1120081]=KV("百衲衣1多效果1",0.6m);bag.Contents.Add(penalty);Complete(bag,state);Check(Value(penalty,1120081)==0.25m,"native negative gift modifier minimizes penalty");
  var fixedBonus=KV("固定攻击",80);var requirement=KV("臂力",10);var untouched=Item();untouched.modifyData!.overrideBonus=new(){fixedBonus};untouched.modifyData.overrideEquipReq=new(){requirement};untouched.modifyData.rawRnd=new(){0.3f};
  state=Capture(bag);bag.Contents.Add(untouched);Complete(bag,state);Check(untouched.modifyData!.overrideBonus![0]==fixedBonus&&untouched.modifyData.overrideEquipReq![0]==requirement&&untouched.modifyData.rawRnd![0]==0.3f,"fixed bonus requirements and native random bookkeeping preserved");
  // An original method throwing still runs the Harmony finalizer. It must not
  // leave later independent admissions stuck in the nested-transaction branch.
  state=Capture(bag);typeof(PerfectRolls).GetMethod("Release",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(plugin,new[]{state});
  state=Capture(bag);var afterError=Item();bag.Contents.Add(afterError);Complete(bag,state);Check(Value(afterError,1120662)==20,"original exception cleanup permits subsequent acquisitions");
  var unrelated=Item();bag.Contents.Add(unrelated);var logged=Item(1);bag.Contents.Add(logged);var source=LogSource(logged);Notify(logged);Call("ReleaseSource",source);
  Check(Value(logged,1120662)==20&&Value(unrelated,1120662)==12,"native instance notification upgrades only the reported equipment");
  var packNotice=new GameItemPack();var late=Item();packNotice.Contents.Add(late);source=LogSource(packNotice);Notify(late);Call("ReleaseSource",source);Check(Value(late,1120662)==12,"notification before admission does not mutate external source inventory");
  state=Capture(bag);bag.Contents.Add(late);Complete(bag,state);Check(Value(late,1120662)==20,"notification before admission resolves only after successful ownership");
  var denied=Item();source=LogSource(denied);Notify(denied);Call("ReleaseSource",source);state=Capture(bag);Complete(bag,state,false);Check(Value(denied,1120662)==12,"failed notified acquisition preserves the external item");
  var failedAfterNotice=Item();state=Capture(bag);bag.Contents.Add(failedAfterNotice);Notify(failedAfterNotice);Check(Value(failedAfterNotice,1120662)==12,"in-flight notice waits for original return value");Complete(bag,state,false);Call("FlushNotices");Check(Value(failedAfterNotice,1120662)==12,"failed return cancels queued notice even if native state partially changed");
  var negative=Item();bag.Contents.Add(negative);source=LogSource(negative);Notify(negative,-1);Notify(negative,0);Notify(negative,1,GameLogType.OnlyChatLog);Notify(negative,1,GameLogType.Left,new GameCharacterInstance());Call("ReleaseSource",source);Check(Value(negative,1120662)==12,"loss zero history-only and NPC notices cannot optimize old equipment");
  var worn=Item();var wearer=PlayerTeamManager.Instance.PlayerDataInstance;worn.cachedEquipingCharacter=wearer;wearer.EquipSlot[4096]=worn;source=LogSource(worn);Notify(worn);Notify(worn);Call("ReleaseSource",source);
  Check(Value(worn,1120662)==20&&wearer.Refreshes==1,"newly reported auto-equipped item refreshes native equipment properties once");wearer.EquipSlot.Clear();
  var formerOwner=new GameCharacterInstance();var transferred=Item();transferred.cachedEquipingCharacter=formerOwner;bag.Contents.Add(transferred);source=LogSource(transferred);Notify(transferred);Call("ReleaseSource",source);Check(Value(transferred,1120662)==20&&formerOwner.Refreshes==0,"stale former NPC owner is not recomputed after a reported transfer");
  var noContext=Item();bag.Contents.Add(noContext);Notify(noContext);Check(Value(noContext,1120662)==12,"template-only notification outside an acquisition never scans same-template old equipment");
  var inFlight=Item();state=Capture(bag);bag.Contents.Add(inFlight);Notify(inFlight);Complete(bag,state);Check(Value(inFlight,1120662)==20&&Value(unrelated,1120662)==12,"template-only notice binds current admission delta");
  var list=new Il2CppSystem.Collections.Generic.List<GameItemInstance>();var listed=Item();list.Add(listed);bag.Contents.Add(listed);source=LogSource(list);Notify(listed);Call("ReleaseSource",source);Check(Value(listed,1120662)==20,"native item-list notification preserves instance identity");
  var preview=Item();source=LogSource(preview);Call("ReleaseSource",source);bag.Contents.Add(preview);Check(Value(preview,1120662)==12,"an unreported preview does not authorize upgrades");
  var replay=Item();bag.Contents.Add(replay);source=LogSource(replay);SaveManager.Instance.IsSavingOrLoading=true;Notify(replay);SaveManager.Instance.IsSavingOrLoading=false;Call("ReleaseSource",source);Check(Value(replay,1120662)==12,"load-time notification ignored");
  var resetPending=Item();source=LogSource(resetPending);Notify(resetPending);Call("ReleaseSource",source);Call("ClearNotices");bag.Contents.Add(resetPending);Call("FlushNotices");Check(Value(resetPending,1120662)==12,"load transition clears pending notification references");
  using(var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"..","..","tests","fixtures","PerfectRolls-Build24794582.json")))){
   int audited=0;
   foreach(var row in fixture.RootElement.EnumerateArray()){
    int id=row.GetProperty("id").GetInt32();string key=row.GetProperty("key").GetString()!;
    decimal first=decimal.Parse(row.GetProperty("first").GetString()!,System.Globalization.CultureInfo.InvariantCulture),second=decimal.Parse(row.GetProperty("second").GetString()!,System.Globalization.CultureInfo.InvariantCulture);
    var direction=row.GetProperty("direction").GetString()=="Lower"?RollDirection.Lower:RollDirection.Higher;
    Check(PerfectRollCatalog.Direction(id,key,first,second)==direction,"audited effect recognized: "+id);
    Check(PerfectRollPolicy.TryBest(key,first,second,0,1,direction,out var best)&&best==(direction==RollDirection.Lower?first:second),"audited effect legal extremum: "+id);audited++;
   }
   Console.WriteLine("Audited Build 24794582 variable effects: "+audited);
  }
  var targets=(System.Collections.Generic.IEnumerable<MethodBase>)typeof(PerfectRolls).GetNestedType("Admissions",BindingFlags.NonPublic)!.GetMethod("TargetMethods",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null)!;
  Check(targets.Count()==5,"all four AddItem overloads and independent admission registered");
  var outer=Capture(bag);var repeated=Capture(bag);
  Check(repeated==null,"nested AddItem overloads reuse outer acquisition without another snapshot context");Complete(bag,repeated);Complete(bag,outer);
  var consumable=Item();consumable.IsStackable=true;Call("Optimize",consumable,false);
  Check(consumable.ModifyReads==0,"stackable rewards never initialize native equipment modification data");
  var emptyLog=LogSource(new Il2CppSystem.Object());
  Check(emptyLog==null,"ordinary text logs retain no equipment notification context");
  Call("ReleaseSource",emptyLog);
  Call("ClearNotices");
  for(int i=0;i<64;i++){var external=Item();var noticeSource=LogSource(external);Notify(external);Call("ReleaseSource",noticeSource);}
  bag.Contents.Scans=0;Call("FlushNotices");
  Check(bag.Contents.Scans<=1,"64 pending reward notifications share one inventory ownership scan");
  Call("ClearNotices");
  plugin.Unload();Console.WriteLine("PerfectRolls native-boundary tests passed: "+checks);
 }
}
