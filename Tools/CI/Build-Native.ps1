# Run independently of Unity and its license. No device output or asset/ROM smoke tests.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Set-Location $root
$out = Join-Path $root 'Verification/ci/native'
New-Item -ItemType Directory -Force $out | Out-Null
$commit = (& git rev-parse HEAD).Trim()
Start-Transcript -Path (Join-Path $out 'native-build.log') | Out-Null
try {
    $changed = @(& git diff --name-only HEAD) | Where-Object { $_ -notin @('Assets/Plugins/x86_64/Idas3Unity.dll','Assets/Plugins/x86_64/Idas3WheelFeedback.dll') }
    $untracked = @(& git ls-files --others --exclude-standard)
    if ($changed.Count -gt 0 -or $untracked.Count -gt 0) { throw 'Use a clean disposable checkout of the reviewed commit; source changes cannot be labelled as that commit.' }
    if ($env:OS -ne 'Windows_NT') { throw 'Windows x64 with MSVC and Windows SDK is required.' }
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    $vs = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    if (-not $vs) { throw 'MSVC C++ tools / Windows SDK missing.' }
    $vcvars = Join-Path $vs 'VC/Auxiliary/Build/vcvarsall.bat'
    & cmd.exe /d /v:on /s /c "call `"$vcvars`" x64 && echo WindowsSDKVersion=!WindowsSDKVersion! && cl 2>&1"
    # cl without input prints its version and exits nonzero; compilation below is authoritative.
    & (Join-Path $root 'Build Native.cmd')
    if ($LASTEXITCODE -ne 0) { throw 'Native DLL compilation failed.' }
    & cmd.exe /d /s /c "call `"$vcvars`" x64 >nul && cmake --build Native/build-unity-d --target host_steering_smoothing_tests original_ffb_tests original_ffb_owner_tests"
    if ($LASTEXITCODE -ne 0) { throw 'Native test compilation failed.' }
    & ctest --test-dir Native/build-unity-d -R '^(host_steering_smoothing|original_ffb|original_ffb_owner)$' --output-on-failure --no-tests=error --output-junit (Join-Path $out 'native-tests.xml')
    if ($LASTEXITCODE -ne 0) { throw 'Native tests failed.' }
    New-Item -ItemType Directory -Force (Join-Path $out 'plugins') | Out-Null
    $dlls = @('Idas3Unity.dll','Idas3WheelFeedback.dll')
    $hashes = @{}
    foreach ($dll in $dlls) {
        $path = Join-Path $root "Assets/Plugins/x86_64/$dll"
        if (-not (Test-Path $path)) { throw "Missing compiled DLL: $dll" }
        Copy-Item $path (Join-Path $out 'plugins')
        $hashes[$dll] = (Get-FileHash $path -Algorithm SHA256).Hash
    }
    @{status='PASS'; sourceCommit=$commit; os=[Environment]::OSVersion.VersionString; visualStudio=$vs; plugins=$hashes; hardware='NOT TESTED'; unity='NOT RUN'} | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $out 'result.json')
} catch {
    @{status='FAILED'; sourceCommit=$commit; error=$_.Exception.Message} | ConvertTo-Json | Set-Content (Join-Path $out 'result.json')
    throw
} finally { Stop-Transcript | Out-Null }
