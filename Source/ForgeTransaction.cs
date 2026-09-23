using System;

internal interface IForgePort
{
    void Validate();
    // 0: unchanged, 1: consumed exactly one, -1: moved or changed unexpectedly.
    int MaterialState();
    int EnchantLevel { get; }
    double StatValue(int index);
    double StatDelta(int index);
    bool EffectsReady { get; }
    void Close();
    void ConfirmedConsume();
    void ConfirmedEnchant();
    void ConfirmedStat(int index);
    void Consume();
    void Enchant();
    void ApplyStat(int index);
    void Record(string message);
}

internal sealed class ForgeTransaction
{
    internal enum Stage { New, Consume, Settle, Enchant, Stat, Done, Failed }
    internal Stage Phase = Stage.New;
    internal int Enchants, Stats;
    internal bool Busy { get { return Phase!=Stage.Done && Phase!=Stage.Failed; } }
    internal string Message="准备锻体";
    private readonly IForgePort port;
    private readonly int count, rewardCount;
    private float deadline;
    private int expectedEnchant;
    private double expectedStat;
    private bool pending;
    private readonly bool complimentary;
    internal ForgeTransaction(IForgePort port,int count)
        : this(port,count,count) { }
    internal ForgeTransaction(IForgePort port,int count,int rewardCount,bool complimentary=false)
    {
        if(count<0 || count>100) throw new ArgumentOutOfRangeException("count");
        if(rewardCount<1 || rewardCount>100) throw new ArgumentOutOfRangeException("rewardCount");
        this.port=port; this.count=count; this.rewardCount=rewardCount;
        this.complimentary=complimentary;
    }
    internal static int RewardCount(int rarity)
    {
        switch(rarity) { case 0:return 0; case 1:return 1; case 2:return 2; case 3:return 3; case 4:return 5; }
        throw new InvalidOperationException("未知稀有度，未吞噬道具");
    }
    internal void Stop(string reason)
    {
        if(!Busy) return;
        Phase=Stage.Failed;
        try { port.Close(); } catch { }
        Message=reason+"；已确认附魔 "+Enchants+"/"+count+"，配方奖励 "+Stats+"/"+rewardCount+"。不会重试。";
        try { port.Record(Message); } catch { }
    }
    internal void ReconcileLateEnchant()
    {
        if(Phase!=Stage.Failed || !pending || expectedEnchant<=0)return;
        try
        {
            port.Validate();int actual=port.EnchantLevel;
            if(actual==expectedEnchant){pending=false;Enchants++;port.ConfirmedEnchant();port.Record("late confirmed enchant "+Enchants);}
            else if(actual>expectedEnchant)pending=false;
        }
        catch { pending=false; }
    }
    internal void Tick(float now)
    {
        if(!Busy) return;
        try
        {
            port.Validate();
            if(Phase==Stage.New)
            {
                if(port.MaterialState()!=0) throw new InvalidOperationException("材料已变化");
                port.Record("dispatch consume");
                Phase=Stage.Consume; deadline=now+8; Message=complimentary?"准备联动锻体…":"等待确认吞噬一件材料…";
                port.Consume(); return;
            }
            if(Phase==Stage.Consume)
            {
                int state=port.MaterialState();
                if(state<0) throw new InvalidOperationException("材料位置或数量异常");
                if(state==1)
                {
                    // Enchantment does not read the attribute baseline. Only the
                    // final transition to stat rewards needs equipment settlement.
                    port.Record("confirmed consume"); Phase=count==0?Stage.Settle:Stage.Enchant;
                    port.ConfirmedConsume();
                    if(count==0)deadline=now+8;
                    Message=complimentary?(count==0?"等待属性稳定…":"开始联动附魔…"):(count==0?"材料已消耗，等待属性刷新…":"材料已消耗，开始附魔…");
                }
                else if(now>=deadline) throw new TimeoutException("吞噬结果未确认");
                if(Phase==Stage.Consume) return;
            }
            if(Phase==Stage.Settle)
            {
                if(!port.EffectsReady)
                {
                    if(now>=deadline) throw new TimeoutException("装备效果刷新未确认");
                    return;
                }
                Phase=Enchants<count?Stage.Enchant:Stage.Stat;
            }
            if(Phase==Stage.Enchant)
            {
                int level=port.EnchantLevel;
                if(pending)
                {
                    if(level==expectedEnchant)
                    {
                        pending=false; expectedEnchant=0; Enchants++; port.ConfirmedEnchant(); port.Record("confirmed enchant "+Enchants);
                        if(Enchants==count)
                        {
                            Phase=Stage.Settle; deadline=now+8;
                            Message="等待装备刷新通知及属性同步…";
                            return;
                        }
                    }
                    else if(level>expectedEnchant) throw new InvalidOperationException("附魔层数出现额外变化");
                    else if(now>=deadline) throw new TimeoutException("附魔结果未确认");
                    if(pending) return;
                }
                if(level<0 || level>=10000) throw new InvalidOperationException("附魔层数超出范围");
                expectedEnchant=level+1; port.Record("dispatch enchant "+expectedEnchant);
                pending=true; deadline=now+8; Message="附魔中："+Enchants+"/"+count;
                port.Enchant(); return;
            }
            if(Phase==Stage.Stat)
            {
                if(pending)
                {
                    double value=port.StatValue(Stats);
                    if(Math.Abs(value-expectedStat)<0.005)
                    {
                        pending=false; int confirmed=Stats++; port.ConfirmedStat(confirmed); port.Record("confirmed stat "+Stats);
                        if(Stats==rewardCount)
                        { Phase=Stage.Done; port.Close(); Message="锻体完成：附魔 +"+count+"，配方奖励 "+rewardCount+" 项。"; port.Record(Message); }
                    }
                    else if(now>=deadline) throw new TimeoutException("属性变化未确认（预期 "+expectedStat+"，实际 "+value+"）");
                    if(pending || !Busy) return;
                }
                expectedStat=port.StatValue(Stats)+port.StatDelta(Stats);
                if(double.IsNaN(expectedStat)||double.IsInfinity(expectedStat)||Math.Abs(expectedStat)>int.MaxValue)
                    throw new InvalidOperationException("属性数值超出范围");
                port.Record("dispatch stat "+Stats);
                pending=true; deadline=now+8; Message="配方奖励中："+Stats+"/"+rewardCount;
                port.ApplyStat(Stats);
            }
        }
        catch(Exception ex) { Stop(ex.Message); }
    }
}
