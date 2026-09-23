using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class ForgeRecipe
{
    internal string Title;
    internal ForgeReward[] Rewards;
    internal string Text { get { var parts=new List<string>(); foreach(var r in Rewards)parts.Add(r.Description);return string.Join("\n",parts.ToArray()); } }
}
internal static class ForgeRecipeCatalog
{
    private static readonly Dictionary<string,string> Rules=ForgeBalance.Rules;
    internal static string RuleType(StatusEntity entity)
    {
        string type=(entity.className??"").Replace("StatusInstance_","");
        if(Rules.ContainsKey(type))return type;
        if(!type.StartsWith("Custom/",StringComparison.Ordinal))return "";
        string key=type.Substring(7).Replace("_","").ToUpperInvariant();
        foreach(string rule in Rules.Keys)if(rule.ToUpperInvariant()==key)return rule;
        switch(key)
        {
            case "STATALLPLAYERDEFENSE":return "TeamDefense";
            case "STATALLPLAYERALLDAMAGE":return "TeamDamage";
            case "STATALLPLAYERELEMENTALDAMAGE":return "TeamElement";
            case "STATALLPLAYERCRIT":return "TeamCritical";
            case "DAMAGEREDUCTION":return "Defense";
            case "DASHRECOVERY":return "DashRecoverySpeed";
            case "CRITICALDAMAGEBONUS":return "CriticalDamageRate";
            case "MAGICCRITICALDAMAGEBONUS":return "MagicCriticalDamageRate";
            case "ALLDAMAGEBONUS":return "FinalDamage";
        }
        return "";
    }
    internal static List<ForgeReward> Load()
    {
        var found=new List<ForgeReward>(); var unique=new HashSet<string>();
        var all=new List<StatusEntity>(Resources.LoadAll<StatusEntity>("Status"));
        // SP creates these at runtime, so Resources.LoadAll does not discover them.
        if(ForgeSPCompatibility.CategoryAvailable("PARTY"))foreach(string id in ForgeSPCompatibility.PartyIDs)
        { var entity=StatusDatabase.GetStatusEntity(id);if(entity!=null)all.Add(entity); }
        var entities=all.ToArray();
        Array.Sort(entities,(a,b)=>string.CompareOrdinal(a.id,b.id));
        foreach(var entity in entities)
        {
            if(entity==null || string.IsNullOrEmpty(entity.id))continue;
            var actual=StatusDatabase.GetStatusEntity(entity.id);
            if(actual==null)continue;
            string type=RuleType(actual);
            if(!Rules.ContainsKey(type))continue;
            int units=Math.Max(1,actual.divideForDisplay);
            var reward=ForgeReward.Parse(actual.id+"/"+units);
            if(unique.Add(reward.Identity)) found.Add(reward);
        }
        if(!found.Exists(r=>r.Kind==6)) throw new InvalidOperationException("最高元素伤害原生定义缺失，请检查游戏版本");
        if(found.Count<4)throw new InvalidOperationException("原生奖励定义不足");
        return found;
    }
    internal static ForgeRecipe[] Generate(List<ForgeReward> pool,int rarity,PlayerAvatar owner,System.Random random,bool storageEligible,Dictionary<string,int> snapshot,bool specialized)
    {
        var specs=new List<ForgeRecipeSpec>();var byId=new Dictionary<string,ForgeReward>();
        var enabledSnapshot=new Dictionary<string,int>(StringComparer.Ordinal);
        foreach(var pair in snapshot)if(ForgeSPCompatibility.CategoryAvailable(pair.Key))enabledSnapshot[pair.Key]=pair.Value;
        foreach(var r in pool)
        {
            ForgeRecipeSpec s;
            if(r.Kind==7)s=new ForgeRecipeSpec { Id=r.Identity,Group="storage",Storage=true,Special=true,Minimum=3,Cap=1 };
            else
            {
                string type=RuleType(StatusDatabase.GetStatusEntity(r.Metadata.Split('/')[0]));
                if(ForgeBalance.IsSpecialized(type) && !specialized)continue;
                string[] values=Rules[type].Split(',');
                int limit=ForgeBalance.MechanismLimit(type);
                s=new ForgeRecipeSpec { Id=r.Identity,Type=type,Group=values[0],Cost=int.Parse(values[1]),Minimum=int.Parse(values[2]),Cap=int.Parse(values[3]),Special=type=="HPSteal"||type=="MPSteal"||limit>0,Specialized=ForgeBalance.IsSpecialized(type),Mechanism=limit>0 };
                if(limit>0 && r.Read(owner)>=limit)s.Cap=0;
                int nativeLimit=ForgeBalance.NativeLimit(type);
                if(nativeLimit>0)s.Cap=Math.Min(s.Cap,Math.Max(0,(int)Math.Floor((nativeLimit-r.Read(owner))/Math.Max(1,StatusDatabase.GetStatusEntity(r.Metadata.Split('/')[0]).divideForDisplay))));
            }
            if(s.Minimum>rarity || s.Cap<=0)continue;
            specs.Add(s);byId.Add(r.Identity,r);
        }
        var cards=ForgeRecipes.Generate(specs,rarity,storageEligible && owner.Inventory.CurrentInventoryStorage<120,random,enabledSnapshot,specialized,HeartEquipment.ForgeBonus(owner));
        var result=new List<ForgeRecipe>();
        foreach(var card in cards)
        {
            var rewards=new List<ForgeReward>();
            foreach(var part in card.Parts)
            {
                var original=byId[part.Id];
                int units=original.Kind==7?1:Math.Max(1,StatusDatabase.GetStatusEntity(original.Metadata.Split('/')[0]).divideForDisplay);
                rewards.Add(original.WithValue(checked(part.Value*units)));
            }
            // Expansion can refresh inventory effects; perform it after attributes.
            rewards.Sort((a,b)=>(a.Kind==7?1:0).CompareTo(b.Kind==7?1:0));
            result.Add(new ForgeRecipe { Title=card.Title,Rewards=rewards.ToArray() });
        }
        return result.ToArray();
    }
    internal static Dictionary<string,int> Snapshot(PlayerAvatar owner)
    {
        if(owner==null || owner.Inventory==null)throw new InvalidOperationException("背包数据尚未就绪");
        var result=new Dictionary<string,int>(StringComparer.Ordinal);
        foreach(var pair in owner.Inventory.currentSetEffectCount)result[pair.Key]=pair.Value;
        return result;
    }
    internal static bool Available(ForgeReward reward,PlayerAvatar owner)
    {
        if(reward.Kind==7)return true;
        string type=RuleType(StatusDatabase.GetStatusEntity(reward.Metadata.Split('/')[0]));
        string required=type=="Luck"?"FORTUNE":type=="DashAttackDamage"?"COMET":type=="FinalWeaponDamage"?"WEAPON":type.StartsWith("Team",StringComparison.Ordinal)?"PARTY":null;
        if(required!=null && !ForgeSPCompatibility.CategoryAvailable(required))return false;
        if(ForgeBalance.IsSpecialized(type) && !BodyForgeSettings.Current.EnableSpecializedRewards)return false;
        int limit=ForgeBalance.NativeLimit(type);
        return limit==0 || reward.Read(owner)+reward.Value<=limit;
    }
}
