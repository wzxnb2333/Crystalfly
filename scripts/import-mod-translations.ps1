[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$InputPath,

    [Parameter(Mandatory)]
    [string]$OutputPath,

    [string]$OfficialCatalogPath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-CellValue {
    param(
        [System.Xml.Linq.XElement]$Cell,
        [string[]]$SharedStrings
    )

    if ($null -eq $Cell) { return '' }
    $type = [string]$Cell.Attribute('t').Value
    if ($type -eq 's') {
        $index = [int]$Cell.Element('{http://schemas.openxmlformats.org/spreadsheetml/2006/main}v').Value
        return $SharedStrings[$index]
    }
    if ($type -eq 'inlineStr') {
        return (($Cell.Element('{http://schemas.openxmlformats.org/spreadsheetml/2006/main}is') |
            Select-Xml -XPath './/*[local-name()="t"]' -ErrorAction SilentlyContinue).Node | ForEach-Object { $_.Value }) -join ''
    }
    return [string]$Cell.Element('{http://schemas.openxmlformats.org/spreadsheetml/2006/main}v').Value
}

function Get-ColumnIndex {
    param([string]$Reference)
    $letters = ($Reference -replace '\d', '')
    $index = 0
    foreach ($letter in $letters.ToCharArray()) {
        [void]($index = ($index * 26) + ([int][char]$letter.ToString().ToUpperInvariant() - [int][char]'A' + 1))
    }
    return $index - 1
}

function Read-WorkbookRows {
    param([string]$Path)

    $archive = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Path))
    try {
        $ns = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'
        $relNs = 'http://schemas.openxmlformats.org/package/2006/relationships'
        $workbook = [System.Xml.Linq.XDocument]::Load($archive.GetEntry('xl/workbook.xml').Open())
        $rels = [System.Xml.Linq.XDocument]::Load($archive.GetEntry('xl/_rels/workbook.xml.rels').Open())
        $sheet = $workbook.Root.Element("{$ns}sheets").Elements("{$ns}sheet") |
            Where-Object { [string]$_.Attribute('name').Value -eq 'Mod列表' } |
            Select-Object -First 1
        if ($null -eq $sheet) { throw 'Workbook sheet Mod列表 was not found.' }
        $relationshipId = [string]$sheet.Attribute('{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id').Value
        $relationship = $rels.Root.Elements("{$relNs}Relationship") |
            Where-Object { [string]$_.Attribute('Id').Value -eq $relationshipId } |
            Select-Object -First 1
        if ($null -eq $relationship) { throw 'Workbook sheet relationship was not found.' }
        $target = ([string]$relationship.Attribute('Target').Value).TrimStart('/')
        if (-not $target.StartsWith('xl/')) { $target = "xl/$target" }

        $sharedStrings = @()
        $sharedEntry = $archive.GetEntry('xl/sharedStrings.xml')
        if ($null -ne $sharedEntry) {
            $shared = [System.Xml.Linq.XDocument]::Load($sharedEntry.Open())
            $sharedStrings = @($shared.Root.Elements("{$ns}si") | ForEach-Object {
                ($_.Descendants("{$ns}t") | ForEach-Object { $_.Value }) -join ''
            })
        }

        $worksheet = [System.Xml.Linq.XDocument]::Load($archive.GetEntry($target).Open())
        $rows = @($worksheet.Root.Element("{$ns}sheetData").Elements("{$ns}row"))
        $result = foreach ($row in $rows) {
            $values = @{}
            foreach ($cell in $row.Elements("{$ns}c")) {
                $values[(Get-ColumnIndex ([string]$cell.Attribute('r').Value))] = Get-CellValue $cell $sharedStrings
            }
            ,$values
        }
        return $result
    }
    finally {
        $archive.Dispose()
    }
}

function Clean-Text([string]$Value) {
    if ($null -eq $Value) { return '' }
    return (($Value -replace "`r`n?", "`n").Trim())
}

