[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$solutionPath = Join-Path $repositoryRoot 'ChatGPTRoster.sln'
$json = & dotnet list $solutionPath package --vulnerable --include-transitive --format json --output-version 1
if ($LASTEXITCODE -ne 0) {
    throw 'The NuGet vulnerability audit could not be completed.'
}

$text = $json -join [Environment]::NewLine
if ($text -match '"vulnerabilities"\s*:') {
    Write-Host $text
    throw 'One or more vulnerable NuGet packages were detected.'
}

Write-Host 'NuGet vulnerability audit passed.'
