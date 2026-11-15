$ErrorActionPreference = 'Stop'
$selector = Join-Path $PSScriptRoot 'select-test-projects.ps1'

function Assert-Selection([string[]]$Paths, [string[]]$Expected, [switch]$Full, [string]$Baseline) {
    $actual = @(& $selector -ChangedPaths $Paths -Full:$Full -Baseline $Baseline | ForEach-Object {
        [IO.Path]::GetFileNameWithoutExtension($_).Replace('Crystalfly.', '').Replace('.Tests', '')
    })
    if (($actual -join ',') -ne ($Expected -join ',')) {
        throw "Unexpected selection for '$($Paths -join ',')': $($actual -join ',')"
    }
}

Assert-Selection @('src/Crystalfly.App/Views/MainWindow.axaml', 'README.md') @('App')
Assert-Selection @('src/Crystalfly.Core/Mods/ModManager.cs') @('Core', 'App')
Assert-Selection @('src/Crystalfly.Steam/Downloads/DownloadPath.cs') @('Steam', 'App')
Assert-Selection @('src/Crystalfly.Updater/Program.cs') @('Updater', 'App')
Assert-Selection @('tests/Crystalfly.App.Tests/Ui/LayoutRenderingTests.cs') @('App')
Assert-Selection @('src\Crystalfly.Core\Mods\ModManager.cs', 'src/Crystalfly.Steam/Steam.cs') @('Steam', 'Core', 'App')
Assert-Selection @('catalog/silksong-mod-documents/example.md') @('Core', 'App')
Assert-Selection @('docs/releases/1.1.8.md') @('App')
Assert-Selection @('README.md', 'docs/screenshots/example.png') @()
Assert-Selection @('Directory.Build.props') @('Updater', 'Steam', 'Core', 'App')
Assert-Selection @() @('Updater', 'Steam', 'Core', 'App')
Assert-Selection @('src/Crystalfly.App/App.axaml') @('Updater', 'Steam', 'Core', 'App') -Full

function Save-FixtureCommit([string]$Message) {
    git add --all
    if ($LASTEXITCODE -ne 0) { throw 'Could not stage the fixture.' }
    git -c user.name=CI -c user.email=ci@example.invalid -c commit.gpgsign=false commit -qm $Message
    if ($LASTEXITCODE -ne 0) { throw 'Could not commit the fixture.' }
}

$tempRoot = (Resolve-Path -LiteralPath ([IO.Path]::GetTempPath())).Path
$fixture = Join-Path $tempRoot ("crystalfly-ci-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
Push-Location -LiteralPath $fixture
try {
    git init -q
    if ($LASTEXITCODE -ne 0) { throw 'Could not initialize the fixture.' }
    New-Item -ItemType Directory -Path 'src/Crystalfly.Core', 'src/Crystalfly.Updater', 'docs' | Out-Null
    Set-Content -LiteralPath 'src/Crystalfly.Core/Old.cs' -Value 'old source'
    Save-FixtureCommit 'Last successful CI'
    $baseline = git rev-parse HEAD
    git mv src/Crystalfly.Core/Old.cs docs/Old.md
    if ($LASTEXITCODE -ne 0) { throw 'Could not rename the fixture source.' }
    Save-FixtureCommit 'Commit with no successful CI'
    Set-Content -LiteralPath 'src/Crystalfly.Updater/New.cs' -Value 'new source'
    Save-FixtureCommit 'Current CI commit'
    Assert-Selection @() @('Updater', 'Core', 'App') -Baseline $baseline
    Assert-Selection @() @('Updater', 'Steam', 'Core', 'App') -Baseline ('0' * 40)
}
finally {
    Pop-Location
    $resolved = (Resolve-Path -LiteralPath $fixture).Path
    if ($resolved -eq $fixture -and (Split-Path -Parent $resolved) -eq $tempRoot) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
'Test project selection checks passed.'
