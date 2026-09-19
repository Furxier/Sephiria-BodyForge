$ErrorActionPreference='Stop'
Add-Type -Path @((Join-Path $PSScriptRoot '../HeartProfiles.cs'),(Join-Path $PSScriptRoot '../HeartEquipment.cs'),(Join-Path $PSScriptRoot '../HeartTooltip.cs'),(Join-Path $PSScriptRoot 'HeartEquipmentTests.cs'))
[HeartEquipmentTests]::Run()
