$ErrorActionPreference='Stop'
$source=Get-Content (Join-Path $PSScriptRoot '../HeartProfiles.cs') -Raw -Encoding UTF8
Add-Type -TypeDefinition ($source+@'
public static class HeartBalanceProbe {
 public static System.Collections.Generic.Dictionary<string,System.Collections.Generic.Dictionary<string,int[]>> Values(){
  var all=new System.Collections.Generic.Dictionary<string,System.Collections.Generic.Dictionary<string,int[]>>();
  foreach(var p in HeartProfiles.All){var stats=new System.Collections.Generic.Dictionary<string,int[]>();foreach(var s in p.Stats)stats.Add(s.ID,s.Values);all.Add(p.Category,stats);}return all;
 }
}
'@)
$weights=Get-Content (Join-Path $PSScriptRoot 'HeartBalanceWeights.json') -Raw | ConvertFrom-Json
$profiles=[HeartBalanceProbe]::Values()
if($profiles.Count -ne 17){throw 'Missing equipment profile'}
foreach($category in $profiles.Keys){
    $stats=$profiles[$category];$previous=0.0
    foreach($level in 0..30){
        $cost=0.0
        foreach($id in $stats.Keys){
            $price=$weights.halfPointCosts.$id;$unit=$weights.rawUnits.$id
            if($null -eq $price -or $unit -le 0){throw "Missing price/units: $id"}
            if($stats[$id].Length -ne 31 -or $stats[$id][$level] -lt 0){throw 'Invalid growth array'}
            if($level -gt 0 -and $stats[$id][$level] -lt $stats[$id][$level-1]){throw "Stat decreases: $category $id"}
            $cost+=$stats[$id][$level]/[double]$unit*$price
        }
        if($category -eq 'ACADEMY' -and $level -ge 25){$cost+=$weights.academyExtraCastHalfPoints}
        if($cost -lt $previous -or $cost -gt 800.001){throw "Growth budget violated: $category level $level"}
        $previous=$cost
        $range=switch($level){5 {@(76,80)} 15 {@(272,280)} 25 {@(590,600)} 30 {@(780,800)} default {$null}}
        if($null -ne $range -and ($cost -lt $range[0] -or $cost -gt $range[1])){throw "Milestone budget drift: $category at $level = $cost"}
    }
}
if(($profiles['ELEMENTAL'].Keys | Sort-Object) -join ',' -ne 'HIGHEST_ELEMENTAL_DAMAGE,MOVE_SPEED'){throw 'Elemental must have only damage and movement'}
if(($profiles['WINDSONG'].Keys | Sort-Object) -join ',' -ne 'ATTACK_SPEED,DASH_RECOVERY_SPEED,MOVE_SPEED'){throw 'Windsong must not grant evasion'}
if($profiles['WINDSONG']['ATTACK_SPEED'][30]*3+$profiles['WINDSONG']['MOVE_SPEED'][30]*5+$profiles['WINDSONG']['DASH_RECOVERY_SPEED'][30]*2 -ne 400){throw 'Windsong must cost exactly 400 points'}
if($profiles['LAKE']['MAX_MP'][30] -ne 60 -or $profiles['LAKE']['MP_REGEN'][30] -ne 5 -or $profiles['LAKE']['MP_REGEN_MULTIPLE'][30] -ne 20 -or $profiles['LAKE']['MP_SKILL_DAMAGE'][30] -ne 60){throw 'Lake rebalance regression'}
'PASS: 17 equipment budgets across 31 levels, milestone parity, mechanism cost, units, monotonic growth, elemental exclusions and lake limits'
