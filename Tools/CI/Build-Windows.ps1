param([ValidateSet('probe','game')][string]$Kind='game', [string]$UnityEditor=$env:IDAS3_UNITY_EDITOR, [switch]$ReuseNative)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Set-Location $root
$out = Join-Path $root "Verification/ci/$Kind"
New-Item -ItemType Directory -Force $out | Out-Null
$commit = (& git rev-parse HEAD).Trim()
$started = [DateTime]::UtcNow.ToString('o')
$previousSource=$env:ID3_SOURCE_COMMIT
$env:ID3_SOURCE_COMMIT=$commit.Substring(0,12)
$result = @{status='FAILED'; kind=$Kind; sourceCommit=$commit; startedUtc=$started; unity='6000.6.0f1'; inputSystem='1.19.0'; os=[Environment]::OSVersion.VersionString; hardware='NOT TESTED'; renderedUI='NOT TESTED'}
try {
    $changed = @(& git diff --name-only HEAD) | Where-Object { $_ -notin @('Assets/Plugins/x86_64/Idas3Unity.dll','Assets/Plugins/x86_64/Idas3WheelFeedback.dll') }
    $untracked = @(& git ls-files --others --exclude-standard)
    if ($changed.Count -gt 0 -or $untracked.Count -gt 0) { throw 'Use a clean disposable checkout of the reviewed commit; source changes cannot be labelled as that commit.' }
    if ($env:OS -ne 'Windows_NT') { throw 'Windows x64 required.' }
    if (-not $UnityEditor -or -not (Test-Path $UnityEditor)) { throw 'Set IDAS3_UNITY_EDITOR to an activated Unity 6000.6.0f1 Windows editor with Mono support.' }
    if ($Kind -eq 'probe') {
        & (Join-Path $root 'Diagnostics/DeviceIdentityProbe/Build-Probe.ps1') -UnityEditor $UnityEditor *>&1 | Tee-Object -FilePath (Join-Path $out 'build.log')
        $player = Join-Path $root 'Diagnostics/DeviceIdentityProbe/build/player'
        $selfTest = Join-Path $player 'build-self-test.json'
        if (-not (Test-Path $selfTest)) { throw 'Probe Unity/native identity and UAT self-test report missing.' }
        Copy-Item $selfTest $out
        & python Tools/CI/Package-Probe.py $player (Join-Path $out "ID3-Probe-$commit.zip")
        if ($LASTEXITCODE -ne 0) { throw 'Probe package validation failed.' }
    } else {
        if (-not $ReuseNative) { & (Join-Path $PSScriptRoot 'Build-Native.ps1') }
        $native = Get-Content (Join-Path $root 'Verification/ci/native/result.json') -Raw | ConvertFrom-Json
        if ($native.status -ne 'PASS' -or $native.sourceCommit -ne $commit) { throw 'Native build must pass for this exact commit.' }
        foreach ($entry in $native.plugins.PSObject.Properties) {
            $path = Join-Path $root "Assets/Plugins/x86_64/$($entry.Name)"
            if ((Get-FileHash $path -Algorithm SHA256).Hash -ne $entry.Value) { throw 'Native DLL hash does not match source build report.' }
        }
        foreach ($required in @('Native/data','RuntimeAssets','Packages/com.rlabrecque.steamworks.net','docs/MULTI_INPUT_SAMPLE.md')) { if (-not (Test-Path (Join-Path $root $required))) { throw "Complete checkout required: missing $required" } }
        $log = Join-Path $out 'unity-build.log'
        $process = Start-Process $UnityEditor -ArgumentList "-batchmode -quit -force-d3d11 -projectPath `"$root`" -buildTarget Win64 -executeMethod Idas3Build.BuildMultiInputPlayer -logFile `"$log`"" -Wait -PassThru
        if ($process.ExitCode -ne 0) { throw "Unity game validation/build failed: $($process.ExitCode)" }
        foreach ($check in @('controller-foundation','device-snapshots','multi-input')) {
            Copy-Item (Join-Path $root "Verification/$check/unity-checks.txt") (Join-Path $out "$check.txt")
        }
        $player = Join-Path $root 'Builds/MultiInputSample'
        foreach ($entry in $native.plugins.PSObject.Properties) {
            $packaged = Join-Path $player "InitialDUnity_Data/Plugins/x86_64/$($entry.Name)"
            if ((Get-FileHash $packaged -Algorithm SHA256).Hash -ne $entry.Value) { throw 'Packaged native DLL differs from the exact-source build.' }
        }
        $result.nativeSourceCommit=$native.sourceCommit; $result.nativePlugins=$native.plugins
        $readme = Join-Path $player 'READ ME.txt'
        [IO.File]::WriteAllText($readme,"SOURCE COMMIT: $commit`nUnity 6000.6.0f1 / Input System 1.19.0 / Windows x64 Mono`n`n"+[IO.File]::ReadAllText($readme))
        # Use the existing privacy/ROM allowlist and full-player CRC verification unchanged.
        & python Tools/CI/Package-Game.py $player (Join-Path $out "ID3-MultiInput-$commit.zip")
        if ($LASTEXITCODE -ne 0) { throw 'Complete game package validation failed.' }
    }
    $result.status='PASS'; $result.package="complete $Kind Windows package produced; runtime/hardware acceptance pending"
} catch { $result.error=$_.Exception.Message; throw }
finally {
    $env:ID3_SOURCE_COMMIT=$previousSource
    if ($Kind -eq 'probe') { $log=Join-Path $root 'Diagnostics/DeviceIdentityProbe/build/unity-build.log'; if(Test-Path $log){Copy-Item $log $out} }
    if (Test-Path (Join-Path $root 'Logs/staging.log')) {Copy-Item (Join-Path $root 'Logs/staging.log') $out}
    $result.finishedUtc=[DateTime]::UtcNow.ToString('o');$result | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $out 'result.json')
}
