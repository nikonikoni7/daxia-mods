using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DaxiaMods;
static class BountyRefreshTests {
 sealed class Task {public int Id{get;set;} public int State{get;set;}}
 sealed class Board {public int Day{get;set;} public List<Task> Tasks{get;set;}=new();}
 static int checks;
 static void Check(bool ok,string label){if(!ok)throw new Exception("FAIL: "+label);checks++;Console.WriteLine("PASS: "+label);}
 static List<Task> Open()=>new(){new(){Id=1},new(){Id=2},new(){Id=3}};
 static bool Valid(List<Task> tasks)=>BountyRefreshPolicy.AllOpen(tasks.Count,i=>tasks[i].Id,i=>tasks[i].State);
 static void Main(){
  Check(Valid(Open()),"three native open tasks may refresh");
  foreach(int state in new[]{1,2,3,4}){var list=Open();list[1].State=state;Check(!Valid(list),"accept/done/cancel/close remains locked: "+state);}
  foreach(int size in new[]{0,1,2,4}){var list=Open().Take(size).ToList();if(size==4)list.Add(new(){Id=4});Check(!Valid(list),"incomplete or malformed list locked: "+size);}
  var invalid=Open();invalid[2].Id=0;Check(!Valid(invalid),"zero task slot rejected");invalid[2].Id=1;Check(!Valid(invalid),"duplicate task IDs rejected");
  Check(BountyRefreshPolicy.CanRefresh(true,true,false,false,false,false),"visible idle board allowed");
  Check(!BountyRefreshPolicy.CanRefresh(false,true,false,false,false,false),"disabled blocked");
  Check(!BountyRefreshPolicy.CanRefresh(true,false,false,false,false,false),"hidden panel blocked");
  Check(!BountyRefreshPolicy.CanRefresh(true,true,true,false,false,false),"save/load blocked");
  Check(!BountyRefreshPolicy.CanRefresh(true,true,false,true,false,false),"map loading blocked");
  Check(!BountyRefreshPolicy.CanRefresh(true,true,false,false,true,false),"story/teleport/battle blocked");
  Check(!BountyRefreshPolicy.CanRefresh(true,true,false,false,false,true),"busy blocked");
  var board=new Board{Day=17,Tasks=Open()};var before=board.Tasks;var op=new BountyRefreshOperation();int generation=0,draws=0;
  Func<bool> available=()=>Valid(board.Tasks);
  bool Run(Action? generate=null,Action? redraw=null)=>op.TryRefresh(available,()=>board.Tasks,v=>board.Tasks=v,()=>new List<Task>(),generate??(()=>{generation++;board.Tasks=Open();}),Valid,redraw??(()=>draws++));
  Check(Run()&&generation==1&&draws==1&&!ReferenceEquals(before,board.Tasks),"whole list replaced in one generation and redraw");
  Check(board.Day==17,"manual refresh leaves native update day untouched");
  Check(Run()&&generation==2,"free repeated refresh before acceptance");
  Check(Run(()=>{Check(!Run(),"reentrant refresh blocked");board.Tasks=Open();}),"outer transaction survives reentry");
  before=board.Tasks;
  try{Run(()=>{board.Tasks.Add(new(){Id=99});throw new InvalidOperationException();});throw new Exception("failure not thrown");}catch(InvalidOperationException){}
  Check(ReferenceEquals(before,board.Tasks)&&Valid(before)&&!op.Busy,"generation exception restores original list and busy flag");
  try{Run(()=>board.Tasks.Add(new(){Id=9}));throw new Exception("short result accepted");}catch(InvalidOperationException){}
  Check(ReferenceEquals(before,board.Tasks),"short generated result rolled back");
  try{Run(()=>board.Tasks=Open(),()=>throw new InvalidOperationException());throw new Exception("redraw failure not thrown");}catch(InvalidOperationException){}
  Check(ReferenceEquals(before,board.Tasks)&&!op.Busy,"redraw failure restores original list even if recovery redraw fails");
  var other=new Board{Tasks=Open()};board.Tasks[0].State=1;
  Check(!Run()&&Valid(other.Tasks),"accept locks only current board");
  var restored=JsonSerializer.Deserialize<Board>(JsonSerializer.Serialize(board))!;Check(!Valid(restored.Tasks),"save/load retains accepted lock without sidecar");
  board.Tasks[0].State=2;Check(!Run(),"completion awaiting delivery stays locked");
  board.Tasks[0].State=4;Check(!Run(),"delivered or auto-completed task stays locked");
  board.Tasks.RemoveAt(0);Check(!Run(),"story auto-completion removing task stays locked");
  board.Day++;board.Tasks=Open();Check(Run(),"native next-day full open batch unlocks");
  board.Tasks[0].State=1;board.Day++;Check(!Run(),"unfinished accepted task persists across days");
  board.Tasks=other.Tasks;Check(Run(),"switching to another save with full open board is independent");
  board=new Board{Tasks=Open()};Check(Run(),"new game does not inherit old lock");
  before=board.Tasks;try{op.TryRefresh(()=>true,()=>board.Tasks,v=>board.Tasks=v,()=>throw new InvalidOperationException(),()=>{},Valid,()=>{});}catch(InvalidOperationException){}
  Check(ReferenceEquals(before,board.Tasks)&&!op.Busy,"allocation failure leaves original tasks and gate intact");
  Console.WriteLine("BOUNTYREFRESH_TESTS_PASS: "+checks);
 }
}
