"""Package only a freshly generated probe player and its reviewed documentation."""
from pathlib import Path
import hashlib,json,stat,sys,zipfile

def build(player,archive):
    player,archive=Path(player),Path(archive)
    required={'Id3IdentityProbe.exe','UnityPlayer.dll','Id3IdentityProbe_Data/globalgamemanagers',
              'Id3IdentityProbe_Data/Managed/Assembly-CSharp.dll','Id3IdentityProbe_Data/Plugins/x86_64/Id3IdentityInventory.dll',
              'README.md','docs/UAT.md','build-self-test.json'}
    top={'Id3IdentityProbe.exe','UnityPlayer.dll','UnityCrashHandler64.exe','dstorage.dll','dstoragecore.dll','README.md','build-self-test.json'}
    dirs={'Id3IdentityProbe_Data','MonoBleedingEdge','D3D12','docs'}
    files=[]
    def visit(folder):
        for p in folder.iterdir():
            rel=p.relative_to(player); info=p.lstat()
            assert not p.is_symlink() and not (getattr(info,'st_file_attributes',0)&stat.FILE_ATTRIBUTE_REPARSE_POINT),rel
            assert not any(s.lower() in {'campaigns','uat','private','share','userdata','userdata-unity-scene','rom'} for s in rel.parts),rel
            if p.is_dir():
                assert rel.parts[0] in dirs,rel
                visit(p)
            else:
                assert p.is_file() and (rel.parts[0] in dirs if len(rel.parts)>1 else rel.name in top),rel
                assert p.suffix.lower() not in {'.secret','.log','.ulf','.chd','.idreplay'},rel
                if rel.parts[0]=='docs': assert rel.as_posix() in {'docs/UAT.md','docs/DEVELOPMENT_HISTORY.md'},rel
                files.append(p)
    visit(player)
    assert required<={p.relative_to(player).as_posix() for p in files},'Incomplete probe player'
    check=json.loads((player/'build-self-test.json').read_text())
    assert check['passed'] and check['nativeIdentityAgreement'] and check['uatRestartExport'],'Probe validation missing'
    archive.parent.mkdir(parents=True,exist_ok=True)
    with zipfile.ZipFile(archive,'x',zipfile.ZIP_DEFLATED) as z:
        for p in files:z.write(p,p.relative_to(player).as_posix())
    with zipfile.ZipFile(archive) as z: assert z.testzip() is None
    with archive.open('rb') as f:digest=hashlib.file_digest(f,'sha256').hexdigest()
    archive.with_suffix('.report.json').write_text(json.dumps(dict(sha256=digest,files=len(files),build=check['identity'],completePlayer=True,hardware='NOT TESTED'),indent=2))
if __name__=='__main__':build(*sys.argv[1:])
