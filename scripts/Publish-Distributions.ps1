param(
    [ValidateSet("fdd")]
    [string[]]$Profiles = @("fdd"),
    [ValidateSet("win-x64")]
    [string]$RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "CrosshairOverlayTool.csproj"
$distRoot = Join-Path $repoRoot "dist"
$localDotnet = Join-Path $repoRoot ".tools\dotnet\dotnet.exe"
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { "dotnet" }

[xml]$projectFile = Get-Content -LiteralPath $projectPath -Raw
$releaseVersion = [string]($projectFile.Project.PropertyGroup |
    ForEach-Object { $_.Version } |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Select-Object -First 1)
if ($releaseVersion -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw "Invalid release version: $releaseVersion"
}

$artifactName = "CrosshairOverlayTool-v$releaseVersion-$RuntimeIdentifier-fdd"
$outputDirectory = Join-Path $distRoot $artifactName
$zipPath = Join-Path $distRoot "$artifactName.zip"
$checksumPath = "$zipPath.sha256"
foreach ($destination in @($outputDirectory, $zipPath, $checksumPath)) {
    if (Test-Path -LiteralPath $destination) {
        throw "Output already exists; it will not be overwritten: $destination"
    }
}
$documents = @("LICENSE", "README.md", "THIRD-PARTY-NOTICES.md")
foreach ($document in $documents) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $document) -PathType Leaf)) {
        throw "Required document is missing: $document"
    }
}

New-Item -ItemType Directory -Force -Path $distRoot | Out-Null
$publishArgs = @("publish", $projectPath, "-c", "Release", "-r", $RuntimeIdentifier,
    "--self-contained", "false", "-o", $outputDirectory,
    "/p:UseAppHost=true", "/p:PublishSingleFile=false", "/p:PublishAot=false",
    "/p:PublishTrimmed=false", "/p:DebugType=None", "/p:DebugSymbols=false")
& $dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "FDD publish failed." }

$expectedFiles = @("CrosshairOverlayTool.exe", "CrosshairOverlayTool.dll", "CrosshairOverlayTool.deps.json", "CrosshairOverlayTool.runtimeconfig.json")
$actualFiles = @(Get-ChildItem -LiteralPath $outputDirectory -Recurse -File | ForEach-Object {
    $_.FullName.Substring($outputDirectory.Length + 1)
})
if (Compare-Object $expectedFiles $actualFiles) { throw "Unexpected FDD output. No ZIP was produced." }
$runtime = Get-Content -LiteralPath (Join-Path $outputDirectory "CrosshairOverlayTool.runtimeconfig.json") -Raw | ConvertFrom-Json
$frameworkNames = @($runtime.runtimeOptions.frameworks | ForEach-Object { $_.name })
if ($null -ne $runtime.runtimeOptions.includedFrameworks -or
    (Compare-Object @("Microsoft.NETCore.App", "Microsoft.WindowsDesktop.App") $frameworkNames)) {
    throw "Output must require the external Desktop Runtime. No ZIP was produced."
}

# Resolve the exact apphost package used by this SDK rather than assuming a cache version.
$resolveArgs = @("msbuild", $projectPath, "-nologo", "-t:ResolveFrameworkReferences",
    "-p:RuntimeIdentifier=$RuntimeIdentifier", "-p:SelfContained=false", "-getItem:ResolvedAppHostPack")
$resolvedJson = & $dotnet @resolveArgs
if ($LASTEXITCODE -ne 0) { throw "Could not resolve the apphost license source." }
$resolved = ($resolvedJson -join [Environment]::NewLine) | ConvertFrom-Json
$packs = @($resolved.Items.ResolvedAppHostPack | Where-Object { $_.RuntimeIdentifier -eq $RuntimeIdentifier })
if ($packs.Count -ne 1) { throw "Expected exactly one resolved apphost package." }
$pack = $packs[0]
$licenseSource = Join-Path $pack.PackageDirectory "LICENSE.TXT"
$noticesSource = Join-Path $pack.PackageDirectory "THIRD-PARTY-NOTICES.TXT"
$nuspecPath = Join-Path $pack.PackageDirectory "$($pack.NuGetPackageId.ToLowerInvariant()).nuspec"
foreach ($sourceFile in @($licenseSource, $noticesSource, $nuspecPath)) {
    if (-not (Test-Path -LiteralPath $sourceFile -PathType Leaf)) {
        throw "Missing apphost attribution source: $sourceFile"
    }
}
[xml]$nuspec = Get-Content -LiteralPath $nuspecPath -Raw
if ($nuspec.package.metadata.license.InnerText -ne "MIT") {
    throw "The apphost license changed; review it before publishing."
}

foreach ($document in $documents) {
    Copy-Item -LiteralPath (Join-Path $repoRoot $document) -Destination $outputDirectory
}
$licensesDirectory = Join-Path $outputDirectory "licenses"
New-Item -ItemType Directory -Path $licensesDirectory | Out-Null
Copy-Item -LiteralPath $licenseSource -Destination (Join-Path $licensesDirectory "dotnet-apphost-LICENSE.txt")
Copy-Item -LiteralPath $noticesSource -Destination (Join-Path $licensesDirectory "dotnet-apphost-THIRD-PARTY-NOTICES.txt")
[ordered]@{
    packageId = $pack.NuGetPackageId
    packageVersion = $pack.NuGetPackageVersion
    packageUrl = "https://www.nuget.org/packages/$($pack.NuGetPackageId)/$($pack.NuGetPackageVersion)"
    repositoryUrl = [string]$nuspec.package.metadata.repository.url
    repositoryCommit = [string]$nuspec.package.metadata.repository.commit
    license = "MIT"
    licenseSha256 = (Get-FileHash -LiteralPath $licenseSource -Algorithm SHA256).Hash
    thirdPartyNoticesSha256 = (Get-FileHash -LiteralPath $noticesSource -Algorithm SHA256).Hash
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $licensesDirectory "apphost-package.json") -Encoding UTF8

Compress-Archive -LiteralPath @(Get-ChildItem -LiteralPath $outputDirectory | ForEach-Object FullName) -DestinationPath $zipPath -CompressionLevel Optimal
$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$zipHash  $artifactName.zip" | Set-Content -LiteralPath $checksumPath -Encoding ASCII
Write-Output "Release: v$releaseVersion (FDD only; external .NET Desktop Runtime 8 x64 required)"
Get-Item -LiteralPath $outputDirectory, $zipPath, $checksumPath | Select-Object Name, Length
