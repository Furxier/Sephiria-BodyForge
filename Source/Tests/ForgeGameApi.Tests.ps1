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
    }
    $inventory=$game.MainModule.Types | Where-Object Name -eq 'GridInventory'
    $search=$inventory.Methods | Where-Object Name -eq 'SearchSetEffectInInventory'
    if(@($search.Body.Instructions | Where-Object {$_.Operand -match 'Charm_Basic::GetItemCategory\('}).Count -ne 1){throw 'Native combo category counting changed'}
    if(-not ($inventory.Fields | Where-Object {$_.Name -eq 'writePermission' -and $_.FieldType.FullName -eq 'System.Boolean'})){throw 'Inventory permission guard changed'}
    $magic=$game.MainModule.Types | Where-Object Name -eq 'Charm_Magic'
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
        if(-not (Test-Path (Join-Path $modDir '0Harmony.dll'))) { throw 'Missing bundled patch library' }
    } finally { $assembly.Dispose() }
    $charging=$game.MainModule.Types | Where-Object Name -eq 'StatusInstance_ChargingCharmBonus'
    $apply=$charging.Methods | Where-Object Name -eq 'ApplyStatusInner'
    if(-not ($apply.Body.Instructions.Operand -contains 'ChargingCharmBonus')){throw 'Charging charm raw stat mapping changed'}
    'PASS: native potion hook, ' + $statMatches.Count + ' native stat mappings, charging charm mapping, two default-on switches, independent assembly'
} finally { $game.Dispose() }


