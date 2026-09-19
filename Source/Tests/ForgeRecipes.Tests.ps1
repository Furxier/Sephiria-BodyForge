$ErrorActionPreference='Stop'
$source='using System; using System.Collections.Generic;'+[Environment]::NewLine
foreach($file in @('ForgeRecipes.cs','ForgeBalance.cs','ForgeAffinity.cs','ForgeTemplates.cs')) {
 $source+=((Get-Content (Join-Path $PSScriptRoot ('../'+$file)) -Raw -Encoding UTF8) -replace '(?m)^using [^;]+;','')
}
$test=Get-Content (Join-Path $PSScriptRoot 'ForgeTemplatesTests.cs') -Raw -Encoding UTF8
Add-Type -TypeDefinition ($source+$test)
[RecipeTests]::Run()
