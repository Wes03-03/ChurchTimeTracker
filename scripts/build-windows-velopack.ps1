[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+([-.][0-9A-Za-z.-]+)?$')]
    [string]$Version,

    [Parameter(Mandatory)]
    [ValidatePattern('^https://github\.com/[^/]+/[^/]+/?$')]
    [string]$RepositoryUrl,

    [ValidateSet('x64', 'arm64')]
    [string]$Architecture = 'x64',

    [string]$ReleaseNotes = ''
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$projectPath = Join-Path $projectRoot 'ChurchTimeTracker.csproj'
$runtimeIdentifier = "win10-$Architecture"
$buildRoot = Join-Path $projectRoot "artifacts\windows\$Version"
$publishDirectory = Join-Path $buildRoot 'publish'
$releaseDirectory = Join-Path $buildRoot 'releases'

New-Item -ItemType Directory -Force -Path $publishDirectory, $releaseDirectory | Out-Null

Push-Location $projectRoot
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw "Tool restore failed with exit code $LASTEXITCODE."
    }

    dotnet publish $projectPath `
        -f net9.0-windows10.0.19041.0 `
        -c Release `
        -o $publishDirectory `
        -p:RuntimeIdentifierOverride=$runtimeIdentifier `
        -p:WindowsPackageType=None `
        -p:WindowsAppSDKSelfContained=true `
        -p:ApplicationDisplayVersion=$Version `
        -p:Version=$Version `
        -p:PackageVersion=$Version `
        -p:UpdateRepositoryUrl=$RepositoryUrl
    if ($LASTEXITCODE -ne 0) {
        throw "Windows publish failed with exit code $LASTEXITCODE."
    }

    dotnet tool run vpk download github `
        --repoUrl $RepositoryUrl `
        --outputDir $releaseDirectory
    if ($LASTEXITCODE -ne 0) {
        Write-Warning 'No previous Velopack release was downloaded. This is expected for the first release.'
    }

    $packArguments = @(
        'tool', 'run', 'vpk', 'pack',
        '--packId', 'ChurchTimeTracker',
        '--packVersion', $Version,
        '--packDir', $publishDirectory,
        '--mainExe', 'ChurchTimeTracker.exe',
        '--packTitle', 'Church Time Tracker',
        '--packAuthors', 'Church Time Tracker',
        '--runtime', "win-$Architecture",
        '--outputDir', $releaseDirectory
    )
    if (-not [string]::IsNullOrWhiteSpace($ReleaseNotes)) {
        $packArguments += @('--releaseNotes', $ReleaseNotes)
    }

    & dotnet @packArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Velopack packaging failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

Write-Host "Windows installer and update files: $releaseDirectory"
