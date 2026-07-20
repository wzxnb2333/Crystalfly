[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

function Assert-Rejected {
    param(
        [Parameter(Mandatory)]
        [scriptblock]$Action,
        [Parameter(Mandatory)]
        [string]$ExpectedMessage
    )

    try {
        & $Action
    }
    catch {
        if ($_.Exception.Message -like "*$ExpectedMessage*") {
            return
        }
        throw "Unexpected rejection: $($_.Exception.Message)"
    }

    throw "Expected rejection containing '$ExpectedMessage'."
}

$installScript = Join-Path $PSScriptRoot 'build-and-install.ps1'
$source = Get-Content -LiteralPath $installScript -Raw
$root = Split-Path -Parent $PSScriptRoot

if ($source -notmatch "\$InstallDirectory = 'D:\\Program Files\\Crystalfly'") {
    throw 'Build-and-install must target D:\Program Files\Crystalfly.'
}
foreach ($requiredArgument in '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-') {
    if ($source -notmatch [regex]::Escape($requiredArgument)) {
        throw "Build-and-install must pass $requiredArgument to Inno Setup."
    }
}
if ($source -notmatch 'Assert-CrystalflyIsStopped' -or $source -notmatch 'Assert-InstalledVersion') {
    throw 'Build-and-install must check both the running process and installed version.'
}
if ($source -notmatch '& pwsh @buildArguments') {
    throw 'Build-and-install must invoke the release build in a child PowerShell process.'
}

. $installScript

$testRoot = Join-Path ([IO.Path]::GetTempPath()) "Crystalfly.InstallTests\$([Guid]::NewGuid().ToString('N'))"
try {
    New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $testRoot 'Directory.Build.props') -Encoding utf8 -Value @'
<Project>
  <PropertyGroup>
    <Version>9.8.7</Version>
  </PropertyGroup>
</Project>
'@

    if ((Get-ProjectVersion -ProjectRoot $testRoot) -ne '9.8.7') {
        throw 'Project version was not read from Directory.Build.props.'
    }
    if ((Get-ReleaseInstallerPath -ProjectRoot $testRoot -ReleaseVersion '9.8.7') -ne (Join-Path $testRoot 'artifacts\installer\Crystalfly-9.8.7-win-x64-setup.exe')) {
        throw 'Release installer path is incorrect.'
    }

    $arguments = Get-InstallerArguments -TargetDirectory 'D:\Program Files\Crystalfly'
    if (($arguments -join ' ') -notmatch '/DIR="D:\\Program Files\\Crystalfly"') {
        throw 'Installer directory argument is incorrect.'
    }
    Assert-Rejected -ExpectedMessage 'installation directory must be' -Action {
        Get-InstallerArguments -TargetDirectory 'D:\Elsewhere' | Out-Null
    }

    Set-Content -LiteralPath (Join-Path $testRoot 'Directory.Build.props') -Encoding utf8 -Value '<Project />'
    Assert-Rejected -ExpectedMessage 'does not contain a valid release version' -Action {
        Get-ProjectVersion -ProjectRoot $testRoot | Out-Null
    }
    Assert-Rejected -ExpectedMessage 'Installation did not create' -Action {
        Assert-InstalledVersion -TargetDirectory $testRoot -ReleaseVersion '9.8.7'
    }
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}

'Build-and-install validation passed.'
