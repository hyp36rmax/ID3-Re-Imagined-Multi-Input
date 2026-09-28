"""Run each independent suite and retain exact-source reports, including failures."""
from pathlib import Path
import json, os, subprocess, sys
root=Path(__file__).resolve().parents[2]
out=root/'Verification/ci/portable';out.mkdir(parents=True,exist_ok=True)
if sys.version_info<(3,11):raise SystemExit('Python 3.11+ required by existing release packager; CI pins 3.12.')
dirty=bool(subprocess.check_output(['git','status','--porcelain'],cwd=root,text=True).strip())
sha=subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip()
commands=[('baseline',[sys.executable,'Tests/DeviceSnapshots/prepare_baseline.py']),
 ('probe-access',[sys.executable,'Diagnostics/DeviceIdentityProbe/Tests/audit.py','--access-only']),
 ('diagnostic-uat',['dotnet','run','--project','Diagnostics/DeviceIdentityProbe/Tests/ProbeTests.csproj','--property:UseSharedCompilation=false']),
 ('controller',['dotnet','run','--project','Tests/ControllerFoundation/ControllerFoundation.csproj','--property:UseSharedCompilation=false']),
 ('feedback-owner',['dotnet','run','--project','Tests/FeedbackOwner/FeedbackOwner.csproj','--property:UseSharedCompilation=false']),
 ('snapshots',['dotnet','run','--project','Tests/DeviceSnapshots/DeviceSnapshots.csproj','--property:UseSharedCompilation=false']),
 ('multi-input',['dotnet','run','--project','Tests/MultiInput/MultiInput.csproj','--property:UseSharedCompilation=false']),
 ('native-json-compile',['clang++','-std=c++17','Diagnostics/DeviceIdentityProbe/Tests/json_test.cpp','-o',str(out/'json-test')]),
 ('native-json',[str(out/'json-test')]),
 ('packaging',[sys.executable,'Tests/CI/check_packaging.py'])]
results=[]
for name,command in commands:
    try:
        p=subprocess.run(command,cwd=root,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
        code,log=p.returncode,p.stdout
    except OSError as e: code,log=1,str(e)
    (out/(name+'.txt')).write_text(log)
    results.append(dict(check=name,status='PASS' if code==0 else 'FAILED',exitCode=code))
    print(name,results[-1]['status'],flush=True)
(out/'result.json').write_text(json.dumps(dict(sourceCommit=sha,uncommittedChanges=dirty,status='PASS' if all(r['exitCode']==0 for r in results) else 'FAILED',checks=results,unity='NOT RUN',windows='NOT RUN',hardware='NOT TESTED'),indent=2))
sys.exit(any(r['exitCode'] for r in results))
