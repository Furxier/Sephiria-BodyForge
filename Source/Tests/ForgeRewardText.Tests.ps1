$ErrorActionPreference='Stop'
$s=Get-Content (Join-Path $PSScriptRoot '../ForgeRewards.cs') -Raw -Encoding UTF8
$method=[regex]::Match($s,'(?s)    private static string Describe\(.*?(?=    internal ForgeReward WithValue)').Value
if(-not $method){throw 'Display methods not found'}
$test=@'
using System;
public class StatusEntity {public int divideForDisplay=1;}
public class KeywordEntity {
    public string Text;
    public string Convert(bool color,bool icon,bool details){return Text;}
}
public static class KeywordDatabase {
    public static KeywordEntity GetEntity(string key){
        return key=="Magic"?new KeywordEntity {Text="<color=blue>魔法</color>"}:
            key=="Nested"?new KeywordEntity {Text="<tag=Magic>伤害"}:
            key=="Loop"?new KeywordEntity {Text="<tag=Loop>"}:null;
    }
}
public static class StatusDatabase {
    public static string GetStatusName(string id){return id=="HP_STEAL"?"HP偷取":id=="fall"?"太阳剑掉落到地面速度提高{VALUE}":id=="inlinePercent"?"触发概率{VALUE}%":id=="trigger"?"冰霜武具额外触发{VALUE}次":id=="missing"?"":"<color=red>"+(id=="crit"?"暴击率":id=="evade"?"闪避":"物理伤害")+"</color>";}
    public static string GetStatusSymbol(string id){return id=="crit"||id=="fall"||id=="inlinePercent"?"%":"";}
    public static StatusEntity GetStatusEntity(string id){return new StatusEntity{divideForDisplay=id=="crit"||id=="evade"?100:1};}
}
public class TextTests {
'@
$test+=$method
$test+=@'
    public static string Run(){
        if(Clean("<tag=Magic>的暴击伤害<sprite name=crit>")!="魔法的暴击伤害")throw new Exception("semantic keyword was stripped");
        if(Clean("<tag=Nested>加成")!="魔法伤害加成")throw new Exception("nested keyword expansion");
        if(Clean("<tag=Unknown>加成")!="Unknown加成")throw new Exception("unknown keyword must retain identity");
        if(Clean("<tag=Loop>")!="Loop")throw new Exception("cyclic keywords must terminate");
        if(Describe("crit",250)!="暴击率 +2.5%")throw new Exception("critical name or unit missing");
        if(Describe("evade",500)!="闪避 +5")throw new Exception("evasion is points, not probability");
        if(Describe("physical",8)!="物理伤害 +8")throw new Exception("damage name missing");
        if(Describe("crit",1250)!="暴击率 +12.5%")throw new Exception("cumulative formatting");
        if(Describe("fall",12)!="太阳剑掉落到地面速度提高+12%")throw new Exception("native value placeholder must be filled once");
        if(Describe("inlinePercent",12)!="触发概率+12%")throw new Exception("percent symbol must not be duplicated");
        if(Describe("trigger",1)!="冰霜武具额外触发+1次")throw new Exception("trigger count placeholder");
        if(Describe("HP_STEAL",10)!="HP偷取 +1%" || Describe("HP_STEAL",1)!="HP偷取 +0.1%" || Describe("HP_STEAL",30)!="HP偷取 +3%")throw new Exception("lifesteal native points must convert to percentages");
        if(Describe("FROSTBITE_DAMAGE",3)!="冻伤伤害 +3%（每层·冰伤/秒）")throw new Exception("frostbite readable coefficient label");
        bool rejected=false;try{Describe("missing",1);}catch(InvalidOperationException){rejected=true;}
        if(!rejected)throw new Exception("cannot silently display anonymous reward");
        return "PASS: localized names, rich-text stripping, percentages, evasion points, totals, missing-name rejection";
    }
}
'@
Add-Type -TypeDefinition $test
[TextTests]::Run()
