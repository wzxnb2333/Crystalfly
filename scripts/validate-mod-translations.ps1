[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$OfficialModLinksPath,

    [string]$TranslationCatalogPath = (Join-Path $PSScriptRoot '..\catalog\mod-translations.zh-CN.v1.json')
)

$ErrorActionPreference = 'Stop'

function Assert-Condition {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

[xml]$official = Get-Content -Raw -LiteralPath $OfficialModLinksPath
$namespace = New-Object System.Xml.XmlNamespaceManager($official.NameTable)
$namespace.AddNamespace('m', $official.DocumentElement.NamespaceURI)
$officialIds = @($official.SelectNodes('/m:ModLinks/m:Manifest/m:Name', $namespace) |
    ForEach-Object { "hkmod:$($_.InnerText.Trim())" } |
    Sort-Object -Unique)

$translations = Get-Content -Raw -LiteralPath $TranslationCatalogPath | ConvertFrom-Json
Assert-Condition ($translations.schemaVersion -eq 1) 'Translation schemaVersion must be 1.'
Assert-Condition ($translations.language -eq 'zh-CN') 'Translation language must be zh-CN.'

$expectedTags = @($official.SelectNodes('/m:ModLinks/m:Manifest/m:Tags/m:Tag', $namespace) |
    ForEach-Object { $_.InnerText.Trim() } |
    Sort-Object -Unique)
$actualTags = @($translations.tagNames.psobject.Properties.Name | Sort-Object)
Assert-Condition (@(Compare-Object ($expectedTags | Sort-Object) $actualTags).Count -eq 0) 'Translation tag keys do not match the official tag set.'
foreach ($tag in $translations.tagNames.psobject.Properties) {
    Assert-Condition ($tag.Value -match '[一-鿿]') "Tag '$($tag.Name)' display name has no Chinese localization."
}

$translatedIds = @($translations.mods | ForEach-Object { [string]$_.id } | Sort-Object -Unique)
Assert-Condition ($translatedIds.Count -eq @($translations.mods).Count) 'Translation catalog contains duplicate Mod IDs.'
Assert-Condition (@(Compare-Object $officialIds $translatedIds).Count -eq 0) 'Translation Mod IDs do not match official ModLinks.'

foreach ($mod in $translations.mods) {
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($mod.displayName)) "Mod '$($mod.id)' is missing a Chinese display name."
    Assert-Condition ($mod.displayName -match '[一-鿿]') "Mod '$($mod.id)' display name has no Chinese localization."
    Assert-Condition ($mod.displayName.Length -le 160) "Mod '$($mod.id)' has an overly long display name."
    if ($null -ne $mod.description) {
        Assert-Condition ($mod.description.Length -le 16384) "Mod '$($mod.id)' has an overly long description."
    }
}

Write-Output "Validated $($translatedIds.Count)/$($officialIds.Count) official ModLinks IDs; all $($translatedIds.Count) display names contain Chinese. Translation wording still requires review."
