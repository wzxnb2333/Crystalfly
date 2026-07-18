# Task 1 Report: Semi/Ursa global theme

## Files changed

- `Directory.Packages.props`
- `src/Crystalfly.App/Crystalfly.App.csproj`
- `src/Crystalfly.App/App.axaml`
- `src/Crystalfly.App/Styles/CrystalflyTheme.axaml`
- `tests/Crystalfly.App.Tests/Ui/ThemeRenderingTests.cs`
- `.superpowers/sdd/task-1-report.md`

## RED evidence

Command:

```powershell
dotnet test '.\tests\Crystalfly.App.Tests\Crystalfly.App.Tests.csproj' -c Release --filter 'FullyQualifiedName~Application_registers_Semi_and_Ursa_themes_without_Fluent'
```

Expected failure confirmed before production changes:

```text
Assert.Contains() Failure: Item not found in collection
Collection: ["FluentTheme", "Styles"]
Not found:  "SemiTheme"
Failed: 1, Passed: 0, Total: 1
```

## GREEN evidence

```powershell
dotnet restore '.\Crystalfly.slnx'
dotnet test '.\tests\Crystalfly.App.Tests\Crystalfly.App.Tests.csproj' -c Release --no-restore --filter 'FullyQualifiedName~ThemeRenderingTests'
dotnet test '.\tests\Crystalfly.App.Tests\Crystalfly.App.Tests.csproj' -c Release --no-restore
```

Results:

- Restore completed successfully.
- Focused theme tests: 16 passed, 0 failed.
- All application tests: 178 passed, 0 failed.
- Resolved package list contains Semi.Avalonia 12.1.0 and both Ursa 2.1.0 packages, with no Fluent theme package.

## Remaining concerns

None found within this task's scope.

## Self-review

- SemiTheme and UrsaSemiTheme precede the Crystalfly semantic theme include.
- Fluent package references and the FluentTheme registration are removed.
- Crystalfly `cfp-*` selectors and `Cf*` resources remain the semantic layer.
- Theme selectors and their rendering tests now use public Button, ListBoxItem, and TextBlock properties instead of template part names.
- Existing contrast, focus, disabled, light/dark, 44 px, and 48 px assertions pass unchanged.
- Semi's Inter metrics needed a semantic minimum width for the English download-rail label; the original rendering assertion now passes without changing the window markup.
- `MainWindow.axaml`, dialogs, settings, and view-model behavior were not changed.
