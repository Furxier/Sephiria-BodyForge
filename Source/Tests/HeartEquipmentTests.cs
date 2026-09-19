// Offline lifecycle harness: fake Unity/network surfaces, real mod implementation.
using System;
using System.Collections.Generic;
using System.Reflection;
namespace UnityEngine {
 public class Object {}
 public class GameObject {public object Component;public T GetComponent<T>(){return (T)Component;}}
 public struct Color {public static Color white;}
 public static class Debug {public static void LogWarning(object value){} public static void LogError(object value){throw new Exception(value.ToString());}}
}
namespace HarmonyLib {
 public class Harmony {public Harmony(string id){} public void Patch(MethodInfo m,HarmonyMethod prefix=null,HarmonyMethod postfix=null,HarmonyMethod finalizer=null,HarmonyMethod transpiler=null){} public void UnpatchSelf(){}}
 public class CodeInstruction {public System.Reflection.Emit.OpCode opcode;public object operand;} public class HarmonyMethod {public HarmonyMethod(Type t,string name){}}
 public static class AccessTools {public static MethodInfo Method(Type t,string n){return t.GetMethod(n,BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);}public static FieldInfo Field(Type t,string n){return t.GetField(n,BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);}}
}
public class BodyForgeSettings {public bool Enabled=true;public static BodyForgeSettings Current=new BodyForgeSettings();}
namespace TMPro {public class TMP_Text {public string text;}}
namespace UnityEngine.UI {public class Image {}}
public interface IItemEntity {int IEntityID {get;}}
public class UI_CharmTooltip {public void UpdateData(object item,int level){}}
public class UI_CharmTierDisplay {public void SetTier(int max,int real,int extra,int enchant){}}
public struct ItemPosition {public sbyte X,Y;public ItemPosition(sbyte x,sbyte y){X=x;Y=y;}}
public class LocalizedString {public string key;public override string ToString(){return key;}}
public class ItemEntity {public int id;public UnityEngine.GameObject resourcePrefab;public string Context {get{return "original";}}}
public static class ItemDatabase {public static ItemEntity Heart;public static ItemEntity FindItemById(int id){return Heart;}}
public static class HorayModAPI {public static event Action OnLoadItemDatabase;public static void Load(){if(OnLoadItemDatabase!=null)OnLoadItemDatabase();}}
public class SkillController {public event Func<int> OnGetMultipleCastCount;public int Count(){int value=0;if(OnGetMultipleCastCount!=null)foreach(Func<int> f in OnGetMultipleCastCount.GetInvocationList())value+=f();return value;}}
public class UnitAvatar {public GridInventory Inventory;public SkillController Skills=new SkillController();public Dictionary<string,int> Stats=new Dictionary<string,int>();public T GetComponent<T>(){return (T)(object)Skills;}}
public class NewItemOwnInstance {public int EntityID,InstanceID;public Charm_Basic Charm;}
public class GridInventory {public bool writePermission;public int RefreshCount;public class Permission:IDisposable{private GridInventory inventory;public Permission(GridInventory i){inventory=i;}public void Dispose(){inventory.RefreshCount++;}}public Dictionary<ItemPosition,NewItemOwnInstance> inventoryMatrix {get{return Items;}}public const int MaxHeight=30;public byte Width=8;public Dictionary<ItemPosition,NewItemOwnInstance> Items=new Dictionary<ItemPosition,NewItemOwnInstance>();public NewItemOwnInstance FindItem(sbyte x,sbyte y){return FindItem(new ItemPosition(x,y));}public NewItemOwnInstance FindItem(ItemPosition p){NewItemOwnInstance i;Items.TryGetValue(p,out i);return i;}}
public class Charm_Basic : UnityEngine.Object {
 public sbyte xIdx,yIdx;public int maxLevel,EffectEnabledLevel,DisplayedLevel;public bool isServer=true,IsEffectEnabled=true;
 public object netIdentity=new object();public UnitAvatar NetworkAvatar;public GridInventory Inventory {get{if(netIdentity==null)throw new NullReferenceException("Mirror asset has no NetworkIdentity");return NetworkAvatar==null?null:NetworkAvatar.Inventory;}}
 public string[] Categories=new string[0];public IEnumerable<string> GetItemCategory(){return Categories;}
 public void OnCharmEffectRefreshed(){}protected void OnDestroy(){}
}
public class Charm_UpCharmDamage : Charm_Basic {public sbyte xOffset,yOffset=-1;}
public class Charm_StatusInstance : Charm_Basic {
 public class StatusGroup {public string statusID;public int[] valuesByLevel;public bool hideIfStatValueIsZero;}
 public StatusGroup[] stats=new StatusGroup[0];private Dictionary<string,int> applied=new Dictionary<string,int>();
 public void OnEnabledEffect(){OnUpdatedLevel(0,DisplayedLevel);}
 public void OnDisabledEffect(){foreach(var pair in applied)NetworkAvatar.Stats[pair.Key]-=pair.Value;applied.Clear();}
 public void OnUpdatedLevel(int old,int next){typeof(HeartEquipment).GetMethod("BeforeApply",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{this});OnDisabledEffect();foreach(var s in stats){int v=s.valuesByLevel[Math.Max(0,Math.Min(maxLevel,next))];applied[s.statusID]=v;int current;NetworkAvatar.Stats.TryGetValue(s.statusID,out current);NetworkAvatar.Stats[s.statusID]=current+v;}typeof(HeartEquipment).GetMethod("AfterApply",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{this});}
 public int GetEffectStringCount(){return stats.Length;}public string GetEffectString(int i,int level,int state,bool all){return "original";}
}
public class DungeonManager {public static DungeonManager Instance=new DungeonManager();public int Enchant=30;public string GetGlobalItemStatValue(int id,string key){return Enchant.ToString();}} public class StatusInstance {public string ToString(bool a,bool b,bool c,UnityEngine.Color d){return "stat";}}
public static class StatusDatabase {public static StatusInstance CreateStatusEntity(string id,int value){return new StatusInstance();}}
public static class HeartEquipmentTests {
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 static object Call(string name,params object[] args){return typeof(HeartEquipment).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);}
 static void Refresh(Charm_StatusInstance c){Call("Refreshed",c);Call("AfterApply",c);}
 public static string Run(){
  TooltipTests();var code=new HarmonyLib.CodeInstruction{opcode=System.Reflection.Emit.OpCodes.Callvirt,operand=typeof(Charm_Basic).GetMethod("GetItemCategory")};var rewritten=new List<HarmonyLib.CodeInstruction>((IEnumerable<HarmonyLib.CodeInstruction>)Call("CountCategories",new object[]{new[]{code}}));Check(rewritten.Count==1 && rewritten[0].opcode==System.Reflection.Emit.OpCodes.Call && ((MethodInfo)rewritten[0].operand).Name=="CountedCategories","native counting call replacement");
  Check(HeartProfiles.All.Length==17,"17 profiles");
  var unique=new HashSet<string>();
  foreach(var p in HeartProfiles.All){Check(unique.Add(p.Category),"unique categories");foreach(var s in p.Stats){Check(s.Values.Length==31,"31 levels");for(int i=1;i<31;i++)Check(s.Values[i]>=s.Values[i-1],"monotone");}}
  Check(HeartProfiles.Select(new[]{"FROST","STURDY"}).Category=="STURDY","deterministic ties");
  Check(HeartProfiles.Select(new[]{"unknown"})==null,"unsupported");
  Check(HeartProfiles.Curve(new[]{0,0,0,1,1})[24]==0 && HeartProfiles.Curve(new[]{0,0,0,1,1})[25]==1,"no early discrete unlock");
  var prefab=new Charm_StatusInstance{netIdentity=null};ItemDatabase.Heart=new ItemEntity{id=1304,resourcePrefab=new UnityEngine.GameObject{Component=prefab}};
  HeartEquipment.Install();Check(prefab.maxLevel==30,"native prefab cap");
  var ordinaryAsset=new Charm_StatusInstance{netIdentity=null};
  object[] ordinaryCount={ordinaryAsset,0};Check((bool)Call("EffectCount",ordinaryCount),"ordinary asset preview retains native effect count without Mirror access");
  object[] ordinaryText={ordinaryAsset,0,0,null};Check((bool)Call("EffectString",ordinaryText),"ordinary asset preview retains native description without Mirror access");
  object[] heartCount={prefab,0};Check(!(bool)Call("EffectCount",heartCount) && (int)heartCount[1]==2,"heart asset preview safe without NetworkIdentity");
  object[] heartText={prefab,1,30,null};Call("EffectString",heartText);Check((string)heartText[3]=="锻体属性预算 +0%","unowned heart asset has no enchantment");
  Check(HeartEquipment.EnchantmentLevel(ordinaryAsset)==0,"asset enchantment lookup never resolves Mirror SyncVar");
  var avatar=new UnitAvatar{Inventory=new GridInventory()};var c=new Charm_StatusInstance{NetworkAvatar=avatar,xIdx=1,yIdx=2,DisplayedLevel=30,EffectEnabledLevel=30};
  var target=new Charm_Basic{Categories=new[]{"FROST"}};
  avatar.Inventory.Items[new ItemPosition(1,2)]=new NewItemOwnInstance{EntityID=1304,Charm=c};
  avatar.Inventory.Items[new ItemPosition(1,1)]=new NewItemOwnInstance{EntityID=5,Charm=target};
  Refresh(c);Check(avatar.Stats["FROST_RELIC_DAMAGE"]==45,"native equipment applied");
  object[] categories={c,null};Check(!(bool)Call("Categories",categories) && new List<string>((IEnumerable<string>)categories[1])[0]=="FROST","one matching synergy");
  int[] levels={0,4,5,14,15,24,25,29,30,100},counts={1,1,2,2,3,3,4,4,5,5};for(int i=0;i<levels.Length;i++){DungeonManager.Instance.Enchant=levels[i];int count=new List<string>((IEnumerable<string>)Call("CountedCategories",c)).Count;Check(count==counts[i],"level-scaled counted synergy");}DungeonManager.Instance.Enchant=30;Check(HeartEquipment.ForgeBonus(avatar)==30,"level 30 boost");DungeonManager.Instance.Enchant=1;c.EffectEnabledLevel=30;Check(HeartEquipment.ForgeBonus(avatar)==1,"level 1 boost");DungeonManager.Instance.Enchant=100;c.EffectEnabledLevel=100;Check(HeartEquipment.ForgeBonus(avatar)==30,"boost capped at 30");c.IsEffectEnabled=false;Check(HeartEquipment.ForgeBonus(avatar)==0,"disabled item no boost");c.IsEffectEnabled=true;c.EffectEnabledLevel=30;DungeonManager.Instance.Enchant=30;
  target.Categories=new[]{"ELEMENTAL"};Refresh(c);categories=new object[]{c,null};Call("Categories",categories);Check(new List<string>((IEnumerable<string>)categories[1])[0]=="ELEMENTAL","synergy follows target");Check(!avatar.Stats.ContainsKey("CRITICAL") && avatar.Stats["HIGHEST_ELEMENTAL_DAMAGE"]==28 && avatar.Stats["MOVE_SPEED"]==12,"elemental damage and movement without crit");
  target.Categories=new[]{"LAKE"};Refresh(c);Check(avatar.Stats["MP_SKILL_DAMAGE"]==60,"lake native MP ability damage");
  target.Categories=new[]{"FROST"};Refresh(c);
  DungeonManager.Instance.Enchant=5;Refresh(c);Check(avatar.Stats["FROST_RELIC_DAMAGE"]==9,"tablet level 30 cannot inflate enchant level 5");DungeonManager.Instance.Enchant=30;Refresh(c);
  for(int i=0;i<100;i++)Refresh(c);Check(avatar.Stats["FROST_RELIC_DAMAGE"]==45,"no refresh stacking");
  target.Categories=new[]{"ACADEMY"};Refresh(c);Check(avatar.Stats["FROST_RELIC_DAMAGE"]==0 && avatar.Stats["MAGIC_CRITICAL"]==2000,"switch removes old stats");
  for(int i=0;i<100;i++)Refresh(c);Check(avatar.Skills.Count()==1,"one multicast subscription");
  DungeonManager.Instance.Enchant=24;c.DisplayedLevel=30;c.EffectEnabledLevel=30;Call("BeforeApply",c);c.OnUpdatedLevel(30,24);Call("AfterApply",c);Check(avatar.Skills.Count()==0,"lose unlock on lower level");
  DungeonManager.Instance.Enchant=30;c.DisplayedLevel=30;c.EffectEnabledLevel=30;
  avatar.Inventory.Items.Remove(new ItemPosition(1,1));Refresh(c);Check(avatar.Stats["MAGIC_CRITICAL"]==0,"empty target removes stats");
  categories=new object[]{c,null};Call("Categories",categories);Check(new List<string>((IEnumerable<string>)categories[1]).Count==0,"empty target clears synergy");Check(HeartEquipment.ForgeBonus(avatar)==30,"forge bonus independent of theme");
  var needle=new Charm_UpCharmDamage();avatar.Inventory.Items[new ItemPosition(1,1)]=new NewItemOwnInstance{EntityID=1289,Charm=needle};
  avatar.Inventory.Items[new ItemPosition(1,0)]=new NewItemOwnInstance{EntityID=5,Charm=target};Refresh(c);Check(avatar.Skills.Count()==1,"native pointer chain");
  needle.yOffset=1;Refresh(c);Check(avatar.Skills.Count()==0,"cycle rejected");
  needle.yOffset=-1;Refresh(c);
  BodyForgeSettings.Current.Enabled=false;HeartEquipment.Tick();Check(avatar.Skills.Count()==0 && avatar.Stats["MAGIC_CRITICAL"]==0,"disable clears");
  BodyForgeSettings.Current.Enabled=true;HeartEquipment.Tick();Call("AfterApply",c);Check(avatar.Skills.Count()==1,"reenable");
  c.isServer=false;target.Categories=new[]{"FROST"};Call("Refreshed",c);Check(avatar.Stats["MAGIC_CRITICAL"]==2000,"client cannot apply equipment");c.isServer=true;
  HeartEquipment.Uninstall();Check(prefab.maxLevel==0 && c.maxLevel==0 && avatar.Stats["MAGIC_CRITICAL"]==0 && avatar.Skills.Count()==0,"unload restores and unsubscribes");
  HeartEquipment.Install();Refresh(c);Call("Destroyed",c);Check(avatar.Stats["FROST_RELIC_DAMAGE"]==0,"destroy cleans effects");HeartEquipment.Uninstall();
  return "PASS: 17 profiles, curves, targeting, pointer cycles, host authority, repeated refresh, switches, multicast lifetime, disable, unload, destroy and 30-level native tooltip bounds";
 }
 static object UI(string method,object[] args){if(method=="End")args=new object[]{args[0],args[1],null,null};return typeof(HeartTooltip).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);}
 static void TooltipTests(){
  for(int capacity=0;capacity<=40;capacity++)for(int level=-5;level<=100;level++){
   int count=HeartTooltip.StarCount(level,capacity);Check(count>=0 && count<=capacity,"star capacity");
  }
  object[] begin={new ItemEntity{id=1304},null};UI("Begin",begin);
  object[] stars={30,25,5,25,new UnityEngine.UI.Image[17]};UI("LimitStars",stars);
  Check((int)stars[0]==17 && (int)stars[1]==0 && (int)stars[2]==0 && (int)stars[3]==0,"30 levels fit native 17 stars");
  var label=new TMPro.TMP_Text();UI("Label",new object[]{new ItemEntity{id=1304},30,label});Check(label.text.Contains("30") && !label.text.Contains(" / "),"compact exact level");
  object[] nested={new ItemEntity{id=5},null};UI("Begin",nested);
  object[] ordinary={30,25,5,25,new UnityEngine.UI.Image[17]};UI("LimitStars",ordinary);Check((int)ordinary[0]==30,"other item unaffected");
  var failure=new Exception("native UI failure");Check(UI("End",new object[]{nested[1],failure})==failure,"preserve original exception");
  UI("End",new object[]{begin[1],null});UI("LimitStars",ordinary);Check((int)ordinary[0]==30,"exception-safe scope cleanup");
  var avatar=new UnitAvatar{Inventory=new GridInventory()};
  var charm=new Charm_StatusInstance{NetworkAvatar=avatar,DisplayedLevel=30,EffectEnabledLevel=30};
  var item=new NewItemOwnInstance{EntityID=1304,Charm=charm};avatar.Inventory.Items[new ItemPosition(0,0)]=item;
  DungeonManager.Instance.Enchant=5;object[] owned={item,null};UI("Begin",owned);
  object[] boosted={30,30,20,30,new UnityEngine.UI.Image[17]};UI("LimitStars",boosted);
  UI("Label",new object[]{item,30,label});Check((int)boosted[1]==2 && (int)boosted[2]==0 && label.text.StartsWith("附魔等级 5"),"tooltip ignores temporary levels");
  UI("End",new object[]{owned[1],null});DungeonManager.Instance.Enchant=30;
  var quotes=new HashSet<string>();foreach(var profile in HeartProfiles.All){string quote=HeartProfiles.Flavor(profile.Category);Check(quote!=HeartProfiles.DefaultFlavor && quotes.Add(quote),"17 distinct approved flavor lines");}
  charm.xIdx=1;charm.yIdx=1;avatar.Inventory.Items[new ItemPosition(1,1)]=item;
  var target=new Charm_Basic{Categories=new[]{"FROST"}};avatar.Inventory.Items[new ItemPosition(1,0)]=new NewItemOwnInstance{Charm=target,EntityID=5};
  owned=new object[]{item,null};UI("Begin",owned);Check(HeartTooltip.CurrentFlavor=="端坐于霜天吧，冰轮丸！","flavor follows frost target");
  object[] other={new ItemEntity{id=5},null};UI("Begin",other);Check(HeartTooltip.CurrentFlavor==HeartProfiles.DefaultFlavor,"other preview cannot inherit heart flavor");
  UI("End",new object[]{other[1],null});Check(HeartTooltip.CurrentFlavor=="端坐于霜天吧，冰轮丸！","nested scope restores flavor");
  UI("End",new object[]{owned[1],new Exception("test")});Check(HeartTooltip.CurrentFlavor==HeartProfiles.DefaultFlavor,"exception clears flavor scope");
  target.Categories=new[]{"LAKE"};owned=new object[]{item,null};UI("Begin",owned);Check(HeartTooltip.CurrentFlavor=="用水好好反省一下吧！","moving target updates flavor");UI("End",new object[]{owned[1],null});
  avatar.Inventory.Items.Remove(new ItemPosition(1,0));Check(HeartEquipment.FlavorFor(charm)==HeartProfiles.DefaultFlavor,"empty target defaults flavor");
 }
}
