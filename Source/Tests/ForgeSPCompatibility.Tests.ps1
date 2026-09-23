$ErrorActionPreference='Stop'
Add-Type -Path @((Join-Path $PSScriptRoot '../ForgeSPCompatibility.cs'),(Join-Path $PSScriptRoot 'ForgeSPCompatibilityTests.cs'))
[ForgeSPCompatibilityTests]::Run()
