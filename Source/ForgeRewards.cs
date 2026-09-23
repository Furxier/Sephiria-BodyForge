using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class ForgeReward
{
    internal string Metadata, Description, Key, Name;
    internal string Identity { get { return Kind+":"+Key; } }
    internal string DisplayTotal(int value)
    {
        return Kind==7?"背包容量 +"+value+" 格":Describe(Metadata.Split('/')[0],value);
    }
    internal string DisplayAmount(int value)
    {
        if(Kind==7)return "+"+value+" 格";
        var id=Metadata.Split('/')[0];var entity=StatusDatabase.GetStatusEntity(id);
        if(id=="FROSTBITE_DAMAGE")return value.ToString("+0;-0;0",System.Globalization.CultureInfo.InvariantCulture)+"%";
        if(id=="HP_STEAL")return (value/10.0).ToString("+0.#;-0.#;0",System.Globalization.CultureInfo.InvariantCulture)+"%";
        return ((double)value/Math.Max(1,entity.divideForDisplay)).ToString("+0.#;-0.#;0",System.Globalization.CultureInfo.InvariantCulture)+Clean(StatusDatabase.GetStatusSymbol(id));
    }
    private static string Describe(string id,int value)
    {
        if(id=="FROSTBITE_DAMAGE")return "冻伤伤害 "+value.ToString("+0;-0;0",System.Globalization.CultureInfo.InvariantCulture)+"%（每层·冰伤/秒）";
        string name=Clean(StatusDatabase.GetStatusName(id)).Trim();
        if(string.IsNullOrEmpty(name))throw new InvalidOperationException("奖励属性名称缺失，请重新读取属性池");
        if(id=="HP_STEAL")return name+" "+(value/10.0).ToString("+0.#;-0.#;0",System.Globalization.CultureInfo.InvariantCulture)+"%";
        var entity=StatusDatabase.GetStatusEntity(id);
        string number=((double)value/Math.Max(1,entity.divideForDisplay)).ToString("+0.#;-0.#;0",System.Globalization.CultureInfo.InvariantCulture);
        string symbol=Clean(StatusDatabase.GetStatusSymbol(id));
        // Some native names are sentences with a value slot, rather than labels.
        // Fill the slot exactly once, including names that already contain the unit.
        if(name.Contains("{VALUE}"))
        {
            if(!string.IsNullOrEmpty(symbol))name=name.Replace("{VALUE}"+symbol,number+symbol);
            return name.Replace("{VALUE}",number+symbol);
        }
        return name+" "+number+symbol;
    }
    private static string Clean(string text) { return PlainKeywordText(text,0); }
    private static string PlainKeywordText(string text,int depth)
    {
        // <tag=Magic> carries meaning, unlike TMP color/sprite formatting.
        // Expand localized keywords before removing presentation tags.
        string expanded=System.Text.RegularExpressions.Regex.Replace(text??"","<tag=([^>]+)>",match=>{
            string key=match.Groups[1].Value.Trim().Trim('"','\'');
            if(depth>=8)return key;
            var keyword=KeywordDatabase.GetEntity(key);
            return keyword==null?key:PlainKeywordText(keyword.Convert(false,false,false),depth+1);
        });
        return System.Text.RegularExpressions.Regex.Replace(expanded,"<[^>]*>","");
    }
    internal ForgeReward WithValue(int value)
    { return Kind==7?Storage():Parse(Metadata.Split('/')[0]+"/"+value); }
    internal static ForgeReward Storage()
    { return new ForgeReward { Kind=7, Key="Storage", Name="背包容量", Metadata="Storage/1",Value=1,Delta=1,Description="背包容量 +1 格" }; }
    internal int Kind, Value;
    internal double Delta;
    // Enum indices verified against this game's StatusInstance implementations.
    private static readonly Dictionary<string,int> Custom = new Dictionary<string,int> {
        {"AP",4},{"AttackSpeed",1},{"BasicAttackDamage",21},{"BuffDuration",54},
        {"CooldownRecoverySpeed",23},{"Critical",32},{"CriticalDamageRate",33},
        {"DashAttackDamage",20},{"DashCount",40},{"DashRecoverySpeed",29},{"DashSpeed",38},
        {"DebuffDuration",53},{"Defense",13},{"EXPDrop",31},{"Evasion",16},{"FinalAP",36},
        {"FinalDamage",17},{"FinalMP",0},{"FinalWeaponDamage",37},{"FireDamage",10},
        {"HPPotionBonus",49},{"HPRegen",6},{"HPSteal",7},{"IceDamage",11},{"LeafDrop",30},
        {"LightningDamage",12},{"Luck",3},{"MPPotionBonus",50},{"MPRegen",5},{"MPSteal",8},
        {"MagicCritical",34},{"MagicCriticalDamageRate",35},{"MinDarkCloud",45},
        {"Negotiation",27},{"PhysicalDamage",9},{"SpecialAttackDamage",22},
        {"SweepCostReduction",25},{"Thorns",55},{"TrueDamage",28}
    };
    internal static ForgeReward Parse(string metadata)
    {
        var stat=StatusDatabase.CreateStatusEntity(metadata);
        if(stat==null || stat.Value<=0) throw new InvalidOperationException("原生属性池包含无效或非正向奖励");
        // Preserve the database ID: ToMetadata() emits CUSTOM/value for custom stats,
        // which CreateStatusEntity explicitly rejects as a network request.
        var reward=new ForgeReward { Metadata=metadata, Value=stat.Value, Delta=stat.Value,
            Name=Clean(StatusDatabase.GetStatusName(metadata.Split('/')[0])), Description=Describe(metadata.Split('/')[0],stat.Value) };
        string type=stat.GetType().Name.Replace("StatusInstance_",""); int index;
        if(Custom.TryGetValue(type,out index)) { reward.Kind=0; reward.Key=((ECustomStat)index).ToString().ToUpperInvariant(); }
        else if(type=="ChargingCharmBonus") { reward.Kind=0;reward.Key="CHARGINGCHARMBONUS"; }
        else if(type=="Custom" || type=="CustomAmp")
        {
            reward.Kind=type=="Custom"?0:1;
            var entity=StatusDatabase.GetStatusEntity(metadata.Split('/')[0]);
            var parts=entity.className.Split('/');
            if(parts.Length<2 || string.IsNullOrEmpty(parts[1])) throw new InvalidOperationException("原生属性键无效");
            reward.Key=parts[1].ToUpperInvariant();
        }
        else if(type=="MaxHP"||type=="MaxHPNoRatio") reward.Kind=2;
        else if(type=="MaxMP") reward.Kind=3;
        else if(type=="MoveSpeed") { reward.Kind=4; reward.Delta=stat.Value*0.01; }
        else if(type=="FinalHP") reward.Kind=5;
        else if(type=="HighestElementalDamage") reward.Kind=6;
        else throw new InvalidOperationException("暂不能核对原生属性："+reward.Description);
        return reward;
    }
    internal double Read(PlayerAvatar player)
    {
        int value;
        switch(Kind)
        {
            case 0:return player.customStats.TryGetValue(Key,out value)?value:0;
            case 1:return player.customStatsAmp.TryGetValue(Key,out value)?value:0;
            case 2:return player.maxHp;
            case 3:return player.maxMp;
            case 4:return player.moveSpeedMultiplier;
            case 5:return player.finalMaxHp;
            case 6:return player.highestElementalBonus;
            case 7:return player.Inventory.CurrentInventoryStorage;
        }
        throw new InvalidOperationException("未知属性类型");
    }
    internal static List<ForgeReward> LoadNativePool()
    {
        string[] chosen=null;
        foreach(var entity in PassiveDatabase.GetAll())
        {
            if(entity==null) continue;
            foreach(var prefab in new[]{entity.lv5PerkPrefab,entity.lv10PerkPrefab,entity.lv20PerkPrefab})
            {
                if(prefab==null) continue;
                foreach(var effect in prefab.GetComponentsInChildren<PassiveObject_PotionAndRandomStat>(true))
                {
                    if(effect.randomStats==null || effect.randomStats.Length==0) continue;
                    if(chosen!=null && string.Join("|",chosen)!=string.Join("|",effect.randomStats))
                        throw new InvalidOperationException("发现多个不同的喝药属性池，请先确认来源");
                    chosen=(string[])effect.randomStats.Clone();
                }
            }
        }
        if(chosen==null) throw new InvalidOperationException("尚未读取到原生喝药属性池，请进入局内后点刷新");
        var result=new List<ForgeReward>();
        foreach(string metadata in chosen) result.Add(Parse(metadata));
        return result;
    }
}
