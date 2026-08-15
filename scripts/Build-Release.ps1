[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')]
    [string]$Version = '0.1.0',

    [switch]$SkipTests,
    [switch]$SkipLaunchCheck
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$solutionPath = Join-Path $repositoryRoot 'ChatGPTRoster.sln'
$projectPath = Join-Path $repositoryRoot 'src\ChatGPTRoster\ChatGPTRoster.csproj'
$artifactsRoot = Join-Path $repositoryRoot 'artifacts'
$versionRoot = Join-Path $artifactsRoot $Version
$packageName = "RosterCompanion-$Version-win-x64"
$publishDirectory = Join-Path $versionRoot $packageName
$zipPath = Join-Path $versionRoot "$packageName.zip"
$checksumPath = Join-Path $versionRoot 'SHA256SUMS.txt'
$assemblyVersion = ($Version -replace '-.*$', '') + '.0'

function Assert-ChildPath {
    param([string]$Parent, [string]$Child)
    $normalizedParent = [System.IO.Path]::GetFullPath($Parent).TrimEnd('\') + '\'
    $normalizedChild = [System.IO.Path]::GetFullPath($Child)
    if (-not $normalizedChild.StartsWith($normalizedParent, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside $normalizedParent"
    }
}

Assert-ChildPath -Parent $repositoryRoot -Child $artifactsRoot
Assert-ChildPath -Parent $artifactsRoot -Child $versionRoot

if (-not $SkipTests) {
    & dotnet restore $solutionPath --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Package restore failed.' }

    & dotnet test $solutionPath --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Release tests failed.' }

    & (Join-Path $PSScriptRoot 'Test-PackageVulnerabilities.ps1')
}

if (Test-Path -LiteralPath $versionRoot) {
    Remove-Item -LiteralPath $versionRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

& dotnet publish $projectPath `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $publishDirectory `
    --no-restore `
    -p:Version=$Version `
    -p:AssemblyVersion=$assemblyVersion `
    -p:FileVersion=$assemblyVersion `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'Self-contained publish failed.' }

$executablePath = Join-Path $publishDirectory 'RosterCompanion.exe'
if (-not (Test-Path -LiteralPath $executablePath)) {
    throw 'The published executable was not created.'
}

Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Destination $publishDirectory
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'README.md') -Destination $publishDirectory
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'PRIVACY.md') -Destination $publishDirectory
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'SECURITY.md') -Destination $publishDirectory
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'CHANGELOG.md') -Destination $publishDirectory
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'THIRD-PARTY-NOTICES.md') -Destination $publishDirectory
$publicDocsDirectory = Join-Path $publishDirectory 'docs'
New-Item -ItemType Directory -Path $publicDocsDirectory -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs\TROUBLESHOOTING.md') -Destination $publicDocsDirectory
$releaseNotesPath = Join-Path $repositoryRoot "docs\RELEASE_NOTES_$Version.md"
if (Test-Path -LiteralPath $releaseNotesPath) {
    Copy-Item -LiteralPath $releaseNotesPath -Destination $publicDocsDirectory
}

$dotnetRoot = [System.IO.Path]::GetDirectoryName((Get-Command dotnet).Source)
$dotnetLicense = Join-Path $dotnetRoot 'LICENSE.txt'
$dotnetNotices = Join-Path $dotnetRoot 'ThirdPartyNotices.txt'
if (-not (Test-Path -LiteralPath $dotnetLicense) -or -not (Test-Path -LiteralPath $dotnetNotices)) {
    throw 'The .NET redistribution license files could not be found.'
}
Copy-Item -LiteralPath $dotnetLicense -Destination (Join-Path $publishDirectory 'DOTNET-LICENSE.txt')
Copy-Item -LiteralPath $dotnetNotices -Destination (Join-Path $publishDirectory 'DOTNET-THIRD-PARTY-NOTICES.txt')

if (-not $SkipLaunchCheck) {
    $smokeRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("RosterCompanion.Smoke." + [Guid]::NewGuid().ToString('N'))
    $smokeData = Join-Path $smokeRoot 'data'
    $smokeCodex = Join-Path $smokeRoot 'codex'
    New-Item -ItemType Directory -Path $smokeData,$smokeCodex -Force | Out-Null
    $previousDataRoot = $env:CHATGPT_ROSTER_DATA_ROOT
    $previousCodexHome = $env:CHATGPT_ROSTER_CODEX_HOME
    $previousDisableAutostart = $env:ROSTER_COMPANION_DISABLE_AUTOSTART
    $previousForceDesktopEnrollment = $env:ROSTER_COMPANION_FORCE_DESKTOP_ENROLLMENT
    $previousSmokeInstance = $env:ROSTER_COMPANION_SMOKE_INSTANCE
    $process = $null
    try {
        $env:CHATGPT_ROSTER_DATA_ROOT = $smokeData
        $env:CHATGPT_ROSTER_CODEX_HOME = $smokeCodex
        $env:ROSTER_COMPANION_DISABLE_AUTOSTART = '1'
        $env:ROSTER_COMPANION_FORCE_DESKTOP_ENROLLMENT = $null
        $env:ROSTER_COMPANION_SMOKE_INSTANCE = [Guid]::NewGuid().ToString('D')
        $process = Start-Process -FilePath $executablePath -PassThru
        Start-Sleep -Seconds 3
        if ($process.HasExited) {
            throw "The packaged application exited during startup with code $($process.ExitCode)."
        }
        Write-Host 'Packaged executable startup check passed.'
    }
    finally {
        if ($null -ne $process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id
            Wait-Process -Id $process.Id -ErrorAction SilentlyContinue
        }
        $env:CHATGPT_ROSTER_DATA_ROOT = $previousDataRoot
        $env:CHATGPT_ROSTER_CODEX_HOME = $previousCodexHome
        $env:ROSTER_COMPANION_DISABLE_AUTOSTART = $previousDisableAutostart
        $env:ROSTER_COMPANION_FORCE_DESKTOP_ENROLLMENT = $previousForceDesktopEnrollment
        $env:ROSTER_COMPANION_SMOKE_INSTANCE = $previousSmokeInstance
        if (Test-Path -LiteralPath $smokeRoot) {
            Remove-Item -LiteralPath $smokeRoot -Recurse -Force
        }
    }
}

Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $zipPath -CompressionLevel Optimal
$exeHash = (Get-FileHash -LiteralPath $executablePath -Algorithm SHA256).Hash.ToLowerInvariant()
$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$checksumLines = @(
    "$exeHash  $packageName/RosterCompanion.exe",
    "$zipHash  $packageName.zip"
)
[System.IO.File]::WriteAllLines($checksumPath, $checksumLines, [System.Text.UTF8Encoding]::new($false))

Write-Host "Release package: $zipPath"
Write-Host "Checksums: $checksumPath"
