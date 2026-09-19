using System;

// One request in flight. A timeout never resends an additive network operation.
internal sealed class ForgeMilestones
{
    internal int Claimed { get; private set; }
    internal bool Pending { get; private set; }
    internal bool Ambiguous { get; private set; }
    internal string Message="";
    private int expected;
    private float deadline;
    internal bool Settling(float now) { return Pending && !Ambiguous && now<deadline; }
    internal static int Entitled(int enchants) { return Math.Min(10,Math.Max(0,enchants)/10); }
    internal void Tick(int enchants,int storage,float now,bool allowDispatch,Action send,Action confirmed)
    {
        if(Ambiguous)return;
        if(Pending)
        {
            if(storage==expected)
            {
                Pending=false;Claimed++;Message="锻体扩容 +1 格已到账";confirmed();return;
            }
            if(storage>expected || storage<expected-1)
            { Ambiguous=true;Message="扩容期间背包容量出现额外变化，待核对；不会重复发送";return; }
            if(now>=deadline)Message="扩容请求尚未确认，保留待核对状态；不会重复发送";
            return;
        }
        if(Claimed>=Entitled(enchants))return;
        if(storage>=120){Message="背包已达 120 格，扩容资格保留";return;}
        if(!allowDispatch)return;
        expected=storage+1;deadline=now+8;Pending=true;Message="锻体扩容等待到账";
        // Pending is set before dispatch, including synchronous completion or exceptions.
        try { send(); } catch(Exception ex) { Message="扩容请求结果待核对："+ex.Message; }
    }
}
