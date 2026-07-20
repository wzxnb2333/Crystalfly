[CmdletBinding()]
param(
    [string]$Version,
    [string]$IsccPath
)

$ErrorActionPreference = 'Stop'

$InstallDirectory = 'D:\Program Files\Crystalfly'

function Get-ProjectVersion {
    param(
        [Parameter(Mandatory)]
        [string]$ProjectRoot
    )

    $propsPath = Join-Path $ProjectRoot 'Directory.Build.props'
    if (-not (Test-Path -LiteralPath $propsPath -PathType Leaf)) {
        throw "Project version file was not found at '$propsPath'."
    }

    [xml]$props = Get-Content -LiteralPath $propsPath -Raw
    $version = @($props.Project.PropertyGroup | ForEach-Object { $_.Version } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -First 1)
    if ($version.Count -ne 1 -or $version[0] -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$') {
        throw "Directory.Build.props does not contain a valid release version."
    }

    [string]$version[0]
}

function Get-ReleaseInstallerPath {
    param(
        [Parameter(Mandatory)]
        [string]$ProjectRoot,
        [Parameter(Mandatory)]
        [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$')]
        [string]$ReleaseVersion
    )

    Join-Path $ProjectRoot "artifacts\installer\Crystalfly-$ReleaseVersion-win-x64-setup.exe"
}

function Get-InstallerArguments {
    param(
        [Parameter(Mandatory)]
        [string]$TargetDirectory
    )

    if (-not [string]::Equals(
        [IO.Path]::GetFullPath($TargetDirectory).TrimEnd('\\'),
        $InstallDirectory,
        [StringComparison]::OrdinalIgnoreCase)) {
        throw "The installation directory must be '$InstallDirectory'."
    }

    @(
        '/VERYSILENT'
        '/SUPPRESSMSGBOXES'
        '/NORESTART'
        '/SP-'
        ('/DIR="{0}"' -f $InstallDirectory)
    )
}

function Assert-CrystalflyIsStopped {
    param(
        [Parameter(Mandatory)]
        [string]$TargetDirectory
    )

    $targetExecutable = Join-Path $TargetDirectory 'Crystalfly.App.exe'
    $running = @(Get-Process -Name 'Crystalfly.App' -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Path -and [string]::Equals(
                [IO.Path]::GetFullPath($_.Path),
                $targetExecutable,
                [StringComparison]::OrdinalIgnoreCase)
        })
    if ($running.Count -gt 0) {
        throw "Crystalfly is running from '$TargetDirectory'. Close it before installing the update."
    }
}

function Assert-InstalledVersion {
    param(
        [Parameter(Mandatory)]
        [string]$TargetDirectory,
        [Parameter(Mandatory)]
        [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$')]
        [string]$ReleaseVersion
    )

    $executable = Join-Path $TargetDirectory 'Crystalfly.App.exe'
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
        throw "Installation did not create '$executable'."
    }

    $installedVersion = (Get-Item -LiteralPath $executable).VersionInfo.ProductVersion
    if ([string]::IsNullOrWhiteSpace($installedVersion) -or -not $installedVersion.StartsWith($ReleaseVersion, [StringComparison]::Ordinal)) {
        throw "Installed Crystalfly version '$installedVersion' does not match '$ReleaseVersion'."
    }
}

function Invoke-Installer {
    param(
        [Parameter(Mandatory)]
        [string]$InstallerPath,
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    $process = Start-Process -FilePath $InstallerPath -ArgumentList ($Arguments -join ' ') `
        -Verb RunAs -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw "Installer failed with exit code $($process.ExitCode)."
    }
}

if ($MyInvocation.InvocationName -eq '.') {
    return
}

$projectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-ProjectVersion -ProjectRoot $projectRoot
}

$buildScript = Join-Path $PSScriptRoot 'build-release.ps1'
if (-not (Test-Path -LiteralPath $buildScript -PathType Leaf)) {
    throw "Release build script was not found at '$buildScript'."
}

$buildArguments = @('-NoProfile', '-File', $buildScript, '-Version', $Version)
if (-not [string]::IsNullOrWhiteSpace($IsccPath)) {
    $buildArguments += @('-IsccPath', $IsccPath)
}

Assert-CrystalflyIsStopped -TargetDirectory $InstallDirectory
& pwsh @buildArguments
if ($LASTEXITCODE -ne 0) {
    throw "Release build failed with exit code $LASTEXITCODE."
}

$installer = Get-ReleaseInstallerPath -ProjectRoot $projectRoot -ReleaseVersion $Version
if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) {
    throw "Release build did not create '$installer'."
}

Invoke-Installer -InstallerPath $installer -Arguments (Get-InstallerArguments -TargetDirectory $InstallDirectory)
Assert-InstalledVersion -TargetDirectory $InstallDirectory -ReleaseVersion $Version

Write-Host "Crystalfly $Version was installed to '$InstallDirectory'."
