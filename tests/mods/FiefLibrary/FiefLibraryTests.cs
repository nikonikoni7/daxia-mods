using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaxiaMods;
static class FiefLibraryTests {
 static int checks;
 static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);checks++;Console.WriteLine("PASS: "+name);}
 static void Main(){
  Check(FiefLibraryPolicy.Target(507,77,"guizi31_4_26","guizi31_C_0"),"only the screenshot shelf accepts interaction");
  foreach(var x in new[]{(507,78,"guizi32_7_26","guizi32_C_0"),(507,77,"guizi31_4_26","wrong"),(506,77,"guizi31_4_26","guizi31_C_0"),(507,77,"wrong","guizi31_C_0")})Check(!FiefLibraryPolicy.Target(x.Item1,x.Item2,x.Item3,x.Item4),"nearby or mismatched furniture rejected");
  Check(FiefLibraryPolicy.Quantity("1",5)==1&&FiefLibraryPolicy.Quantity("5",5)==5,"one and full-stack quantities accepted");
  foreach(var q in new[]{"0","-1","6","2147483648","1.5","abc"})Check(FiefLibraryPolicy.Quantity(q,5)==0,"invalid quantity rejected: "+q);
  int source=5,dest=0;string saved="old";var op=new FiefLibraryTransaction();
  Action restore=()=>{source=5;dest=0;saved="old";};
  Check(op.Execute(()=>true,()=>{source-=2;dest+=2;},()=>saved="new",restore,()=>{if(source!=3||dest!=2)throw new Exception();}),"successful transfer commits both sides");
  Check(source==3&&dest==2&&saved=="new","transfer and persisted working data agree");restore();
  try{op.Execute(()=>true,()=>{source-=2;throw new IOException("native add failed");},()=>saved="new",restore,()=>{});throw new Exception("not thrown");}catch(IOException){}
  Check(source==5&&dest==0&&saved=="old"&&!op.Busy,"native failure rolls back before permitting another transfer");
  try{op.Execute(()=>true,()=>{source--;dest++;},()=>{saved="bad";throw new IOException("encode failed");},restore,()=>{});throw new Exception("not thrown");}catch(IOException){}
  Check(source==5&&dest==0&&saved=="old","serialization failure rolls back both sides and payload");
  Check(!op.Execute(()=>false,()=>source--,()=>{},restore,()=>{})&&source==5,"stale context cannot mutate inventory");
  bool inner=true;op.Execute(()=>true,()=>{inner=op.Execute(()=>true,()=>source--,()=>{},restore,()=>{});},()=>{},restore,()=>{});Check(!inner&&source==5,"reentrant callback cannot duplicate a transaction");
  try{op.Execute(()=>true,()=>throw new IOException(),()=>{},()=>throw new InvalidOperationException(),()=>{});}catch(AggregateException){}
  Check(op.Blocked&&!op.Execute(()=>true,()=>source--,()=>{},restore,()=>{}),"failed rollback permanently blocks further transfers");
  string raw=LibraryEnvelope.Encode("AQID",7);var envelope=LibraryEnvelope.Decode(raw);Check(envelope.Revision==7&&envelope.Pack=="AQID","versioned payload retains complete serialized bytes");
  bool bad=false;try{LibraryEnvelope.Decode(raw.Replace("AQID","AQIE"));}catch(InvalidDataException){bad=true;}Check(bad,"corrupt warehouse never becomes empty data");
  var journal=new LibrarySaveJournal{BeforeInventoryHash="OLD",BeforePayload="before",AfterInventoryDigest="NEW",AfterPayload="after"};
  Check(journal.Resolve("OLD","x")=="before"&&journal.Resolve("x","NEW")=="after","partial save recovery selects warehouse paired with exact inventory evidence");
  bad=false;try{journal.Resolve("x","unknown");}catch(InvalidDataException){bad=true;}Check(bad,"unrecognized checkpoint is rejected");
  bad=false;try{journal.Resolve("OLD","NEW");}catch(InvalidDataException){bad=true;}Check(bad,"conflicting recovery evidence is rejected");
  int published=0;var events=new LibraryDeferredEvents();events.Enqueue(()=>published++);Check(published==0,"inventory events wait until both sides commit");events.Commit();Check(published==1,"successful transfer publishes original event");events.Enqueue(()=>published++);Check(published==2,"late native event runs after committed transaction");
  events=new();events.Enqueue(()=>published++);events.Discard();events.Enqueue(()=>published++);Check(published==2,"failed transfer discards queued and late events");
  string checkpoint="{\"Data\":{\"value\":{\"_ES3Ref\":\"123\",\"numeric\":{-1:{\"miscStringDict\":{\"warehouse\":\"wrong\"}}},\"miscStringDict\":{\"warehouse\":\"live \\\"name\\\" } [ :\"},\"playerTeamInventory\":{\"contents\":[],\"currencies\":{100:2}}}}}";
  Check(LibraryCheckpoint.Payload(checkpoint,"warehouse")=="live \"name\" } [ :","checkpoint reads exact top-level string without resolving Unity references or nested lookalikes");
  Check(LibraryCheckpoint.Payload(checkpoint,"missing")=="","old checkpoint without warehouse has empty payload");
  Check(LibraryCheckpoint.Member(checkpoint,"Data","value","playerTeamInventory")=="{\"contents\":[],\"currencies\":{100:2}}","native numeric dictionary keys survive raw inventory extraction");
  foreach(string invalid in new[]{"{", "{\"Data\":[]}","{\"Data\":{},\"Data\":{}}","{\"Data\":{]}","{\"Data\":{\"value\":{\"miscStringDict\":{\"warehouse\":null}}}}"}){
   bad=false;try{LibraryCheckpoint.Payload(invalid,"warehouse");}catch(Exception e)when(e is InvalidDataException||e is System.Text.Json.JsonException){bad=true;}Check(bad,"invalid or ambiguous checkpoint is rejected without a default success");
  }
  string slot=Path.Combine(Path.GetTempPath(),"FiefLibraryLocation-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(slot);
  try{
   var pin=new LibraryLocationRecovery{Slot=slot,SourceMap=1002,TargetMap=10170};
   File.WriteAllText(Path.Combine(slot,"one.save"),"original");
   pin.Files["one.save"]=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(slot,"one.save"))));
   Check(pin.Matches(slot),"exact slot and all original save bytes admit location recovery");
   Check(!pin.Matches(slot+"Other"),"same slot name under a different path cannot recover");
   File.WriteAllText(Path.Combine(slot,"extra.save"),"extra");Check(!pin.Matches(slot),"new save file invalidates the complete fingerprint");File.Delete(Path.Combine(slot,"extra.save"));
   File.WriteAllText(Path.Combine(slot,"one.save"),"changed");Check(!pin.Matches(slot),"overwriting the slot invalidates old recovery authorization");
   pin.Files.Clear();Check(!pin.Matches(slot),"empty recovery evidence is rejected");
  }finally{foreach(var f in Directory.GetFiles(slot))File.Delete(f);Directory.Delete(slot);}
  Console.WriteLine("FIEFLIBRARY_TESTS_PASS: "+checks);
 }
}
