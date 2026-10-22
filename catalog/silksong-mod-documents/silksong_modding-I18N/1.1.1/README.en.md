# Silksong.I18N

A Hollow Knight: Silksong mod that automatically loads localized text for other mods.

## Translate a Mod (for Users)

To translate a mod, you need to find that mod's translations folder. The translations folder should
be in another folder with the mod's name in `BepInEx/plugins`. If the mod uses I18N, it will have a
`languages`, `Languages`, or `LANGUAGES` folder next to its `.dll` file. That folder should contain
one or more `.json` files named for the languages they define: `en.json`, `fr.json`, and so on. The
capitalization doesn't matter.

The allowed languages are: `en.json` (English), `fr.json` (French), `de.json` (German), `zh.json`
(Simplified Chinese), `es.json` (Spanish), `ko.json` (Korean), `ja.json` (Japanese), `it.json`
(Italian), `pt.json` (Brazillian Portugese), and `ru.json` (Russian). You cannot currently translate
a mod into a language that is not one of these unless another mod provides support.

To add your language to a mod, first you need to create your translation file. Copy one of the mod's
`.json` language files (whichever has the most lines) and rename it to your language's two-letter
code. If you wanted to translate an English mod to Brazillian Portugese, you would copy the mod's
`en.json` file and rename it to `pt.json`.

Then, open the new translation file in your text editor (Notepad on Windows) and translate all of
the text **after the colons (`:`)** on each line of the file. **Do not edit the text before the
colons (`:`)!** The text before the colons is how the mod knows which line is which.

For example, in the following translation file:

```json
{
    "TRANSLATION_KEY": "Localized text!",
    "CUSTOM_TOOL_NAME": "Super Cool Tool",
    "CUSTOM_TOOL_DESC": "The name of this tool rhymes. How amusing.",
    "SOME_DIALOGUE": "Hi! I’m an NPC…<hpage>And I’m Hornet.<page>Yay!"
}
```

You should edit `Localized text!`, `Super Cool Tool`, `The name of this tool rhymes. How amusing.`,
and `Hi! I’m an NPC…<hpage>And I’m Hornet.<page>Yay!` by replacing the text with its translation
into your language. You should **not** edit `TRANSLATION_KEY`, `CUSTOM_TOOL_NAME`,
`CUSTOM_TOOL_DESC`, `SOME_DIALOGUE`, or any of the colons (`:`), straight quotation marks (`"`),
commas at the ends of lines (`,`), or the curly brackets (`{` and `}`).

To use your translation in-game, change the game's language (in the main menu) to your language.

## Usage (for Mod Developers)

If your mod requires localized text to function, add this as a dependency by adding the following
attribute to your mod's `BaseUnityPlugin` class:

```cs
[BepInDependency("org.silksong-modding.i18n")]
```

Additionally, if you're going to publish your mod on Thunderstore, you need to add this as a
dependency in your mod's Thunderstore manifest. To do that, add the following like to your
`thunderstore.toml` file after the `[package.dependencies]` definition:

```toml
silksong_modding-I18N = "1.0.2"
```

Alternatively, if you don't have a `thunderstore.toml` file, you can add this as a dependency by
adding `"silksong_modding-I18N-1.0.2"` to the `dependencies` array in your Thunderstore
`manifest.json` file.

This mod does not need to be added to your project as a reference.

### Set a Default Language

You should set a default language for your mod to use if the game's current language isn't supported
or is missing translations. The default language should be whichever language is the best supported
by your mod.

You can set your mod's default language directly from C# code by adding the
`System.Resources.NeutralResourcesLanguage` attribute to your mod's assembly. To set your mod's
default language to English, you could add the following attribute to your mod's source code:

```cs
[assembly: System.Resources.NeutralResourcesLanguage("EN")]
```

Anywhere in the C# code will work, but the recommended location is `Properties/AssemblyInfo.cs`.

Alternatively, you can define the `NeutralLanguage` property in your mod's project file. To set your
mod's default language to English, you could add the following to your project file:

```xml
<PropertyGroup>
    <NeutralLanguage>EN</NeutralLanguage>
</PropertyGroup>
```

#### Format Strings and Default Language

In some cases, it may be desirable to define a language string of the form `{1} {0}` to be passed as the first
argument of `string.Format`. This may lead to grammatical issues in other languages; in such cases it
may be desirable to use the form `{0} - {1}`, for example `Bellway - Bilewater` is less likely to be unnatural
in an arbitrary language than `Bilewater Bellway` which is natural in English. However, if it is desired to
preserve the correct order in English, then EN cannot be used as the default language. To accomplish this, the
following scheme is recommended:

- Set the default language to an arbitrary language such as `DEFAULT`.
- Populate `default.json` with default format strings and English-language text (or any other desired language)
- Populate `en.json` with only the overridden format strings

### Add Localized Text

