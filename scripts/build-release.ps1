[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64',
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$')]
    [string]$Version = '0.6.0',
    [string]$IsccPath,
    [string]$SigningKeyPath,
    [string]$ReleaseNotesPath,
    [string]$PublishedAt
)

$ErrorActionPreference = 'Stop'

function Invoke-Native {
    param(
        [Parameter(Mandatory)]
        [string]$Description,
        [Parameter(Mandatory)]
        [scriptblock]$Command
    )

    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

function Reset-ControlledDirectory {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter(Mandatory)]
        [string]$AllowedRoot
    )

    $root = [IO.Path]::GetFullPath($AllowedRoot).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
    $target = [IO.Path]::GetFullPath($Path)
    if (-not $target.StartsWith(
        $root + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to reset '$target' outside '$root'."
    }

    if (Test-Path -LiteralPath $target) {
        Remove-Item -LiteralPath $target -Recurse -Force
    }
    New-Item -ItemType Directory -Path $target -Force | Out-Null
}

function Reset-ReleaseStaging {
    param(
        [Parameter(Mandatory)]
        [string]$ArtifactsPath,
        [Parameter(Mandatory)]
        [ValidateSet('win-x64')]
        [string]$Runtime
    )

    foreach ($path in @(
        (Join-Path $ArtifactsPath "publish\$Runtime"),
        (Join-Path $ArtifactsPath "updater\$Runtime"),
        (Join-Path $ArtifactsPath "portable\$Runtime"),
        (Join-Path $ArtifactsPath 'installer')
    )) {
        Reset-ControlledDirectory -Path $path -AllowedRoot $ArtifactsPath
    }

    Get-ChildItem -LiteralPath $ArtifactsPath -Filter 'Crystalfly-*-portable.zip' -File |
        ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
    $checksums = Join-Path $ArtifactsPath 'SHA256SUMS.txt'
    if (Test-Path -LiteralPath $checksums) {
        Remove-Item -LiteralPath $checksums -Force
    }
    foreach ($manifestPath in @(
        (Join-Path $ArtifactsPath 'update-manifest.v1.json'),
        (Join-Path $ArtifactsPath 'update-manifest.v1.json.bak')
    )) {
        if (Test-Path -LiteralPath $manifestPath) {
            Remove-Item -LiteralPath $manifestPath -Force
        }
    }
}

function Resolve-IsccPath {
    param(
        [string]$Path
    )

    if ($Path) {
        if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
            throw "ISCC.exe was not found at '$Path'."
        }
        return $Path
    }

    $onPath = Get-Command 'ISCC.exe' -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty Source
    $candidates = @(
        $onPath
        if (${env:ProgramFiles(x86)}) {
            Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
        }
        if ($env:ProgramFiles) {
            Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'
        }
        if ($env:LOCALAPPDATA) {
            Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
        }
    )
    $resolved = $candidates |
        Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) } |
        Select-Object -First 1
    if (-not $resolved) {
        throw 'ISCC.exe was not found. Pass -IsccPath with the Inno Setup compiler path.'
    }

    $resolved
}

