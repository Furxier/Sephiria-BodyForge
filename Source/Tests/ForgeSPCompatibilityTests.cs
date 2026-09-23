using System;
using System.Collections.Generic;
using System.Reflection;
namespace UnityEngine {
 public static class Time {public static float unscaledTime;}
 public static class Debug {public static void Log(object o){}public static void LogError(object o){throw new Exception(o.ToString());}}
}
namespace HarmonyLib {
 public class Harmony {public static int Patches;public Harmony(string id){}public void Patch(MethodInfo m,HarmonyMethod prefix=null,HarmonyMethod postfix=null){if(m==null)throw new Exception("missing patch");Patches++;}public void UnpatchSelf(){Patches=0;}}
 public class HarmonyMethod {public HarmonyMethod(Type type,string method){if(AccessTools.Method(type,method)==null)throw new Exception("missing callback");}}
 public static class AccessTools {const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
  public static MethodInfo Method(Type t,string n){return t.GetMethod(n,Flags);}public static FieldInfo Field(Type t,string n){return t.GetField(n,Flags);}public static PropertyInfo Property(Type t,string n){return t.GetProperty(n,Flags);}}
}
public class ModConfigData {public bool AddCategory{get;set;}public bool AddItem{get;set;}public bool ModifyCharm{get;set;}}
public static class ModConfig {public static ModConfigData Instance{get;set;}}
public class ItemCategoryEntity {public bool isEnabled;}
public static class ItemDatabase {public static Dictionary<string,ItemCategoryEntity> Categories=new Dictionary<string,ItemCategoryEntity>();public static ItemCategoryEntity FindItemCategory(string id){ItemCategoryEntity e;Categories.TryGetValue(id,out e);return e;}}
public class StatusEntity {public string className;public int divideForDisplay;}
public static class StatusDatabase {public static Dictionary<string,StatusEntity> Stats=new Dictionary<string,StatusEntity>();public static StatusEntity GetStatusEntity(string id){StatusEntity s;Stats.TryGetValue(id,out s);return s;}}
public class UnitAvatar {static int nextID;readonly int id=++nextID;public string Name="player";public int Reads,Refreshes;public int GetInstanceID(){return id;}public bool isServer;public Dictionary<string,int> customStats=new Dictionary<string,int>();public int GetCustomStatUnsafe(string key){Reads++;int n;return customStats.TryGetValue(key,out n)?n:0;}}
public class PlayerAvatar:UnitAvatar {}
public class DungeonManager {}
public class PlayerSpawner {public static List<PlayerSpawner> MultiplayerList=new List<PlayerSpawner>();public PlayerAvatar PlayerAvatar;}
namespace SPMod {
 public static class StarSephiriaMod {public static bool _isPatched;public static PartyTuple PartyStats=new PartyTuple();public static Dictionary<string,Tool.ModuleComponents.AllPlayerStatus> PartyEffect=new Dictionary<string,Tool.ModuleComponents.AllPlayerStatus>();}
 public struct PartyTuple {public bool Item1;public Tool.ModuleComponents.AllPlayerStatus Item2;}
 public static class StarSephiriaPatches {public static class UnitAvatar_Patches {public static bool UpdateHighestElementalBonus_Prefix(UnitAvatar p){p.Refreshes++;return true;}}}
 public static class DungeonPatchs {public static class DungeonManager_Patch {public static void Update_Postfix(DungeonManager d){}}}
}
namespace SPMod.Tool {
 public static class ModuleComponents {public class AllPlayerStatus {public int Defense,AllDamage,ElementalDamage,Crit;public AllPlayerStatus(){}public AllPlayerStatus(int d,int a,int e,int c){Defense=d;AllDamage=a;ElementalDamage=e;Crit=c;}}}
 public static class Tools {public static ModuleComponents.AllPlayerStatus GetMaxPlayerStatus(){return new ModuleComponents.AllPlayerStatus();}}
}
public static class ForgeSPCompatibilityTests {
 static object Call(string n,params object[] args){return typeof(ForgeSPCompatibility).GetMethod(n,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);}
 static void Check(bool condition,string reason){if(!condition)throw new Exception(reason);}
 static SPMod.Tool.ModuleComponents.AllPlayerStatus Maximum(){UnityEngine.Time.unscaledTime+=1;Check(!(bool)Call("BeforePartyUpdate"),"original full-table loop always skipped");return SPMod.StarSephiriaMod.PartyStats.Item2;}
 public static string Run(){
  ForgeSPCompatibility.Tick();Check(!ForgeSPCompatibility.CategoryAvailable("COMET") && ForgeSPCompatibility.CategoryAvailable("FROST"),"inactive SP doesn't gate vanilla");
  SPMod.StarSephiriaMod._isPatched=true;ModConfig.Instance=new ModConfigData();UnityEngine.Time.unscaledTime=3;ForgeSPCompatibility.Tick();
  Check(HarmonyLib.Harmony.Patches==2,"all compatibility hooks installed once");ForgeSPCompatibility.Tick();Check(HarmonyLib.Harmony.Patches==2,"no repeated patching");
  foreach(string c in new[]{"FORTUNE","COMET","WEAPON","PARTY"})ItemDatabase.Categories[c]=new ItemCategoryEntity();
  Check(!ForgeSPCompatibility.CategoryAvailable("FORTUNE")&&!ForgeSPCompatibility.CategoryAvailable("WEAPON")&&!ForgeSPCompatibility.CategoryAvailable("PARTY"),"disabled config combinations");
  ModConfig.Instance.AddCategory=true;Check(ForgeSPCompatibility.CategoryAvailable("COMET") && !ForgeSPCompatibility.CategoryAvailable("WEAPON"),"categories independently gated");
  ModConfig.Instance.AddItem=true;Check(ForgeSPCompatibility.CategoryAvailable("WEAPON") && !ForgeSPCompatibility.CategoryAvailable("PARTY"),"party requires actual registered status definitions");
  string[] keys={"STAT_ALLPLAYERDEFENSE","STAT_ALLPLAYERALLDAMAGE","STAT_ALLPLAYERELEMENTALDAMAGE","STAT_ALLPLAYERCRIT"};
  for(int i=0;i<4;i++)StatusDatabase.Stats[ForgeSPCompatibility.PartyIDs[i]]=new StatusEntity{className="StatusInstance_Custom/"+keys[i]};
  Check(ForgeSPCompatibility.CategoryAvailable("PARTY"),"SP zero display divisor means raw unit one");
  StatusDatabase.Stats["ALL_PLAYER_CRIT"].divideForDisplay=100;Check(!ForgeSPCompatibility.CategoryAvailable("PARTY"),"incompatible crit units fail closed");StatusDatabase.Stats["ALL_PLAYER_CRIT"].divideForDisplay=1;
  object[] client={new UnitAvatar{isServer=false},true};Check(!(bool)Call("HighestAuthority",client)&&!(bool)client[1],"client skips SP writer and native writer even for owned player");
  object[] host={new UnitAvatar{isServer=true},true};Check((bool)Call("HighestAuthority",host),"host retains normal SP elemental calculation");
  object[] gone={null,true};Check(!(bool)Call("HighestAuthority",gone)&&!(bool)gone[1],"destroyed avatar skipped");
  var a=new PlayerAvatar{isServer=true,Name="A"};var b=new PlayerAvatar{isServer=true,Name="B"};a.customStats[keys[0]]=10;a.customStats[keys[3]]=3;b.customStats[keys[0]]=4;b.customStats[keys[2]]=6;
  var first=new PlayerSpawner{PlayerAvatar=a};var second=new PlayerSpawner{PlayerAvatar=b};PlayerSpawner.MultiplayerList.Add(first);
  var max=Maximum();Check(max.Defense==10&&max.Crit==3,"single player contribution multiplier");
  PlayerSpawner.MultiplayerList.Add(second);max=Maximum();Check(max.Defense==20&&max.Crit==6&&max.ElementalDamage==12,"team per-stat max, not sum; crit remains displayed units");
  b.customStats[keys[0]]=5;Check(Maximum().Defense==20&&b.customStats[keys[0]]==5,"covered contribution retained, no permanent grants");
  int reads=a.Reads+b.Reads,refreshes=a.Refreshes+b.Refreshes;
  for(int i=0;i<1000;i++)Call("BeforePartyUpdate");Check(reads==a.Reads+b.Reads,"same-frame checks perform no extra stat reads");
  Maximum();Check(reads+8==a.Reads+b.Reads && refreshes==a.Refreshes+b.Refreshes,"stable poll reads four stats per player once without refreshing attributes");
  SPMod.StarSephiriaMod.PartyStats.Item1=true;Maximum();Check(!SPMod.StarSephiriaMod.PartyStats.Item1 && refreshes==a.Refreshes+b.Refreshes,"covered contribution dirty flag cannot force duplicate calculation");
  SPMod.StarSephiriaMod.PartyEffect["A"]=new SPMod.Tool.ModuleComponents.AllPlayerStatus();SPMod.StarSephiriaMod.PartyEffect["B"]=new SPMod.Tool.ModuleComponents.AllPlayerStatus();SPMod.StarSephiriaMod.PartyEffect["old"]=new SPMod.Tool.ModuleComponents.AllPlayerStatus();
  PlayerSpawner.MultiplayerList.Remove(first);max=Maximum();Check(max.Defense==5&&max.Crit==0&&max.ElementalDamage==6,"departed player never leaves stale contribution");
  Check(SPMod.StarSephiriaMod.PartyEffect.Count==1 && SPMod.StarSephiriaMod.PartyEffect.ContainsKey("B"),"stale SP records pruned but current contributor retained");
  b.Name="renamed";SPMod.StarSephiriaMod.PartyEffect["renamed"]=new SPMod.Tool.ModuleComponents.AllPlayerStatus();Maximum();Check(SPMod.StarSephiriaMod.PartyEffect.Count==1,"renames release old name entries");
  var clientPlayer=new PlayerAvatar{isServer=false,Name="client"};PlayerSpawner.MultiplayerList.Add(new PlayerSpawner{PlayerAvatar=clientPlayer});Maximum();Check(clientPlayer.Refreshes==0,"client avatar never receives local synchronization writes");PlayerSpawner.MultiplayerList.RemoveAt(1);
  b.customStats.Clear();max=Maximum();Check(max.Defense==0&&max.ElementalDamage==0,"unequip clears stats");
  PlayerSpawner.MultiplayerList.Clear();UnityEngine.Time.unscaledTime+=1;ForgeSPCompatibility.Tick();Check(SPMod.StarSephiriaMod.PartyEffect.Count==0 && SPMod.StarSephiriaMod.PartyStats.Item2.Defense==0,"title screen releases old names without dungeon Update");
  SPMod.StarSephiriaMod._isPatched=false;Check(!ForgeSPCompatibility.CategoryAvailable("PARTY"),"unloaded SP inactive");
  ForgeSPCompatibility.Uninstall();Check(HarmonyLib.Harmony.Patches==0&&!ForgeSPCompatibility.CategoryAvailable("WEAPON"),"unload removes patches and metadata");
  return "PASS: optional SP/config/status gates, authority, raw party contribution, live maximum, scaling, teammate departure, empty run and unload";
 }
}
