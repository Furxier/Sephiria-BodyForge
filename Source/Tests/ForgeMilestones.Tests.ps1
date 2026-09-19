$ErrorActionPreference='Stop'
$src=Get-Content (Join-Path $PSScriptRoot '../ForgeMilestones.cs') -Raw -Encoding UTF8
Add-Type -TypeDefinition ($src+@'
public static class MilestoneTests {
 static void Check(bool c,string m){if(!c)throw new System.Exception(m);}
 public static string Run(){
  var m=new ForgeMilestones();int sends=0,confirmed=0;
  System.Action send=()=>sends++,confirm=()=>confirmed++;
  m.Tick(9,40,0,true,send,confirm);Check(sends==0,"below milestone");
  m.Tick(12,40,1,false,send,confirm);Check(sends==0,"no dispatch during transaction");
  m.Tick(12,40,2,true,send,confirm);Check(sends==1&&m.Pending,"dispatch once");
  m.Tick(12,40,20,true,send,confirm);Check(sends==1&&!m.Settling(20),"timeout does not retry or lock forging forever");
  m.Tick(12,41,21,true,send,confirm);Check(m.Claimed==1&&confirmed==1,"late confirmation");
  m.Tick(12,41,22,true,send,confirm);Check(sends==1,"same milestone not repeated");
  m.Tick(35,41,23,true,send,confirm);m.Tick(35,42,24,true,send,confirm);
  m.Tick(35,42,25,true,send,confirm);m.Tick(35,43,26,true,send,confirm);
  Check(m.Claimed==3&&sends==3,"multiple milestones serial");
  m.Tick(40,120,27,true,send,confirm);Check(sends==3,"storage cap preserves debt");
  m.Tick(40,119,28,true,send,confirm);m.Tick(40,120,29,true,send,confirm);Check(m.Claimed==4,"debt settled when space available");
  Check(ForgeMilestones.Entitled(999)==10,"ten slot maximum");
  var ambiguous=new ForgeMilestones();ambiguous.Tick(10,40,0,true,send,confirm);ambiguous.Tick(10,42,1,true,send,confirm);
  Check(ambiguous.Ambiguous&&ambiguous.Claimed==0,"unexpected capacity not falsely claimed");
  var failure=new ForgeMilestones();failure.Tick(10,40,0,true,()=>{throw new System.Exception("uncertain");},confirm);
  failure.Tick(10,40,20,true,send,confirm);Check(failure.Pending&&failure.Claimed==0,"dispatch exception kept pending");
  return "PASS: milestone thresholds, partial progress, timeout, late confirmation, serial claims, cap, ambiguity and dispatch failures";
 }
}
'@)
[MilestoneTests]::Run()