function Get-RelativeFiles {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $root = [IO.Path]::GetFullPath($Path)
    @(Get-ChildItem -LiteralPath $root -Recurse -File | ForEach-Object {
        [IO.Path]::GetRelativePath($root, $_.FullName).Replace('\', '/')
    } | Sort-Object -Unique)
}

function Assert-PublishContainsNoSymbols {
    param(
        [Parameter(Mandatory)]
        [string]$PublishPath
    )

    $symbols = @(Get-ChildItem -LiteralPath $PublishPath -Recurse -File -Filter '*.pdb')
    if ($symbols.Count -ne 0) {
        throw "Publish output contains PDB files: $($symbols.Name -join ', ')."
    }
}

function Remove-PublishSymbols {
    param(
        [Parameter(Mandatory)]
        [string]$PublishPath
    )

    Get-ChildItem -LiteralPath $PublishPath -Recurse -File -Filter '*.pdb' |
        ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
}

function Assert-PortableMatchesPublish {
    param(
        [Parameter(Mandatory)]
        [string]$PublishPath,
        [Parameter(Mandatory)]
        [string]$PortablePath
    )

    $publishFiles = @(Get-RelativeFiles -Path $PublishPath)
    if ('Crystalfly.App.exe' -notin $publishFiles) {
        throw 'Publish output is missing Crystalfly.App.exe.'
    }
    $expected = @($publishFiles + 'portable.flag' | Sort-Object -Unique)
    $actual = @(Get-RelativeFiles -Path $PortablePath)
    $difference = @(Compare-Object -ReferenceObject $expected -DifferenceObject $actual)
    if ($difference.Count -ne 0) {
        throw "Portable inventory differs from publish output: $($difference.InputObject -join ', ')."
    }
}

function Assert-ZipMatchesDirectory {
    param(
        [Parameter(Mandatory)]
        [string]$ZipPath,
        [Parameter(Mandatory)]
        [string]$DirectoryPath
    )

    $root = [IO.Path]::GetFullPath($DirectoryPath)
    $rootPrefix = $root.TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $expected = @(Get-RelativeFiles -Path $root)
    $archive = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        $entries = @{}
        foreach ($entry in $archive.Entries) {
            if ([string]::IsNullOrEmpty($entry.Name)) {
                continue
            }

            $entryPath = $entry.FullName.Replace('/', '\')
            $fullPath = [IO.Path]::GetFullPath($entryPath, $root)
            if (-not $fullPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
                throw "ZIP entry '$($entry.FullName)' resolves outside the portable directory."
            }
            $relativePath = [IO.Path]::GetRelativePath($root, $fullPath).Replace('\', '/')
            if ($entries.ContainsKey($relativePath)) {
                throw "ZIP contains duplicate normalized path '$relativePath'."
            }
            $entries[$relativePath] = $entry
        }

        $actual = @($entries.Keys | Sort-Object)
        $difference = @(Compare-Object -ReferenceObject $expected -DifferenceObject $actual)
        if ($difference.Count -ne 0) {
            throw "ZIP inventory differs from portable output: $($difference.InputObject -join ', ')."
        }

        foreach ($relativePath in $expected) {
            $filePath = Join-Path $root $relativePath
            $file = Get-Item -LiteralPath $filePath
            $entry = $entries[$relativePath]
            if ($entry.Length -ne $file.Length) {
                throw "ZIP entry '$relativePath' length differs from the portable file."
            }

            $expectedHash = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash
            $stream = $entry.Open()
            try {
                $actualHash = (Get-FileHash -InputStream $stream -Algorithm SHA256).Hash
            }
            finally {
                $stream.Dispose()
            }
            if (-not [string]::Equals($expectedHash, $actualHash, [StringComparison]::OrdinalIgnoreCase)) {
                throw "ZIP entry '$relativePath' content differs from the portable file."
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Write-ArtifactChecksums {
    param(
        [Parameter(Mandatory)]
        [string[]]$Paths,
        [Parameter(Mandatory)]
        [string]$ArtifactsPath,
        [Parameter(Mandatory)]
        [string]$OutputPath
    )

    $root = [IO.Path]::GetFullPath($ArtifactsPath).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
    $rootPrefix = $root + [IO.Path]::DirectorySeparatorChar
    $lines = foreach ($path in $Paths) {
        $fullPath = [IO.Path]::GetFullPath($path)
        if (-not $fullPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to hash '$fullPath' outside '$root'."
        }
        $hash = Get-FileHash -LiteralPath $fullPath -Algorithm SHA256
        $relativePath = [IO.Path]::GetRelativePath($root, $fullPath).Replace('\', '/')
        "{0} *{1}" -f $hash.Hash, $relativePath
    }
    $lines | Set-Content -LiteralPath $OutputPath -Encoding ascii
}

if ($MyInvocation.InvocationName -eq '.') {
    return
}

$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$publish = Join-Path $artifacts "publish\$Runtime"
$updaterPublish = Join-Path $artifacts "updater\$Runtime"
$portable = Join-Path $artifacts "portable\$Runtime"
$appProject = Join-Path $root 'src\Crystalfly.App\Crystalfly.App.csproj'
$updaterProject = Join-Path $root 'src\Crystalfly.Updater\Crystalfly.Updater.csproj'
$releaseToolProject = Join-Path $root 'tools\Crystalfly.ReleaseTool\Crystalfly.ReleaseTool.csproj'
$updatePublicKey = Join-Path $root 'src\Crystalfly.App\Updates\stable-1.pub'
$zip = Join-Path $artifacts "Crystalfly-$Version-$Runtime-portable.zip"
$installer = Join-Path $artifacts "installer\Crystalfly-$Version-win-x64-setup.exe"
$updateManifest = Join-Path $artifacts 'update-manifest.v1.json'
$checksums = Join-Path $artifacts 'SHA256SUMS.txt'

if ([string]::IsNullOrWhiteSpace($SigningKeyPath)) {
    $SigningKeyPath = Join-Path $root '.env.update-signing'
}
if ([string]::IsNullOrWhiteSpace($ReleaseNotesPath)) {
    $ReleaseNotesPath = Join-Path $root "docs\releases\$Version.md"
}
if ([string]::IsNullOrWhiteSpace($PublishedAt)) {
    $PublishedAt = [DateTimeOffset]::UtcNow.ToString('O', [Globalization.CultureInfo]::InvariantCulture)
}
foreach ($requiredFile in $SigningKeyPath, $ReleaseNotesPath, $updatePublicKey) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required release input was not found: '$requiredFile'."
    }
}

Reset-ReleaseStaging -ArtifactsPath $artifacts -Runtime $Runtime
Invoke-Native 'Solution restore' {
    dotnet restore (Join-Path $root 'Crystalfly.slnx')
}
Invoke-Native 'Runtime restore' {
    dotnet restore $appProject -r $Runtime
}
Invoke-Native 'Updater runtime restore' {
    dotnet restore $updaterProject -r $Runtime
}
Invoke-Native 'Release build' {
    dotnet build (Join-Path $root 'Crystalfly.slnx') -c $Configuration --no-restore
}
Invoke-Native 'Release tests' {
    dotnet test (Join-Path $root 'Crystalfly.slnx') -c $Configuration --no-build
}
Invoke-Native 'Self-contained publish' {
    dotnet publish $appProject -c $Configuration -r $Runtime --self-contained true `
        --no-restore -p:Version=$Version -p:DebugSymbols=false -p:DebugType=None `
        -p:CopyOutputSymbolsToPublishDirectory=false -o $publish
}
Invoke-Native 'Updater single-file publish' {
    dotnet publish $updaterProject -c $Configuration -r $Runtime --self-contained true `
        --no-restore -p:Version=$Version -p:PublishSingleFile=true `
        -p:DebugSymbols=false -p:DebugType=None `
        -p:CopyOutputSymbolsToPublishDirectory=false -o $updaterPublish
}

$updaterExecutable = Join-Path $updaterPublish 'Crystalfly.Updater.exe'
if (-not (Test-Path -LiteralPath $updaterExecutable -PathType Leaf)) {
    throw "Updater publish did not create '$updaterExecutable'."
}
Copy-Item -LiteralPath $updaterExecutable -Destination (Join-Path $publish 'Crystalfly.Updater.exe') -Force

Remove-PublishSymbols -PublishPath $publish
Assert-PublishContainsNoSymbols -PublishPath $publish
Get-ChildItem -LiteralPath $publish -Force |
    Copy-Item -Destination $portable -Recurse -Force
New-Item -ItemType File -Path (Join-Path $portable 'portable.flag') -Force | Out-Null
Assert-PortableMatchesPublish -PublishPath $publish -PortablePath $portable
Compress-Archive -Path (Join-Path $portable '*') -DestinationPath $zip -CompressionLevel Optimal

Add-Type -AssemblyName System.IO.Compression.FileSystem
Assert-ZipMatchesDirectory -ZipPath $zip -DirectoryPath $portable

$IsccPath = Resolve-IsccPath -Path $IsccPath

Invoke-Native 'Inno Setup build' {
    & $IsccPath "/DPublishDir=$publish" "/DAppVersion=$Version" `
        (Join-Path $root 'installer\Crystalfly.iss')
}
if (-not (Test-Path -LiteralPath $installer)) {
    throw "Installer was not created at '$installer'."
}

Invoke-Native 'Signed update manifest' {
    dotnet run --project $releaseToolProject -c $Configuration --no-build -- sign `
        --version $Version `
        --notes $ReleaseNotesPath `
        --portable $zip `
        --installer $installer `
        --private-env $SigningKeyPath `
        --output $updateManifest `
        --published-at $PublishedAt `
        --tag "v$Version"
}
if (-not (Test-Path -LiteralPath $updateManifest -PathType Leaf)) {
    throw "Signed update manifest was not created at '$updateManifest'."
}
Invoke-Native 'Signed update manifest verification' {
    dotnet run --project $releaseToolProject -c $Configuration --no-build -- verify `
        --manifest $updateManifest `
        --public-key $updatePublicKey
}

$hashes = Get-FileHash -Algorithm SHA256 -LiteralPath $zip, $installer, $updateManifest
Write-ArtifactChecksums -Paths $zip, $installer, $updateManifest -ArtifactsPath $artifacts -OutputPath $checksums
$hashes
