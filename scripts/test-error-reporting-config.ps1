[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'build-release.ps1')
$root = Split-Path -Parent $PSScriptRoot
$configPath = Join-Path $root 'config/error-reporting.official.json'
$defaults = @(Get-ErrorReportingBuildProperties)
if ($defaults.Count -ne 3 -or $defaults[0] -ne '-p:CrystalflyErrorReportingDsn=') {
    throw 'Default builds must have no receiving endpoint.'
}
$official = @(Get-ErrorReportingBuildProperties -ConfigPath $configPath)
if ($official.Count -ne 3 -or $official[1] -ne '-p:CrystalflyErrorReportingRegion=US-NewYork' -or
    $official[2] -ne '-p:CrystalflyErrorReportingRetention=90 days') {
    throw 'Explicit official build must contain the verified hosting disclosures.'
}
$temporary = Join-Path ([IO.Path]::GetTempPath()) ('crystalfly-reporting-' + [guid]::NewGuid() + '.json')
try {
    foreach ($dsn in @('http://public@host.invalid/1', 'https://public:secret@host.invalid/1',
        'https://public@host.invalid/1?token=secret', 'https://public@host.invalid/1;p:Injected=true')) {
        @{ dsn = $dsn; region = 'US'; retention = '90 days' } | ConvertTo-Json | Set-Content -LiteralPath $temporary
        $rejected = $false
        try { Get-ErrorReportingBuildProperties -ConfigPath $temporary | Out-Null } catch { $rejected = $true }
        if (-not $rejected) { throw 'Unsafe diagnostic configuration was accepted.' }
    }
} finally {
    Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue
}
Write-Output 'Error reporting release configuration checks passed.'
