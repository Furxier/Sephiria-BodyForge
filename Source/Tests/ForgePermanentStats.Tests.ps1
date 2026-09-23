$ErrorActionPreference='Stop'
Add-Type -Path @((Join-Path $PSScriptRoot '../ForgePermanentStats.cs'),(Join-Path $PSScriptRoot 'ForgePermanentStatsTests.cs'))
[ForgePermanentStatsTests]::Run()
