import sys, json, zipfile, collections, re, struct
from pathlib import Path

from reference_paths import binary, output_directory, parser

args = parser('Inspect Unity assets and extract native inputs and localization tables.').parse_args()
root = output_directory(args, 'inspect-assets')
import UnityPy

with zipfile.ZipFile(args.apk_dir / 'base.apk') as apk:
    data = apk.read('assets/bin/Data/data.unity3d')
    meta = apk.read('assets/bin/Data/Managed/Metadata/global-metadata.dat')
    (root / 'global-metadata.dat').write_bytes(meta)
(root / 'libil2cpp.so').write_bytes(binary(args.apk_dir))
env = UnityPy.load(data)
print('Counts', collections.Counter(o.type.name for o in env.objects))
print('Versions', sorted(set(o.assets_file.unity_version for o in env.objects)))
rows = []
errors = collections.Counter()
for o in env.objects:
    if o.type.name in ['MonoBehaviour', 'MonoScript', 'TextAsset', 'GameObject']:
        try:
            tree = o.parse_as_dict()
            if o.type.name == 'TextAsset' and tree.get('m_Name') in ['Skulls', 'DiceHands', 'GameUI', 'Dialogues']:
                (root / (tree['m_Name'] + '.txt')).write_text(tree['m_Script'], encoding='utf-8')
            rows.append(dict(file=o.assets_file.name, id=o.path_id, type=o.type.name, data=tree))
        except Exception as e:
            errors[str(e)[:160]] += 1
            if o.type.name == 'MonoBehaviour':
                head = o.parse_monobehaviour_head()
                raw = o.get_raw_data()
                rows.append(dict(file=o.assets_file.name, id=o.path_id, type=o.type.name, name=head.m_Name,
                    script_id=head.m_Script.path_id, script_file=head.m_Script.file_id,
                    strings=[v.decode('utf-8', 'replace') for v in re.findall(rb'[\x20-\x7e]{5,}',raw)]))
(root / 'assets.json').write_text(json.dumps(rows, indent=2, ensure_ascii=True, default=str), encoding='utf-8')
print('Errors', errors)
print('Interesting', [(r.get('name',r.get('data',{}).get('m_Name','')),r['id']) for r in rows if r['type']=='TextAsset' or (r['type']=='MonoBehaviour' and r.get('name',r.get('data',{}).get('m_Name','')))])
print('Metadata header', struct.unpack_from('<10I',meta))
literal_offset, literal_size, data_offset = struct.unpack_from('<3I', meta, 8)
literals=[]
for i in range(literal_size // 8):
    length, offset = struct.unpack_from('<II', meta, literal_offset+i*8)
    value=meta[data_offset+offset:data_offset+offset+length].decode('utf-8','replace')
    literals.append(value)
(root / 'literals.json').write_text(json.dumps(literals,indent=2,ensure_ascii=True),encoding='utf-8')
(root / 'literals.txt').write_text('\n'.join(f'{i}: {s}' for i,s in enumerate(literals)),encoding='utf-8')
