$ErrorActionPreference = 'Stop'
$selector = Join-Path $PSScriptRoot 'select-test-projects.ps1'

function Assert-Selection([string[]]$Paths, [string[]]$Expected, [switch]$Full) {
    $actual = @(& $selector -ChangedPaths $Paths -Full:$Full | ForEach-Object {
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
'Test project selection checks passed.'
