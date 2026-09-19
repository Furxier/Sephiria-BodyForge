using System.Collections.Generic;

internal static class ForgeBalance
{
    internal const int StorageLimit=10;
    // group, price in half-points, minimum rarity, per-card unit cap.
    // Utility/recovery prices are initial tuning values, not measured equivalence.
    internal static readonly Dictionary<string,string> Rules=new Dictionary<string,string> {
        {"PhysicalDamage","physical,16,0,100"},
        {"BasicAttackDamage","weapon,5,0,100"},{"SpecialAttackDamage","weapon,5,0,100"},
        {"MaxHP","guard,4,0,100"},{"MaxMP","sustain,4,0,100"},
        {"Defense","guard,8,0,100"},{"AttackSpeed","attack,6,0,100"},
        {"MoveSpeed","utility,10,0,100"},{"Critical","attack,12,0,100"},
        {"MPRegen","sustain,8,0,100"},
        {"MagicCritical","magic,8,1,100"},{"MagicCriticalDamageRate","magic,3,1,100"},
        {"MagicDamageBonus","magic,5,0,100"},{"MagicCostReduce","magic-cost,8,1,10"},
        {"FollowerAttackSpeed","companion,5,1,100"},{"FollowerCritical","companion,8,1,100"},
        {"MPRegenMultiple","lake,8,1,100"},{"FinalMP","lake,6,1,100"},
        {"FlameSwordIgnoreDefense","sword,10,2,10"},
        {"HighestElementalDamage","element,24,0,100"},
        {"FireDamage","element,12,0,100"},{"IceDamage","element,12,0,100"},{"LightningDamage","element,12,0,100"},
        {"CooldownRecoverySpeed","utility,6,0,100"},{"DashRecoverySpeed","utility,4,0,100"},
        {"CriticalDamageRate","attack,4,0,100"},
        {"HPSteal","sustain,0,3,1"},
        {"FinalDamage","attack,8,0,100"},{"FinalHP","guard,4,0,100"},
        {"Evasion","guard,10,0,100"},{"TrueDamage","physical,20,0,100"},
        {"FlameSwordDamage","sword,6,0,100"},{"FrostRelicDamage","frost,6,0,100"},
        {"DarkCloudDamage","cloud,4,0,100"},{"BurnDamage","burn,4,0,100"},
        {"FreezeDamage","freeze,4,0,100"},{"ElectricDamage","electric,4,0,100"},
        {"FollowerDamage","companion,4,0,100"},{"FlameSwordCritical","sword,8,1,100"},
        {"FlameSwordFastFall","sword-speed,4,1,100"},{"DarkCloudSpeed","cloud-speed,4,1,100"},
        {"BurnSpeed","burn-speed,4,1,100"},{"ChargingCharmBonus","frost-speed,6,1,100"},
        {"DarkCloudRestoreDuringBattle","cloud-restore,6,2,100"},{"MinDarkCloud","cloud-capacity,16,1,100"},
        {"BurnDuration","burn-duration,4,2,100"},{"DebuffDamage","debuff,6,0,100"},
        {"DebuffDuration","debuff-duration,6,1,100"},{"FollowerDefense","companion-defense,6,1,100"},
        {"LeafDrop","economy,6,0,100"},{"Negotiation","economy,12,1,100"},
        {"ChargingCharmAmplify","mechanism,80,3,1"},{"BurnStack","mechanism,72,3,1"},{"ElectricStack","mechanism,72,3,1"},
        {"DashCount","mechanism,80,3,1"},{"FlameSwordMax","mechanism,72,3,1"}
    };
    internal static bool IsSpecialized(string type) { return ForgeAffinity.Specialized.Contains(type); }
    // Check total native value, including equipment, so reconnecting cannot reset eligibility.
    internal static int MechanismLimit(string type)
    { return type=="ChargingCharmAmplify"||type=="DashCount"?1:type=="BurnStack"||type=="ElectricStack"||type=="FlameSwordMax"?2:0; }
    internal static int NativeLimit(string type)
    { int mechanism=MechanismLimit(type);return mechanism>0?mechanism:type=="MagicCostReduce"?50:type=="FlameSwordIgnoreDefense"?50:0; }
}
