import sys,json,zipfile
from pathlib import Path
from reference_paths import binary, metadata, output_directory, parser

args = parser('Resolve reference scene localization and challenge event links.').parse_args()
p = args.input_dir
out = output_directory(args, 'scene-links')
import UnityPy
with zipfile.ZipFile(args.apk_dir/'base.apk') as z:
    env=UnityPy.load(z.read('assets/bin/Data/data.unity3d'))
objects={}
for o in env.objects:
    if o.type.name in ['GameObject','RectTransform','Transform']:
        objects[(o.assets_file.name,o.path_id)]=o.parse_as_dict()
def path(file,gid):
    go=objects.get((file,gid),{})
    label=go.get('m_Name','?')
    for comp in go.get('m_Component',[]):
        tid=comp.get('component',comp).get('m_PathID')
        t=objects.get((file,tid),{})
        if 'm_Father' in t and t['m_Father']['m_PathID']:
            father=objects[(file,t['m_Father']['m_PathID'])]
            return path(file,father['m_GameObject']['m_PathID'])+'/'+label
    return label
a=json.loads((p/'design-assets.json').read_text())
result=[]
for r in a:
    if r['script']=='LocalizedText':
        row=dict(path=path(r['file'],r['data']['m_GameObject']['m_PathID']),data=r['data'])
        result.append(row)
menu=next(r for r in a if r['script']=='MenuHandler')
for i,v in enumerate(menu['data']['challengeToggleObjs']):
    print('Challenge',i,path(menu['file'],v['m_PathID']))
for r in result:
    if 'Challenge' in str(r['data']): print(r)
(out/'scene-localization.json').write_text(json.dumps(result,indent=2))
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
g=TypeTreeGenerator('6000.0.58f2')
g.load_il2cpp(binary(args.apk_dir),metadata(args.apk_dir))
base_nodes=g.get_nodes
g.get_nodes=lambda assembly, fullname: base_nodes(assembly.removesuffix('.dll'),fullname)
env.typetree_generator=g
events=[]
for o in env.objects:
    if o.type.name=='MonoBehaviour' and b'GameUI.Challenge' in o.get_raw_data():
        tree=o.parse_as_dict()
        label=path(o.assets_file.name,tree['m_GameObject']['m_PathID'])
        events.append(dict(path=label,data=tree))
(out/'challenge-events.json').write_text(json.dumps(events,indent=2))
