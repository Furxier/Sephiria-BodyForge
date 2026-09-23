using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class BodyForgePanel
{
    private sealed class ForgeRow
    {
        internal ItemPosition Position;
        internal int Instance, Entity, Quantity, Count, Rarity;
        internal bool Target;
        internal bool Complimentary;
        internal int SubBag=-1;
        internal string Name, Text;
        internal Color Color;
    }
    private readonly List<ForgeRow> forgeRows=new List<ForgeRow>();
    private List<ForgeReward> forgePool;
    private int[] forgeCounts={ForgeTransaction.RewardCount(0),ForgeTransaction.RewardCount(1),
        ForgeTransaction.RewardCount(2),ForgeTransaction.RewardCount(3),ForgeTransaction.RewardCount(4)};
    private string forgePoolText="点击刷新，读取原生属性定义。", forgeMessage="先选材料与目标，再从三份配方中选择一份。";
    private string forgeResults="";
    private int forgeMaterial=-1, forgeTarget=-1;
    private bool forgeBlocked;
    private ForgeTransaction forgeJob;
    private int lastForgeRarity=-1;
    private float forgeRecoveryAt;
    private void BlockForge()
    { forgeBlocked=true; forgeRecoveryAt=Time.realtimeSinceStartup+8; }
    private void RecoverForge()
    {
        if(!forgeBlocked || ForgeBusy) return;
        if(Time.realtimeSinceStartup<forgeRecoveryAt)
        { forgeMessage="请等待 "+Mathf.CeilToInt(forgeRecoveryAt-Time.realtimeSinceStartup)+" 秒，让已发送请求完成同步。"; return; }
        if(forgeJob!=null)forgeJob.ReconcileLateEnchant();
        forgeBlocked=false; forgeJob=null; forgeMaterial=-1; forgeTarget=-1;
        forgeMessage="已解除锁定，可重新选择材料；旧请求不会重发，请核对上次实际奖励。";
    }
    private bool ForgeBusy { get { return forgeJob!=null && forgeJob.Busy; } }
    private void RefreshForgePool()
    {
        try
        {

            forgePool=ForgeRecipeCatalog.Load();
            var text=new System.Text.StringBuilder("配方可选奖励：");
            foreach(var reward in forgePool) { if(text.Length>12) text.Append("；"); text.Append(reward.Description); }
            forgePoolText=text.ToString();
        }
        catch(Exception ex) { forgePool=null; forgePoolText=ex.Message; }
    }
    private void RefreshForgeRows()
    {
        forgeRows.Clear();
        if(!Ready()) return;
        foreach(var pair in owner.Inventory.inventoryMatrix)
        {
            var instance=pair.Value;
            if(instance==null || instance.Quantity<=0) continue;
            var item=ItemDatabase.FindItemById(instance.EntityID);
            if(item==null || (item.type!=EItemType.Charm && item.type!=EItemType.StoneTablet)) continue;
            string name=Name(item);
            int count;
            int rarity=(int)item.rarity;
            count=rarity>=0 && rarity<forgeCounts.Length?forgeCounts[rarity]:-1;
            forgeRows.Add(new ForgeRow { Position=pair.Key,Instance=instance.InstanceID,Entity=instance.EntityID,
                Quantity=instance.Quantity,Count=count,Rarity=rarity,Target=instance.Charm!=null,Name=name,Color=ItemNameColor(item),
                Text=name+" ×"+instance.Quantity+"  ["+(pair.Key.x+1)+","+(pair.Key.y+1)+"]" });
        }
        foreach(var pair in owner.Inventory.subBagMatrix)
        {
            var instance=pair.Value;
            if(instance.quantity<=0)continue;
            var item=ItemDatabase.FindItemById(instance.entityID);
            if(item==null || (item.type!=EItemType.Charm && item.type!=EItemType.StoneTablet))continue;
            int rarity=(int)item.rarity;
            forgeRows.Add(new ForgeRow { SubBag=pair.Key,Instance=instance.instanceID,Entity=instance.entityID,
                Quantity=instance.quantity,Count=rarity>=0&&rarity<forgeCounts.Length?forgeCounts[rarity]:-1,
                Rarity=rarity,Target=false,Name=Name(item),Color=ItemNameColor(item) });
        }
        forgeRows.Sort((a,b)=>a.SubBag!=b.SubBag?a.SubBag.CompareTo(b.SubBag):a.Position.y==b.Position.y?a.Position.x.CompareTo(b.Position.x):a.Position.y.CompareTo(b.Position.y));
    }
    private ForgeRow ForgeSelected(int instance)
    {
        if(nativeMaterialRow!=null && nativeMaterialRow.Complimentary && nativeMaterialRow.Instance==instance)return nativeMaterialRow;
        foreach(var row in forgeRows) if(row.Instance==instance) return row;
        return null;
    }
    private void StartForge()
    {
        if(ForgeBusy || forgeBlocked || !BodyForgeSettings.Current.Enabled || !Ready()) return;
        if(milestones.Settling(Time.realtimeSinceStartup)) { forgeMessage="扩容正在同步，请稍后继续锻体。"; return; }
        try
        {
            RefreshForgeRows();
            var material=ForgeSelected(forgeMaterial); var target=ForgeSelected(forgeTarget);
            if(material==null || (material.Count!=0 && (target==null || material.Instance==target.Instance || !target.Target)))
                throw new InvalidOperationException("请选择不同的材料和目标神器");
            if(forgePool==null || forgePool.Count==0) throw new InvalidOperationException("未读取到有效属性池");
            int count=material.Count;
            if(count<0 || (count>0 && EnchantLevel(target.Instance)+count>10000)) throw new InvalidOperationException("稀有度或附魔层数不支持");
            int targetID=count==0?-1:target.Instance;
            if(chosenRecipe==null || chosenMaterial!=material.Instance || chosenTarget!=targetID)
            { OpenStandaloneRecipes(material,target);return; }
            if(!RecipeAvailable(chosenRecipe))throw new InvalidOperationException("所选奖励已达上限或已关闭，请改选其他已生成配方");
            var draws=chosenRecipe.Rewards;
            string result="本次所选配方：\n"+chosenRecipe.Text;
            var player=owner;
            var port=new ForgePort(player,material.Position,material.Complimentary?null:material.SubBag<0?player.Inventory.inventoryMatrix[material.Position]:null,
                count==0?default(ItemPosition):target.Position,targetID,draws,()=>owner==player && LocalPlayer()==player,
                ()=>{consumedMaterials++;if(material.Complimentary)externalForges++;recipeCache.Remove(material.Instance);},
                ()=>earnedEnchants++,RecordReward,count>0,
                index=>ForgeRecipeCatalog.Available(draws[index],player) &&
                    (draws[index].Kind!=7 || EarnedStorage()<ForgeBalance.StorageLimit),
                material.SubBag,material.Instance,material.Quantity,material.Entity,material.Complimentary);
            port.Validate();
            lastForgeRarity=material.Complimentary?material.Rarity:-1;
            forgeResults=result; forgeJob=new ForgeTransaction(port,count,draws.Length,material.Complimentary);
            ClearRecipeSelection();
            forgeJob.Tick(Time.unscaledTime); forgeMessage=forgeJob.Message;
            if(forgeJob.Phase==ForgeTransaction.Stage.Failed) { BlockForge(); AccountFinishedJob(); }
        }
        catch(Exception ex) { forgeMessage="未开始锻体："+ex.Message; }
    }
    private void StopForge(string reason)
    {
        if(!ForgeBusy) return;
        forgeJob.Stop(reason); BlockForge(); forgeMessage=forgeJob.Message; AccountFinishedJob();
    }
    private void TickForge()
    {
        if(ForgeBusy)
        {
            forgeJob.Tick(Time.unscaledTime); forgeMessage=forgeJob.Message;
            if(forgeJob.Phase==ForgeTransaction.Stage.Failed) BlockForge();
            if(!forgeJob.Busy) { forgeMaterial=-1; AccountFinishedJob(); }
        }
    }
}
