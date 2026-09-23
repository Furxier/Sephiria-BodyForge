using System;
using UnityEngine;

// BCL delegate contract: neither Mod references the other's assembly.
internal static class ForgeExternalBridge
{
    internal const string Key="Local.Sephiria.BodyForge.StartByRarity.v1";
    private static readonly Func<int,string> start=Start;
    internal static void Install(){AppDomain.CurrentDomain.SetData(Key,start);}
    internal static void Uninstall(){if(object.ReferenceEquals(AppDomain.CurrentDomain.GetData(Key),start))AppDomain.CurrentDomain.SetData(Key,null);}
    private static string Start(int rarity)
    {
        var panel=BodyForgePanel.Instance;
        return panel==null || !panel.isActiveAndEnabled?"锻体界面未就绪":panel.StartExternalForge(rarity);
    }
}

public sealed partial class BodyForgePanel
{
    internal string StartExternalForge(int rarity)
    {
        if(rarity<0 || rarity>4)return "请选择有效稀有度";
        if(!BodyForgeSettings.Current.Enabled)return "请先开启锻体 Mod";
        if(!Ready() || LocalPlayer()!=owner)return "请先进入自己的角色";
        EnsureForgeProgress();
        if(NativeActive || ForgeBusy || forgeModal!=null)return "请先完成或关闭当前锻体界面";
        if(forgeBlocked)return "上次锻体尚未解除锁定，请从背包锻体入口处理";
        if(milestones.Settling(Time.realtimeSinceStartup))return "里程碑奖励正在同步，请稍后重试";
        if(UIManager.Instance==null || !NativeForgeHooks.Installed)return "原生背包入口未就绪";
        var panel=UIManager.Instance.GetElement<UI_CharacterStatusPanel>();
        var holder=UIManager.Instance.GetElement<UI_MessageBoxHolder>();
        if(panel==null || holder==null || holder.HasOpenedBox || NativePickerBusy())return "请关闭确认框并放下正在拖动的道具";
        try
        {
            if(!panel.IsOpened)
            {
                if(owner.IsInBattle)return "请脱离战斗后再锻体";
                var stack=UIManager.Instance.CurrentControlStack;
                if(stack!=null && stack.Count>0)return "请先关闭其他游戏界面";
                panel.Open();
            }
            nextNativeScan=0;TickNativeUI();
            if(!NativeContextValid())return "请打开自己的普通背包并关闭商店等操作界面";
            RefreshForgePool();if(forgePool==null)return forgePoolText;
            string[] names={"普通","罕见","稀有","传奇","永恒"};
            // One persistent candidate set per tier; cancel/reopen cannot reset rerolls.
            var material=new ForgeRow {Complimentary=true,Instance=-100-rarity,Entity=-100-rarity,Quantity=1,
                Rarity=rarity,Count=ForgeTransaction.RewardCount(rarity),Name=names[rarity]+"锻体"};
            ClearRecipeSelection();nativeMaterialRow=material;nativeTargetRow=null;
            forgeMaterial=material.Instance;forgeTarget=-1;nativeGeneration++;
            nativeStep=material.Count==0?NativeStep.Choice:NativeStep.Target;
            nativeHint="无需材料 · "+names[rarity]+"锻体：请选择主背包中的附魔目标。";
            if(material.Count==0)ShowNativeConfirmation();
            return null;
        }
        catch(Exception ex){string error="联动启动失败："+ex.Message;CancelNative(error);return error;}
    }
}
