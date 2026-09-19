$ErrorActionPreference='Stop'
$source=Get-Content (Join-Path $PSScriptRoot '../ForgePanel.cs') -Raw -Encoding UTF8
$start=$source.IndexOf('    private float forgeRecoveryAt;')
$end=$source.IndexOf('    private bool ForgeBusy')
$methods=$source.Substring($start,$end-$start)
$prefix=@'
using System;
public static class Time { public static float realtimeSinceStartup; }
public static class Mathf { public static int CeilToInt(float n){return (int)Math.Ceiling(n);} }
public class FakeJob { public void ReconcileLateEnchant(){} }
public class RecoveryTests {
    private bool forgeBlocked,ForgeBusy;
    private FakeJob forgeJob;
    private int forgeMaterial=1,forgeTarget=2;
    private string forgeMessage;
'@
$suffix=@'
    public static string Run() {
        var p=new RecoveryTests(); p.forgeJob=new FakeJob();
        Time.realtimeSinceStartup=100; p.BlockForge();
        Time.realtimeSinceStartup=107; p.RecoverForge();
        if(!p.forgeBlocked || p.forgeJob==null) throw new Exception("Early recovery allowed");
        Time.realtimeSinceStartup=108; p.ForgeBusy=true; p.RecoverForge();
        if(!p.forgeBlocked) throw new Exception("Busy transaction unlocked");
        p.ForgeBusy=false; p.RecoverForge();
        if(p.forgeBlocked || p.forgeJob!=null || p.forgeMaterial!=-1 || p.forgeTarget!=-1)
            throw new Exception("Recovery did not discard old transaction and selection");
        p.RecoverForge();
        return "PASS: cooldown, busy guard, reset of failed transaction and selection, repeated recovery";
    }
}
'@
Add-Type -TypeDefinition ($prefix+$methods+$suffix)
[RecoveryTests]::Run()
