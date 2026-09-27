"""Synthetic package boundaries, using the actual production packaging functions."""
from pathlib import Path
import importlib.util,json,tempfile
root=Path(__file__).resolve().parents[2]
def load(name,path):
    spec=importlib.util.spec_from_file_location(name,root/path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
probe=load('probe_package','Tools/CI/Package-Probe.py');game=load('game_package','Tools/CI/Package-Game.py')
checks=0
def reject(fn):
    global checks
    try:fn()
    except (AssertionError,ValueError):checks+=1;return
    raise AssertionError('Unsafe/incomplete package accepted')
with tempfile.TemporaryDirectory() as temp:
    t=Path(temp);p=t/'player';p.mkdir()
    reject(lambda:probe.build(p,t/'incomplete.zip'))
    for f in ['MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll','Id3IdentityProbe.exe','UnityPlayer.dll','Id3IdentityProbe_Data/globalgamemanagers','Id3IdentityProbe_Data/Managed/Assembly-CSharp.dll','Id3IdentityProbe_Data/Plugins/x86_64/Id3IdentityInventory.dll','README.md','docs/UAT.md']:
        path=p/f;path.parent.mkdir(parents=True,exist_ok=True);path.write_text('synthetic')
    (p/'build-self-test.json').write_text(json.dumps(dict(passed=True,nativeIdentityAgreement=True,uatRestartExport=True,identity='synthetic')))
    probe.build(p,t/'probe.zip');checks+=1
    (p/'private').mkdir();(p/'private/raw.json').write_text('private');reject(lambda:probe.build(p,t/'private.zip'));(p/'private/raw.json').unlink();(p/'private').rmdir()
    (p/'docs/unreviewed.secret').write_text('private');reject(lambda:probe.build(p,t/'secret.zip'));(p/'docs/unreviewed.secret').unlink()
    q=t/'game';q.mkdir()
    for f in ['InitialDUnity.exe','UnityPlayer.dll','InitialDUnity_Data/globalgamemanagers','InitialDUnity_Data/Managed/Assembly-CSharp.dll']:
        path=q/f;path.parent.mkdir(parents=True,exist_ok=True);path.write_text('synthetic')
    reject(lambda:game.build(q,t/'missing-runtime.zip'))
    for f in ['MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll','InitialDUnity_Data/Plugins/x86_64/Idas3Unity.dll','InitialDUnity_Data/Plugins/x86_64/Idas3WheelFeedback.dll','InitialDUnity_Data/Plugins/x86_64/steam_api64.dll','READ ME.txt']:
        path=q/f;path.parent.mkdir(parents=True,exist_ok=True);path.write_text('synthetic')
    game.build(q,t/'game.zip');checks+=1
    mono=p/'MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll';mono.unlink();reject(lambda:probe.build(p,t/'missing-probe-runtime.zip'))
    (q/'userdata-unity-scene').mkdir();reject(lambda:game.build(q,t/'save.zip'));(q/'userdata-unity-scene').rmdir()
    (q/'rom').mkdir();(q/'rom/game.chd').write_text('private');reject(lambda:game.build(q,t/'rom.zip'))
print(f'PASS {checks} synthetic package checks; no runnable player produced.')
