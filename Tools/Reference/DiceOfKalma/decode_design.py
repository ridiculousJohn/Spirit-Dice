import sys, json, csv, zipfile, collections
from pathlib import Path
from reference_paths import binary as read_binary, metadata, output_directory, parser

args = parser('Decode localized rules and custom Unity design assets.').parse_args()
root = args.input_dir
out = output_directory(args, 'decode-design')
import UnityPy
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator

for name in ['Skulls','DiceHands','GameUI','Dialogues']:
    with (root / (name+'.txt')).open(encoding='utf-8',newline='') as f:
        rows=list(csv.DictReader(f))
    english=[{'key':r['Key'],'text':r.get('English',''),'description':r.get('Description','')} for r in rows if r['Key']]
    (out/(name+'-english.json')).write_text(json.dumps(english,indent=2),encoding='utf-8')
    (out/(name+'-english.txt')).write_text('\n'.join(r['key']+' | '+r['text'].replace('\n',' / ') for r in english),encoding='utf-8')
    print(name, len(english))

binary = read_binary(args.apk_dir)
with zipfile.ZipFile(args.apk_dir/'base.apk') as apk:
    env=UnityPy.load(apk.read('assets/bin/Data/data.unity3d'))
generator=TypeTreeGenerator('6000.0.58f2')
generator.load_il2cpp(binary,metadata(args.apk_dir))
original_get_nodes = generator.get_nodes
generator.get_nodes = lambda assembly, fullname: original_get_nodes(assembly.removesuffix('.dll'), fullname)
env.typetree_generator=generator
rows=[]
errors=collections.Counter()
for o in env.objects:
    if o.type.name!='MonoBehaviour': continue
    try:
        head=o.parse_monobehaviour_head()
        script=head.m_Script.deref().parse_as_object()
        if script.m_AssemblyName not in ['Assembly-CSharp.dll','Assembly-CSharp']: continue
        tree=o.parse_as_dict()
        rows.append(dict(file=o.assets_file.name,id=o.path_id,script=script.m_ClassName,data=tree))
    except Exception as e:
        errors[str(e)[:200]]+=1
(out/'design-assets.json').write_text(json.dumps(rows,indent=2,ensure_ascii=True,default=str),encoding='utf-8')
print('Decoded',len(rows),'Scripts',collections.Counter(r['script'] for r in rows))
print('Errors', errors)
