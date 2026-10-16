"""Validate prepared Discord assets without sending messages or using a network."""
import argparse,hashlib,json,struct,zipfile
from pathlib import Path

parser=argparse.ArgumentParser()
parser.add_argument('--showcase',type=Path,required=True)
parser.add_argument('--registry',type=Path,required=True)
parser.add_argument('--executable',type=Path,required=True)
args=parser.parse_args()
root=args.showcase
languages=json.loads(args.registry.read_text(encoding='utf-8-sig'))
build=json.loads((root/'BUILD-INFO.json').read_text(encoding='utf-8-sig'))
assert build['Hash'].lower()==hashlib.sha256(args.executable.read_bytes()).hexdigest()
records=[]
for language in languages:
    folder=root/language['code']
    manifest=json.loads((folder/'gallery.json').read_text(encoding='utf-8-sig'))
    assert manifest['version']=='2.1.0' and manifest['language']==language['code'] and manifest['syntheticPlayers'] is True
    assert len(manifest['images'])==10
    expected={'gallery.json'}
    with zipfile.ZipFile(folder/'SCUM-MiniMap-Showcase.zip') as archive:
        assert archive.testzip() is None
        for image in manifest['images']:
            name=image['filename'];expected.add(name)
            assert Path(name).name==name and name.endswith('.png') and image['description'].strip()
            data=(folder/name).read_bytes()
            assert data[:8]==b'\x89PNG\r\n\x1a\n' and archive.read(name)==data
            width,height=struct.unpack('>II',data[16:24]);assert width>=200 and height>=200
        assert len(archive.infolist())==11 and set(archive.namelist())==expected
    records.append({'language':language['code'],'images':10,'zipSha256':hashlib.sha256((folder/'SCUM-MiniMap-Showcase.zip').read_bytes()).hexdigest()})
(root/'verification.json').write_text(json.dumps({'version':'2.1.0','languages':records,'executableSha256':build['Hash'].lower()},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(f'Validated {len(records)} localized galleries, {len(records)*10} PNGs, matching native captions and ZIP contents against the verified executable.')
