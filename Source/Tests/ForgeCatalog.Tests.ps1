$ErrorActionPreference='Stop'
$source='using System; using System.Collections.Generic; using UnityEngine;'+[Environment]::NewLine
foreach($file in @('SharedStatCatalog.cs','ForgeRecipes.cs','ForgeBalance.cs','ForgeAffinity.cs','ForgeTemplates.cs','ForgeRecipeCatalog.cs')){
    $s=Get-Content (Join-Path $PSScriptRoot ('../'+$file)) -Raw -Encoding UTF8
    $source+=($s -replace '(?m)^using [^;]+;','')
}
$test=@'
namespace UnityEngine {public static class Resources {public static T[] LoadAll<T>(string path){return (T[])(object)StatusDatabase.Entities.ToArray();}}}
internal static class ForgeSPCompatibility {internal static readonly string[] PartyIDs={"ALL_PLAYER_DEFENSE","ALL_PLAYER_ALL_DAMAGE","ALL_PLAYER_ELEMENTAL_DAMAGE","ALL_PLAYER_CRIT"};internal static bool Enabled=true;internal static bool CategoryAvailable(string c){return Enabled;}}
public class StatusEntity {public string id,className;public int divideForDisplay=1;}
public static class StatusDatabase {
    public static System.Collections.Generic.List<StatusEntity> Entities=new System.Collections.Generic.List<StatusEntity>();
    public static System.Collections.Generic.List<StatusEntity> Runtime=new System.Collections.Generic.List<StatusEntity>();
    public static StatusEntity GetStatusEntity(string id){return Entities.Find(e=>e.id==id)??Runtime.Find(e=>e.id==id);}
}
public class PlayerAvatar {public System.Collections.Generic.Dictionary<string,int> customStats=new System.Collections.Generic.Dictionary<string,int>();public GridInventory Inventory=new GridInventory();}
internal static class HeartEquipment {internal static int Bonus;internal static int ForgeBonus(PlayerAvatar owner){return Bonus;}}
public class GridInventory {public int CurrentInventoryStorage=40;public Dictionary<string,int> currentSetEffectCount=new Dictionary<string,int>();}
internal class BodyForgeSettings {internal static BodyForgeSettings Current=new BodyForgeSettings();internal bool EnableSpecializedRewards=true;}
internal class ForgeReward {
    internal string Metadata,Description,Identity;internal int Kind,Value;
    internal static ForgeReward Parse(string m){string id=m.Split('/')[0];return new ForgeReward{Metadata=m,Description=m,Identity=id,Value=int.Parse(m.Split('/')[1]),Kind=id=="HighestElementalDamage"?6:0};}
    internal static ForgeReward Storage(){return new ForgeReward{Metadata="Storage/1",Identity="Storage",Kind=7,Value=1};}
    internal ForgeReward WithValue(int v){return Kind==7?Storage():Parse(Metadata.Split('/')[0]+"/"+v);}
    internal double Read(PlayerAvatar p){int v;return p.customStats.TryGetValue(Identity,out v)?v:0;}
}
public static class CatalogTests {
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    public static string Run(){
        foreach(var r in ForgeBalance.Rules)StatusDatabase.Entities.Add(new StatusEntity{id=r.Key,className="StatusInstance_"+r.Key,divideForDisplay=r.Key=="Critical"||r.Key=="MagicCritical"||r.Key=="FollowerCritical"||r.Key=="Evasion"?100:1});
        StatusDatabase.GetStatusEntity("BasicAttackDamage").className="StatusInstance_Custom/BasicAttackDamage";
        StatusDatabase.GetStatusEntity("WeaponRange").className="StatusInstance_Custom/WeaponRange";
        StatusDatabase.GetStatusEntity("MPSkillDamage").className="StatusInstance_Custom/MPSkillDamage";
        Check(ForgeRecipeCatalog.RuleType(StatusDatabase.GetStatusEntity("WeaponRange"))=="WeaponRange","native range key maps to budget rule");
        Check(ForgeRecipeCatalog.RuleType(StatusDatabase.GetStatusEntity("MPSkillDamage"))=="MPSkillDamage"&&ForgeAffinity.Specialized.Contains("MPSkillDamage"),"MP skill damage is lake specialized, not generic");
        Check(SharedStatCatalog.RewardCost("WeaponRange")==4&&ForgeTemplates.All.Length>0,"range price in half-points");
        StatusDatabase.Entities.Add(new StatusEntity{id="removedMP",className="StatusInstance_MPSteal"});
        StatusDatabase.Entities.Add(new StatusEntity{id="removedHP",className="StatusInstance_HPRegen"});
        var pool=ForgeRecipeCatalog.Load();Check(pool.Count==ForgeBalance.Rules.Count&&!pool.Exists(r=>r.Kind==7),"all supported rules, storage removed");
        Check(!pool.Exists(r=>r.Identity=="removedMP"||r.Identity=="removedHP"),"removed rewards excluded even when native definitions exist");
        var owner=new PlayerAvatar();var seen=new HashSet<string>();
        for(int rarity=0;rarity<5;rarity++)for(int seed=0;seed<500;seed++)foreach(var c in ForgeRecipeCatalog.Generate(pool,rarity,owner,new Random(seed),false,new Dictionary<string,int>(),true))foreach(var r in c.Rewards){
            Check(r.Kind!=7,"run quota excludes storage");string id=r.Metadata.Split('/')[0];seen.Add(id);
            var e=StatusDatabase.GetStatusEntity(id);Check(r.Value%e.divideForDisplay==0,"displayed units convert exactly once");
            if(id=="HPSteal"||id=="MPSteal")Check(rarity>=3&&r.Value==1,"steal uses native unit, not percent");
            else{int cost=int.Parse(ForgeBalance.Rules[id].Split(',')[1]);Check(r.Value/e.divideForDisplay*cost<=ForgeRecipes.Budget(rarity),"raw value respects displayed budget");}
        }
        Check(seen.Contains("PhysicalDamage")&&seen.Contains("BasicAttackDamage")&&seen.Contains("SpecialAttackDamage")&&seen.Contains("Critical")&&seen.Contains("Evasion"),"new stats and scaled units reachable");
        var snapshot=new Dictionary<string,int>();foreach(string key in new[]{"ACADEMY","LAKE","SHADOW","FLAMESWORD","FROST","DARKCLOUD","EMBER","GLACIER","MAGITECH","CURSE","COMPANION","SAVVY","PLANET","FORTUNE","COMET","WEAPON","PARTY"})snapshot[key]=10;
        var specialSeen=new HashSet<string>();int mechanisms=0;
        for(int rarity=0;rarity<5;rarity++)for(int seed=0;seed<1500;seed++)foreach(bool enabled in new[]{false,true}) {
            var cards=ForgeRecipeCatalog.Generate(pool,rarity,owner,new Random(seed),false,snapshot,enabled);
            int specialCards=0,mechanismCards=0;
            foreach(var card in cards){int cost=0;bool specialized=false;
                foreach(var reward in card.Rewards){string type=reward.Metadata.Split('/')[0];
                    var rule=ForgeBalance.Rules[type].Split(',');Check(rarity>=int.Parse(rule[2]),"specialized minimum rarity");
                    if(ForgeBalance.IsSpecialized(type)){Check(enabled,"disabled specialized reward cannot roll");specialized=true;specialSeen.Add(type);}
                    if(ForgeBalance.MechanismLimit(type)>0){Check(card.Rewards.Length<=2&&reward.Value==1,"mechanism +1 with optional companion stat");mechanismCards++;mechanisms++;}
                    cost+=reward.Value/StatusDatabase.GetStatusEntity(type).divideForDisplay*int.Parse(rule[1]);
                }
                if(specialized)specialCards++;Check(cost<=ForgeRecipes.Budget(rarity),"specialized card shares one budget");
            }
            Check(specialCards<=2,"generic card never contains specialized stats");Check(mechanismCards<=1,"one mechanism per set");
        }
        foreach(string type in ForgeAffinity.Specialized)Check(specialSeen.Contains(type),"eligible specialty reachable: "+type);
        Check(mechanisms>0,"mechanism draws exercised");
        foreach(string type in new[]{"MagicCostReduce","FlameSwordIgnoreDefense"}){
            owner.customStats[type]=ForgeBalance.NativeLimit(type)-1;
            Check(ForgeRecipeCatalog.Available(ForgeReward.Parse(type+"/1"),owner),"last legal unit");
            Check(!ForgeRecipeCatalog.Available(ForgeReward.Parse(type+"/2"),owner),"cached reward cannot cross native eligibility limit");
        }
        foreach(string type in new[]{"ChargingCharmAmplify","BurnStack","ElectricStack","DashCount","FlameSwordMax"})owner.customStats[type]=ForgeBalance.MechanismLimit(type);
        for(int seed=0;seed<200;seed++)foreach(var card in ForgeRecipeCatalog.Generate(pool,4,owner,new Random(seed),false,snapshot,true))foreach(var r in card.Rewards)
            Check(ForgeBalance.MechanismLimit(r.Metadata.Split('/')[0])==0,"native totals gate capped mechanism even without journal");
        foreach(string type in new[]{"LeafDrop","Negotiation"}){
            owner.customStats[type]=100000;Check(ForgeRecipeCatalog.Available(ForgeReward.Parse(type+"/1"),owner),"economy has no cumulative cap");
        }
        HeartEquipment.Bonus=30;bool improved=false;
        for(int rarity=0;rarity<5;rarity++)for(int seed=0;seed<100;seed++)foreach(var card in ForgeRecipeCatalog.Generate(pool,rarity,owner,new Random(seed),false,snapshot,true)){
            int cost=0;foreach(var reward in card.Rewards){string type=reward.Metadata.Split('/')[0];cost+=reward.Value/StatusDatabase.GetStatusEntity(type).divideForDisplay*int.Parse(ForgeBalance.Rules[type].Split(',')[1]);}
            Check(cost<=ForgeRecipes.BoostedBudget(rarity,30),"boost respects budget");if(cost>ForgeRecipes.Budget(rarity))improved=true;
        }
        Check(improved && ForgeRecipes.BoostedBudget(4,30)==156,"30 percent reaches actual generated cards");
        HeartEquipment.Bonus=0;
        StatusDatabase.Entities.RemoveAll(e=>e.id.StartsWith("Team"));
        string[] raw={"STAT_ALLPLAYERDEFENSE","STAT_ALLPLAYERALLDAMAGE","STAT_ALLPLAYERELEMENTALDAMAGE","STAT_ALLPLAYERCRIT"};
        for(int i=0;i<4;i++)StatusDatabase.Runtime.Add(new StatusEntity{id=ForgeSPCompatibility.PartyIDs[i],className="StatusInstance_Custom/"+raw[i],divideForDisplay=0});
        pool=ForgeRecipeCatalog.Load();Check(pool.Count==ForgeBalance.Rules.Count,"runtime-created SP status definitions discovered without Resources assets");
        bool teamCrit=false;for(int seed=0;seed<100;seed++)foreach(var card in ForgeRecipeCatalog.Generate(pool,4,owner,new Random(seed),false,new Dictionary<string,int>{{"PARTY",10}},true))foreach(var r in card.Rewards)
         if(r.Metadata.StartsWith("ALL_PLAYER_CRIT/")){teamCrit=true;Check(r.Value==2,"team critical is two displayed percentage points, not two hundred");}
        Check(teamCrit,"runtime party recipes reachable");
        ForgeSPCompatibility.Enabled=false;
        Check(!ForgeRecipeCatalog.Available(ForgeReward.Parse("ALL_PLAYER_DEFENSE/1"),owner),"cached team reward blocked after optional provider disabled");
        foreach(var card in ForgeRecipeCatalog.Generate(pool,4,owner,new Random(5),false,new Dictionary<string,int>{{"PARTY",10}},true))foreach(var r in card.Rewards)
         Check(!r.Metadata.StartsWith("ALL_PLAYER_"),"disabled SP snapshot falls back to generic");
        return "PASS: 15000 specialty sets, equipment budget bonus, toggle, generic slot, rarity, budgets, mechanism limits, all specialty reachability, uncapped economy, raw units";
    }
}
'@
Add-Type -TypeDefinition ($source+$test)
[CatalogTests]::Run()

