[CmdletBinding()]
param(
    [string[]]$ChangedPaths = @(),
    [switch]$Full
)

$modules = @('Updater', 'Steam', 'Core', 'App')
$selected = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
if ($Full -or $ChangedPaths.Count -eq 0) {
    $selected.UnionWith([string[]]$modules)
}
else {
    foreach ($path in $ChangedPaths) {
        switch -Regex ($path.Replace('\', '/')) {
            '^src/Crystalfly\.(Core|Steam|Updater)/' {
                [void]$selected.Add($Matches[1])
                [void]$selected.Add('App')
                break
            }
            '^src/Crystalfly\.App/|^tests/Crystalfly\.Diagnostics\.Probe/|^docs/releases/' {
                [void]$selected.Add('App')
                break
            }
            '^tests/Crystalfly\.(Updater|Steam|Core|App)\.Tests/' {
                [void]$selected.Add($Matches[1])
                break
            }
            '^catalog/' {
                $selected.UnionWith([string[]]@('Core', 'App'))
                break
            }
            '^docs/|\.md$' { break }
            default { $selected.UnionWith([string[]]$modules) }
        }
    }
}

foreach ($module in $modules) {
    if ($selected.Contains($module)) {
        "tests/Crystalfly.$module.Tests/Crystalfly.$module.Tests.csproj"
    }
}
