using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

// Optional bridge: no SP assembly reference, status registration or network writes.
// Contributions remain native custom stats and use native equipment/orphan cleanup.
internal static class ForgeSPCompatibility
{
    internal static readonly string[] PartyIDs={"ALL_PLAYER_DEFENSE","ALL_PLAYER_ALL_DAMAGE","ALL_PLAYER_ELEMENTAL_DAMAGE","ALL_PLAYER_CRIT"};
    private static readonly string[] keys={"STAT_ALLPLAYERDEFENSE","STAT_ALLPLAYERALLDAMAGE","STAT_ALLPLAYERELEMENTALDAMAGE","STAT_ALLPLAYERCRIT"};
    private static Harmony harmony;
    private static PropertyInfo config;
    private static PropertyInfo addCategory,addItem,modifyCharm;
    private static FieldInfo active,partyStats,partyEffects,dirty,statsValue;
    private static FieldInfo[] statFields;
    private static ConstructorInfo statsConstructor;
    private static Func<UnitAvatar,bool> highestRefresh;
    private static readonly int[] maximum=new int[4];
    private static readonly HashSet<string> liveNames=new HashSet<string>(StringComparer.Ordinal);
    private static readonly List<object> staleNames=new List<object>();
    private static HashSet<int> members=new HashSet<int>(),nextMembers=new HashSet<int>();
    private static float nextScan,nextPartyCheck;
    private static bool failed;
    internal static void Tick()
    {
        // The dungeon Update hook is absent after returning to the title screen.
        if(harmony!=null && PlayerSpawner.MultiplayerList.Count==0)BeforePartyUpdate();
        if(harmony!=null || failed || Time.unscaledTime<nextScan)return;
        nextScan=Time.unscaledTime+2;
        foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var mod=assembly.GetType("SPMod.StarSephiriaMod",false);
            if(mod==null)continue;
            var enabled=AccessTools.Field(mod,"_isPatched");
            if(enabled==null || !(bool)enabled.GetValue(null))continue;
            try
            {
                active=enabled;
                var configType=assembly.GetType("ModConfig",true);
                config=AccessTools.Property(configType,"Instance");
                var dataType=config.PropertyType;
                addCategory=AccessTools.Property(dataType,"AddCategory");addItem=AccessTools.Property(dataType,"AddItem");modifyCharm=AccessTools.Property(dataType,"ModifyCharm");
                if(addCategory==null || addItem==null || modifyCharm==null)throw new MissingMemberException("SP 配置接口已变化");
                partyStats=AccessTools.Field(mod,"PartyStats");
                partyEffects=AccessTools.Field(mod,"PartyEffect");
                if(partyEffects==null || !typeof(IDictionary).IsAssignableFrom(partyEffects.FieldType))throw new MissingFieldException("SP 派对记录接口已变化");
                dirty=partyStats.FieldType.GetField("Item1");statsValue=partyStats.FieldType.GetField("Item2");
                statsConstructor=statsValue.FieldType.GetConstructor(new[]{typeof(int),typeof(int),typeof(int),typeof(int)});
                if(statsConstructor==null)throw new MissingMethodException("SP 派对状态构造接口已变化");
                statFields=new FieldInfo[4];string[] names={"Defense","AllDamage","ElementalDamage","Crit"};
                for(int i=0;i<4;i++) { statFields[i]=statsValue.FieldType.GetField(names[i]);if(statFields[i]==null)throw new MissingFieldException(names[i]); }
                var prefix=AccessTools.Method(assembly.GetType("SPMod.StarSephiriaPatches+UnitAvatar_Patches",true),"UpdateHighestElementalBonus_Prefix");
                var maximum=AccessTools.Method(assembly.GetType("SPMod.Tool.Tools",true),"GetMaxPlayerStatus");
                var dungeon=AccessTools.Method(assembly.GetType("SPMod.DungeonPatchs+DungeonManager_Patch",true),"Update_Postfix");
                if(prefix==null || prefix.ReturnType!=typeof(bool) || maximum==null || maximum.ReturnType!=statsValue.FieldType || dungeon==null ||
                    dungeon.ReturnType!=typeof(void) || dungeon.GetParameters().Length!=1 || dungeon.GetParameters()[0].ParameterType!=typeof(DungeonManager))
                    throw new MissingMethodException("SP 派对接口已变化");
                harmony=new Harmony("local.sephiria.bodyforge.sp");
                // Patch SP's method itself, covering both Harmony and its direct Update call.
                harmony.Patch(prefix,prefix:new HarmonyMethod(typeof(ForgeSPCompatibility),"HighestAuthority"));
                highestRefresh=(Func<UnitAvatar,bool>)Delegate.CreateDelegate(typeof(Func<UnitAvatar,bool>),prefix);
                harmony.Patch(dungeon,prefix:new HarmonyMethod(typeof(ForgeSPCompatibility),"BeforePartyUpdate"));
                Debug.Log("[BodyForge] SP 羁绊联动与派对同步兼容已安装");
            }
            catch(Exception ex)
            {
                if(harmony!=null)harmony.UnpatchSelf();harmony=null;failed=true;
                Debug.LogError("[BodyForge] SP 联动接口不兼容，已关闭新增联动："+ex);
            }
            return;
        }
    }
    private static bool Config(PropertyInfo property)
    { var instance=config==null?null:config.GetValue(null,null);return instance!=null && property!=null && (bool)property.GetValue(instance,null); }
    internal static bool CategoryAvailable(string category)
    {
        if(category!="FORTUNE" && category!="COMET" && category!="WEAPON" && category!="PARTY")return true;
        if(harmony==null || active==null || !(bool)active.GetValue(null))return false;
        var entity=ItemDatabase.FindItemCategory(category);if(entity==null)return false;
        if(category=="FORTUNE" || category=="COMET")return Config(addCategory);
        // WEAPON/PARTY exist as disabled vanilla assets; SP's added items use them.
        if(category=="WEAPON")return Config(addItem) || entity.isEnabled;
        if(!(Config(modifyCharm)||Config(addItem)))return false;
        for(int i=0;i<PartyIDs.Length;i++)
        {
            var stat=StatusDatabase.GetStatusEntity(PartyIDs[i]);
            if(stat==null || !string.Equals(stat.className,"StatusInstance_Custom/"+keys[i],StringComparison.OrdinalIgnoreCase) || Math.Max(1,stat.divideForDisplay)!=1)return false;
        }
        return true;
    }
    private static bool HighestAuthority(UnitAvatar __0,ref bool __result)
    {
        // The dictionary is server-authoritative, even for the locally owned client.
        if(__0!=null && __0.isServer)return true;
        __result=false;return false;
    }
    private static void CollectMaximum()
    {
        Array.Clear(maximum,0,maximum.Length);liveNames.Clear();nextMembers.Clear();int count=0;
        foreach(var spawner in PlayerSpawner.MultiplayerList)
        {
            var player=spawner==null?null:spawner.PlayerAvatar;if(player==null)continue;
            count++;
            nextMembers.Add(player.GetInstanceID());liveNames.Add(player.Name);
            for(int i=0;i<keys.Length;i++)maximum[i]=Math.Max(maximum[i],player.GetCustomStatUnsafe(keys[i]));
        }
        // Match SP 2.5.1's truncation and scaling; use live avatars, not name-keyed stale entries.
        float scale=count<=1?1f:2f/Math.Max(1,count-1);
        for(int i=0;i<maximum.Length;i++)maximum[i]=(int)(maximum[i]*scale);
    }
    private static void PruneDeparted()
    {
        var records=partyEffects.GetValue(null) as IDictionary;if(records==null)return;
        staleNames.Clear();
        foreach(object name in records.Keys)if(!(name is string) || !liveNames.Contains((string)name))staleNames.Add(name);
        foreach(object name in staleNames)records.Remove(name);
        staleNames.Clear();
    }
    private static bool BeforePartyUpdate()
    {
        if(Time.unscaledTime<nextPartyCheck)return false;nextPartyCheck=Time.unscaledTime+.25f;
        CollectMaximum();PruneDeparted();
        var tuple=partyStats.GetValue(null);var current=statsValue.GetValue(tuple);
        bool changed=current==null || !members.SetEquals(nextMembers);
        for(int i=0;i<maximum.Length;i++)if(current==null || (int)statFields[i].GetValue(current)!=maximum[i])changed=true;
        var swap=members;members=nextMembers;nextMembers=swap;nextMembers.Clear();
        if(changed)statsValue.SetValue(tuple,statsConstructor.Invoke(new object[]{maximum[0],maximum[1],maximum[2],maximum[3]}));
        if(changed || (bool)dirty.GetValue(tuple)){dirty.SetValue(tuple,false);partyStats.SetValue(null,tuple);}
        if(changed)foreach(var spawner in PlayerSpawner.MultiplayerList)
        {
            var player=spawner==null?null:spawner.PlayerAvatar;
            if(player!=null && player.isServer)highestRefresh(player);
        }
        // Replace SP's old full-table pass, instead of recalculating it in a postfix.
        return false;
    }
    internal static void Uninstall()
    {
        if(harmony!=null)harmony.UnpatchSelf();harmony=null;
        config=addCategory=addItem=modifyCharm=null;active=partyStats=partyEffects=dirty=statsValue=null;statFields=null;
        statsConstructor=null;highestRefresh=null;liveNames.Clear();staleNames.Clear();members.Clear();nextMembers.Clear();Array.Clear(maximum,0,maximum.Length);
        failed=false;nextScan=nextPartyCheck=0;
    }
}
