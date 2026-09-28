"""Bind complete authored red/yellow/blue/neutral speed-atlas families.

GetSpeedColor in the supplied WBP_SpeedMeter_Base uses <90/<150/<210.
Only already imported four-texture families are bound. Identical legacy sets
are marked for the runtime palette; no texture files are modified here.
"""
import json,re,hashlib
from pathlib import Path
root=Path(__file__).resolve().parents[1]
path=root/'Assets/Resources/ArcadeHud/Catalog/catalog.json'
data=json.loads(path.read_text(encoding='utf-8'));count=0;meters=[]
for meter in data['meters']:
    changed=False
    for layer in meter['layers']:
        if layer.get('role') not in ('speed1','speed10','speed100') and layer['name']!='SpeedRate':continue
        texture=layer.get('texture','')
        if not re.search(r'_SpdNum(?:_Oth)?0[1-4]$',texture):continue
        choices=[texture[:-2]+f'{i:02}' for i in range(1,5)]
        if not all((root/'Assets/Resources'/f'{choice}.png').is_file() for choice in choices):continue
        layer['speedTextures']=choices
        # Several legacy families export four identical yellow atlases. They
        # need a runtime palette, not merely another filename at each threshold.
        hashes={hashlib.sha256((root/'Assets/Resources'/f'{choice}.png').read_bytes()).digest() for choice in choices}
        layer['speedPalette']=len(hashes)==1
        count+=1;changed=True
    if changed:meters.append(meter['id'])
path.write_text(json.dumps(data,ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf-8')
print(json.dumps(dict(layers=count,meters=meters)))
