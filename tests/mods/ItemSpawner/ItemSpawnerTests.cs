using System;
using System.Reflection;
static class ItemSpawnerTests {
 static int checks;
 static Type policy=null!;
 static object? Call(string name,params object[] args)=>policy.GetMethod(name,BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(null,args);
 static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static void Main(){
  policy=Assembly.GetExecutingAssembly().GetType("DaxiaMods.ItemSpawnerPolicy")!;
  Check(policy!=null,"Missing item-only policy implementation");
  foreach(var invalid in new[]{"","0","-1","1000","1.5","abc","2147483648"})
   Check((int)Call("Count",invalid,true)! ==0,"Reject invalid quantity: "+invalid);
  Check((int)Call("Count","1",true)! ==1,"Allow one");
  Check((int)Call("Count","999",true)! ==999,"Allow bounded stack");
  Check((int)Call("Count"," 12 ",true)! ==12,"Trim quantity");
  Check((int)Call("Count","99",false)! ==1,"Nonstackable items added individually");
  Check((bool)Call("Matches",12345,"金疮药","金疮")!,"Chinese name search");
  Check((bool)Call("Matches",12345,"金疮药","234")!,"ID search");
  Check((bool)Call("Matches",12345,"测试ABC","abc")!,"Case insensitive search");
  Check((bool)Call("Matches",12345,"金疮药","  ")!,"Empty query matches all");
  Check(!(bool)Call("Matches",12345,"金疮药","银针")!,"Nonmatch excluded");
  foreach(var pair in new[]{("Equip_Weapon_Sword","装备"),("Equip_Pet","装备"),("KungfuBook_Inner","秘籍"),("Consumeable_Edible_Medicine","食药"),("Consumeable_Material_Wood","材料"),("Consumeable_Recipe","配方"),("Quest","其他")})
   Check((string)Call("Category",pair.Item1)! ==pair.Item2,"Chinese category "+pair.Item1);
  Check((int)Call("Page",999,0,7)! ==0,"Empty list has page zero");
  Check((int)Call("Page",-1,10,7)! ==0,"Clamp negative page");
  Check((int)Call("Page",99,14,7)! ==1,"Clamp last exact page");
  Check((int)Call("Page",99,15,7)! ==2,"Partial final page");
  Check(!(bool)Call("CanAdd",false,true,false,false)!,"No inventory denies add");
  Check(!(bool)Call("CanAdd",true,false,false,false)!,"Outside exploration denies add");
  Check(!(bool)Call("CanAdd",true,true,true,false)!,"Saving/loading denies add");
  Check(!(bool)Call("CanAdd",true,true,false,true)!,"Story/battle transition denies add");
  Check((bool)Call("CanAdd",true,true,false,false)!,"Idle exploration permits add");
  var cartType=Assembly.GetExecutingAssembly().GetType("DaxiaMods.ItemSpawnerCart");
  Check(cartType!=null,"Missing confirmation batch cart");
  var cart=Activator.CreateInstance(cartType!,true)!;
  object? Cart(string method,params object[] args)=>cartType!.GetMethod(method,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(cart,args);
  System.Collections.Generic.Dictionary<int,int> Snapshot()=>(System.Collections.Generic.Dictionary<int,int>)Cart("Snapshot")!;
  Check(!(bool)Cart("Stage",0,"1",true)!,"Reject invalid item ID");
  Check(!(bool)Cart("Stage",1,"0",true)!,"Reject invalid batch quantity");
  Check((bool)Cart("Stage",1,"7",true)!,"Stage stackable item");
  Check((bool)Cart("Stage",1,"3",true)!&&Snapshot()[1]==10,"Merge duplicate stacks");
  Check(!(bool)Cart("Stage",1,"999",true)!&&Snapshot()[1]==10,"Reject overflow without mutation");
  Check((bool)Cart("Stage",2,"9",false)!&&Snapshot()[2]==1,"Nonstackable selection is one");
  Check((bool)Cart("Stage",2,"9",false)!&&Snapshot()[2]==1,"Duplicate nonstackable remains one");
  var frozen=Snapshot();Cart("Remove",1);
  Check(frozen[1]==10&&!Snapshot().ContainsKey(1),"Review snapshot independent from cart changes");
  Cart("Clear");Check(Snapshot().Count==0,"Clear staged items");
  for(int i=1;i<=50;i++)Check((bool)Cart("Stage",i,"1",true)!,"Stage within batch limit "+i);
  Check(!(bool)Cart("Stage",51,"1",true)!&&Snapshot().Count==50,"Reject batch over 50 types");
  Console.WriteLine("ItemSpawner policy checks passed: "+checks);
 }
}
