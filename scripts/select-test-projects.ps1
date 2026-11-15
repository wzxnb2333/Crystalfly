[CmdletBinding()]
param(
    [string[]]$ChangedPaths = @(),
    [string]$Baseline,
    [switch]$Full
)

if (-not $Full -and $Baseline -match '^[0-9a-f]{40}$') {
    git merge-base --is-ancestor $Baseline HEAD 2>$null
    if ($LASTEXITCODE -eq 0) {
        # A rename must count in both its old and new modules.
        $diff = @(git -c core.quotePath=false diff --no-renames --name-only $Baseline HEAD --)
        if ($LASTEXITCODE -eq 0) {
            $ChangedPaths = $diff
        }
        else {
            Write-Warning 'Git diff failed. Running all standard tests.'
        }
    }
}

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
