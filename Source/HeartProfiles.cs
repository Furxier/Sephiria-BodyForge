using System;
using System.Collections.Generic;

// Equipment growth only. These values never enter permanent forging rewards.
internal static class HeartProfiles
{
    internal const int ItemID=1304, MaxLevel=15, LateUnlockLevel=13;
    internal static int BudgetBonus(int level) { return Math.Max(0,Math.Min(MaxLevel,level))*2; }
    internal const string DefaultFlavor="如果一直锻造的话...";
    internal static string Flavor(string category)
    {
        switch(category)
        {
            case "FORTUNE":return "命运是沉睡的奴隶。";
            case "COMET":return "我的钻头可是突破天际的钻头啊！";
            case "WEAPON":return "此身为剑所天成。";
            case "PARTY":return "因为我们是妖精的尾巴！";
            case "PLANET":return "我的征途是星辰大海！";
            case "MYSTIC":return "灰雾之上的神秘主宰。";
            case "ALCHEMY":return "人不付出牺牲，就无法得到任何回报。";
            case "STURDY":return "不要祈祷！祈祷会占用你的双手！";
            case "ELEMENTAL":return "绝对没问题的！";
            case "WINDSONG":return "选择你不会后悔的那一边吧。";
            case "GUARDIAN":return "我想守护朋友，也想守护这个世界！";
            case "PRECISION":return "真相永远只有一个！";
            case "SHADOW":return "……我相信你。";
            case "ACADEMY":return "魔法，就是在寻找的时候最有趣啊。";
            case "LAKE":return "用水好好反省一下吧！";
            case "FLAMESWORD":return "燃烧你的心！";
            case "FROST":return "端坐于霜天吧，冰轮丸！";
            case "DARKCLOUD":return "路飞一定会成为海贼王！";
            case "EMBER":return "谢谢你们爱我。";
            case "GLACIER":return "起舞吧，袖白雪！";
            case "MAGITECH":return "接下来，就是我个人的战斗了。";
            case "CURSE":return "我要成为新世界的神。";
            case "COMPANION":return "总有一天，我们一起去见它吧！";
            case "SAVVY":return "说谎时，重要的不是谎言的内容，而是为什么要说谎。";
            default:return DefaultFlavor;
        }
    }
    internal static int ComboCount(int level) { return level>=15?5:level>=13?4:level>=8?3:level>=3?2:1; }
    internal sealed class Stat
    {
        internal readonly string ID;
        internal readonly int[] Values;
        internal readonly SharedStatCatalog.Definition Definition;
        internal Stat(string id,params int[] nodes) { ID=id;Definition=SharedStatCatalog.Equipment(id);Values=Curve(nodes); }
    }
    internal sealed class Profile
    {
        internal readonly string Category,Name;
        internal readonly Stat[] Stats;
        internal Profile(string category,string name,params Stat[] stats)
        { Category=category;Name=name;Stats=stats; }
    }
    private static Stat S(string id,params int[] nodes) { return new Stat(id,nodes); }
    // Fixed tie order for artifacts with multiple categories; never adds profiles together.
    internal static readonly Profile[] All={
        new Profile("FORTUNE","命运",S("LUCK",0,1,3,6,8),S("TRUE_DAMAGE",1,1,4,9,12),S("CRITICAL",0,200,600,1300,1600),S("EVASION",0,100,500,1200,1600)),
        new Profile("COMET","彗星",S("DASH_ATTACK_DAMAGE",2,12,32,68,80),S("DASH_RECOVERY_SPEED",0,15,24,40,50),S("DASH_COUNT",0,0,1,2,3)),
        new Profile("WEAPON","锻造",S("FINAL_WEAPONDAMAGE",1,3,12,26,35),S("HIGHEST_ELEMENTAL_DAMAGE",0,1,3,6,8),S("BASIC_ATTACK_DAMAGE",0,3,11,24,32),S("SPECIAL_ATTACK_DAMAGE",0,3,11,24,32)),
        new Profile("PARTY","派对",S("ALL_PLAYER_DEFENSE",1,1,6,10,14),S("ALL_PLAYER_ALL_DAMAGE",0,1,4,9,12),S("ALL_PLAYER_ELEMENTAL_DAMAGE",0,0,1,3,4),S("ALL_PLAYER_CRIT",0,2,3,6,8)),
        new Profile("PLANET","行星",S("TRUE_DAMAGE",0,1,3,7,10),S("PLANET_ATTACK_SPEED",0,6,21,45,60),S("PLANET_DAMAGE",1,5,20,40,50),S("FINAL_DAMAGE",0,2,7,15,20)),
        new Profile("STURDY","坚固",S("PHYSICAL_DAMAGE",1,3,10,18,25),S("SPECIAL_ATTACK_DAMAGE",0,6,16,31,40),S("BASIC_ATTACK_DAMAGE",0,0,8,16,20),S("CRITICAL_DAMAGE_RATE",0,0,0,19,25)),
        new Profile("ELEMENTAL","元素",S("HIGHEST_ELEMENTAL_DAMAGE",1,2,10,21,28),S("MOVE_SPEED",0,3,4,9,12)),
        new Profile("WINDSONG","风之歌",S("ATTACK_SPEED",2,8,25,50,70),S("MOVE_SPEED",0,3,7,16,20),S("DASH_RECOVERY_SPEED",0,0,15,35,45)),
        new Profile("GUARDIAN","守护",S("DEFENSE",1,5,17,37,50),S("MAX_HP",0,10,36,76,100)),
        new Profile("PRECISION","精密",S("CRITICAL",100,200,900,1900,2500),S("CRITICAL_DAMAGE_RATE",0,14,43,93,125)),
        new Profile("SHADOW","影子",S("EVASION",100,400,1000,2200,3000),S("DASH_RECOVERY_SPEED",0,10,25,50,65),S("MOVE_SPEED",0,0,8,18,24)),
        new Profile("ACADEMY","学院",S("COOLDOWN_RECOVERY_SPEED",1,12,15,19,30),S("MP_REGEN",0,1,2,3,5),S("MAGIC_CRITICAL",0,0,1000,1300,2000),S("MAGIC_CRITICAL_DAMAGE_RATE",0,0,31,39,60)),
        new Profile("LAKE","湖泊",S("MAX_MP",2,7,22,46,60),S("MP_REGEN",0,1,1,3,5),S("MP_REGEN_MULTIPLE",0,0,7,15,20),S("MP_SKILL_DAMAGE",0,7,21,45,60)),
        new Profile("FLAMESWORD","太阳剑",S("FLAME_SWORD_DAMAGE",3,17,48,101,135),S("FIRE_DAMAGE",0,1,4,7,10),S("FLAME_SWORD_FAST_FALL",0,0,12,16,20),S("FLAME_SWORD_CRITICAL",0,0,0,8,10)),
        new Profile("FROST","冰霜武具",S("FROST_RELIC_DAMAGE",3,11,28,40,60),S("ICE_DAMAGE",0,2,7,10,15),S("CHARGING_CHARM_BONUS",0,0,11,16,24),S("CHARGING_CHARM_AMPLIFY",0,0,0,1,1)),
        new Profile("DARKCLOUD","乌云",S("DARK_CLOUD_DAMAGE",3,10,35,75,100),S("DARK_CLOUD_SPEED",0,17,59,127,170),S("DARK_CLOUD_RESTORE_DURING_BATTLE",0,3,10,22,30)),
        new Profile("EMBER","余烬",S("BURN_DAMAGE",3,11,29,45,65),S("FIRE_DAMAGE",0,3,9,13,20),S("BURN_SPEED",0,0,14,21,30),S("BURN_STACK",0,0,0,1,1)),
        new Profile("GLACIER","冰川",S("FREEZE_DAMAGE",3,8,25,35,60),S("ICE_DAMAGE",0,3,10,15,20),S("FROSTBITE_DAMAGE",0,3,15,20,30),S("FREEZE_THRESHOLD",0,0,0,1,1)),
        new Profile("MAGITECH","魔法科技",S("ELECTRIC_DAMAGE",3,11,37,54,80),S("LIGHTNING_DAMAGE",0,3,11,17,25),S("ELECTRIC_STACK",0,0,0,1,1)),
        new Profile("CURSE","诅咒",S("DEBUFF_DAMAGE",2,11,40,87,115),S("DEBUFF_DURATION",0,5,18,38,50)),
        new Profile("COMPANION","同伴",S("FOLLOWER_DAMAGE",3,11,40,80,105),S("FOLLOWER_DEFENSE",0,4,10,30,40),S("FOLLOWER_ATTACK_SPEED",0,4,18,38,52)),
        new Profile("SAVVY","谈判",S("LEAF_DROP",2,10,35,75,100),S("NEGOTIATION",0,2,7,15,20),S("EXP_DROP",0,3,13,30,40),S("FINAL_DAMAGE",0,3,9,18,25)),
        new Profile("MYSTIC","神秘",S("TRUE_DAMAGE",1,2,8,16,20),S("FINAL_DAMAGE",0,5,15,20,35),S("HP_STEAL",0,0,0,1,1)),
        new Profile("ALCHEMY","炼金",S("HIGHEST_ELEMENTAL_DAMAGE",1,1,4,7,10),S("MOVE_SPEED",0,2,4,6,8),S("CRITICAL",0,100,400,700,1000),S("CRITICAL_DAMAGE_RATE",0,2,10,14,20),S("FINAL_DAMAGE",0,2,7,14,20),S("HP_STEAL",0,0,0,1,1))
    };
    internal static Profile Select(IEnumerable<string> categories,Func<string,bool> available=null)
    {
        if(categories==null)return null;
        var set=new HashSet<string>(categories,StringComparer.Ordinal);
        foreach(var profile in All)if(set.Contains(profile.Category) && (available==null || available(profile.Category)))return profile;
        return null;
    }
    internal static int[] Curve(int[] nodes)
    {
        if(nodes==null || nodes.Length!=5)throw new ArgumentException("Five growth nodes required");
        int[] levels={0,3,8,13,15};var result=new int[MaxLevel+1];int unlock=0;
        while(unlock<5 && nodes[unlock]==0)unlock++;
        for(int level=0;level<=MaxLevel;level++)
        {
            if(unlock==5 || level<levels[unlock])continue;
            int segment=0;while(segment<3 && level>levels[segment+1])segment++;
            result[level]=nodes[segment]+(nodes[segment+1]-nodes[segment])*(level-levels[segment])/(levels[segment+1]-levels[segment]);
        }
        return result;
    }
}
