param([string]$OutputDirectory,[string]$GameDirectory)
$ErrorActionPreference='Stop'
$gameDir=$GameDirectory
if([string]::IsNullOrWhiteSpace($gameDir)) { $gameDir=$env:SEPHIRIA_DIR }
if([string]::IsNullOrWhiteSpace($gameDir)) { $gameDir=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent }
$runtimeDir=Join-Path $gameDir 'AddOns/BodyForge'
$distributionDir=Join-Path $PSScriptRoot 'Distribution'
if([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory=Join-Path $PSScriptRoot 'artifacts' }
$OutputDirectory=[System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$metadata=Get-Content -LiteralPath (Join-Path $distributionDir 'metadata.json') -Raw | ConvertFrom-Json
if($metadata.modVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid release version' }
# Explicit allowlist: source, tests, saves, logs and personal configuration cannot leak into a release.
$files=@('0Harmony.dll','Mono.Cecil.dll','Mono.Cecil.Mdb.dll','Mono.Cecil.Pdb.dll','Mono.Cecil.Rocks.dll',
    'MonoMod.RuntimeDetour.dll','MonoMod.Utils.dll','metadata.json','使用说明.md','readme.md')
foreach($file in $files) {
    $fileRoot=if($file.EndsWith('.dll')){$runtimeDir}else{$distributionDir}
    if(!(Test-Path -LiteralPath (Join-Path $fileRoot $file) -PathType Leaf)) { throw "Missing release file: $file" }
}
$stage=Join-Path $OutputDirectory ('staging-'+[guid]::NewGuid().ToString('N'))
$packageMod=Join-Path $stage 'BodyForge'
New-Item -ItemType Directory -Path $packageMod -Force | Out-Null
& (Join-Path $PSScriptRoot 'Source/build.ps1') -OutputPath (Join-Path $packageMod 'BodyForge.dll') -GameDirectory $gameDir
foreach($file in $files) {
    $fileRoot=if($file.EndsWith('.dll')){$runtimeDir}else{$distributionDir}
    Copy-Item -LiteralPath (Join-Path $fileRoot $file) -Destination $packageMod
}
# Ship documented defaults, never the local player's edited config.
Set-Content -LiteralPath (Join-Path $packageMod 'config.json') -Encoding UTF8 -Value '{"Enabled":true,"EnableSpecializedRewards":true}'
$archive=Join-Path $OutputDirectory ('BodyForge-'+$metadata.modVersion+'.zip')
Compress-Archive -LiteralPath $packageMod -DestinationPath $archive -Force
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[System.IO.Compression.ZipFile]::OpenRead($archive)
try {
    $expected=@($files+'BodyForge.dll'+'config.json' | ForEach-Object { 'BodyForge/'+$_ })
    $actual=@($zip.Entries | Where-Object { ![string]::IsNullOrEmpty($_.Name) } | ForEach-Object { $_.FullName.Replace('\','/') })
    if(Compare-Object $expected $actual) { throw 'Release archive contents do not match runtime allowlist' }
} finally { $zip.Dispose() }
Write-Output "Release: $archive"

