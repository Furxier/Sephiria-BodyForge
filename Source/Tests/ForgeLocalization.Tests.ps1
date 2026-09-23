$ErrorActionPreference='Stop'
$source=Get-Content (Join-Path $PSScriptRoot '../ForgeLocalization.cs') -Raw -Encoding UTF8
Add-Type -TypeDefinition ($source+@'
public sealed class LocalizationManager {
 public static LocalizationManager Instance;
 public string CurrentLanguage;
}
public static class ForgeLocalizationTests {
 static void Check(bool ok,string message){if(!ok)throw new System.Exception(message);}
 public static string Run(){
  Check(ForgeLocalization.Text("锻体收益")=="锻体收益","missing manager defaults to source");
  LocalizationManager.Instance=new LocalizationManager{CurrentLanguage="zh-TW"};
  Check(ForgeLocalization.Text("锻体收益：选择配方、查看详情、重新锻体")=="鍛體收益：選擇配方、查看詳情、重新鍛體","traditional UI");
  Check(ForgeLocalization.Text("永久附魔等级、伤害、乌云、恢复速度、经验掉落")=="永久附魔等級、傷害、烏雲、恢復速度、經驗掉落","heart labels");
  string rich="<link=太阳剑><color=#80E878>伤害 +30%</color></link><sprite=\"Keyword\" name=乌云>{VALUE}";
  string expected="<link=太阳剑><color=#80E878>傷害 +30%</color></link><sprite=\"Keyword\" name=乌云>{VALUE}";
  Check(ForgeLocalization.Text(rich)==expected,"rich text identifiers remain exact");
  Check(ForgeLocalization.Text("默认界面、鼠标、加载")=="預設介面、滑鼠、載入","Taiwan terminology");
  Check(ForgeLocalization.Text("鍛體傷害 +3% HP MP")=="鍛體傷害 +3% HP MP","already traditional and units");
  LocalizationManager.Instance.CurrentLanguage="zh-CN";
  Check(ForgeLocalization.Text("锻体收益")=="锻体收益","switch back to source");
  LocalizationManager.Instance.CurrentLanguage="en-US";
  Check(ForgeLocalization.Text(rich)==rich,"other languages unchanged");
  Check(ForgeLocalization.Text(null)==null && ForgeLocalization.Text("")=="","null and empty");
  return "PASS: zh-TW selection, live language reads, labels, Taiwan phrases, rich tags, units and fallback";
 }
}
'@)
[ForgeLocalizationTests]::Run()
