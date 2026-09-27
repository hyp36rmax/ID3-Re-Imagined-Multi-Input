"""Static access-boundary check; does not claim a Windows binary or hardware audit."""
from pathlib import Path
import argparse
import re
import subprocess

root = Path(__file__).resolve().parents[1]
native = (root / 'Native/inventory.cpp').read_text()
code = re.sub(r'//[^\n]*|/\*.*?\*/', '', native, flags=re.S)
forbidden = r'\b(?:Acquire|SetCooperativeLevel|CreateEffect|SetParameters|SendForceFeedbackCommand|SetProperty|RegisterRawInputDevices|XInputSetState|HidD_Set\w*|WriteFile)\s*\('
assert not re.search(forbidden, code), 'Forbidden device mutation API'
assert 'CreateFileW(path,0,FILE_SHARE_READ|FILE_SHARE_WRITE' in code
assert '#include "original_' not in code and 'wheel_feedback_backend' not in code
player = (root / 'UnityProject/Assets/Runtime/ProbePlayer.cs').read_text()
assert 'Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ID3IdentityProbe", "Campaigns", campaignName)' in player
assert 'new Pseudonyms(File.ReadAllBytes(keyFile), session)' in player
parser=argparse.ArgumentParser()
parser.add_argument("--access-only",action="store_true",help="Audit probe API access only; later approved production milestones have separate diff review")
args=parser.parse_args()
repo = root.parents[1]
changes = [] if args.access_only else subprocess.check_output(['git', 'diff', '--name-only', '377e4ad9451a34ed4bc0acf735038372d8dec876'], cwd=repo, text=True).splitlines()
assert all(p.startswith('Diagnostics/DeviceIdentityProbe/') for p in changes), changes
print('PASS: static device-access boundary; no Windows execution.' if args.access_only else 'PASS: static device-access boundary and production-file diff checks; no Windows execution.')
