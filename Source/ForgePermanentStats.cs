using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

// Compact records only AFTER the native delta was applied successfully. Never replay
// rewards or remove/reapply an accumulated bonus. Native removal reads the summed Value.
internal static class ForgePermanentStats
{
    private sealed class Cache {internal readonly Dictionary<string,StatusInstance> Records=new Dictionary<string,StatusInstance>(StringComparer.Ordinal);}
    private static ConditionalWeakTable<UnitAvatar,Cache> caches=new ConditionalWeakTable<UnitAvatar,Cache>();
    private static readonly FieldInfo listField=AccessTools.Field(typeof(UnitAvatar),"orphanedStatusInstancesServerside");
    private static readonly FieldInfo valueField=AccessTools.Field(typeof(StatusInstance),"value");
    private static readonly FieldInfo appliedField=AccessTools.Field(typeof(StatusInstance),"applied");
    private static readonly FieldInfo targetField=AccessTools.Field(typeof(StatusInstance),"<CurrentTarget>k__BackingField");
    private static Harmony harmony;
    private static bool failed;
    internal static void Install()
    {
        if(harmony!=null)return;
        if(listField==null || listField.FieldType!=typeof(List<StatusInstance>) || valueField==null || valueField.FieldType!=typeof(int) || appliedField==null || appliedField.FieldType!=typeof(bool) || targetField==null || targetField.FieldType!=typeof(UnitAvatar))
            throw new MissingFieldException("永久属性记录接口已变化，未启用压缩");
        var command=AccessTools.Method(typeof(UnitAvatar),"UserCode_CmdAddOrphanedStatusInstance__String");
        if(command==null)throw new MissingMethodException("原生永久属性请求入口已变化");
        harmony=new Harmony("local.sephiria.bodyforge.permanent");
        try { harmony.Patch(command,prefix:new HarmonyMethod(typeof(ForgePermanentStats),"BeforeRequest"),postfix:new HarmonyMethod(typeof(ForgePermanentStats),"AfterRequest")); }
        catch {Uninstall();throw;}
    }
    internal static void Apply(UnitAvatar player,string metadata)
    {
        var stat=StatusDatabase.CreateStatusEntity(metadata);
        player.AddOrphanedStatusInstance(stat);
        Compact(player,stat,metadata);
    }
    private static List<StatusInstance> Records(UnitAvatar player) {return (List<StatusInstance>)listField.GetValue(player);}
    private static void BeforeRequest(UnitAvatar __instance,out int __state)
    {
        __state=-1;
        try {if(__instance!=null && __instance.isServer && !failed)__state=Records(__instance).Count;}
        catch(Exception ex){StopOptimization(ex);}
    }
    private static void AfterRequest(UnitAvatar __instance,string __0,int __state)
    {
        if(__state<0 || __instance==null || !__instance.isServer)return;
        try
        {
            var records=Records(__instance);
            // A reentrant native callback may have added other records; leave those untouched.
            if(records.Count==__state+1)Compact(__instance,records[__state],__0);
        }
        catch(Exception ex){StopOptimization(ex);}
    }
    private static bool Eligible(StatusInstance stat,string id)
    {
        if(stat==null || stat.Value<=0 || stat.GetType().Assembly!=typeof(StatusInstance).Assembly)return false;
        var entity=StatusDatabase.GetStatusEntity(id);if(entity==null)return false;
        string rule=ForgeRecipeCatalog.RuleType(entity);
        if(!ForgeBalance.Rules.ContainsKey(rule))return false;
        // These alter resource ratios/clamps or floating-point movement state. Keep native order.
        if(rule=="MaxHP" || rule=="MaxMP" || rule=="FinalHP" || rule=="MoveSpeed")return false;
        string native=(entity.className??"").Split('/')[0];
        if(stat.GetType().Name!=native)return false;
        // Only plain base-class records: no extra event subscribers or mutable effect state.
        if(stat.GetType().BaseType!=typeof(StatusInstance))return false;
        if(AccessTools.Method(stat.GetType(),"SetTargetInner").DeclaringType!=typeof(StatusInstance))return false;
        foreach(var method in new[]{"ApplyStatusInner","RemoveStatusInner","SetTargetInner"})
            if(Harmony.GetPatchInfo(AccessTools.Method(stat.GetType(),method))!=null || Harmony.GetPatchInfo(AccessTools.Method(typeof(StatusInstance),method))!=null)return false;
        return true;
    }
    private static bool SameCustomKey(StatusInstance a,StatusInstance b)
    {
        var field=AccessTools.Field(a.GetType(),"customID");
        return field==null || Equals(field.GetValue(a),field.GetValue(b));
    }
    internal static void Compact(UnitAvatar player,StatusInstance added,string metadata)
    {
        if(harmony==null || failed || player==null || !player.isServer)return;
        try
        {
            string id=metadata.Split('/')[0];
            if(!Eligible(added,id) || !object.ReferenceEquals(targetField.GetValue(added),player) || !(bool)appliedField.GetValue(added))return;
            var list=Records(player);
            if(list.Count==0 || !object.ReferenceEquals(list[list.Count-1],added))return;
            var cache=caches.GetValue(player,p=>new Cache());StatusInstance saved;
            if(!cache.Records.TryGetValue(id,out saved) || saved==added || !object.ReferenceEquals(targetField.GetValue(saved),player) || !(bool)appliedField.GetValue(saved) ||
                !list.Contains(saved) || saved.GetType()!=added.GetType() || !SameCustomKey(saved,added))
            {cache.Records[id]=added;return;}
            long sum=(long)saved.Value+added.Value;
            if(sum>int.MaxValue)return; // Never overflow an existing cleanup record.
            valueField.SetValue(saved,(int)sum);
            // Transfer only the cleanup obligation. The new delta already reached the player.
            valueField.SetValue(added,0);appliedField.SetValue(added,false);added.ClearTarget();
            list.RemoveAt(list.Count-1);
        }
        catch(Exception ex){StopOptimization(ex);}
    }
    private static void StopOptimization(Exception ex)
    {
        if(failed)return;
        failed=true;caches=new ConditionalWeakTable<UnitAvatar,Cache>();
        Debug.LogError("[BodyForge] 永久属性记录压缩已停止；不会重发属性："+ex);
    }
    internal static void Uninstall()
    {
        if(harmony!=null)harmony.UnpatchSelf();harmony=null;failed=false;
        // Native records remain self-contained and can be removed without this Mod.
        caches=new ConditionalWeakTable<UnitAvatar,Cache>();
    }
}
