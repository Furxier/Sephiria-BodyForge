using System;
using System.Collections.Generic;

internal static class ForgeAffinity
{
    internal static readonly HashSet<string> Specialized=new HashSet<string>(new[]{
        "Luck","DashAttackDamage","FinalWeaponDamage","TeamDefense","TeamDamage","TeamElement","TeamCritical",
        "EXPDrop","PlanetDamage","PlanetAttackSpeed",
        "FlameSwordDamage","FlameSwordCritical","FlameSwordFastFall","FrostRelicDamage","ChargingCharmBonus",
        "ChargingCharmAmplify","DarkCloudDamage","DarkCloudSpeed","DarkCloudRestoreDuringBattle","MinDarkCloud",
        "BurnDamage","BurnSpeed","BurnDuration","BurnStack","FreezeDamage","FrostbiteDamage","ElectricDamage","ElectricStack",
        "DebuffDamage","DebuffDuration","FollowerDamage","FollowerDefense","Negotiation","LeafDrop",
        "MagicCritical","MagicCriticalDamageRate","MagicDamageBonus","MagicCostReduce","FollowerAttackSpeed","FollowerCritical",
        "MPRegenMultiple","FinalMP","MPSkillDamage","FlameSwordIgnoreDefense","DashCount","FlameSwordMax"});
    internal static double Strength(string category,int count)
    {
        int[] thresholds=new[]{2,4,6,8,10};
        int tier=0;foreach(int t in thresholds)if(count>=t)tier++;
        double p=(double)(tier-1)/Math.Max(1,thresholds.Length-1); return tier==0?0:1+3*p*p;
    }
}

