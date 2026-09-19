$ErrorActionPreference='Stop'
$source=Get-Content (Join-Path $PSScriptRoot '../ForgeProgress.cs') -Raw -Encoding UTF8
$fake=@'
namespace UnityEngine {public static class Time {public static float realtimeSinceStartup=100;}}
public class DungeonManager { public static DungeonManager Instance=new DungeonManager(); public int sessionSerial=1; }
public class PlayerAvatar { public GridInventory Inventory=new GridInventory(); }
public class GridInventory { public int CurrentInventoryStorage=40; public void AddStorage(int n){CurrentInventoryStorage+=n;} }
internal class BodyForgeSettings {internal static BodyForgeSettings Current=new BodyForgeSettings();internal bool EnableSpecializedRewards=true,Enabled=true;}
internal static class ForgeAffinity {internal static System.Collections.Generic.HashSet<string> Specialized=new System.Collections.Generic.HashSet<string>();}
internal class ForgeReward {internal string Identity="HP";internal int Kind,Value=3;internal string DisplayTotal(int amount){return Identity+" +"+amount;} internal static ForgeReward Storage(){return new ForgeReward{Identity="Bag",Kind=7,Value=1};} }
internal class ForgeRecipe {internal ForgeReward[] Rewards=new[]{new ForgeReward()};}

