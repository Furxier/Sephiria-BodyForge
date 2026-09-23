$ErrorActionPreference='Stop'
$fake=@'
public static class LedgerRefreshTests {
 public static string Run(){
  var a=new ForgeLedgerRefresh {Width=780,Height=560,Revision=1,Message="ok",Details=false,Consumed=0,Enchants=0,Completed=0,Failed=0,Storage=0};
  for(int i=0;i<10000;i++)if(!a.Same(a))throw new System.Exception("idle ledger invalidated");
  foreach(var f in typeof(ForgeLedgerRefresh).GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)){
   object b=a;var t=f.FieldType;
   object v=t==typeof(float)?(object)123f:t==typeof(bool)?(object)true:t==typeof(long)?(object)2L:t==typeof(int)?(object)1:(object)"changed";
   f.SetValue(b,v);if(a.Same((ForgeLedgerRefresh)b))throw new System.Exception("missing invalidation: "+f.Name);
  }
  return "PASS: idle ledger snapshot and every resize/detail/reward/counter/message invalidation";
 }
}
'@
Add-Type -TypeDefinition ((Get-Content (Join-Path $PSScriptRoot '../ForgeLedgerRefresh.cs') -Raw)+$fake)
[LedgerRefreshTests]::Run()
