$ErrorActionPreference='Stop'
$source=(Get-Content (Join-Path $PSScriptRoot '../SharedStatCatalog.cs') -Raw -Encoding UTF8)+((Get-Content (Join-Path $PSScriptRoot '../HeartProfiles.cs') -Raw -Encoding UTF8) -replace '(?m)^using [^;]+;','')
Add-Type -TypeDefinition ($source+@'
public static class HeartBalanceProbe {
 public static int RewardPrice(string id){return SharedStatCatalog.RewardCost(SharedStatCatalog.Equipment(id).Type);} public static int Price(string id){return SharedStatCatalog.Equipment(id).EquipmentCost;} public static int Unit(string id){return SharedStatCatalog.Equipment(id).RawUnit;} public static int Extra(){return SharedStatCatalog.AcademyExtraCastHalfPoints;}
 public static System.Collections.Generic.Dictionary<string,System.Collections.Generic.Dictionary<string,int[]>> Values(){
  var all=new System.Collections.Generic.Dictionary<string,System.Collections.Generic.Dictionary<string,int[]>>();
  foreach(var p in HeartProfiles.All){var stats=new System.Collections.Generic.Dictionary<string,int[]>();foreach(var s in p.Stats)stats.Add(s.ID,s.Values);all.Add(p.Category,stats);}return all;
 }
}
'@)

$profiles=[HeartBalanceProbe]::Values()
if($profiles.Count -ne 24){throw 'Missing equipment profile'}
if($profiles['COMET']['DASH_ATTACK_DAMAGE'][15] -ne 80 -or $profiles['COMET']['DASH_RECOVERY_SPEED'][15] -ne 50 -or $profiles['COMET']['DASH_COUNT'][15] -ne 3){throw 'User-confirmed comet maximum changed'}
foreach($level in 0..15){$expected=if($level -ge 15){3}elseif($level -ge 13){2}elseif($level -ge 8){1}else{0};if($profiles['COMET']['DASH_COUNT'][$level] -ne $expected){throw 'Comet unlock changed'}}
if([HeartBalanceProbe]::Price('DASH_COUNT') -ne 200){throw 'Equipment dash must cost 100 real points independently from permanent +1'}
if($profiles['FORTUNE']['LUCK'][15] -ne 8 -or $profiles['FORTUNE']['TRUE_DAMAGE'][15] -ne 12 -or $profiles['FORTUNE']['CRITICAL'][15] -ne 1600 -or $profiles['FORTUNE']['EVASION'][15] -ne 1600){throw 'Fortune maximum changed'}
if($profiles['WEAPON']['FINAL_WEAPONDAMAGE'][15] -ne 35 -or $profiles['WEAPON']['HIGHEST_ELEMENTAL_DAMAGE'][15] -ne 8 -or $profiles['WEAPON']['BASIC_ATTACK_DAMAGE'][15] -ne 32 -or $profiles['WEAPON']['SPECIAL_ATTACK_DAMAGE'][15] -ne 32){throw 'Weapon maximum changed'}
if($profiles['PARTY']['ALL_PLAYER_DEFENSE'][15] -ne 14 -or $profiles['PARTY']['ALL_PLAYER_ALL_DAMAGE'][15] -ne 12 -or $profiles['PARTY']['ALL_PLAYER_ELEMENTAL_DAMAGE'][15] -ne 4 -or $profiles['PARTY']['ALL_PLAYER_CRIT'][15] -ne 8){throw 'Party raw contribution maximum changed'}
foreach($category in $profiles.Keys){
    $stats=$profiles[$category];$previous=0.0
    foreach($level in 0..15){
        $cost=0.0
        foreach($id in $stats.Keys){
            $price=[HeartBalanceProbe]::Price($id);$unit=[HeartBalanceProbe]::Unit($id)
            if($id -notin @('DASH_COUNT','CHARGING_CHARM_AMPLIFY','BURN_STACK','ELECTRIC_STACK','HP_STEAL') -and $price -ne [HeartBalanceProbe]::RewardPrice($id)){throw "Scalar equipment/reward price mismatch: $id"}
            if($null -eq $price -or $unit -le 0){throw "Missing price/units: $id"}
            if($stats[$id].Length -ne 16 -or $stats[$id][$level] -lt 0){throw 'Invalid growth array'}
            if($level -gt 0 -and $stats[$id][$level] -lt $stats[$id][$level-1]){throw "Stat decreases: $category $id"}
            $cost+=$stats[$id][$level]/[double]$unit*$price
        }
        if($category -eq 'ACADEMY' -and $level -ge 13){$cost+=[HeartBalanceProbe]::Extra()}
        if($cost -lt $previous -or $cost -gt $(if($category -eq 'COMET'){1200.001}else{800.001})){throw "Growth budget violated: $category level $level"}
        $previous=$cost
        $range=switch($level){3 {@(76,80)} 8 {@(272,280)} 13 {@(590,600)} 15 {@(780,800)} default {$null}}
        if($category -ne 'COMET' -and $null -ne $range -and ($cost -lt $range[0] -or $cost -gt $range[1])){throw "Milestone budget drift: $category at $level = $cost"}
    }
}
if(($profiles['ELEMENTAL'].Keys | Sort-Object) -join ',' -ne 'HIGHEST_ELEMENTAL_DAMAGE,MOVE_SPEED'){throw 'Elemental must have only damage and movement'}
if(($profiles['DARKCLOUD'].Keys | Sort-Object) -join ',' -ne 'DARK_CLOUD_DAMAGE,DARK_CLOUD_RESTORE_DURING_BATTLE,DARK_CLOUD_SPEED'){throw 'Cloud equipment must not grant capacity'}
if($profiles['DARKCLOUD']['DARK_CLOUD_SPEED'][15] -ne 170 -or $profiles['DARKCLOUD']['DARK_CLOUD_DAMAGE'][15] -ne 100){throw 'Cloud rebalance regression'}
if($profiles['DARKCLOUD']['DARK_CLOUD_RESTORE_DURING_BATTLE'][15] -ne 30){throw 'Cloud recovery must be halved'}
$cloudNodes=@{DARK_CLOUD_DAMAGE=@(3,10,35,75,100);DARK_CLOUD_SPEED=@(0,17,59,127,170);DARK_CLOUD_RESTORE_DURING_BATTLE=@(0,3,10,22,30)}
$cloudLevels=@(0,3,8,13,15)
foreach($stat in $cloudNodes.Keys){for($i=0;$i -lt 5;$i++){if($profiles['DARKCLOUD'][$stat][$cloudLevels[$i]] -ne $cloudNodes[$stat][$i]){throw "Cloud half-strength milestone: $stat"}}}
if($profiles['FLAMESWORD'].ContainsKey('FLAME_SWORD_MAX') -or $profiles['FLAMESWORD']['FLAME_SWORD_DAMAGE'][15] -ne 135){throw 'Sword equipment rebalance regression'}
if(($profiles['WINDSONG'].Keys | Sort-Object) -join ',' -ne 'ATTACK_SPEED,DASH_RECOVERY_SPEED,MOVE_SPEED'){throw 'Windsong must not grant evasion'}
if($profiles['WINDSONG']['ATTACK_SPEED'][15]*3+$profiles['WINDSONG']['MOVE_SPEED'][15]*5+$profiles['WINDSONG']['DASH_RECOVERY_SPEED'][15]*2 -ne 400){throw 'Windsong must cost exactly 400 points'}
if($profiles['LAKE']['MAX_MP'][15] -ne 60 -or $profiles['LAKE']['MP_REGEN'][15] -ne 5 -or $profiles['LAKE']['MP_REGEN_MULTIPLE'][15] -ne 20 -or $profiles['LAKE']['MP_SKILL_DAMAGE'][15] -ne 60){throw 'Lake rebalance regression'}
'PASS: 24 equipment budgets across 16 levels, milestone parity, mechanism cost, units, monotonic growth, elemental exclusions and lake limits'

if($profiles['MYSTIC']['HP_STEAL'][12] -ne 0 -or $profiles['MYSTIC']['HP_STEAL'][13] -ne 1){throw 'Mystic lifesteal unlock regression'}
if($profiles['ALCHEMY'].Count -ne 6 -or $profiles['ALCHEMY']['HP_STEAL'][12] -ne 0 -or $profiles['ALCHEMY']['HP_STEAL'][13] -ne 1){throw 'Alchemy mix/unlock regression'}
if($profiles['GLACIER']['FROSTBITE_DAMAGE'][15] -ne 30 -or $profiles['GLACIER']['FREEZE_THRESHOLD'][12] -ne 0 -or $profiles['GLACIER']['FREEZE_THRESHOLD'][13] -ne 1){throw 'Glacier frostbite/threshold progression'}
