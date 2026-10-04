[CmdletBinding()]
param(
    [ValidateSet('x64', 'arm64')]
    [string]$Architecture = 'x64',

    [string]$CertificateThumbprint = ''
)

$ErrorActionPreference = 'Stop'
$projectPath = Join-Path (Split-Path $PSScriptRoot -Parent) 'ChurchTimeTracker.csproj'
$runtimeIdentifier = "win10-$Architecture"
$publishArguments = @(
    'publish', $projectPath,
    '-f', 'net9.0-windows10.0.19041.0',
    '-c', 'Release',
    "-p:RuntimeIdentifierOverride=$runtimeIdentifier",
    '-p:WindowsPackageType=MSIX',
    '-p:WindowsAppSDKSelfContained=true',
    '-p:GenerateAppxPackageOnBuild=true',
    '-p:AppxBundle=Never'
)

if ([string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    Write-Warning 'Building an unsigned development MSIX. Supply -CertificateThumbprint for an installable release.'
    $publishArguments += '-p:AppxPackageSigningEnabled=false'
}
else {
    $publishArguments += '-p:AppxPackageSigningEnabled=true'
    $publishArguments += "-p:PackageCertificateThumbprint=$CertificateThumbprint"
}

& dotnet @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "Windows publish failed with exit code $LASTEXITCODE."
}

$projectRoot = Split-Path $projectPath -Parent
$package = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'bin\Release') -Filter '*.msix' -Recurse |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($null -eq $package) {
    throw 'Publish completed, but no MSIX package was found.'
}

Write-Host "Installer created: $($package.FullName)"
