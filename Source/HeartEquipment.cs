using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

// Reuses the original StatusInstance component and native equipment removal.
// No additional NetworkBehaviour, RPC, permanent stat grant or worker thread.
internal static class HeartEquipment
{
    private sealed class State
    {
        internal Charm_StatusInstance Charm;
        internal Charm_StatusInstance.StatusGroup[] Original;
        internal int OriginalMax;
        internal HeartProfiles.Profile Profile;
        internal int Level=-1;
        internal SkillController Skills;
        internal int Extra;
        internal int CastCount() { return Extra; }
        internal void Unbind()
        {
            if(Skills!=null)Skills.OnGetMultipleCastCount-=CastCount;
            Skills=null;Extra=0;
        }
    }
    private static readonly Dictionary<Charm_StatusInstance,State> states=new Dictionary<Charm_StatusInstance,State>();
    private static readonly HashSet<Charm_Basic> resolving=new HashSet<Charm_Basic>();
    private static readonly MethodInfo update=AccessTools.Method(typeof(Charm_StatusInstance),"OnUpdatedLevel");
    private static readonly MethodInfo disable=AccessTools.Method(typeof(Charm_StatusInstance),"OnDisabledEffect");
    private static readonly FieldInfo writing=AccessTools.Field(typeof(GridInventory),"writePermission");
    private static Harmony harmony;
    private static Charm_StatusInstance prefab;
    private static int originalMax;
    private static bool restoring,lastEnabled;
    internal static void Install()
    {
        if(harmony!=null)return;
        harmony=new Harmony("local.sephiria.bodyforge.heart");
        try
        {
            Patch(typeof(Charm_StatusInstance),"OnEnabledEffect","BeforeApply","AfterApply");
            Patch(typeof(Charm_StatusInstance),"OnUpdatedLevel","BeforeApply","AfterApply");
            Patch(typeof(Charm_StatusInstance),"OnDisabledEffect",null,"Disabled");
            Patch(typeof(Charm_Basic),"OnCharmEffectRefreshed",null,"Refreshed");
            Patch(typeof(Charm_Basic),"OnDestroy","Destroyed",null);
            Patch(typeof(Charm_Basic),"GetItemCategory","Categories",null);
            harmony.Patch(AccessTools.Method(typeof(GridInventory),"SearchSetEffectInInventory"),transpiler:new HarmonyMethod(typeof(HeartEquipment),"CountCategories"));
            Patch(typeof(Charm_StatusInstance),"GetEffectStringCount","EffectCount",null);
            Patch(typeof(Charm_StatusInstance),"GetEffectString","EffectString",null);
            Patch(typeof(ItemEntity),"get_Context","Context",null);
            Patch(typeof(LocalizedString),"ToString","Flavor",null);
            HeartTooltip.Install(harmony);
            HorayModAPI.OnLoadItemDatabase+=DatabaseReady;
            lastEnabled=BodyForgeSettings.Current.Enabled;
            DatabaseReady();
        }
        catch { Uninstall();throw; }
    }
    private static void Patch(Type type,string name,string prefix,string postfix)
    {
        var method=AccessTools.Method(type,name);
        if(method==null)throw new MissingMethodException(type.Name,name);
        harmony.Patch(method,prefix:prefix==null?null:new HarmonyMethod(typeof(HeartEquipment),prefix),
            postfix:postfix==null?null:new HarmonyMethod(typeof(HeartEquipment),postfix));
    }
    private static void DatabaseReady()
    {
        var entity=ItemDatabase.FindItemById(HeartProfiles.ItemID);
        if(entity==null || entity.resourcePrefab==null)return;
        var next=entity.resourcePrefab.GetComponent<Charm_StatusInstance>();
        if(next==null)throw new InvalidOperationException("心之重担的原生组件已变化");
        if(prefab!=next)
        {
            if(prefab!=null)prefab.maxLevel=originalMax;
            prefab=next;originalMax=next.maxLevel;
        }
        prefab.maxLevel=BodyForgeSettings.Current.Enabled?HeartProfiles.MaxLevel:originalMax;
    }
    private static bool IsHeart(Charm_Basic charm)
    {
        if(charm==null)return false;
        if(charm==prefab)return true;
        var inventory=InventoryOf(charm);
        if(inventory==null)return false;
        var item=inventory.FindItem(charm.xIdx,charm.yIdx);
        return item!=null && item.EntityID==HeartProfiles.ItemID && item.Charm==charm;
    }
    private static GridInventory InventoryOf(Charm_Basic charm)
    {
        // Journal/wishing previews use asset components without a NetworkIdentity.
        // Inventory's getter resolves a Mirror SyncVar and is unsafe on those assets.
        return charm==null || charm.netIdentity==null?null:charm.Inventory;
    }
    private static HeartProfiles.Profile Resolve(Charm_Basic heart)
    {
        if(!resolving.Add(heart))return null;
        try { return ResolveInner(heart); }
        finally { resolving.Remove(heart); }
    }
    internal static string FlavorFor(Charm_Basic heart)
    {
        var profile=heart==null?null:Resolve(heart);
        return HeartProfiles.Flavor(profile==null?null:profile.Category);
    }
    private static HeartProfiles.Profile ResolveInner(Charm_Basic heart)
    {
        if(!BodyForgeSettings.Current.Enabled)return null;
        var inventory=InventoryOf(heart);
        if(inventory==null)return null;
        int x=heart.xIdx,y=heart.yIdx-1;
        var visited=new HashSet<ItemPosition>();
        visited.Add(new ItemPosition(heart.xIdx,heart.yIdx));
        // Original gold/blue needle offset traversal, without their direct-damage-only filter.
        while(x>=0 && y>=0 && x<inventory.Width && y<GridInventory.MaxHeight)
        {
            var pos=new ItemPosition((sbyte)x,(sbyte)y);
            if(!visited.Add(pos))return null;
            var item=inventory.FindItem(pos);
            if(item==null || item.Charm==null || item.EntityID==HeartProfiles.ItemID)return null;
            var needle=item.Charm as Charm_UpCharmDamage;
            if(needle==null)return HeartProfiles.Select(item.Charm.GetItemCategory());
            x+=needle.xOffset;y+=needle.yOffset;
        }
        return null;
    }
    private static bool Categories(Charm_Basic __instance,ref IEnumerable<string> __result)
    {
        if(restoring || !BodyForgeSettings.Current.Enabled || !IsHeart(__instance))return true;
        var profile=Resolve(__instance);
        __result=profile==null?new string[0]:new[]{profile.Category};
        return false;
    }
    internal static int ForgeBonus(UnitAvatar avatar)
    {
        if(!BodyForgeSettings.Current.Enabled || avatar==null || avatar.Inventory==null)return 0;
        int best=0;
        foreach(var item in avatar.Inventory.inventoryMatrix.Values)
            if(item!=null && item.EntityID==HeartProfiles.ItemID && item.Charm!=null && item.Charm.IsEffectEnabled)
                best=Math.Max(best,EnchantmentLevel(item.Charm));
        return best;
    }
    private static int ComboLevel(Charm_Basic charm)
    {
        // Native combo counting happens before tablet level bonuses. Use permanent
        // enchantment for a stable tier that cannot feed back through combo/tablet levels.
        var inventory=InventoryOf(charm);
        if(inventory==null)return 0;
        var item=inventory.FindItem(charm.xIdx,charm.yIdx);int level;
        return item!=null && DungeonManager.Instance!=null && int.TryParse(DungeonManager.Instance.GetGlobalItemStatValue(item.InstanceID,"Enchant"),out level)?level:0;
    }
    internal static int EnchantmentLevel(Charm_Basic charm)
    { return Math.Max(0,Math.Min(HeartProfiles.MaxLevel,ComboLevel(charm))); }
    private static IEnumerable<string> CountedCategories(Charm_Basic charm)
    {
        if(restoring || !BodyForgeSettings.Current.Enabled || !IsHeart(charm))return charm.GetItemCategory();
        var profile=Resolve(charm);
        if(profile==null)return new string[0];
        var result=new string[HeartProfiles.ComboCount(ComboLevel(charm))];
        for(int i=0;i<result.Length;i++)result[i]=profile.Category;
        return result;
    }
    private static IEnumerable<CodeInstruction> CountCategories(IEnumerable<CodeInstruction> instructions)
    {
        int replaced=0;
        foreach(var instruction in instructions)
        {
            var method=instruction.operand as MethodInfo;
            if(instruction.opcode==OpCodes.Callvirt && method!=null && method.DeclaringType==typeof(Charm_Basic) && method.Name=="GetItemCategory")
            { instruction.opcode=OpCodes.Call;instruction.operand=AccessTools.Method(typeof(HeartEquipment),"CountedCategories");replaced++; }
            yield return instruction;
        }
        if(replaced!=1)throw new InvalidOperationException("原生连击统计入口已变化，未启用重担修改");
    }
    private static State Configure(Charm_StatusInstance charm)
    {
        State state;
        if(!states.TryGetValue(charm,out state))
        {
            state=new State {Charm=charm,Original=charm.stats,OriginalMax=originalMax};
            states.Add(charm,state);
        }
        var profile=Resolve(charm);
        charm.maxLevel=BodyForgeSettings.Current.Enabled?HeartProfiles.MaxLevel:state.OriginalMax;
        int level=EnchantmentLevel(charm);
        if(profile!=state.Profile || level!=state.Level || charm.stats==state.Original)
        {
            state.Profile=profile;state.Level=level;
            if(profile==null)charm.stats=state.Original;
            else
            {
                var groups=new Charm_StatusInstance.StatusGroup[profile.Stats.Length];
                // Native callbacks index by tablet-boosted level. Every slot carries the
                // permanent-enchantment value so temporary levels cannot change the reward.
                for(int i=0;i<groups.Length;i++)
                {
                    var values=new int[HeartProfiles.MaxLevel+1];
                    for(int j=0;j<values.Length;j++)values[j]=profile.Stats[i].Values[level];
                    groups[i]=new Charm_StatusInstance.StatusGroup {
                        statusID=profile.Stats[i].ID,valuesByLevel=values,hideIfStatValueIsZero=true};
                }
                charm.stats=groups;
            }
        }
        return state;
    }
    private static void BeforeApply(Charm_StatusInstance __instance)
    {
        if(!restoring && __instance.isServer && IsHeart(__instance))Configure(__instance);
    }
    private static void AfterApply(Charm_StatusInstance __instance)
    {
        if(restoring || !__instance.isServer)return;
        State state;if(!states.TryGetValue(__instance,out state))return;
        state.Unbind();
        if(state.Profile!=null && state.Profile.Category=="ACADEMY" && __instance.IsEffectEnabled && state.Level>=25)
        {
            state.Skills=__instance.NetworkAvatar.GetComponent<SkillController>();
            if(state.Skills!=null){state.Extra=1;state.Skills.OnGetMultipleCastCount+=state.CastCount;}
        }
    }
    private static void Disabled(Charm_StatusInstance __instance)
    { State state;if(states.TryGetValue(__instance,out state))state.Unbind(); }
    private static void Refreshed(Charm_Basic __instance)
    {
        var charm=__instance as Charm_StatusInstance;
        if(restoring || charm==null || !charm.isServer || !IsHeart(charm))return;
        State previous;states.TryGetValue(charm,out previous);
        var oldProfile=previous==null?null:previous.Profile;
        int oldLevel=previous==null?-1:previous.Level;
        var state=Configure(charm);
        if(charm.IsEffectEnabled && (oldProfile!=state.Profile || oldLevel!=state.Level))
            update.Invoke(charm,new object[]{charm.EffectEnabledLevel,charm.EffectEnabledLevel});
    }
    private static void Destroyed(Charm_Basic __instance)
    {
        var charm=__instance as Charm_StatusInstance;
        if(object.ReferenceEquals(charm,null))return;
        State state;if(!states.TryGetValue(charm,out state))return;
        state.Unbind();
        try { if(charm.isServer)disable.Invoke(charm,null); }
        catch(Exception ex){Debug.LogError("[BodyForge] 心之重担销毁清理失败："+ex);}
        finally { states.Remove(charm); }
    }
    internal static void Tick()
    {
        if(harmony==null || lastEnabled==BodyForgeSettings.Current.Enabled)return;
        lastEnabled=BodyForgeSettings.Current.Enabled;DatabaseReady();
        foreach(var state in new List<State>(states.Values))if(state.Charm!=null)Refreshed(state.Charm);
        RefreshInventories();
    }
    private static void RefreshInventories()
    {
        var inventories=new HashSet<GridInventory>();
        foreach(var state in states.Values)
            if(state.Charm!=null && state.Charm.isServer && state.Charm.Inventory!=null)inventories.Add(state.Charm.Inventory);
        foreach(var inventory in inventories)RefreshInventory(inventory);
    }
    private static void RefreshInventory(GridInventory inventory)
    {
        // An active native inventory transaction will recalculate categories when it exits.
        if(inventory==null || writing==null || (bool)writing.GetValue(inventory))return;
        using(new GridInventory.Permission(inventory)) { }
    }
    internal static void Uninstall()
    {
        HorayModAPI.OnLoadItemDatabase-=DatabaseReady;
        restoring=true;
        var inventories=new HashSet<GridInventory>();
        try
        {
            foreach(var state in states.Values)
            {
                state.Unbind();var charm=state.Charm;if(charm==null)continue;
                if(charm.isServer && charm.Inventory!=null)inventories.Add(charm.Inventory);
                try
                {
                    if(charm.isServer)disable.Invoke(charm,null);
                    charm.stats=state.Original;charm.maxLevel=state.OriginalMax;
                    if(charm.isServer && charm.IsEffectEnabled)update.Invoke(charm,new object[]{0,0});
                }
                catch(Exception ex){Debug.LogError("[BodyForge] 心之重担清理失败："+ex);}
            }
            states.Clear();if(prefab!=null)prefab.maxLevel=originalMax;prefab=null;
            HeartTooltip.Reset();
            resolving.Clear();
            if(harmony!=null)harmony.UnpatchSelf();harmony=null;
            foreach(var inventory in inventories)
                try { RefreshInventory(inventory); } catch(Exception ex){Debug.LogError("[BodyForge] 重担连击清理失败："+ex);}
        }
        finally { restoring=false; }
    }
    private static bool Context(ItemEntity __instance,ref string __result)
    {
        if(!BodyForgeSettings.Current.Enabled || __instance.id!=HeartProfiles.ItemID)return true;
        __result=HeartTooltip.CurrentFlavor;return false;
    }
    private static bool Flavor(LocalizedString __instance,ref string __result)
    {
        if(!BodyForgeSettings.Current.Enabled || __instance.key!="Item_MindBurden_FlavorText")return true;
        __result=HeartTooltip.CurrentFlavor;return false;
    }
    private static bool EffectCount(Charm_StatusInstance __instance,ref int __result)
    {
        if(!BodyForgeSettings.Current.Enabled || !IsHeart(__instance))return true;
        var profile=Resolve(__instance);
        __result=2+(profile==null?0:profile.Stats.Length+(profile.Category=="ACADEMY"?1:0));return false;
    }
    private static bool EffectString(Charm_StatusInstance __instance,int __0,int __1,ref string __result)
    {
        if(!BodyForgeSettings.Current.Enabled || !IsHeart(__instance))return true;
        var profile=Resolve(__instance);int level=EnchantmentLevel(__instance);
        if(__0==0)__result=profile==null?"指向上方神器，随其羁绊切换自身属性及连击。成长上限 30 级。":"当前适配："+profile.Name+" · 连击 +"+HeartProfiles.ComboCount(ComboLevel(__instance))+"（按附魔等级）";
        else if(__0==1)__result="锻体属性预算 +"+level+"%";
        else if(profile!=null && __0<=profile.Stats.Length+1)
        {
            var stat=profile.Stats[__0-2];int value=stat.Values[level];
            __result=value==0?null:StatusDatabase.CreateStatusEntity(stat.ID,value).ToString(true,false,true,Color.white);
        }
        else __result=profile!=null && profile.Category=="ACADEMY" && level>=25?"魔法书额外释放 +1 次":null;
        return false;
    }
}
