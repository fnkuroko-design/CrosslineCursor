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
    (Compare-Object @("Microsoft.NETCore.App", "Microsoft.WindowsDesktop.App") $frameworkNames) -or
    $runtime.runtimeOptions.tfm -ne "net10.0" -or
    @($runtime.runtimeOptions.frameworks | Where-Object { $_.version -notmatch '^10\.0\.\d+$' }).Count -ne 0) {
    throw "Output must require the external .NET Desktop Runtime 10. No ZIP was produced."
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
# SDK-bundled packs omit NuGet metadata, but their resolved directory identifies
# the exact package and version. The official binary is verified below.
$hostPackageId = if ($pack.NuGetPackageId) { $pack.NuGetPackageId } else {
    Split-Path -Leaf (Split-Path -Parent $pack.PackageDirectory)
}
$hostPackageVersion = if ($pack.NuGetPackageVersion) { $pack.NuGetPackageVersion } else {
    Split-Path -Leaf $pack.PackageDirectory
}
if ($hostPackageId -ne "Microsoft.NETCore.App.Host.win-x64" -or $hostPackageVersion -notmatch '^10\.0\.\d+$') {
    throw "Expected the stable .NET 10 win-x64 apphost package."
}
$licenseSource = Join-Path $pack.PackageDirectory "LICENSE.TXT"
$noticesSource = Join-Path $pack.PackageDirectory "THIRD-PARTY-NOTICES.TXT"
$nuspecPath = Join-Path $pack.PackageDirectory "$($hostPackageId.ToLowerInvariant()).nuspec"

# SDK-bundled host packs may omit attribution files. Obtain the same official NuGet
# package and verify that its apphost is byte-identical to the host used by MSBuild.
if (-not (Test-Path -LiteralPath $licenseSource) -or
    -not (Test-Path -LiteralPath $noticesSource) -or
    -not (Test-Path -LiteralPath $nuspecPath)) {
    $packageId = $hostPackageId.ToLowerInvariant()
    $packageVersion = $hostPackageVersion
    $attributionDirectory = Join-Path $repoRoot "temp\apphost-attribution\$packageId\$packageVersion"
    New-Item -ItemType Directory -Force -Path $attributionDirectory | Out-Null
    $packageArchivePath = Join-Path $attributionDirectory "$packageId.$packageVersion.nupkg"
    $packageUri = "https://api.nuget.org/v3-flatcontainer/$packageId/$packageVersion/$packageId.$packageVersion.nupkg"
    Invoke-WebRequest -Uri $packageUri -OutFile $packageArchivePath -UseBasicParsing
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $packageArchive = [System.IO.Compression.ZipFile]::OpenRead($packageArchivePath)
    try {
        $entriesToExtract = [ordered]@{
            "LICENSE.TXT" = "LICENSE.TXT"
            "THIRD-PARTY-NOTICES.TXT" = "THIRD-PARTY-NOTICES.TXT"
            "$packageId.nuspec" = "$packageId.nuspec"
            "runtimes/win-x64/native/apphost.exe" = "apphost.exe"
        }
        foreach ($entryName in $entriesToExtract.Keys) {
            $matches = @($packageArchive.Entries | Where-Object { $_.FullName -ieq $entryName })
            if ($matches.Count -ne 1) { throw "Official apphost package is missing or duplicates $entryName" }
            $entry = $matches[0]
            $destination = Join-Path $attributionDirectory $entriesToExtract[$entryName]
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destination, $true)
        }
    } finally { $packageArchive.Dispose() }
    $officialHostHash = (Get-FileHash -LiteralPath (Join-Path $attributionDirectory "apphost.exe") -Algorithm SHA256).Hash
    $resolvedHostHash = (Get-FileHash -LiteralPath $pack.Path -Algorithm SHA256).Hash
    if ($officialHostHash -ne $resolvedHostHash) {
        throw "Official package apphost differs from the resolved SDK host; attribution cannot be verified."
    }
    $licenseSource = Join-Path $attributionDirectory "LICENSE.TXT"
    $noticesSource = Join-Path $attributionDirectory "THIRD-PARTY-NOTICES.TXT"
    $nuspecPath = Join-Path $attributionDirectory "$packageId.nuspec"
}
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
    packageId = $hostPackageId
    packageVersion = $hostPackageVersion
    packageUrl = "https://www.nuget.org/packages/$hostPackageId/$hostPackageVersion"
    repositoryUrl = [string]$nuspec.package.metadata.repository.url
    repositoryCommit = [string]$nuspec.package.metadata.repository.commit
    license = "MIT"
    licenseSha256 = (Get-FileHash -LiteralPath $licenseSource -Algorithm SHA256).Hash
    thirdPartyNoticesSha256 = (Get-FileHash -LiteralPath $noticesSource -Algorithm SHA256).Hash
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $licensesDirectory "apphost-package.json") -Encoding UTF8

Compress-Archive -LiteralPath @(Get-ChildItem -LiteralPath $outputDirectory | ForEach-Object FullName) -DestinationPath $zipPath -CompressionLevel Optimal
$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$zipHash  $artifactName.zip" | Set-Content -LiteralPath $checksumPath -Encoding ASCII
Write-Output "Release: v$releaseVersion (FDD only; external .NET Desktop Runtime 10 x64 required)"
Get-Item -LiteralPath $outputDirectory, $zipPath, $checksumPath | Select-Object Name, Length
