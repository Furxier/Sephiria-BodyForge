$ErrorActionPreference = 'Stop'
$source = Get-Content (Join-Path $PSScriptRoot '../ForgeTransaction.cs') -Raw -Encoding UTF8
$tests = @'
internal sealed class FakeForgePort : IForgePort
{
    internal int Material, Level, ConsumeCalls, EnchantCalls, StatCalls;
    internal bool Valid=true, Auto=true, BadJournal, ThrowAfterConsume, Refreshed=true;
    internal int Closes;
    public bool EffectsReady { get { return Refreshed; } }
    public void Close() { Closes++; }
    internal int ConfirmedMaterials,ConfirmedLayers,ConfirmedRewards;
    public void ConfirmedConsume() { ConfirmedMaterials++; }
    public void ConfirmedEnchant() { ConfirmedLayers++; }
    public void ConfirmedStat(int index) { ConfirmedRewards++; }
    internal double Value;
    public void Validate() { if(!Valid) throw new System.Exception("invalid target"); }
    public int MaterialState() { return Material; }
    public int EnchantLevel { get { return Level; } }
    public double StatValue(int index) { return Value; }
    public double StatDelta(int index) { return 2; }
    public void Consume()
    { ConsumeCalls++; if(Auto) Material=1; if(ThrowAfterConsume) throw new System.Exception("uncertain send"); }
    public void Enchant() { EnchantCalls++; if(Auto) Level++; }
    public void ApplyStat(int index) { StatCalls++; if(Auto) Value+=2; }
    public void Record(string message) { if(BadJournal) throw new System.Exception("disk failure"); }
}
public static class ForgeTransactionTests
{
    private static void Check(bool condition,string name) { if(!condition) throw new System.Exception(name); }
    public static string Run()
    {
        int[] counts={0,1,2,3,5};
        for(int rarity=0;rarity<5;rarity++)
        {
            int count=ForgeTransaction.RewardCount(rarity);
            Check(count==counts[rarity],"rarity reward table");
            var p=new FakeForgePort(); var job=new ForgeTransaction(p,count,2);
            for(int t=0;t<80;t++) job.Tick(t);
            Check(job.Phase==ForgeTransaction.Stage.Done,"completed transaction");
            Check(p.ConsumeCalls==1 && p.EnchantCalls==count && p.StatCalls==2,"exactly once for each step");
            Check(p.Value==4 && job.Stats==2,"repeated random attribute accumulation");
            Check(p.Closes==1,"completion releases event subscriptions once");
            Check(p.ConfirmedMaterials==1 && p.ConfirmedLayers==count && p.ConfirmedRewards==2,"ledger receives only confirmed operations");
        }
        var zero=new FakeForgePort {Refreshed=false};var zeroJob=new ForgeTransaction(zero,0,2);
        zeroJob.Tick(0);zeroJob.Tick(1);
        Check(zeroJob.Phase==ForgeTransaction.Stage.Settle && zero.EnchantCalls==0 && zero.StatCalls==0,"zero enchant waits for consumed-item effects");
        zero.Refreshed=true;zeroJob.Tick(2);zeroJob.Tick(3);zeroJob.Tick(4);
        Check(zeroJob.Phase==ForgeTransaction.Stage.Done && zero.EnchantCalls==0 && zero.StatCalls==2,"zero enchant grants selected attributes only");
        var zeroTimeout=new FakeForgePort {Refreshed=false};var zeroWait=new ForgeTransaction(zeroTimeout,0,1);
        zeroWait.Tick(0);zeroWait.Tick(1);zeroWait.Tick(9);
        Check(zeroWait.Phase==ForgeTransaction.Stage.Failed && zeroTimeout.StatCalls==0 && zeroTimeout.EnchantCalls==0,"zero enchant refresh timeout never grants or enchants");
        var delayed=new FakeForgePort { Auto=false }; var wait=new ForgeTransaction(delayed,1);
        wait.Tick(0); wait.Tick(1); wait.Tick(2);
        Check(delayed.ConsumeCalls==1 && delayed.EnchantCalls==0,"wait for consumption before rewards");
        wait.Tick(8); wait.Tick(9);
        Check(wait.Phase==ForgeTransaction.Stage.Failed && delayed.ConsumeCalls==1,"consumption timeout has no retry");
        delayed.Material=1; wait.Tick(10);
        Check(delayed.StatCalls==0,"late consumption does not restart failed job");

        var moved=new FakeForgePort { Material=-1 }; var movedJob=new ForgeTransaction(moved,1);
        movedJob.Tick(0); Check(moved.ConsumeCalls==0,"moved material is not consumed");
        var changed=new FakeForgePort(); var changedJob=new ForgeTransaction(changed,2);
        changedJob.Tick(0); changed.Valid=false; changedJob.Tick(1);
        Check(changedJob.Phase==ForgeTransaction.Stage.Failed && changed.EnchantCalls==0,"target or connection change stops after consume");

        var disk=new FakeForgePort { BadJournal=true }; var diskJob=new ForgeTransaction(disk,1);
        diskJob.Tick(0); Check(disk.ConsumeCalls==0,"journal failure blocks consumption");
        var thrown=new FakeForgePort { ThrowAfterConsume=true }; var thrownJob=new ForgeTransaction(thrown,1);
        thrownJob.Tick(0); thrownJob.Tick(1);
        Check(thrown.ConsumeCalls==1 && thrown.EnchantCalls==0,"ambiguous send is not retried");

        var stopped=new FakeForgePort(); var stopJob=new ForgeTransaction(stopped,1);
        stopJob.Tick(0); stopJob.Stop("closed"); stopJob.Tick(1);
        Check(stopped.EnchantCalls==0,"closing stops subsequent operations");

        var stat=new FakeForgePort(); var statJob=new ForgeTransaction(stat,1);
        statJob.Tick(0); statJob.Tick(1); statJob.Tick(2);
        stat.Auto=false; statJob.Tick(3); statJob.Tick(4); statJob.Tick(11); statJob.Tick(12);
        Check(statJob.Phase==ForgeTransaction.Stage.Failed && stat.StatCalls==1,"attribute timeout never duplicates grant");
        Check(statJob.Enchants==1 && statJob.Stats==0,"partial result counts retained");

        var extra=new FakeForgePort { Auto=false }; var extraJob=new ForgeTransaction(extra,1);
        extraJob.Tick(0); extra.Material=1; extraJob.Tick(1); extraJob.Tick(2);
        extra.Level=2; extraJob.Tick(3);
        Check(extraJob.Phase==ForgeTransaction.Stage.Failed && extra.StatCalls==0,"unexpected enchant change stops");
        var fast=new FakeForgePort(); var fastJob=new ForgeTransaction(fast,8);
        fastJob.Tick(0); fastJob.Tick(.016f);
        Check(fast.EnchantCalls==1,"consume confirmation dispatches first enchant immediately");
        fastJob.Tick(.032f);
        Check(fast.EnchantCalls==2 && fastJob.Enchants==1,"confirmation dispatches next layer in same tick");
        for(int frame=3;frame<=9;frame++) fastJob.Tick(frame*.016f);
        Check(fastJob.Enchants==8 && fast.StatCalls==0,"no stats before equipment settlement");
        fast.Refreshed=false;
        fastJob.Tick(.16f); Check(fast.StatCalls==0,"must wait for native equipment refresh");
        fast.Refreshed=true;
        fastJob.Tick(.176f); Check(fast.StatCalls==1,"first reward immediately after refresh confirmation");
        fastJob.Tick(.192f); Check(fast.StatCalls==2 && fastJob.Stats==1,"confirmed reward immediately dispatches next");
        for(int frame=2;frame<=8;frame++) fastJob.Tick(.176f+frame*.016f);
        Check(fastJob.Phase==ForgeTransaction.Stage.Done && fast.StatCalls==8 && fast.EnchantCalls==8,"no fixed-time settlement delay");
        var missing=new FakeForgePort { Refreshed=false }; var missingJob=new ForgeTransaction(missing,1);
        missingJob.Tick(0); missingJob.Tick(.01f); missingJob.Tick(.02f); missingJob.Tick(8.1f); missingJob.Tick(9);
        Check(missingJob.Phase==ForgeTransaction.Stage.Failed && missing.StatCalls==0 && missing.Closes==1,"missing refresh times out and detaches without granting stats");
        var recipe=new FakeForgePort();var recipeJob=new ForgeTransaction(recipe,8,2);
        for(int t=0;t<40;t++)recipeJob.Tick(t);
        Check(recipeJob.Phase==ForgeTransaction.Stage.Done && recipe.EnchantCalls==8 && recipe.StatCalls==2,"recipe reward count independent of enchant rarity");
        Check(stat.ConfirmedRewards==0 && stat.ConfirmedLayers==1,"uncertain reward excluded from totals; partial enchant retained");
        statJob.ReconcileLateEnchant();
        Check(stat.ConfirmedLayers==1,"stat timeout cannot recount an already confirmed enchant");
        var late=new FakeForgePort { Auto=false }; var lateJob=new ForgeTransaction(late,1);
        lateJob.Tick(0);late.Material=1;lateJob.Tick(1);lateJob.Tick(10);
        Check(lateJob.Phase==ForgeTransaction.Stage.Failed,"enchant response times out");
        late.Level=1;lateJob.ReconcileLateEnchant();lateJob.ReconcileLateEnchant();
        Check(late.ConfirmedLayers==1 && lateJob.Enchants==1 && late.StatCalls==0 && late.EnchantCalls==1,"late enchant counted once without restarting or resending");
        return "PASS: five rarities, delayed sync, timeouts, moved material, invalid target, journal/send failure, stop, partial rewards, no duplicate grants";
    }
}
'@
Add-Type -TypeDefinition ($source + $tests)
[ForgeTransactionTests]::Run()