To add localized text to your mod, create a directory named `languages` next to your mod's assembly.
Inside that directory, you can add files named `{lang}.json`, where `{lang}` is the two-letter
language code used by the game. Language files are loaded case-insensitively. If your mod is
`YourName.YourModName`, the following file structure would define localized text for your mod in
English and French:

```
BepInEx/
    plugins/
        silksong_modding-I18N/
            Silksong.I18N.dll
        YourName.YourModName/
            YourName.YourModName.dll
            languages/
                en.json
                fr.json
```

The following language codes are supported by the game: `EN` (English), `FR` (French), `DE`
(German), `ZH` (Simplified Chinese), `ES` (Spanish), `KO` (Korean), `JA` (Japanese), `IT` (Italian),
`PT` (Brazillian Portugese), and `RU` (Russian).

The contents of the localized text files are JSON objects mapping translation keys to localized text
values. For example, the contents of `en.json` could be:

```json
{
    "TRANSLATION_KEY": "Localized text!",
    "CUSTOM_TOOL_NAME": "Super Cool Tool",
    "CUSTOM_TOOL_DESC": "The name of this tool rhymes. How amusing.",
    "SOME_DIALOGUE": "Hi! I’m an NPC…<hpage>And I’m Hornet.<page>Yay!"
}
```

If your mod defines a default language and the game is in a language that your mod doesn't have a
translation file for, the default language's translation file will be used instead. If your mod has
a translation file for the game's language but some of the keys are missing, the missing keys will
be loaded from the default language's translation file instead.

If your mod had a default language of `EN` and the contents of `fr.json` were as follows:

```json
{
    "CUSTOM_TOOL_NAME": "Outil super cool",
    "CUSTOM_TOOL_DESC": "Le nom de cet outil ne rime pas. Ce n’est pas drôle."
}
```

Then the keys `TRANSLATION_KEY` and `SOME_DIALOGUE` would be loaded from `en.json` instead.

It's **strongly recommended** that you never put the colon character (`:`) in any translation keys
used by your mod, as users translating your mod could be confused by that if they don't know JSON
and don't have JSON syntax highlighting in their text editor.

#### Include Localized Text in Your Project File

To add your localized text to your mod package, you will have to update your `.csproj` file.
Locate the `ItemGroup` containing `Binaries` items and edit it to include your language files by
adding a new entry for `languages/*.json`. Additionally, edit the `PackDir` properties on each entry
to all start with `plugins`. The result should look like this:

```xml
<ItemGroup>
  <!-- Replace `PackDir="/"` with `PackDir="plugins"`: -->
  <Binaries Include="$(TargetPath)" LocalDir="/" PackDir="plugins" />
  <Binaries Include="$(TargetDir)/$(TargetName).pdb" LocalDir="/" PackDir="plugins" />
  <!-- Add this line: -->
  <Binaries Include="languages/*.json" LocalDir="languages" PackDir="plugins/languages" />
</ItemGroup>
```

`LocalDir` is where the file will be copied when installing your mod locally as a post-build step.
`PackDir` is where the file will be copied in the Thunderstore package. Due to Thunderstore
installation rules, these will generally be different when you want a nested directory in your
installed package (such as here).

If you created your mod with an older version of the Silksong plugin template, you will also need to
update the Copy targets to handle the `LocalDir` and `PackDir` properties. You can reference a more
updated version of the
[template](https://github.com/silksong-modding/Silksong.Modding.Templates/blob/61ab392917e46cb272ed1c38ee27cd4b79c91754/Silksong.Modding.Templates/content/SilksongPlugin/Silksong.Plugin.1.csproj#L57-L75)
to see the necessary changes.

### Use Localized Text

Your mod's localized text will be loaded to a sheet named `Mods.{id}`, where `{id}` is the unique ID
of your mod's BepInEx plugin. If your mod is `YourName.YourModName`, the sheet will be named
`Mods.YourName.YourModName`.

To access localized text you can use the `TeamCherry.Localization.LocalisedString` and
`TeamCherry.Localization.Language` classes. Keep in mind that the `LocalisedString` class name uses
the Australian English spelling (“localised”) but the namespace `TeamCherry.Localization` does not.
Make sure any files using localized text import the `TeamCherry.Localization` namespace like so:

```cs
using TeamCherry.Localization;
```

Then, you can access your mod's localized text as a `LocalisedString` by creating one with the right
sheet name and key name. If your mod's plugin is `YourModPlugin` and it defines a key called
`CUSTOM_TOOL_NAME`, you can access that key like so:

```cs
new LocalisedString($"Mods.{YourModPlugin.Id}", "CUSTOM_TOOL_NAME");
```

Alternatively, you can directly access strings loaded by your mod by calling `Language.Get`. If
your mod's plugin is `YourModPlugin` and it defines a key called `CUSTOM_TOOL_DESC`, you can access
that key as a `string` like so:

```cs
Language.Get("CUSTOM_TOOL_DESC", $"Mods.{YourModPlugin.Id}")
```
