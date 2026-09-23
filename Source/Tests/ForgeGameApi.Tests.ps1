param([string]$AssemblyPath,[string]$GameDirectory)
$ErrorActionPreference = 'Stop'
$projectDir = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$gameDir=$GameDirectory
if([string]::IsNullOrWhiteSpace($gameDir)) { $gameDir=$env:SEPHIRIA_DIR }
if([string]::IsNullOrWhiteSpace($gameDir)) { $gameDir=Split-Path (Split-Path $projectDir -Parent) -Parent }
$modDir = Join-Path $gameDir 'AddOns/BodyForge'
Add-Type -Path (Join-Path $modDir 'Mono.Cecil.dll')
$game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $gameDir 'Sephiria_Data/Managed/Assembly-CSharp.dll'))
try {
    $unit=$game.MainModule.Types | Where-Object Name -eq 'UnitAvatar'
    $status=$game.MainModule.Types | Where-Object Name -eq 'StatusInstance'
    if(-not ($unit.Fields | Where-Object {$_.Name -eq 'orphanedStatusInstancesServerside' -and $_.FieldType.FullName -eq 'System.Collections.Generic.List`1<StatusInstance>'})){throw 'Native permanent status list changed'}
    foreach($pair in @(@('value','System.Int32'),@('applied','System.Boolean'),@('<CurrentTarget>k__BackingField','UnitAvatar'))){
        if(-not ($status.Fields | Where-Object {$_.Name -eq $pair[0] -and $_.FieldType.FullName -eq $pair[1]})){throw ('Native status field changed: '+$pair[0])}
    }
    $command=$unit.Methods | Where-Object Name -eq 'UserCode_CmdAddOrphanedStatusInstance__String'
    if(($command.Parameters.ParameterType.FullName -join ',') -ne 'System.String' -or -not ($command.Body.Instructions.Operand -match 'StatusInstance::ApplyStatus')){throw 'Native client permanent grant changed'}
    $clear=$status.Methods | Where-Object Name -eq 'ClearTarget'
    if($clear.IsVirtual -or ($clear.Body.Instructions.Operand -match '::RemoveStatus')){throw 'ClearTarget now changes applied attributes'}
    # Every currently eligible direct scalar class must remain integer addition/subtraction.
    $balance=Get-Content (Join-Path $PSScriptRoot '../ForgeBalance.cs') -Raw
    $scalarRules=[regex]::Matches($balance,'\{"([A-Za-z]+)",Rule') | ForEach-Object {$_.Groups[1].Value}
    $scalarNames=@($scalarRules | Where-Object {$_ -notin @('MaxHP','MaxMP','FinalHP','MoveSpeed')} | ForEach-Object {'StatusInstance_'+$_})+@('StatusInstance_Custom')
    $checked=0
    foreach($type in $game.MainModule.Types | Where-Object {$_.Name -in $scalarNames -and $_.BaseType.FullName -eq 'StatusInstance'}){
        if($type.Methods | Where-Object Name -eq 'SetTargetInner'){continue}
        foreach($name in @('ApplyStatusInner','RemoveStatusInner')){
            $m=$type.Methods | Where-Object Name -eq $name
            if($null -eq $m){throw "Missing scalar method: $type.$name"}
            $calls=@($m.Body.Instructions | Where-Object {$_.OpCode.Name -in @('call','callvirt')} | ForEach-Object {$_.Operand.ToString()})
            $unknown=@($calls | Where-Object {$_ -notmatch 'StatusInstance::(ApplyStatusInner|RemoveStatusInner|get_CurrentTarget|get_Value)\(' -and $_ -notmatch 'UnitAvatar::(AddCustomStat|set_NetworkhighestElementalBonus)\(' -and $_ -notmatch 'System.String::IsNullOrEmpty\('})
            if($unknown.Count){throw "Non-scalar behavior in $type.$name : $unknown"}
            if(-not ($calls -match 'UnitAvatar::(AddCustomStat|set_NetworkhighestElementalBonus)\(')){throw "Missing native additive write: $type.$name"}
            if($name -eq 'RemoveStatusInner' -and -not ($m.Body.Instructions.OpCode.Name -match '^(neg|sub)$')){throw "Missing additive cleanup: $type"}
        }
        $checked++
    }
    if($checked -lt 20){throw 'Too few native scalar implementations validated'}
    $source = Get-Content (Join-Path $PSScriptRoot '../ForgeRewards.cs') -Raw
    $statMatches = [regex]::Matches($source, '\{"([A-Za-z]+)",(\d+)\}')
    foreach ($match in $statMatches) {
        $type = $game.MainModule.Types | Where-Object Name -eq ('StatusInstance_' + $match.Groups[1].Value)
        $method = $type.Methods | Where-Object Name -eq 'ApplyStatusInner'
        $instructions = @($method.Body.Instructions)
        $constant = $instructions | Where-Object { $_.OpCode.Name -like 'ldc.i4*' } | Select-Object -First 1
        if ($null -eq $constant) { throw "Missing native stat constant: $type" }
        $actual = if ($null -ne $constant.Operand) { [int]$constant.Operand } else { [int]($constant.OpCode.Name -replace 'ldc.i4.', '') }
        if ($actual -ne [int]$match.Groups[2].Value) { throw "Native stat mapping changed: $type" }
        if (-not ($instructions.Operand -match 'UnitAvatar::AddCustomStat\(ECustomStat,System.Int32\)')) { throw "Native stat application changed: $type" }
    }
    $passive = $game.MainModule.Types | Where-Object Name -eq 'PassiveObject_PotionAndRandomStat'
    $handler = $passive.Methods | Where-Object Name -eq 'HandleDrinkPotion'
    if (-not ($handler.Body.Instructions.Operand -match 'UnitAvatar::AddOrphanedStatusInstance')) { throw 'Potion effect API changed' }
    $required = @{
        StatusInstance_HighestElementalDamage=@('ApplyStatusInner','RemoveStatusInner')
        GridInventory=@('AddStorage','add_OnCharmEffectRefreshedForServer','remove_OnCharmEffectRefreshedForServer','add_OnCharmEffectRefreshedForClient','remove_OnCharmEffectRefreshedForClient','ReleasePermission','UserCode_RpcReleasePermission')
        UI_CharacterStatusPanel=@('OnItemClicked','OnItemLongClicked','OnSubBagItemClicked','HandleDrop','HandleDropSubBag','ActivateSelectedItem','OnClosed')
        UI_NewInventoryIcon=@('OnPointerDown','OnPointerUp','OnBeginDrag','OnDrag','OnEndDrag','OnDrop')
        UI_ItemDropZone=@('DropItem','DropSubBagItem')
        UI_SubBagIcon=@('OnBeginDrag','OnDrag','OnEndDrag','OnDrop','get_X','get_Inventory')
        UI_MessageBoxHolder=@('OpenYesNo','OpenYes')
        Charm_StatusInstance=@('OnEnabledEffect','OnDisabledEffect','OnUpdatedLevel','GetEffectStringCount','GetEffectString')
        Charm_Basic=@('OnCharmEffectRefreshed','OnDestroy','get_Inventory','GetItemCategory')
        SkillController=@('add_OnGetMultipleCastCount','remove_OnGetMultipleCastCount')
        LocalizedString=@('ToString')
        UI_CharmTooltip=@('UpdateData')
        UI_CharmTierDisplay=@('SetTier')
        UI_StoneTabletAppliedFrame=@('SetActiveCharm','LVUpSetActive')
        GreenBat=@('Update','CreateBullet')
        StatusInstance_EXPDrop=@('ApplyStatusInner','RemoveStatusInner')
    }
    $planet=$game.MainModule.Types | Where-Object Name -eq 'GreenBat'
    if(-not (($planet.Methods | Where-Object Name -eq 'Update').Body.Instructions.Operand -contains 'PLANETATTACKSPEED')){throw 'Planet attack speed native reader changed'}
    if(-not (($planet.Methods | Where-Object Name -eq 'CreateBullet').Body.Instructions.Operand -contains 'PLANETDAMAGE')){throw 'Planet damage native reader changed'}
    $panel=$game.MainModule.Types | Where-Object Name -eq 'UI_CharacterStatusPanel'
    $select=$panel.Methods | Where-Object {$_.Name -eq 'OnItemSelected' -and $_.Parameters.Count -eq 2}
    if(-not ($select.Body.Instructions.Operand -match 'UI_StoneTabletAppliedFrame::SetActiveCharm')){throw 'Native selection highlight reset changed'}
    $inventory=$game.MainModule.Types | Where-Object Name -eq 'GridInventory'
    $search=$inventory.Methods | Where-Object Name -eq 'SearchSetEffectInInventory'
    if(@($search.Body.Instructions | Where-Object {$_.Operand -match 'Charm_Basic::GetItemCategory\('}).Count -ne 1){throw 'Native combo category counting changed'}
    if(-not ($inventory.Fields | Where-Object {$_.Name -eq 'writePermission' -and $_.FieldType.FullName -eq 'System.Boolean'})){throw 'Inventory permission guard changed'}
    $magic=$game.MainModule.Types | Where-Object Name -eq 'Charm_Magic'
    $weapon=$game.MainModule.Types | Where-Object Name -eq 'WeaponSimple'
    foreach($methodName in @('CreateBasicAttackProjectile','CreateDashAttackProjectile','CreateSpecialAttackProjectile')){
        $ops=@(($weapon.Methods | Where-Object Name -eq $methodName).Body.Instructions)
        $found=$false
        for($i=1;$i -lt $ops.Count;$i++){if($ops[$i].Operand -match 'UnitAvatar::GetCustomStat\(ECustomStat\)' -and $ops[$i-1].Operand -eq 24){$found=$true;break}}
        if(!$found){throw "Weapon range consumer changed: $methodName"}
    }
    if(-not (($magic.Methods | Where-Object Name -eq 'CreateMagic').Body.Instructions.Operand -contains 'MPSKILLDAMAGE')){throw 'MP ability damage reader changed'}
    $tier=$game.MainModule.Types | Where-Object Name -eq 'UI_CharmTierDisplay'
    if(-not ($tier.Fields | Where-Object { $_.Name -eq 'starImages' -and $_.FieldType.FullName -eq 'UnityEngine.UI.Image[]' })) {throw 'Native tier star array changed'}
    $tooltip=$game.MainModule.Types | Where-Object Name -eq 'UI_CharmTooltip'
    if(-not ($tooltip.Fields | Where-Object { $_.Name -eq 'levelText' -and $_.FieldType.FullName -eq 'TMPro.TMP_Text' })) {throw 'Native tooltip level label changed'}
    $equipment=$game.MainModule.Types | Where-Object Name -eq 'Charm_StatusInstance'
    foreach($name in @('OnDisabledEffect','OnUpdatedLevel')) {
        $method=$equipment.Methods | Where-Object Name -eq $name
        if(-not ($method.Body.Instructions.Operand -match 'StatusInstance::RemoveStatus') -or
            -not ($method.Body.Instructions.Operand -match 'StatusInstance::ClearTarget')) {throw 'Native equipment cleanup changed'}
    }
    $needle=$game.MainModule.Types | Where-Object Name -eq 'Charm_UpCharmDamage'
    foreach($name in @('xOffset','yOffset')) {
        if(-not ($needle.Fields | Where-Object { $_.Name -eq $name -and $_.FieldType.FullName -eq 'System.SByte' -and $_.IsPublic })) {throw 'Native needle direction changed'}
    }
    foreach($name in @('DecreaseSubBagItemQuantity','ServerDecreaseSubBagItemQuantity')) {
        $m=$inventory.Methods | Where-Object Name -eq $name
        if($null -eq $m -or ($m.Parameters.ParameterType.FullName -join ',') -ne 'System.SByte,System.SByte') {throw 'Subbag consume API changed'}
    }
    $subConsume=$inventory.Methods | Where-Object Name -eq 'ServerDecreaseSubBagItemQuantity'
    if(-not ($subConsume.Body.Instructions.Operand -match 'GridInventory/Permission::.ctor')) {throw 'Subbag equipment refresh permission changed'}
    $release=$inventory.Methods | Where-Object Name -eq 'ReleasePermission'
    $ops=@($release.Body.Instructions)
    $refresh=($ops | Where-Object { $_.Operand -match 'Charm_Basic::RefreshCharm' } | Select-Object -Last 1).Offset
    $notify=($ops | Where-Object { $_.Operand -match 'GridInventory::OnCharmEffectRefreshedForServer' } | Select-Object -Last 1).Offset
    if($null -eq $refresh -or $null -eq $notify -or $notify -le $refresh) { throw 'Equipment notification no longer follows charm refresh' }
    foreach($name in $required.Keys) {
        $type=$game.MainModule.Types | Where-Object Name -eq $name
        foreach($methodName in $required[$name]) {
            $methods=@($type.Methods | Where-Object Name -eq $methodName)
            if($methods.Count -ne 1) { throw "Missing or ambiguous native UI method: $name.$methodName" }
        }
    }
    $config = Get-Content (Join-Path $modDir 'config.json') -Raw | ConvertFrom-Json
    if ($config.Enabled -ne $true -or $config.EnableSpecializedRewards -ne $true) { throw 'Expected default-on main and specialized switches' }
    if([string]::IsNullOrEmpty($AssemblyPath)){$AssemblyPath=Join-Path $modDir 'BodyForge.dll'}
    $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Resolve-Path $AssemblyPath).Path)
    try {
        if($assembly.MainModule.AssemblyReferences.Name -match 'SilentInventory|SPMod') { throw 'Unexpected runtime mod dependency' }
        $icon=$assembly.MainModule.Resources | Where-Object Name -eq 'BodyForge.ForgeEntry.png'
        if($null -ne $icon){throw 'Removed toolbar artwork must not be embedded'}
        if(-not (Test-Path (Join-Path $modDir '0Harmony.dll'))) { throw 'Missing bundled patch library' }
    } finally { $assembly.Dispose() }
    $charging=$game.MainModule.Types | Where-Object Name -eq 'StatusInstance_ChargingCharmBonus'
    $spPath=Join-Path $gameDir 'AddOns/SPMod/SPMod.dll'
    if(Test-Path $spPath){
        $sp=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($spPath)
        try {
            $mod=$sp.MainModule.Types | Where-Object FullName -eq 'SPMod.StarSephiriaMod'
            if(-not ($mod.Fields | Where-Object {$_.Name -eq '_isPatched' -and $_.FieldType.FullName -eq 'System.Boolean'})){throw 'SP enabled flag changed'}
            if(-not ($mod.Fields | Where-Object {$_.Name -eq 'PartyStats' -and $_.FieldType.FullName -like '*ValueTuple*AllPlayerStatus*'})){throw 'SP party tuple changed'}
            if(-not ($mod.Fields | Where-Object {$_.Name -eq 'PartyEffect' -and $_.FieldType.FullName -like 'System.Collections.Generic.Dictionary*System.String*AllPlayerStatus*'})){throw 'SP stale party record container changed'}
            $patches=$sp.MainModule.Types | Where-Object FullName -eq 'SPMod.StarSephiriaPatches'
            $highest=($patches.NestedTypes | Where-Object Name -eq 'UnitAvatar_Patches').Methods | Where-Object Name -eq 'UpdateHighestElementalBonus_Prefix'
            if($highest.ReturnType.FullName -ne 'System.Boolean' -or ($highest.Parameters.ParameterType.FullName -join ',') -ne 'UnitAvatar'){throw 'SP authority guard target changed'}
            $party=($patches.NestedTypes | Where-Object Name -eq 'PlayerAvatar_Patches').Methods | Where-Object Name -eq 'UpdatePartyEffect'
            foreach($key in @('STAT_ALLPLAYERDEFENSE','STAT_ALLPLAYERALLDAMAGE','STAT_ALLPLAYERELEMENTALDAMAGE','STAT_ALLPLAYERCRIT')){
                if(-not ($party.Body.Instructions.Operand -contains $key)){throw "SP contribution reader changed: $key"}
            }
            $tools=$sp.MainModule.Types | Where-Object FullName -eq 'SPMod.Tool.Tools'
            $max=$tools.Methods | Where-Object Name -eq 'GetMaxPlayerStatus'
            if($max.ReturnType.FullName -ne 'SPMod.Tool.ModuleComponents/AllPlayerStatus'){throw 'SP max aggregation return changed'}
            $crit=@(($tools.Methods | Where-Object Name -eq 'GetMaxPlayerStatusForName').Body.Instructions)
            if(-not ($crit | Where-Object {$_.OpCode.Name -eq 'ldc.i4.s' -and $_.Operand -eq 100})){throw 'SP party critical no longer uses displayed integer units'}
            $module=$sp.MainModule.Types | Where-Object FullName -eq 'SPMod.Tool.ModuleComponents'
            $stats=$module.NestedTypes | Where-Object Name -eq 'AllPlayerStatus'
            if(-not ($stats.Methods | Where-Object {$_.Name -eq '.ctor' -and ($_.Parameters.ParameterType.FullName -join ',') -eq 'System.Int32,System.Int32,System.Int32,System.Int32'})){throw 'SP four-stat constructor changed'}
            $dungeon=($sp.MainModule.Types | Where-Object FullName -eq 'SPMod.DungeonPatchs').NestedTypes | Where-Object Name -eq 'DungeonManager_Patch'
            $update=$dungeon.Methods | Where-Object Name -eq 'Update_Postfix'
            if($update.ReturnType.FullName -ne 'System.Void' -or ($update.Parameters.ParameterType.FullName -join ',') -ne 'DungeonManager'){throw 'SP replacement Update signature changed'}
            $create=($module.NestedTypes | Where-Object Name -eq 'ModKeyWord').Methods | Where-Object Name -eq 'CreateStatus'
            if(-not ($create.Body.Instructions.Operand -contains 'StatusInstance_Custom/')){throw 'SP status request construction changed'}
            $content=$sp.MainModule.Types | Where-Object FullName -eq 'SPMod.ModContent.LoadModContent'
            foreach($id in @('ALL_PLAYER_DEFENSE','ALL_PLAYER_ALL_DAMAGE','ALL_PLAYER_ELEMENTAL_DAMAGE','ALL_PLAYER_CRIT')){
                if(-not (($content.Methods | Where-Object Name -eq '.cctor').Body.Instructions.Operand -contains $id)){throw "SP status definition missing: $id"}
            }
            $needle=$sp.MainModule.Types | Where-Object FullName -eq 'SPMod.ModContent.CharmStatusInheritance'
            $validate=$needle.Methods | Where-Object Name -eq 'IsDependencyValid'
            if($validate.ReturnType.FullName -ne 'System.Boolean' -or ($validate.Parameters.ParameterType.FullName -join ',') -ne 'Charm_Basic'){throw 'SP target validation API changed'}
            $hasTarget=$needle.Methods | Where-Object Name -eq 'HasValidTarget'
            if(-not ($hasTarget.Body.Instructions.Operand -match '::IsDependencyValid')){throw 'SP target check bypasses validation'}
            $refresh=$needle.Methods | Where-Object Name -eq 'RefreshCharm'
            $clear=$refresh.Body.Instructions | Where-Object {$_.OpCode.Name -eq 'stfld' -and $_.Operand -match 'Charm_StatusInstance::stats'} | Select-Object -First 1
            $remove=$refresh.Body.Instructions | Where-Object {$_.Operand -match '::OnUpdatedLevel'} | Select-Object -First 1
            $check=$refresh.Body.Instructions | Where-Object {$_.Operand -match '::HasValidTarget'} | Select-Object -First 1
            if($null -eq $clear -or $null -eq $remove -or $null -eq $check -or $clear.Offset -ge $remove.Offset -or $remove.Offset -ge $check.Offset){throw 'SP no longer removes copied effects before target validation'}
            $category=$needle.Methods | Where-Object Name -eq 'SearchCategory'
            if(-not ($category.Body.Instructions.Operand -match '::Clear') -or -not ($category.Body.Instructions.Operand -match '::HasValidTarget')){throw 'SP category cleanup changed'}
        } finally { $sp.Dispose() }
    }
    $apply=$charging.Methods | Where-Object Name -eq 'ApplyStatusInner'
    if(-not ($apply.Body.Instructions.Operand -contains 'ChargingCharmBonus')){throw 'Charging charm raw stat mapping changed'}
    'PASS: native potion hook, ' + $statMatches.Count + ' native stat mappings, charging charm mapping, two default-on switches, independent assembly'
} finally { $game.Dispose() }


