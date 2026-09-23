$ErrorActionPreference='Stop'
$src=Get-Content (Join-Path $PSScriptRoot '../ForgeMilestones.cs') -Raw -Encoding UTF8
Add-Type -TypeDefinition ($src+@'
public static class MilestoneTests {
 static void Check(bool c,string m){if(!c)throw new System.Exception(m);}
 public static string Run(){
  var m=new ForgeMilestones();int sends=0,confirmed=0;
  System.Action send=()=>sends++,confirm=()=>confirmed++;
  Check(!m.Choose(9,0,40,0,send)&&sends==0,"below milestone");
  Check(m.Choose(12,1,0,1,send),"explicit penetration choice");
  Check(!m.Choose(12,2,0,2,send)&&sends==1,"double click blocked");
  m.Poll(0,20,confirm);Check(sends==1&&!m.Settling(20)&&m.Pending,"timeout does not retry");
  m.Poll(3,21,confirm);Check(m.Claimed==1&&m.StorageClaimed==0&&confirmed==1,"late penetration confirmation");
  Check(!m.Choose(12,0,40,22,send),"same entitlement not repeated");
  Check(m.Choose(35,2,0,23,send),"lifesteal choice");m.Poll(10,24,confirm);
  Check(m.Choose(35,0,40,25,send),"storage choice");m.Poll(41,26,confirm);
  Check(m.Claimed==3&&m.StorageClaimed==1&&sends==3,"separate claim and storage counters");
  Check(!m.Choose(40,0,120,27,send)&&m.Choose(40,2,10,28,send),"full bag allows stats");m.Poll(20,29,confirm);
  Check(ForgeMilestones.Entitled(999)==99,"choices continue beyond ten rewards");
  var capped=new ForgeMilestones();for(int i=0;i<10;i++){Check(capped.Choose(200,0,40+i,0,send),"slot choice");capped.Poll(41+i,1,confirm);}
  Check(!capped.Choose(200,0,50,2,send)&&capped.Choose(200,1,0,2,send),"ten slot cap independent of stats");
  var over=new ForgeMilestones();Check(!over.Choose(10,1,98,0,send)&&!over.Pending,"penetration cap");
  var ambiguous=new ForgeMilestones();ambiguous.Choose(10,1,0,0,send);ambiguous.Poll(4,1,confirm);
  Check(ambiguous.Ambiguous&&ambiguous.Claimed==0,"unexpected change not falsely claimed");
  var failure=new ForgeMilestones();failure.Choose(10,2,0,0,()=>{throw new System.Exception("uncertain");});
  failure.Poll(0,20,confirm);Check(failure.Pending&&failure.Claimed==0,"dispatch exception pending");
  Check(!failure.Choose(20,0,40,21,send),"pending choice cannot be replaced");
  return "PASS: explicit choices, no duplicate rewards, separate storage cap, penetration cap, timeout, late sync and ambiguity";
 }
}
'@)
[MilestoneTests]::Run()
