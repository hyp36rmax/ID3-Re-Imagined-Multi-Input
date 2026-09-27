"""Require runtime/native dependencies, then reuse the existing release privacy boundary."""
from pathlib import Path
import importlib.util,sys

def build(player,archive):
    player=Path(player)
    required={'MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll',
              'InitialDUnity_Data/Plugins/x86_64/Idas3Unity.dll',
              'InitialDUnity_Data/Plugins/x86_64/Idas3WheelFeedback.dll',
              'InitialDUnity_Data/Plugins/x86_64/steam_api64.dll','READ ME.txt'}
    assert all((player/p).is_file() for p in required),'Incomplete game runtime/native dependencies'
    spec=importlib.util.spec_from_file_location('release_package',Path(__file__).parents[1]/'Build-ReleasePackage.py')
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    return module.build(player,archive)
if __name__=='__main__':build(*sys.argv[1:])
