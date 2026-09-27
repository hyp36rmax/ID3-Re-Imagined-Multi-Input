param([string]$UnityEditor = $env:IDAS3_UNITY_EDITOR)
$ErrorActionPreference = 'Stop'
$probeRoot = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $probeRoot '../..')).Path
$template = Join-Path $probeRoot 'UnityProject'
$buildRoot = Join-Path $probeRoot 'build'
if (-not $IsWindows -and $env:OS -ne 'Windows_NT') { throw 'Build on Windows x64.' }
if (-not $UnityEditor) { $UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe' }
if (-not (Test-Path $UnityEditor)) { throw 'Set IDAS3_UNITY_EDITOR or pass -UnityEditor for Unity 6000.6.0f1.' }
# Reuse the repository's vswhere/vcvarsall + NMake approach, but build only this isolated DLL.
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) { throw 'Install Visual Studio C++ build tools and Windows SDK.' }
$vs = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vs) { throw 'MSVC x64 build tools not found.' }
$vcvars = Join-Path $vs 'VC\Auxiliary\Build\vcvarsall.bat'
Get-Command cmake -ErrorAction Stop | Out-Null
$head = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot read Git build identity.' }
# Digest diagnostic source, including local edits; exclude generated build/import output.
$sources = @('Native','UnityProject/Assets','UnityProject/Packages','UnityProject/ProjectSettings','Build-Probe.ps1') |
    ForEach-Object { Get-ChildItem (Join-Path $probeRoot $_) -File -Recurse }
$manifest = ($sources | Sort-Object FullName | ForEach-Object {
    $_.FullName.Substring($probeRoot.Length).Replace('\','/') + ':' + (Get-FileHash $_.FullName -Algorithm SHA256).Hash
}) -join "`n"
$sha = [System.Security.Cryptography.SHA256]::Create()
try { $digest = ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($manifest)))).Replace('-','').ToLowerInvariant() } finally { $sha.Dispose() }
$identity = "$head-$digest"
New-Item -ItemType Directory -Force $buildRoot | Out-Null
# Import a disposable copy, never the tracked template or production Unity project.
$project = Join-Path $buildRoot "projects/$digest"
New-Item -ItemType Directory -Force $project | Out-Null
foreach ($part in @('Assets','Packages','ProjectSettings')) { Copy-Item (Join-Path $template $part) $project -Recurse -Force }
$nativeBuild = Join-Path $buildRoot 'native'
$nativeSource = Join-Path $probeRoot 'Native'
$command = "call `"$vcvars`" x64 >nul && cmake -S `"$nativeSource`" -B `"$nativeBuild`" -G `"NMake Makefiles`" -DCMAKE_BUILD_TYPE=Release -DPROBE_BUILD_ID=$identity && cmake --build `"$nativeBuild`""
& cmd.exe /d /s /c $command
if ($LASTEXITCODE -ne 0) { throw 'Native probe build failed.' }
$plugins = Join-Path $project 'Assets/Plugins/x86_64'
$resources = Join-Path $project 'Assets/Resources'
New-Item -ItemType Directory -Force $plugins,$resources | Out-Null
Copy-Item (Join-Path $nativeBuild 'Id3IdentityInventory.dll') $plugins -Force
$utf8 = New-Object Text.UTF8Encoding($false)
$metadata = @{ identity=$identity; expectedUnity='6000.6.0f1'; expectedInput='1.19.0' } | ConvertTo-Json -Compress
[IO.File]::WriteAllText((Join-Path $resources 'probe-build.json'),$metadata,$utf8)
$oldPlayer = $env:ID3_PROBE_PLAYER
try {
    $env:ID3_PROBE_PLAYER = Join-Path $buildRoot 'player/Id3IdentityProbe.exe'
    $unityLog = Join-Path $buildRoot 'unity-build.log'
    $unityArgs = "-batchmode -quit -projectPath `"$project`" -buildTarget Win64 -executeMethod ProbeBuild.WindowsPlayer -logFile `"$unityLog`""
    $unityProcess = Start-Process -FilePath $UnityEditor -ArgumentList $unityArgs -Wait -PassThru
    if ($unityProcess.ExitCode -ne 0 -or -not (Test-Path $env:ID3_PROBE_PLAYER)) { throw 'Unity probe build failed. Inspect build/unity-build.log.' }
    Copy-Item (Join-Path $probeRoot 'README.md') (Join-Path $buildRoot 'player/README.md') -Force
    Write-Host "Built isolated probe: $env:ID3_PROBE_PLAYER"
    Write-Host "Build identity: $identity"
} finally { $env:ID3_PROBE_PLAYER = $oldPlayer }
