param([ValidateSet('probe','game')][string]$Kind)
$ErrorActionPreference = 'Stop'
$editor = $env:IDAS3_UNITY_EDITOR
if (-not $env:UNITY_SERIAL -or -not $env:UNITY_EMAIL -or -not $env:UNITY_PASSWORD) { throw 'BLOCKED: paid Unity serial/account activation secrets not configured. No Unity build attempted.' }
$activationLog = Join-Path $env:RUNNER_TEMP 'id3-unity-activation-private.log'
$returnLog = Join-Path $env:RUNNER_TEMP 'id3-unity-return-private.log'
# Never upload activation/return logs, license files, or account state. Process arguments
# necessarily contain activation secrets; use only an ephemeral trusted hosted runner.
try {
    $start = [Diagnostics.ProcessStartInfo]::new($editor)
    $start.UseShellExecute = $false
    foreach ($arg in @('-batchmode','-nographics','-quit','-serial',$env:UNITY_SERIAL,'-username',$env:UNITY_EMAIL,'-password',$env:UNITY_PASSWORD,'-logFile',$activationLog)) { $start.ArgumentList.Add($arg) }
    $process = [Diagnostics.Process]::Start($start)
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw 'Unity license activation failed. Private activation log is deliberately not published.' }
    & (Join-Path $PSScriptRoot 'Build-Windows.ps1') -Kind $Kind -UnityEditor $editor -ReuseNative
} finally {
    try { if (Test-Path $editor) {
        $process = Start-Process $editor -ArgumentList "-batchmode -nographics -quit -returnlicense -logFile `"$returnLog`"" -Wait -PassThru
        if ($process.ExitCode -ne 0) { Write-Warning 'Unity license return failed; owner must check seat status before retry.' }
    }
    } catch { Write-Warning 'Unity license return could not run; owner must check seat status.' }
    Remove-Item $activationLog,$returnLog -ErrorAction SilentlyContinue
    # Defense in depth: sanitize the small, explicit build-report tree before upload.
    $reportRoot = Join-Path $PSScriptRoot '../../Verification/ci'
    if (Test-Path $reportRoot) {
        Get-ChildItem $reportRoot -Recurse -File | Where-Object { $_.Extension -in '.log','.txt','.json','.xml' } | ForEach-Object {
            $text = [IO.File]::ReadAllText($_.FullName)
            foreach ($secret in @($env:UNITY_SERIAL,$env:UNITY_EMAIL,$env:UNITY_PASSWORD)) { if ($secret) { $text=$text.Replace($secret,'[REDACTED]') } }
            [IO.File]::WriteAllText($_.FullName,$text)
        }
    }
}