$tagNames = [ordered]@{
    Gameplay = '玩法'
    Utility = '工具'
    Cosmetic = '装饰'
    Library = '前置'
    Expansion = '扩展'
    Charm = '护符'
    Joke = '整活'
    Optimization = '优化'
    Accessibility = '辅助'
    Boss = 'Boss'
}
$tagAliases = @{
    '玩法' = 'Gameplay'
    '工具' = 'Utility'
    '装饰' = 'Cosmetic'
    '前置' = 'Library'
    '扩展' = 'Expansion'
    '护符' = 'Charm'
    '整活' = 'Joke'
    '优化' = 'Optimization'
    '辅助' = 'Accessibility'
    'BOSS' = 'Boss'
}
$allowedChineseTags = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($tagName in $tagNames.Values) { [void]$allowedChineseTags.Add($tagName) }

$rows = @(Read-WorkbookRows -Path $InputPath)
if ($rows.Count -lt 2) { throw 'Workbook contains no Mod rows.' }
$headers = $rows[0]
$expectedHeaders = @(
    'Mod 名称', 'Mod 中文名', '版本', '前置', '联动', '标签', '说明（中文）', '说明（英文）',
    "mod+前置批量下载`n（右键复制在夸盘中打开）"
)
for ($i = 0; $i -lt $expectedHeaders.Count; $i++) {
    if ((Clean-Text $headers[$i]) -ne $expectedHeaders[$i]) {
        throw "Unexpected header at column $($i + 1): '$($headers[$i])'."
    }
}

$officialIds = $null
if (-not [string]::IsNullOrWhiteSpace($OfficialCatalogPath)) {
    $official = Get-Content -Raw -LiteralPath $OfficialCatalogPath | ConvertFrom-Json
    $officialIds = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($mod in @($official.mods)) { [void]$officialIds.Add([string]$mod.id) }
}

$seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$mods = foreach ($row in $rows | Select-Object -Skip 1) {
    $name = Clean-Text $row[0]
    $displayName = Clean-Text $row[1]
    if ([string]::IsNullOrWhiteSpace($name) -or [string]::IsNullOrWhiteSpace($displayName)) {
        throw 'Every Mod row must contain an English and Chinese name.'
    }
    $id = "hkmod:$name"
    if (-not $seen.Add($id)) { throw "Duplicate Mod name '$name'." }
    if ($null -ne $officialIds -and -not $officialIds.Contains($id)) {
        throw "Mod '$name' does not exist in the official catalog."
    }

    $tags = @((Clean-Text $row[5]) -split ';' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    foreach ($tag in $tags) {
        if (-not $allowedChineseTags.Contains($tag)) {
            throw "Mod '$name' has an unknown Chinese tag '$tag'."
        }
        if (-not $tagAliases.ContainsKey($tag)) {
            throw "Mod '$name' has no stable English mapping for Chinese tag '$tag'."
        }
    }
    $entry = [ordered]@{
        id = $id
        displayName = $displayName
    }
    $description = Clean-Text $row[6]
    if ($description) { $entry.description = $description }
    [pscustomobject]$entry
}

$document = [ordered]@{
    schemaVersion = 1
    language = 'zh-CN'
    tagNames = $tagNames
    mods = @($mods | Sort-Object id)
}
$json = $document | ConvertTo-Json -Depth 8
$parent = Split-Path -Parent ([System.IO.Path]::GetFullPath($OutputPath))
if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
Set-Content -LiteralPath $OutputPath -Value $json -Encoding utf8NoBOM
$written = Get-Content -Raw -LiteralPath $OutputPath | ConvertFrom-Json
if (@($written.mods).Count -ne $seen.Count) {
    throw "Generated catalog count does not match source count: $(@($written.mods).Count) vs $($seen.Count)."
}
Write-Output "Generated $(@($written.mods).Count) translation entries at $OutputPath"
