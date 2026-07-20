[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

function Assert-Rejected {
    param(
        [Parameter(Mandatory)]
        [string]$Message,
        [Parameter(Mandatory)]
        [scriptblock]$Check,
        [Parameter(Mandatory)]
        [string]$ExpectedMessage
    )

    try {
        & $Check
    }
    catch {
        if ($_.Exception.Message -notmatch $ExpectedMessage) {
            throw "Unexpected rejection: $($_.Exception.Message)"
        }
        return
    }
    throw $Message
}

$buildScript = Join-Path $PSScriptRoot 'build-release.ps1'
$source = Get-Content -Raw -LiteralPath $buildScript
$root = Split-Path -Parent $PSScriptRoot
$installerSource = Get-Content -Raw -LiteralPath (Join-Path $root 'installer\Crystalfly.iss')
$readme = Get-Content -Raw -LiteralPath (Join-Path $root 'README.md')
$absoluteIsccPathPattern = '(?i)-IsccPath\s+[''"]?[A-Z]:[\\/]'
if ($source -notmatch "\[ValidateSet\('win-x64'\)\]") {
    throw 'Runtime must be restricted to win-x64 before release paths are constructed.'
}
if ($source -notmatch '(?m)\[string\]\$Version\s*=\s*''0\.1\.10''') {
    throw 'Release build must default to version 0.1.10.'
}
if ($source -notmatch '(?m)-p:CopyOutputSymbolsToPublishDirectory=false') {
    throw 'Release publish must exclude debug symbols from the publish directory.'
}
foreach ($requiredDefine in 'PublishDir', 'AppVersion') {
    $guardPattern = "(?ms)#ifndef\s+$requiredDefine\b(?:(?!#endif).)*#error\b(?:(?!#endif).)*#endif"
    if ($installerSource -notmatch $guardPattern) {
        throw "Inno Setup must stop when $requiredDefine is not defined."
    }
}
if ($installerSource -match '(?m)^\s*#define\s+(?:PublishDir|AppVersion)\b') {
    throw 'Inno Setup must not silently use default release inputs.'
}
if ($installerSource -notmatch '(?ms)^\[InstallDelete\].*?Type:\s*files;\s*Name:\s*"\{app\}\\Avalonia\.Themes\.Fluent\.dll"') {
    throw 'Inno Setup upgrades must remove the retired Fluent theme assembly.'
}
if ($installerSource -notmatch '(?m)^DefaultDirName=D:\\Program Files\\\{#AppName\}$') {
    throw 'Inno Setup must default to D:\\Program Files\\Crystalfly.'
}
if ($installerSource -notmatch '(?m)^PrivilegesRequired=admin$') {
    throw 'The fixed Program Files install must request administrator privileges.'
}
if ("-IsccPath 'D:\Tools\Inno Setup 6\ISCC.exe'" -notmatch $absoluteIsccPathPattern) {
    throw 'The README absolute ISCC path check does not cover arbitrary drive paths.'
}
if ('-IsccPath "Z:/Tools/Inno Setup 6/ISCC.exe"' -notmatch $absoluteIsccPathPattern) {
    throw 'The README absolute ISCC path check does not cover slash-based drive paths.'
}
if ($readme -match $absoluteIsccPathPattern) {
    throw 'README must not hard-code a machine-specific Inno Setup path.'
}
$releaseCommandPattern = '(?i)build-release\.ps1[''"`\s\\\r\n]+-Version\s+[''"]0\.1\.10[''"]'
if ($readme -notmatch $releaseCommandPattern) {
    throw 'README must pin local release builds to version 0.1.10.'
}
$englishReadme = [regex]::Match($readme, '(?ms)^## English\s*(?<content>.*)$').Groups['content'].Value
if (
    $englishReadme -notmatch '(?i)pwsh\s+-NoProfile\s+-File\s+[''"]?\.\\scripts\\build-release\.ps1' -or
    $englishReadme -notmatch '(?i)automatically[^\r\n]*Inno Setup 6' -or
    $englishReadme -notmatch '(?i)-IsccPath' -or
    $englishReadme -notmatch '(?i)`artifacts`[^\r\n]*portable[^\r\n]*installer[^\r\n]*SHA256SUMS\.txt'
) {
    throw 'English README must document release build, ISCC discovery/override, and artifacts.'
}

. $buildScript

$symbolTestRoot = Join-Path ([IO.Path]::GetTempPath()) "Crystalfly.SymbolTests\$([Guid]::NewGuid().ToString('N'))"
try {
    New-Item -ItemType Directory -Path $symbolTestRoot -Force | Out-Null
    New-Item -ItemType File -Path (Join-Path $symbolTestRoot 'app.pdb') -Force | Out-Null
    New-Item -ItemType File -Path (Join-Path $symbolTestRoot 'app.exe') -Force | Out-Null
    Remove-PublishSymbols -PublishPath $symbolTestRoot
    Assert-PublishContainsNoSymbols -PublishPath $symbolTestRoot
    if (-not (Test-Path -LiteralPath (Join-Path $symbolTestRoot 'app.exe'))) {
        throw 'Removing publish symbols must not remove application files.'
    }
}
finally {
    Remove-Item -LiteralPath $symbolTestRoot -Recurse -Force -ErrorAction SilentlyContinue
}

& pwsh -NoProfile -File $buildScript -Runtime '..\..\..' 2>&1 | Out-Null
if ($LASTEXITCODE -eq 0) {
    throw 'A traversing runtime value was accepted.'
}

$nativeStopped = $false
try {
    Invoke-Native 'expected failure' { dotnet --definitely-invalid-option *> $null }
}
catch {
    $nativeStopped = $true
}
if (-not $nativeStopped) {
    throw 'A failed native command did not stop the release pipeline.'
}

$testRoot = Join-Path ([IO.Path]::GetTempPath()) "Crystalfly.ReleaseTests\$([Guid]::NewGuid().ToString('N'))"
try {
    $artifacts = Join-Path $testRoot 'artifacts'
    $publish = Join-Path $artifacts 'publish\win-x64'

    $fakeProgramFiles = Join-Path $testRoot 'Program Files'
    $fakeLocalAppData = Join-Path $testRoot 'LocalAppData'
    $fakeIscc = Join-Path $fakeLocalAppData 'Programs\Inno Setup 6\ISCC.exe'
    New-Item -ItemType Directory -Path (Split-Path -Parent $fakeIscc) -Force | Out-Null
    New-Item -ItemType File -Path $fakeIscc -Force | Out-Null
    $savedPath = $env:PATH
    $savedProgramFiles = $env:ProgramFiles
    $savedProgramFilesX86 = ${env:ProgramFiles(x86)}
    $savedLocalAppData = $env:LOCALAPPDATA
    try {
        $env:PATH = ''
        $env:ProgramFiles = $fakeProgramFiles
        ${env:ProgramFiles(x86)} = $fakeProgramFiles
        $env:LOCALAPPDATA = $fakeLocalAppData
        $resolvedIscc = Resolve-IsccPath
        $invalidExplicitPathStopped = $false
        try {
            Resolve-IsccPath -Path (Join-Path $testRoot 'missing\ISCC.exe') | Out-Null
        }
        catch {
            $invalidExplicitPathStopped = $true
        }
    }
    finally {
        $env:PATH = $savedPath
        $env:ProgramFiles = $savedProgramFiles
        ${env:ProgramFiles(x86)} = $savedProgramFilesX86
        $env:LOCALAPPDATA = $savedLocalAppData
    }
    if ($resolvedIscc -ne $fakeIscc) {
        throw "Automatic ISCC discovery returned '$resolvedIscc' instead of '$fakeIscc'."
    }
    if (-not $invalidExplicitPathStopped) {
        throw 'An invalid explicit ISCC path silently fell back to automatic discovery.'
    }

    New-Item -ItemType Directory -Path $publish -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $publish 'stale.dll') -Value 'stale'

    Reset-ControlledDirectory -Path $publish -AllowedRoot $artifacts
    if ((Get-ChildItem -LiteralPath $publish -Force).Count -ne 0) {
        throw 'Reset-ControlledDirectory did not remove stale publish files.'
    }

    $portable = Join-Path $artifacts 'portable\win-x64'
    $installerOutput = Join-Path $artifacts 'installer'
    New-Item -ItemType Directory -Path $portable, $installerOutput -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $publish 'stale.dll') -Value 'stale'
    Set-Content -LiteralPath (Join-Path $portable 'stale.dll') -Value 'stale'
    Set-Content -LiteralPath (Join-Path $installerOutput 'stale-setup.exe') -Value 'stale'
    Set-Content -LiteralPath (Join-Path $artifacts 'Crystalfly-old-win-x64-portable.zip') -Value 'stale'
    Set-Content -LiteralPath (Join-Path $artifacts 'SHA256SUMS.txt') -Value 'stale'

    Reset-ReleaseStaging -ArtifactsPath $artifacts -Runtime 'win-x64'
    $staleOutputs = @(
        Get-ChildItem -LiteralPath $publish, $portable, $installerOutput -Recurse -File
        Get-ChildItem -LiteralPath $artifacts -Filter 'Crystalfly-*-portable.zip' -File
        Get-Item -LiteralPath (Join-Path $artifacts 'SHA256SUMS.txt') -ErrorAction SilentlyContinue
    )
    if ($staleOutputs.Count -ne 0) {
        throw "Release staging retained stale outputs: $($staleOutputs.FullName -join ', ')."
    }

    $archiveStopped = $false
    try {
        Compress-Archive -LiteralPath (Join-Path $testRoot 'missing.file') `
            -DestinationPath (Join-Path $artifacts 'must-not-exist.zip')
    }
    catch {
        $archiveStopped = $true
    }
    if (-not $archiveStopped) {
        throw 'A failed Compress-Archive command did not stop the release pipeline.'
    }

    $outside = Join-Path $testRoot 'outside'
    New-Item -ItemType Directory -Path $outside -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $outside 'sentinel.txt') -Value 'keep'
    $traversalStopped = $false
    try {
        Reset-ControlledDirectory -Path $outside -AllowedRoot $artifacts
    }
    catch {
        $traversalStopped = $true
    }
    if (-not $traversalStopped -or -not (Test-Path -LiteralPath (Join-Path $outside 'sentinel.txt'))) {
        throw 'Controlled directory reset escaped its allowed root.'
    }

    $pdb = Join-Path $publish 'Crystalfly.App.pdb'
    Set-Content -LiteralPath $pdb -Value 'symbols'
    Assert-Rejected -Message 'Publish inventory accepted a PDB file.' `
        -ExpectedMessage 'PDB' -Check {
        Assert-PublishContainsNoSymbols -PublishPath $publish
    }
    Remove-Item -LiteralPath $pdb
    Assert-PublishContainsNoSymbols -PublishPath $publish

    New-Item -ItemType Directory -Path (Join-Path $publish 'runtimes') -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $publish 'Crystalfly.App.exe') -Value 'app'
    Set-Content -LiteralPath (Join-Path $publish 'runtimes\native.dll') -Value 'native'
    Get-ChildItem -LiteralPath $publish -Force |
        Copy-Item -Destination $portable -Recurse
    New-Item -ItemType File -Path (Join-Path $portable 'portable.flag') -Force | Out-Null

    Assert-PortableMatchesPublish -PublishPath $publish -PortablePath $portable
    Set-Content -LiteralPath (Join-Path $portable 'stale.dll') -Value 'stale'
    $inventoryStopped = $false
    try {
        Assert-PortableMatchesPublish -PublishPath $publish -PortablePath $portable
    }
    catch {
        $inventoryStopped = $true
    }
    if (-not $inventoryStopped) {
        throw 'Portable inventory accepted a file not present in publish output.'
    }
    Remove-Item -LiteralPath (Join-Path $portable 'stale.dll')

    $zip = Join-Path $artifacts 'portable.zip'
    Compress-Archive -Path (Join-Path $portable '*') -DestinationPath $zip
    Assert-ZipMatchesDirectory -ZipPath $zip -DirectoryPath $portable

    $duplicateZip = Join-Path $artifacts 'duplicate.zip'
    Copy-Item -LiteralPath $zip -Destination $duplicateZip
    $archive = [System.IO.Compression.ZipFile]::Open($duplicateZip, 'Update')
    try {
        $archive.CreateEntry('runtimes\native.dll').Open().Dispose()
    }
    finally {
        $archive.Dispose()
    }
    Assert-Rejected -Message 'ZIP validation accepted a duplicate normalized path.' `
        -ExpectedMessage 'duplicate normalized path' -Check {
        Assert-ZipMatchesDirectory -ZipPath $duplicateZip -DirectoryPath $portable
    }

    $lengthZip = Join-Path $artifacts 'wrong-length.zip'
    Copy-Item -LiteralPath $zip -Destination $lengthZip
    $archive = [System.IO.Compression.ZipFile]::Open($lengthZip, 'Update')
    try {
        $archive.GetEntry('runtimes/native.dll').Delete()
        $archive.CreateEntry('runtimes/native.dll').Open().Dispose()
    }
    finally {
        $archive.Dispose()
    }
    Assert-Rejected -Message 'ZIP validation accepted a file with the wrong length.' `
        -ExpectedMessage 'length differs' -Check {
        Assert-ZipMatchesDirectory -ZipPath $lengthZip -DirectoryPath $portable
    }

    $contentZip = Join-Path $artifacts 'wrong-content.zip'
    Copy-Item -LiteralPath $zip -Destination $contentZip
    $archive = [System.IO.Compression.ZipFile]::Open($contentZip, 'Update')
    try {
        $entry = $archive.GetEntry('runtimes/native.dll')
        $entryLength = [int]$entry.Length
        $entry.Delete()
        $stream = $archive.CreateEntry('runtimes/native.dll').Open()
        try {
            $bytes = [byte[]]::new($entryLength)
            $stream.Write($bytes, 0, $bytes.Length)
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
    Assert-Rejected -Message 'ZIP validation accepted changed content with the original length.' `
        -ExpectedMessage 'content differs' -Check {
        Assert-ZipMatchesDirectory -ZipPath $contentZip -DirectoryPath $portable
    }

    $archive = [System.IO.Compression.ZipFile]::Open($zip, 'Update')
    try {
        $archive.CreateEntry('stale.dll').Open().Dispose()
    }
    finally {
        $archive.Dispose()
    }
    $zipInventoryStopped = $false
    try {
        Assert-ZipMatchesDirectory -ZipPath $zip -DirectoryPath $portable
    }
    catch {
        $zipInventoryStopped = $true
    }
    if (-not $zipInventoryStopped) {
        throw 'ZIP inventory accepted a file not present in the portable directory.'
    }

    $installer = Join-Path $installerOutput 'Crystalfly-0.1.10-win-x64-setup.exe'
    Set-Content -LiteralPath $installer -Value 'installer'
    $checksums = Join-Path $artifacts 'SHA256SUMS.txt'
    Write-ArtifactChecksums -Paths $zip, $installer -ArtifactsPath $artifacts -OutputPath $checksums
    $checksumLines = Get-Content -LiteralPath $checksums
    if (-not ($checksumLines -match '\*portable\.zip$')) {
        throw 'Checksums must address the portable archive from the artifacts root.'
    }
    if (-not ($checksumLines -match '\*installer/Crystalfly-0\.1\.10-win-x64-setup\.exe$')) {
        throw 'Checksums must address the installer relative to the artifacts root.'
    }
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}

'Release build validation passed.'
