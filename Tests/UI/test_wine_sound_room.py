"""Run with xvfb-run on Linux after Idas3MenuFontBuild.BuildDiagnostic."""
from pathlib import Path
import json
import os
import subprocess
import tempfile

root = Path(__file__).resolve().parents[2]
proof = Path(os.environ.get('IDAS3_FONT_TEST_OUTPUT', root / 'Verification/linux-reports-20260927/wine-font'))
proof.mkdir(parents=True, exist_ok=False)
fixture = Path(tempfile.mkdtemp(prefix='idas3-font-wine-'))
prefix = fixture / 'prefix'
player = root / 'Builds/MenuFontCheck/InitialDUnity.exe'
assert player.is_file(), 'Build the isolated font diagnostic first.'
wine = '/usr/lib/wine/wine64'
env = dict(os.environ, WINEPREFIX=str(prefix), WINEDEBUG='-all',
           WINEDLLOVERRIDES='mscoree,mshtml=;winemenubuilder.exe=d')

def win(p):
    return 'Z:' + str(p.resolve()).replace('/', '\\')

with (proof / 'wine.log').open('w') as output:
    process = subprocess.run(
        [wine, win(player), '-idas3-menu-font-smoke', win(proof / 'capture'),
         '-screen-fullscreen', '0', '-screen-width', '1200', '-screen-height', '720',
         '-logFile', win(proof / 'player.log')],
        cwd=player.parent, env=env, stdout=output, stderr=subprocess.STDOUT, timeout=180)
result = json.loads((proof / 'capture/report.json').read_text())
result['wine'] = subprocess.check_output([wine, '--version'], env=env, text=True).strip()
result['exitCode'] = process.returncode
result['isolatedPrefix'] = str(prefix)
(proof / 'wine-font-report.json').write_text(json.dumps(result, indent=2))
assert process.returncode == 0 and result['passed'], result
print(json.dumps(result), flush=True)
