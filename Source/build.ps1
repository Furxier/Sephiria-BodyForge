param([string]$OutputPath,[string]$GameDirectory)
$ErrorActionPreference = 'Stop'
$projectDir = Split-Path $PSScriptRoot -Parent
$gameDir = $GameDirectory
if([string]::IsNullOrWhiteSpace($gameDir)) { $gameDir=$env:SEPHIRIA_DIR }
if([string]::IsNullOrWhiteSpace($gameDir)) { $gameDir=Split-Path (Split-Path $projectDir -Parent) -Parent }
$gameDir=[System.IO.Path]::GetFullPath($gameDir)
if(!(Test-Path -LiteralPath (Join-Path $gameDir 'Sephiria_Data/Managed/Assembly-CSharp.dll'))) { throw 'Set -GameDirectory or SEPHIRIA_DIR to your Sephiria installation' }
$modDir = Join-Path $gameDir 'AddOns/BodyForge'
$managed = Join-Path $gameDir 'Sephiria_Data/Managed'
$compiler = 'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$refs = @('mscorlib.dll','System.dll','System.Core.dll','netstandard.dll','Assembly-CSharp.dll','Mirror.dll','Unity.InputSystem.dll','UnityEngine.CoreModule.dll','UnityEngine.IMGUIModule.dll','UnityEngine.TextRenderingModule.dll','UnityEngine.JSONSerializeModule.dll','UnityEngine.UI.dll','UnityEngine.UIModule.dll','Unity.TextMeshPro.dll')
$arguments = @('/nologo','/noconfig','/target:library','/nostdlib+','/optimize+')
if([string]::IsNullOrEmpty($OutputPath)) { $OutputPath=Join-Path $projectDir 'artifacts/BodyForge.dll' }
$OutputPath=[System.IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Path (Split-Path $OutputPath -Parent) -Force | Out-Null
$arguments += '/out:' + $OutputPath
foreach ($ref in $refs) { $arguments += '/reference:' + (Join-Path $managed $ref) }
$arguments += '/reference:' + (Join-Path $modDir '0Harmony.dll')
foreach ($source in @('BodyForge.cs','ForgePanel.cs','ForgeRewards.cs','ForgePort.cs','ForgeTransaction.cs','ForgeEffectGate.cs','SharedPanelInput.cs','NativeForgeHooks.cs','NativeForgeFlow.cs','NativeForgeUI.cs','NativeToolbarLayout.cs','ForgeRecipes.cs','ForgeBalance.cs','ForgeAffinity.cs','ForgeTemplates.cs','ForgeRecipeCatalog.cs','ForgeProgress.cs','ForgeModalUI.cs','ForgeLedgerUI.cs')) { $arguments += (Join-Path $PSScriptRoot $source) }
$arguments += (Join-Path $PSScriptRoot 'ForgeMilestones.cs')
$arguments += (Join-Path $PSScriptRoot 'HeartProfiles.cs')
$arguments += (Join-Path $PSScriptRoot 'SharedStatCatalog.cs')
$arguments += (Join-Path $PSScriptRoot 'ForgeSPCompatibility.cs')
$arguments += (Join-Path $PSScriptRoot 'ForgeLedgerRefresh.cs')
$arguments += (Join-Path $PSScriptRoot 'ForgePermanentStats.cs')
$arguments += (Join-Path $PSScriptRoot 'ForgeExternal.cs')
$arguments += (Join-Path $PSScriptRoot 'HeartEquipment.cs')
$arguments += (Join-Path $PSScriptRoot 'HeartTooltip.cs')
$arguments += (Join-Path $PSScriptRoot 'ForgeLocalization.cs')
$arguments += (Join-Path $PSScriptRoot 'ForgeLocalizedLabel.cs')
$arguments += (Join-Path $PSScriptRoot 'ForgeMilestoneUI.cs')
& $compiler $arguments
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
