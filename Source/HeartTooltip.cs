using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine.UI;

internal static class HeartTooltip
{
    private static bool inHeartTooltip;
    private static int enchantmentLevel;
    private static string flavor;
    internal static string CurrentFlavor { get { return inHeartTooltip && flavor!=null?flavor:HeartProfiles.DefaultFlavor; } }
    private struct Scope { internal bool Active;internal int Level;internal string Flavor; }
    private static readonly HashSet<int> reported=new HashSet<int>();
    internal static void Install(Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(UI_CharmTooltip),"UpdateData"),
            prefix:new HarmonyMethod(typeof(HeartTooltip),"Begin"),
            postfix:new HarmonyMethod(typeof(HeartTooltip),"Label"),
            finalizer:new HarmonyMethod(typeof(HeartTooltip),"End"));
        harmony.Patch(AccessTools.Method(typeof(UI_CharmTierDisplay),"SetTier"),
            prefix:new HarmonyMethod(typeof(HeartTooltip),"LimitStars"));
    }
    internal static void Reset() { inHeartTooltip=false;enchantmentLevel=0;flavor=null;reported.Clear(); }
    private static bool Heart(object value)
    {
        var item=value as NewItemOwnInstance;
        if(item!=null)return item.EntityID==HeartProfiles.ItemID;
        var entity=value as ItemEntity;
        if(entity!=null)return entity.id==HeartProfiles.ItemID;
        var identity=value as IItemEntity;
        return identity!=null && identity.IEntityID==HeartProfiles.ItemID;
    }
    private static void Begin(object __0,out Scope __state)
    {
        __state=new Scope {Active=inHeartTooltip,Level=enchantmentLevel,Flavor=flavor};
        inHeartTooltip=BodyForgeSettings.Current.Enabled && Heart(__0);
        var item=__0 as NewItemOwnInstance;
        enchantmentLevel=inHeartTooltip && item!=null && item.Charm!=null?HeartEquipment.EnchantmentLevel(item.Charm):0;
        flavor=inHeartTooltip && item!=null?HeartEquipment.FlavorFor(item.Charm):null;
    }
    private static Exception End(Scope __state,Exception __exception,object __instance,object __0)
    {
        inHeartTooltip=__state.Active;enchantmentLevel=__state.Level;flavor=__state.Flavor;
        if(__exception!=null && __instance!=null)Diagnose(__instance,__0,__exception);
        return __exception;
    }
    private static void Diagnose(object tooltip,object data,Exception error)
    {
        // Bounded diagnostic only: preserve the original exception, never hide broken UI.
        try
        {
            var identity=data as IItemEntity;var owned=data as NewItemOwnInstance;
            var entity=data as ItemEntity;
            int id=owned!=null?owned.EntityID:entity!=null?entity.id:identity!=null?identity.IEntityID:-1;
            if(reported.Count>=32 || !reported.Add(id))return;
            if(entity==null && id>=0)entity=ItemDatabase.FindItemById(id);
            var missing=new List<string>();
            for(Type type=tooltip.GetType();type!=null;type=type.BaseType)
                foreach(var field in type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
                {
                    if(field.FieldType.IsValueType)continue;
                    var value=field.GetValue(tooltip);
                    if(value==null || (value is UnityEngine.Object && (UnityEngine.Object)value==null))missing.Add(field.Name);
                }
            UnityEngine.Debug.LogWarning("[BodyForge.TooltipDiagnostic] item="+id+" data="+(data==null?"null":data.GetType().FullName)
                +" entity="+(entity!=null)+" prefab="+(entity!=null && entity.resourcePrefab!=null)
                +" charm="+(entity!=null && entity.resourcePrefab!=null && entity.resourcePrefab.GetComponent<Charm_Basic>()!=null)
                +" missingFields="+string.Join(",",missing.ToArray())+" error="+error);
        }
        catch { /* Diagnostics must not replace the original failure. */ }
    }
    internal static int StarCount(int level,int capacity)
    { return (int)((long)Math.Max(0,Math.Min(HeartProfiles.MaxLevel,level))*Math.Max(0,capacity)/HeartProfiles.MaxLevel); }
    private static void LimitStars(ref int __0,ref int __1,ref int __2,ref int __3,Image[] ___starImages)
    {
        if(!inHeartTooltip || ___starImages==null)return;
        int capacity=Math.Min(HeartProfiles.MaxLevel,___starImages.Length);
        __0=capacity;__1=StarCount(enchantmentLevel,capacity);__2=0;__3=__1;
    }
    private static void Label(object __0,int __1,TMP_Text ___levelText)
    {
        if(!inHeartTooltip || ___levelText==null)return;
        var item=__0 as NewItemOwnInstance;
        int level=item!=null && item.Charm!=null?HeartEquipment.EnchantmentLevel(item.Charm):0;
        // Native text enumerates all 31 levels; retain exact level without wrapping that list.
        ___levelText.text="附魔等级 "+level+" · 成长上限 "+HeartProfiles.MaxLevel;
    }
}
