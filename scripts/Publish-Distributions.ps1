param(
    [ValidateSet("fdd", "scd", "single-file")]
    [string[]]$Profiles = @("fdd"),

    [string]$RuntimeIdentifier = "win-x64",

    [switch]$CleanOnly
)

$ErrorActionPreference = "Stop"

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDirectory
$projectPath = Join-Path $repoRoot "CrosshairOverlayTool.csproj"
$distRoot = Join-Path $repoRoot "dist"
$localDotnet = Join-Path $repoRoot ".tools\\dotnet\\dotnet.exe"
$dotnet = if (Test-Path $localDotnet) { $localDotnet } else { "dotnet" }

[xml]$projectFile = Get-Content -LiteralPath $projectPath -Raw
$releaseVersion = [string]($projectFile.Project.PropertyGroup |
    ForEach-Object { $_.Version } |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Select-Object -First 1)

if ([string]::IsNullOrWhiteSpace($releaseVersion)) {
    throw "Release version was not found in CrosshairOverlayTool.csproj."
}

$artifactPrefix = "CrosshairOverlayTool-v$releaseVersion-$RuntimeIdentifier"

$profileMap = @{
    "fdd" = @{
        FolderName = "$artifactPrefix-fdd"
        Arguments = @(
            "publish", $projectPath,
            "-c", "Release",
            "-r", $RuntimeIdentifier,
            "--self-contained", "false",
            "-o", "",
            "/p:UseAppHost=true",
            "/p:PublishSingleFile=false",
            "/p:DebugType=None",
            "/p:DebugSymbols=false"
        )
    }
    "scd" = @{
        FolderName = "$artifactPrefix-scd"
        Arguments = @(
            "publish", $projectPath,
            "-c", "Release",
            "-r", $RuntimeIdentifier,
            "--self-contained", "true",
            "-o", "",
            "/p:PublishSingleFile=false",
            "/p:DebugType=None",
            "/p:DebugSymbols=false"
        )
    }
    "single-file" = @{
        FolderName = "$artifactPrefix-singlefile"
        Arguments = @(
            "publish", $projectPath,
            "-c", "Release",
            "-r", $RuntimeIdentifier,
            "--self-contained", "true",
            "-o", "",
            "/p:PublishSingleFile=true",
            "/p:IncludeNativeLibrariesForSelfExtract=true",
            "/p:EnableCompressionInSingleFile=true",
            "/p:DebugType=None",
            "/p:DebugSymbols=false"
        )
    }
}

New-Item -ItemType Directory -Force -Path $distRoot | Out-Null
Write-Output "Release version: v$releaseVersion"

foreach ($profile in $Profiles) {
    $config = $profileMap[$profile]
    if ($null -eq $config) {
        throw "Unknown profile: $profile"
    }

    $outputDirectory = Join-Path $distRoot $config.FolderName
    $zipPath = Join-Path $distRoot "$($config.FolderName).zip"

    if (Test-Path $outputDirectory) {
        Remove-Item -Path $outputDirectory -Recurse -Force
    }

    if (Test-Path $zipPath) {
        Remove-Item -Path $zipPath -Force
    }

    if ($CleanOnly) {
        continue
    }

    $arguments = [string[]]$config.Arguments.Clone()
    $outputIndex = [Array]::IndexOf($arguments, "-o") + 1
    $arguments[$outputIndex] = $outputDirectory

    & $dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Publish failed for profile '$profile'."
    }

    Get-ChildItem -Path $outputDirectory -Recurse -Filter "*.pdb" | Remove-Item -Force

    Compress-Archive -Path (Join-Path $outputDirectory "*") -DestinationPath $zipPath -CompressionLevel Optimal
}

Get-ChildItem -Path $distRoot | Select-Object Name, Length, LastWriteTime
