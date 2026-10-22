# FilteredLogs

Filters logs to include only those from specified log sources.

This mod only applies when the caller has been built in debug mode.

## Usage

This mod should be declared as a package reference by placing the following line in your csproj file:

```
<PackageReference Include="Silksong.FilteredLogs" Version="1.0.0" />
```

See [the nuget page](https://www.nuget.org/packages/Silksong.FilteredLogs) for the most up to date version.

This mod should be declared as a soft dependency, to ensure that it loads before
your mod. To do so, place the following attribute on your plugin class, below
the BepInAutoPlugin attribute:

```
[BepInDependency("io.github.flibber-hk.filteredlogs", BepInDependency.DependencyFlags.SoftDependency)]
```

This mod should typically not be declared as a dependency on Thunderstore; the compiler will
remove all references to this mod's API when built in release mode so your mod will
function as normal on other users' machines. You will have to download this mod
yourself, of course.

Typically, you should place the following line at the start of your plugin's awake method:

```
FilteredLogs.API.ApplyFilter(Name);
```

This will cause all logs not from your mod to be filtered. (Of course, when building in release mode
this line will be ignored by the compiler.)

The overloads of ApplyFilter provide alternate ways to filter the logs; for more details consult the documentation.
