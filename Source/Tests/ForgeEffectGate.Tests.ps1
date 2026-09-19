$ErrorActionPreference='Stop'
$source=Get-Content (Join-Path $PSScriptRoot '../ForgeEffectGate.cs') -Raw -Encoding UTF8
$tests=@'
public static class EffectGateTests {
    static void Check(bool ok,string name){if(!ok)throw new System.Exception(name);}
    public static string Run(){
        var g=new ForgeEffectGate(); var v=new double[]{10,20};
        g.Request(); Check(!g.Ready(true,0,v),"server must see native refresh");
        g.Refreshed(); Check(g.Ready(true,0,v),"server can proceed immediately");
        Check(!g.Ready(false,1,v),"client needs snapshot");
        Check(!g.Ready(false,1,v),"same-frame polling cannot advance stability");
        Check(!g.Ready(false,2,v),"client needs two stable transitions");
        v[0]=11; Check(!g.Ready(false,3,v),"late synced attribute resets stability");
        Check(!g.Ready(false,4,v),"one stable transition");
        Check(g.Ready(false,5,v),"stable across frames");
        g.Request(); Check(!g.Ready(false,6,v),"new request invalidates old refresh");
        g.Refreshed(); g.Ready(false,7,v); g.Ready(false,8,v);
        g.Refreshed(); Check(!g.Ready(false,9,v),"additional refresh invalidates samples");
        g.Ready(false,10,v); Check(g.Ready(false,11,v),"recovers after later refresh");
        v[0]=double.NaN; Check(!g.Ready(false,12,v),"invalid snapshot rejected");
        g.Refreshed();g.Refreshed();g.Request();
        Check(!g.Ready(true,13,new double[]{1}),"surplus old refreshes cannot confirm a future request");
        g.Refreshed();Check(g.Ready(true,14,new double[]{1}),"new request requires fresh notification");
        return "PASS: refresh gating, host immediate completion, separate frames, delayed stats, resampling, invalid values";
    }
}
'@
Add-Type -TypeDefinition ($source+$tests)
[EffectGateTests]::Run()
