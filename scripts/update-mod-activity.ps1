[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ModLinksRepository,
    [Parameter(Mandatory)]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

function Read-ByteLine {
    param([Parameter(Mandatory)] [IO.Stream]$Stream)

    $bytes = [Collections.Generic.List[byte]]::new()
    while ($true) {
        $value = $Stream.ReadByte()
        if ($value -lt 0) {
            throw 'Unexpected end of Git object stream.'
        }
        if ($value -eq 10) {
            break
        }
        if ($value -ne 13) {
            $bytes.Add([byte]$value)
        }
    }
    return [Text.Encoding]::UTF8.GetString($bytes.ToArray())
}

function Read-ExactBytes {
    param(
        [Parameter(Mandatory)] [IO.Stream]$Stream,
        [Parameter(Mandatory)] [int]$Length
    )

    $bytes = [byte[]]::new($Length)
    $offset = 0
    while ($offset -lt $Length) {
        $read = $Stream.Read($bytes, $offset, $Length - $offset)
        if ($read -eq 0) {
            throw 'Unexpected end of Git object stream.'
        }
        $offset += $read
    }
    return $bytes
}

function Get-ModLinksHistory {
    param([Parameter(Mandatory)] [string]$Repository)

    $history = @(git -C $Repository log --reverse --format='%H|%aI' -- ModLinks.xml)
    if ($LASTEXITCODE -ne 0 -or $history.Count -eq 0) {
        throw 'Unable to read ModLinks.xml history.'
    }
    return $history
}

function Open-GitObjectReader {
    param([Parameter(Mandatory)] [string]$Repository)

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = 'git'
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    [void]$startInfo.ArgumentList.Add('-C')
    [void]$startInfo.ArgumentList.Add($Repository)
    [void]$startInfo.ArgumentList.Add('cat-file')
    [void]$startInfo.ArgumentList.Add('--batch')

    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw 'Unable to start Git object reader.'
    }
    return $process
}

$repository = [IO.Path]::GetFullPath($ModLinksRepository)
if (-not (Test-Path -LiteralPath (Join-Path $repository '.git') -PathType Container)) {
    throw "ModLinksRepository is not a Git checkout: '$repository'."
}

$history = Get-ModLinksHistory -Repository $repository
$activity = @{}
$currentIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$previousSnapshot = @{}
$latestRevision = $null
$latestTimestamp = $null
$reader = Open-GitObjectReader -Repository $repository
try {
    foreach ($historyLine in $history) {
        $parts = $historyLine -split '\|', 2
        if ($parts.Count -ne 2) {
            throw "Invalid git history entry: '$historyLine'."
        }
        $revision = $parts[0]
        $timestamp = [DateTimeOffset]::Parse($parts[1], [Globalization.CultureInfo]::InvariantCulture)
        $reader.StandardInput.WriteLine("${revision}:ModLinks.xml")
        $reader.StandardInput.Flush()

        $header = Read-ByteLine -Stream $reader.StandardOutput.BaseStream
        if ($header -notmatch '^[0-9a-f]{40} blob (?<size>\d+)$') {
            throw "Unable to read ModLinks.xml at revision '$revision': $header"
        }
        $xmlBytes = Read-ExactBytes -Stream $reader.StandardOutput.BaseStream -Length ([int]$Matches.size)
        if ($reader.StandardOutput.BaseStream.ReadByte() -ne 10) {
            throw "Invalid Git object delimiter at revision '$revision'."
        }

        try {
            [xml]$xml = [Text.Encoding]::UTF8.GetString($xmlBytes)
            $namespace = [Xml.XmlNamespaceManager]::new($xml.NameTable)
            $namespace.AddNamespace('m', $xml.DocumentElement.NamespaceURI)
            $snapshot = @{}
            foreach ($manifest in $xml.SelectNodes('/m:ModLinks/m:Manifest', $namespace)) {
                $name = $manifest.SelectSingleNode('m:Name', $namespace).InnerText.Trim()
                $version = $manifest.SelectSingleNode('m:Version', $namespace).InnerText.Trim()
                if ([string]::IsNullOrWhiteSpace($name) -or [string]::IsNullOrWhiteSpace($version)) {
                    throw "Revision '$revision' contains an incomplete manifest."
                }
                $snapshot["hkmod:$name"] = $manifest.OuterXml
            }
        }
        catch {
            Write-Warning "Skipping invalid ModLinks.xml revision '$revision': malformed XML or manifest."
            continue
        }

        foreach ($id in $snapshot.Keys) {
            if (-not $activity.ContainsKey($id)) {
                $activity[$id] = [ordered]@{
                    id = $id
                    addedAt = $timestamp.ToUniversalTime().ToString('O')
                    updatedAt = $timestamp.ToUniversalTime().ToString('O')
                }
            }
            elseif (-not $previousSnapshot.ContainsKey($id) -or $previousSnapshot[$id] -ne $snapshot[$id]) {
                $activity[$id].updatedAt = $timestamp.ToUniversalTime().ToString('O')
            }
        }
        $currentIds.Clear()
        foreach ($id in $snapshot.Keys) {
            [void]$currentIds.Add($id)
        }
        $previousSnapshot = $snapshot
        $latestRevision = $revision
        $latestTimestamp = $timestamp
    }
}
finally {
    $reader.StandardInput.Close()
    if (-not $reader.HasExited) {
        $reader.WaitForExit()
    }
    $reader.Dispose()
}

if ($null -eq $latestTimestamp) {
    throw 'ModLinks.xml history did not contain a valid revision.'
}

$entries = @($activity.Values | Where-Object { $currentIds.Contains($_.id) })
[Array]::Sort($entries, [Comparison[object]] {
        param($left, $right)
        return [StringComparer]::Ordinal.Compare([string]$left.id, [string]$right.id)
    })
$entries = @($entries | ForEach-Object {
        [ordered]@{
            id = $_.id
            addedAt = $_.addedAt
            updatedAt = $_.updatedAt
        }
    })
$invalidEntries = @($entries | Where-Object {
        $_.id -notmatch '^hkmod:.+' -or
        $_.id.Length -gt 256 -or
        [string]::IsNullOrWhiteSpace($_.addedAt) -or
        [string]::IsNullOrWhiteSpace($_.updatedAt)
    })
if ($entries.Count -gt 5000 -or $invalidEntries.Count -gt 0) {
    throw 'Generated Mod activity catalog violates the v1 schema constraints.'
}

$catalog = [ordered]@{
    schemaVersion = 1
    generatedAt = $latestTimestamp.ToUniversalTime().ToString('O')
    sourceRevision = $latestRevision
    entries = $entries
}
$target = [IO.Path]::GetFullPath($OutputPath)
$targetDirectory = Split-Path -Parent $target
New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
$temporary = Join-Path $targetDirectory ".$(Split-Path -Leaf $target).$([Guid]::NewGuid().ToString('N')).tmp"
try {
    $json = $catalog | ConvertTo-Json -Depth 6
    [IO.File]::WriteAllText($temporary, $json + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporary -Destination $target -Force
}
finally {
    if (Test-Path -LiteralPath $temporary) {
        Remove-Item -LiteralPath $temporary -Force
    }
}

Write-Output "Wrote $($entries.Count) active Mod entries from $latestRevision."
