using System;

// One explicit choice/request at a time. Uncertain additive requests are never retried.
internal sealed class ForgeMilestones
{
    internal int Claimed { get; private set; }
    internal int StorageClaimed { get; private set; }
    internal int Choice { get; private set; }
    internal bool Pending { get; private set; }
    internal bool Ambiguous { get; private set; }
    internal string Message="";
    private double before,expected;
    private float deadline;
    internal bool Settling(float now) { return Pending && !Ambiguous && now<deadline; }
    internal static int Entitled(int enchants) { return Math.Max(0,enchants)/10; }
    internal bool Choose(int enchants,int choice,double current,float now,Action send)
    {
        if(Pending || Ambiguous || Claimed>=Entitled(enchants) || choice<0 || choice>2 || double.IsNaN(current) || double.IsInfinity(current))return false;
        if(choice==0 && (StorageClaimed>=10 || current>=120))return false;
        if(choice==1 && current+3>100)return false;
        Choice=choice;before=current;expected=current+(choice==0?1:choice==1?3:10);deadline=now+8;
        Pending=true;Message="里程碑奖励等待到账";
        try{send();}catch(Exception ex){Message="奖励请求结果待核对："+ex.Message;}
        return true;
    }
    internal void Poll(double current,float now,Action confirmed)
    {
        if(!Pending || Ambiguous)return;
        if(double.IsNaN(current)||double.IsInfinity(current)){Ambiguous=true;Message="奖励属性无法核对，不会重复发放";return;}
        if(Math.Abs(current-expected)<.0001)
        {
            Pending=false;Claimed++;if(Choice==0)StorageClaimed++;
            Message="里程碑奖励已到账";confirmed();return;
        }
        if(Math.Abs(current-before)>.0001){Ambiguous=true;Message="奖励同步期间属性出现额外变化，待核对；不会重复发放";return;}
        if(now>=deadline)Message="奖励尚未确认，保留待核对状态；不会重复发放";
    }
}
