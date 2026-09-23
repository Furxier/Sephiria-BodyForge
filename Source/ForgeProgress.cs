using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public sealed partial class BodyForgePanel
{
    private const int RecipeCacheLimit=4096;
    private sealed class CachedRecipes { internal int Entity, Rerolls; internal ForgeRecipe[] Cards; internal Dictionary<string,int> Snapshot; internal bool Specialized; }
    private sealed class EarnedReward { internal ForgeReward Reward; internal int Amount,LastAmount,LastBatch,Order; }
    private readonly Dictionary<int,CachedRecipes> recipeCache=new Dictionary<int,CachedRecipes>();
    private readonly Dictionary<string,EarnedReward> earned=new Dictionary<string,EarnedReward>();
    private int earnedEnchants, consumedMaterials, completedForges, failedForges;
    private int externalForges;
    private ForgeMilestones milestones=new ForgeMilestones();
    private DungeonManager progressDungeon;
    private PlayerAvatar progressOwner;
    private GridInventory progressInventory;
    private int progressSession;
    private int progressEpoch;
    private long ledgerRevision;
    private ForgeRecipe chosenRecipe;
    private int chosenMaterial=-1, chosenTarget=-1;
    private ForgeTransaction accountedJob;
    private void EnsureForgeProgress()
    {
        var dungeon=DungeonManager.Instance;
        var inventory=owner==null?null:owner.Inventory;
        int session=dungeon==null?0:dungeon.sessionSerial;
        if(object.ReferenceEquals(progressDungeon,dungeon) && progressSession==session &&
            object.ReferenceEquals(progressOwner,owner) && object.ReferenceEquals(progressInventory,inventory))return;
        CancelNative("冒险已切换");StopForge("冒险已切换");CloseForgeModal();
        progressDungeon=dungeon; progressSession=session;progressOwner=owner;progressInventory=inventory;
        forgeJob=null;accountedJob=null;
        forgeBlocked=false;forgeRecoveryAt=0;
        progressEpoch++;
        recipeCache.Clear(); earned.Clear(); earnedEnchants=consumedMaterials=completedForges=failedForges=0;
        externalForges=0;
        milestones=new ForgeMilestones();
        ResetMilestoneChoice();
        ClearRecipeSelection();
    }
    private void ClearRecipeSelection() { chosenRecipe=null; chosenMaterial=chosenTarget=-1; }
    private ForgeRecipe[] RecipesFor(ForgeRow material)
    {
        EnsureForgeProgress();
        CachedRecipes cache;
        if(!recipeCache.TryGetValue(material.Instance,out cache) || cache.Entity!=material.Entity)
        {
            // Do not evict old entries: that would restore exhausted rerolls on dropped/reacquired items.
            if(cache==null && recipeCache.Count>=RecipeCacheLimit)
                throw new InvalidOperationException("本次冒险配方记录已满，请先锻体已生成配方的材料；未消耗道具");
            cache=new CachedRecipes { Entity=material.Entity,Rerolls=RerollLimit(material.Rarity),Snapshot=ForgeRecipeCatalog.Snapshot(owner),Specialized=BodyForgeSettings.Current.EnableSpecializedRewards };
            cache.Cards=ForgeRecipeCatalog.Generate(forgePool,material.Rarity,owner,random,EarnedStorage()<ForgeBalance.StorageLimit,cache.Snapshot,cache.Specialized);
            recipeCache[material.Instance]=cache;
        }
        return cache.Cards;
    }
    private static int RerollLimit(int rarity)
    { switch(rarity){case 1:return 1;case 2:return 2;case 3:return 3;case 4:return 5;default:return 0;} }
    private int RerollsLeft(ForgeRow material) { RecipesFor(material);return recipeCache[material.Instance].Rerolls; }
    private bool RerollRecipes(ForgeRow material)
    {
        if(RerollsLeft(material)<=0)return false;
        var cache=recipeCache[material.Instance];
        var cards=ForgeRecipeCatalog.Generate(forgePool,material.Rarity,owner,random,EarnedStorage()<ForgeBalance.StorageLimit,cache.Snapshot,cache.Specialized);
        cache.Cards=cards;cache.Rerolls--;ClearRecipeSelection();return true;
    }
    private bool RecipeAvailable(ForgeRecipe recipe)
    { foreach(var r in recipe.Rewards) if(!ForgeRecipeCatalog.Available(r,owner) || (r.Kind==7 && (owner.Inventory.CurrentInventoryStorage>=120 || EarnedStorage()>=ForgeBalance.StorageLimit)))return false;return true; }
    private int EarnedStorage()
    { return milestones.StorageClaimed; }
    private string StorageProgress()
    {
        int due=ForgeMilestones.Entitled(earnedEnchants)-milestones.Claimed;
        if(due>0)return "里程碑奖励待领取 "+due+" 次"+(milestones.Pending?"（正在核对）":"");
        return "下次三选一附魔进度 "+(earnedEnchants%10)+"/10";
    }
    private void TickMilestones()
    {
        if(!Ready() || ForgeBusy)return;
        if(forgeJob!=null)forgeJob.ReconcileLateEnchant();
        PollMilestoneChoice();
    }
    private void RecordReward(ForgeReward reward)
    {
        EarnedReward entry;
        if(!earned.TryGetValue(reward.Identity,out entry)) {entry=new EarnedReward {Reward=reward,Order=earned.Count};earned.Add(reward.Identity,entry);}
        entry.Amount=checked(entry.Amount+reward.Value);
        entry.LastAmount=entry.LastBatch==consumedMaterials?checked(entry.LastAmount+reward.Value):reward.Value;
        entry.LastBatch=consumedMaterials;
        ledgerRevision++;
    }
    private string[] ProgressLines()
    {
        var result=new List<string>();
        result.Add("消耗材料 "+(consumedMaterials-externalForges)+" 件 · 联动 "+externalForges+" 次    已确认附魔 +"+earnedEnchants);
        result.Add("完成 "+completedForges+" 次    中止 "+failedForges+" 次");
        result.Add("本次冒险锻体扩容 "+EarnedStorage()+" / "+ForgeBalance.StorageLimit+" 格");
        result.Add(StorageProgress());
        var keys=new List<string>(earned.Keys);keys.Sort(StringComparer.Ordinal);
        foreach(var key in keys){var e=earned[key];result.Add(e.Reward.DisplayTotal(e.Amount));}
        if(earned.Count==0)result.Add("尚无已确认的属性或背包奖励");
        return result.ToArray();
    }
    private void AccountFinishedJob()
    {
        if(forgeJob==null || forgeJob.Busy || accountedJob==forgeJob)return;
        accountedJob=forgeJob;
        if(forgeJob.Phase==ForgeTransaction.Stage.Done)completedForges++;else failedForges++;
    }
}
