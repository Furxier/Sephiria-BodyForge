using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
namespace UnityEngine {public static class Debug {public static int Errors;public static void LogError(object o){Errors++;}}}
namespace HarmonyLib {
 public class Harmony {public static string Patched;public Harmony(string id){}public void Patch(MethodInfo m,HarmonyMethod prefix=null,HarmonyMethod postfix=null){}public void UnpatchSelf(){}public static object GetPatchInfo(MethodBase m){return m.DeclaringType.Name==Patched?new object():null;}}
 public class HarmonyMethod {public HarmonyMethod(Type t,string n){}}
 public static class AccessTools {const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
  public static MethodInfo Method(Type t,string n){return t.GetMethod(n,Flags);}public static FieldInfo Field(Type t,string n){return t.GetField(n,Flags);}}
}
public class UnitAvatar {
 public bool isServer=true;public long Total;public int Applications;
 private readonly List<StatusInstance> orphanedStatusInstancesServerside=new List<StatusInstance>();
 public List<StatusInstance> Records {get{return orphanedStatusInstancesServerside;}}
 public void AddOrphanedStatusInstance(StatusInstance s){if(!isServer)return;Records.Add(s);s.SetTarget(this);s.Apply();Applications++;}
 public void UserCode_CmdAddOrphanedStatusInstance__String(string s){AddOrphanedStatusInstance(StatusDatabase.CreateStatusEntity(s));}
 public void Clear(){foreach(var s in Records){s.Remove();s.ClearTarget();}Records.Clear();}
}
public class StatusInstance {
 private int value;private bool applied;
 protected UnitAvatar CurrentTarget {get;private set;}
 public int Value{get{return value;}}
 public StatusInstance(int v){value=v;}
 public void SetTarget(UnitAvatar p){CurrentTarget=p;SetTargetInner();}
 public void ClearTarget(){CurrentTarget=null;}
 protected virtual void SetTargetInner(){}
 protected virtual void ApplyStatusInner(){CurrentTarget.Total+=Value;}
 protected virtual void RemoveStatusInner(){CurrentTarget.Total-=Value;}
 public void Apply(){applied=true;ApplyStatusInner();}public void Remove(){if(!applied)return;applied=false;RemoveStatusInner();}
}
public class Plain:StatusInstance {public Plain(int v):base(v){}protected override void ApplyStatusInner(){base.ApplyStatusInner();}protected override void RemoveStatusInner(){base.RemoveStatusInner();}}
public class Custom:StatusInstance {private string customID;public Custom(int v,string k):base(v){customID=k;}public string Key{get{return customID;}}}
public class Events:StatusInstance {public Events(int v):base(v){}protected override void SetTargetInner(){}}
public class Resource:StatusInstance {public Resource(int v):base(v){}}
public class StatusEntity {public string className,Rule;}
public static class StatusDatabase {
 public static Dictionary<string,StatusEntity> Entities=new Dictionary<string,StatusEntity>();public static string CustomKey="a";
 public static StatusEntity GetStatusEntity(string id){StatusEntity e;return Entities.TryGetValue(id,out e)?e:null;}
 public static StatusInstance CreateStatusEntity(string s){var parts=s.Split('/');var e=GetStatusEntity(parts[0]);int v=int.Parse(parts[1]);return e.className=="Custom"?new Custom(v,CustomKey):e.className=="Events"?(StatusInstance)new Events(v):e.className=="Resource"?new Resource(v):new Plain(v);}
}
internal static class ForgeRecipeCatalog {internal static string RuleType(StatusEntity e){return e.Rule;}}
internal static class ForgeBalance {internal static Dictionary<string,string> Rules=new Dictionary<string,string>{{"Critical",""},{"MaxHP",""},{"MaxMP",""},{"FinalHP",""},{"MoveSpeed",""}};}
public static class ForgePermanentStatsTests {
 static void Check(bool c,string m){if(!c)throw new Exception(m);}
 static object Call(string n,params object[] a){return typeof(ForgePermanentStats).GetMethod(n,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,a);}
 static void Definition(string id,string native,string rule){StatusDatabase.Entities[id]=new StatusEntity{className=native,Rule=rule};}
 [MethodImpl(MethodImplOptions.NoInlining)]static WeakReference Temporary(){var p=new UnitAvatar();ForgePermanentStats.Apply(p,"P/1");ForgePermanentStats.Apply(p,"P/1");return new WeakReference(p);}
 public static string Run(){
  Definition("P","Plain","Critical");Definition("P2","Plain","Critical");Definition("C","Custom","Critical");Definition("E","Events","Critical");
  ForgePermanentStats.Install();var p=new UnitAvatar();
  for(int i=0;i<10000;i++)ForgePermanentStats.Apply(p,"P/3");
  Check(p.Records.Count==1&&p.Total==30000&&p.Records[0].Value==30000&&p.Applications==10000,"10000 native grants compact without replay");
  p.Clear();Check(p.Total==0,"native cleanup subtracts exact accumulated amount");ForgePermanentStats.Apply(p,"P/2");ForgePermanentStats.Apply(p,"P/4");Check(p.Total==6&&p.Records.Count==1,"cleared cache record replaced");
  ForgePermanentStats.Apply(p,"P2/2");ForgePermanentStats.Apply(p,"E/2");ForgePermanentStats.Apply(p,"E/2");Check(p.Records.Count==4,"different ID and eventful records preserved");
  foreach(string r in new[]{"MaxHP","MaxMP","FinalHP","MoveSpeed"}){Definition(r,"Resource",r);ForgePermanentStats.Apply(p,r+"/2");ForgePermanentStats.Apply(p,r+"/2");}
  Check(p.Records.Count==12,"resource/movement records retain native order");
  HarmonyLib.Harmony.Patched="Plain";ForgePermanentStats.Apply(p,"P/1");ForgePermanentStats.Apply(p,"P/1");Check(p.Records.Count==14,"other-mod patched status not merged");HarmonyLib.Harmony.Patched=null;
  var c=new UnitAvatar();ForgePermanentStats.Apply(c,"C/1");StatusDatabase.CustomKey="b";ForgePermanentStats.Apply(c,"C/1");ForgePermanentStats.Apply(c,"C/2");Check(c.Records.Count==2&&c.Total==4,"custom keys never conflated");c.Clear();Check(c.Total==0,"custom cleanup");
  var remote=new UnitAvatar();object[] state={remote,0};Call("BeforeRequest",state);remote.UserCode_CmdAddOrphanedStatusInstance__String("P/2");Call("AfterRequest",remote,"P/2",state[1]);
  state=new object[]{remote,0};Call("BeforeRequest",state);remote.UserCode_CmdAddOrphanedStatusInstance__String("P/3");Call("AfterRequest",remote,"P/3",state[1]);Check(remote.Records.Count==1&&remote.Total==5,"native remote command compacted after one grant");
  state=new object[]{remote,0};Call("BeforeRequest",state);remote.UserCode_CmdAddOrphanedStatusInstance__String("P/3");remote.UserCode_CmdAddOrphanedStatusInstance__String("P/4");Call("AfterRequest",remote,"P/3",state[1]);Check(remote.Records.Count==3&&remote.Total==12,"reentrant command left intact");
  var client=new UnitAvatar{isServer=false};ForgePermanentStats.Apply(client,"P/2");Check(client.Total==0&&client.Records.Count==0,"no client local mutation");
  var huge=new UnitAvatar();ForgePermanentStats.Apply(huge,"P/2147483647");ForgePermanentStats.Apply(huge,"P/1");Check(huge.Records.Count==2&&huge.Total==2147483648L,"overflow preserves original records");huge.Clear();Check(huge.Total==0,"overflow cleanup");
  var weak=Temporary();for(int i=0;i<3;i++){GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();}Check(!weak.IsAlive,"weak cache does not root avatar/status cycle");
  ForgePermanentStats.Uninstall();p.Clear();remote.Clear();Check(p.Total==0&&remote.Total==0,"unload keeps native cleanup self-contained");
  Check(UnityEngine.Debug.Errors==0,"no silent compaction failure");return "PASS: 10000 awards, exact cleanup, native command, authority, event/resource exclusions, custom key changes, overflow, unload and weak ownership";
 }
}
