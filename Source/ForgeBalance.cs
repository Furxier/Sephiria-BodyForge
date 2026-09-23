using System.Collections.Generic;

internal static class ForgeBalance
{
    internal const int StorageLimit=10;
    // group, price in half-points, minimum rarity, per-card unit cap.
    // Utility/recovery prices are initial tuning values, not measured equivalence.
    internal static readonly Dictionary<string,string> Rules=new Dictionary<string,string> {
        {"Luck",Rule("Luck","fortune",2,100)},
        {"DashAttackDamage",Rule("DashAttackDamage","comet",0,100)},
        {"FinalWeaponDamage",Rule("FinalWeaponDamage","weapon",0,100)},
        {"TeamDefense",Rule("TeamDefense","party",0,100)},
        {"TeamDamage",Rule("TeamDamage","party",0,100)},
        {"TeamElement",Rule("TeamElement","party",0,100)},
        {"TeamCritical",Rule("TeamCritical","party",0,100)},
        {"EXPDrop",Rule("EXPDrop","economy",1,100)},
        {"PlanetDamage",Rule("PlanetDamage","planet",0,100)},
        {"PlanetAttackSpeed",Rule("PlanetAttackSpeed","planet-speed",1,100)},
        {"PhysicalDamage",Rule("PhysicalDamage","physical",0,100)},
        {"WeaponRange",Rule("WeaponRange","utility",0,15)},
        {"MPSkillDamage",Rule("MPSkillDamage","lake",1,100)},
        {"BasicAttackDamage",Rule("BasicAttackDamage","weapon",0,100)},{"SpecialAttackDamage",Rule("SpecialAttackDamage","weapon",0,100)},
        {"MaxHP",Rule("MaxHP","guard",0,100)},{"MaxMP",Rule("MaxMP","sustain",0,100)},
        {"Defense",Rule("Defense","guard",0,100)},{"AttackSpeed",Rule("AttackSpeed","attack",0,100)},
        {"MoveSpeed",Rule("MoveSpeed","utility",0,100)},{"Critical",Rule("Critical","attack",0,100)},
        {"MPRegen",Rule("MPRegen","sustain",0,100)},
        {"MagicCritical",Rule("MagicCritical","magic",1,100)},{"MagicCriticalDamageRate",Rule("MagicCriticalDamageRate","magic",1,100)},
        {"MagicDamageBonus",Rule("MagicDamageBonus","magic",0,100)},{"MagicCostReduce",Rule("MagicCostReduce","magic-cost",1,10)},
        {"FollowerAttackSpeed",Rule("FollowerAttackSpeed","companion",1,100)},{"FollowerCritical",Rule("FollowerCritical","companion",1,100)},
        {"MPRegenMultiple",Rule("MPRegenMultiple","lake",1,100)},{"FinalMP",Rule("FinalMP","lake",1,100)},
        {"FlameSwordIgnoreDefense",Rule("FlameSwordIgnoreDefense","sword",2,10)},
        {"HighestElementalDamage",Rule("HighestElementalDamage","element",0,100)},
        {"FireDamage",Rule("FireDamage","element",0,100)},{"IceDamage",Rule("IceDamage","element",0,100)},{"LightningDamage",Rule("LightningDamage","element",0,100)},
        {"CooldownRecoverySpeed",Rule("CooldownRecoverySpeed","utility",0,100)},{"DashRecoverySpeed",Rule("DashRecoverySpeed","utility",0,100)},
        {"CriticalDamageRate",Rule("CriticalDamageRate","attack",0,100)},
        {"HPSteal",Rule("HPSteal","sustain",3,1)},
        {"FinalDamage",Rule("FinalDamage","attack",0,100)},{"FinalHP",Rule("FinalHP","guard",0,100)},
        {"Evasion",Rule("Evasion","guard",0,100)},{"TrueDamage",Rule("TrueDamage","physical",0,100)},
        {"FlameSwordDamage",Rule("FlameSwordDamage","sword",0,100)},{"FrostRelicDamage",Rule("FrostRelicDamage","frost",0,100)},
        {"DarkCloudDamage",Rule("DarkCloudDamage","cloud",0,100)},{"BurnDamage",Rule("BurnDamage","burn",0,100)},
        {"FreezeDamage",Rule("FreezeDamage","freeze",0,100)},
        {"FrostbiteDamage",Rule("FrostbiteDamage","frostbite",1,100)},{"ElectricDamage",Rule("ElectricDamage","electric",0,100)},
        {"FollowerDamage",Rule("FollowerDamage","companion",0,100)},{"FlameSwordCritical",Rule("FlameSwordCritical","sword",1,100)},
        {"FlameSwordFastFall",Rule("FlameSwordFastFall","sword-speed",1,100)},{"DarkCloudSpeed",Rule("DarkCloudSpeed","cloud-speed",1,200)},
        {"BurnSpeed",Rule("BurnSpeed","burn-speed",1,100)},{"ChargingCharmBonus",Rule("ChargingCharmBonus","frost-speed",1,100)},
        {"DarkCloudRestoreDuringBattle",Rule("DarkCloudRestoreDuringBattle","cloud-restore",2,100)},{"MinDarkCloud",Rule("MinDarkCloud","cloud-capacity",1,100)},
        {"BurnDuration",Rule("BurnDuration","burn-duration",2,100)},{"DebuffDamage",Rule("DebuffDamage","debuff",0,100)},
        {"DebuffDuration",Rule("DebuffDuration","debuff-duration",1,100)},{"FollowerDefense",Rule("FollowerDefense","companion-defense",1,100)},
        {"LeafDrop",Rule("LeafDrop","economy",0,100)},{"Negotiation",Rule("Negotiation","economy",1,100)},
        {"ChargingCharmAmplify",Rule("ChargingCharmAmplify","mechanism",3,1)},{"BurnStack",Rule("BurnStack","mechanism",3,1)},{"ElectricStack",Rule("ElectricStack","mechanism",3,1)},
        {"DashCount",Rule("DashCount","mechanism",3,1)},{"FlameSwordMax",Rule("FlameSwordMax","mechanism",3,1)}
    };
    private static string Rule(string type,string group,int rarity,int cap) { return group+","+SharedStatCatalog.RewardCost(type)+","+rarity+","+cap; }
    internal static bool IsSpecialized(string type) { return ForgeAffinity.Specialized.Contains(type); }
    // Check total native value, including equipment, so reconnecting cannot reset eligibility.
    internal static int MechanismLimit(string type)
    { return type=="ChargingCharmAmplify"||type=="DashCount"?1:type=="BurnStack"||type=="ElectricStack"||type=="FlameSwordMax"?2:0; }
    internal static int NativeLimit(string type)
    { int mechanism=MechanismLimit(type);return mechanism>0?mechanism:type=="MagicCostReduce"?50:type=="FlameSwordIgnoreDefense"?50:0; }
}
