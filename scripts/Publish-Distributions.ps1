param(
    [ValidateSet("fdd", "scd", "single-file")]
    [string[]]$Profiles = @("fdd", "scd"),

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

$profileMap = @{
    "fdd" = @{
        FolderName = "CrosshairOverlayTool-$RuntimeIdentifier-fdd"
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
        FolderName = "CrosshairOverlayTool-$RuntimeIdentifier-scd"
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
        FolderName = "CrosshairOverlayTool-$RuntimeIdentifier-singlefile"
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