internal class ForgeRecipeCatalog {
 internal static int Generated,SnapshotCalls; internal static System.Collections.Generic.Dictionary<string,int> LastSnapshot;
 internal static System.Collections.Generic.Dictionary<string,int> Snapshot(PlayerAvatar owner){SnapshotCalls++;return new System.Collections.Generic.Dictionary<string,int>{{"FROST",SnapshotCalls}};}
 internal static bool Available(ForgeReward r,PlayerAvatar owner){return true;}
 internal static ForgeRecipe[] Generate(object pool,int rarity,PlayerAvatar owner,System.Random random,bool storageEligible,System.Collections.Generic.Dictionary<string,int> snapshot,bool specialized){Generated++;LastSnapshot=snapshot;return new[]{new ForgeRecipe(),new ForgeRecipe(),new ForgeRecipe()};}
}
internal class ForgeTransaction {internal enum Stage {Done,Failed} internal Stage Phase=Stage.Done; internal bool Busy=false;internal void ReconcileLateEnchant(){} }
public sealed partial class BodyForgePanel {
    private class ForgeRow {internal int Instance=10,Entity=100,Rarity=0;}
    private PlayerAvatar owner=new PlayerAvatar();private object forgePool=new object();private System.Random random=new System.Random();
    private ForgeTransaction forgeJob;
    private bool ForgeBusy=false,forgeBlocked=false;private float forgeRecoveryAt=0;private bool Ready(){return owner!=null;}
    private void CancelNative(string reason){} private void StopForge(string reason){}private void CloseForgeModal(){}
    static void Check(bool ok,string why){if(!ok)throw new System.Exception(why);}
    public static string Run(){
        var p=new BodyForgePanel();p.EnsureForgeProgress();var row=new ForgeRow();
        var first=p.RecipesFor(row);p.ClearRecipeSelection();
        Check(!p.RerollRecipes(row),"white material has no rerolls");
        for(int rarity=1;rarity<=4;rarity++) {
            var rerollRow=new ForgeRow {Instance=100+rarity,Rarity=rarity};
            int limit=new[]{0,1,2,3,5}[rarity];
            var before=p.RecipesFor(rerollRow);
            var snapshot=ForgeRecipeCatalog.LastSnapshot;int calls=ForgeRecipeCatalog.SnapshotCalls;
            Check(p.RerollsLeft(rerollRow)==limit,"rarity reroll limit");
            for(int i=0;i<limit;i++)Check(p.RerollRecipes(rerollRow),"available reroll succeeds");
            Check(object.ReferenceEquals(snapshot,ForgeRecipeCatalog.LastSnapshot)&&calls==ForgeRecipeCatalog.SnapshotCalls,"rerolls retain first synergy snapshot");
            Check(!object.ReferenceEquals(before,p.RecipesFor(rerollRow)),"reroll replaces cards");
            p.ClearRecipeSelection();Check(p.RerollsLeft(rerollRow)==0 && !p.RerollRecipes(rerollRow),"cancel cannot restore exhausted rerolls");
            p.recipeCache.Remove(rerollRow.Instance);Check(p.RerollsLeft(rerollRow)==limit,"consumed stack refreshes reroll allowance");
        }
        Check(p.chosenRecipe==null && p.chosenMaterial==-1 && p.chosenTarget==-1,"selection reset");
        Check(object.ReferenceEquals(first,p.RecipesFor(row)),"cancel/reopen/target switch reuses candidates");
        p.owner.Inventory.CurrentInventoryStorage=120;
        Check(object.ReferenceEquals(first,p.RecipesFor(row)),"capacity change cannot reroll cached cards");
        var bag=new ForgeRecipe {Rewards=new[]{new ForgeReward {Kind=7}}};Check(!p.RecipeAvailable(bag),"cached bag reward disabled at cap");
        p.recipeCache.Remove(row.Instance);Check(!object.ReferenceEquals(first,p.RecipesFor(row)),"confirmed consumption advances stack candidates");
        p.owner.Inventory.CurrentInventoryStorage=40;
        p.earnedEnchants=100;for(int i=0;i<20;i++)p.TickMilestones();
        Check(p.EarnedStorage()==10&&p.owner.Inventory.CurrentInventoryStorage==50,"milestones grant exactly ten slots");
        p.TickMilestones();Check(p.EarnedStorage()==10,"no repeat expansion");
        p.RecordReward(new ForgeReward());p.RecordReward(new ForgeReward());
        Check(p.earned["HP"].Amount==6,"confirmed rewards aggregate");
        Check(p.earned["HP"].LastAmount==6,"same transaction delta aggregates");
        int order=p.earned["HP"].Order,epoch=p.progressEpoch;
        p.consumedMaterials++;p.RecordReward(new ForgeReward());
        Check(p.earned["HP"].Amount==9&&p.earned["HP"].LastAmount==3&&p.earned["HP"].LastBatch==p.consumedMaterials,"new transaction resets delta without resetting total");
        Check(p.earned["HP"].Order==order,"existing row retains insertion order");
        p.forgeJob=new ForgeTransaction();p.AccountFinishedJob();p.AccountFinishedJob();Check(p.completedForges==1,"completion count is idempotent");
        p.forgeBlocked=true;p.forgeRecoveryAt=999;
        DungeonManager.Instance.sessionSerial++;p.EnsureForgeProgress();
        Check(!p.forgeBlocked && p.forgeRecoveryAt==0,"new adventure cannot inherit failure lock from prior session");
        Check(p.progressEpoch!=epoch,"session epoch invalidates old scroll without retaining inventory reference");
        Check(p.earned.Count==0 && p.completedForges==0 && p.recipeCache.Count==0,"new adventure clears old totals and candidates");
        Check(p.forgeJob==null && p.accountedJob==null,"old transaction references released");
        p.RecordReward(new ForgeReward());p.RecipesFor(row);p.owner=new PlayerAvatar();p.EnsureForgeProgress();
        Check(p.earned.Count==0 && p.recipeCache.Count==0,"replacement avatar in same session clears old rewards");
        p.RecordReward(new ForgeReward());p.owner.Inventory=new GridInventory();p.EnsureForgeProgress();
        Check(p.earned.Count==0,"replacement inventory resets progress");
        p.RecordReward(new ForgeReward());p.owner=null;p.EnsureForgeProgress();
        Check(p.earned.Count==0 && p.progressOwner==null && p.progressInventory==null,"disconnect releases old context immediately");
        p.owner=new PlayerAvatar();p.EnsureForgeProgress();
        for(int i=0;i<RecipeCacheLimit;i++)p.RecipesFor(new ForgeRow {Instance=i+20000});
        var saved=p.RecipesFor(new ForgeRow {Instance=20000});bool full=false;
        try{p.RecipesFor(new ForgeRow {Instance=99999});}catch(System.InvalidOperationException){full=true;}
        Check(full&&p.recipeCache.Count==RecipeCacheLimit,"bounded cache refuses new candidates without eviction");
        Check(object.ReferenceEquals(saved,p.RecipesFor(new ForgeRow {Instance=20000})),"full cache retains existing cards and reroll state");
        p.recipeCache.Remove(20000);p.RecipesFor(new ForgeRow {Instance=99999});
        Check(p.recipeCache.Count==RecipeCacheLimit,"consumed cached material frees one slot");
        return "PASS: cached candidates, capacity revalidation, stack advancement, reward aggregation, session reset";
    }
}
'@
Add-Type -TypeDefinition ($source+((Get-Content (Join-Path $PSScriptRoot '../ForgeBalance.cs') -Raw -Encoding UTF8) -replace 'using System.Collections.Generic;','')+((Get-Content (Join-Path $PSScriptRoot '../ForgeMilestones.cs') -Raw -Encoding UTF8) -replace 'using System;','')+$fake) -WarningAction SilentlyContinue
[BodyForgePanel]::Run()
