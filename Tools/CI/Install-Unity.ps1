# Version/changeset pinned to ProjectVersion.txt and Unity's official release page.
$ErrorActionPreference = 'Stop'
$destination = Join-Path $env:RUNNER_TEMP 'ID3Unity'
$installer = Join-Path $env:RUNNER_TEMP 'UnitySetup64-6000.6.0f1.exe'
Invoke-WebRequest 'https://download.unity3d.com/download_unity/f7f8ed4d1e24/Windows64EditorInstaller/UnitySetup64-6000.6.0f1.exe' -OutFile $installer
$signature = Get-AuthenticodeSignature $installer
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Unity Technologies') { throw 'Unity installer signature is not valid / not the expected publisher.' }
# Windows editor includes the Windows Mono player engine; IL2CPP is not used.
$process = Start-Process $installer -ArgumentList "/S /D=$destination" -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "Unity installer exit $($process.ExitCode)" }
$editor = Join-Path $destination 'Editor/Unity.exe'
$mono = Join-Path $destination 'Editor/Data/PlaybackEngines/windowsstandalonesupport'
if (-not (Test-Path $editor) -or -not (Test-Path $mono)) { throw 'Unity Windows editor / standalone support missing after installation.' }
"IDAS3_UNITY_EDITOR=$editor" | Add-Content $env:GITHUB_ENV
Remove-Item $installer
