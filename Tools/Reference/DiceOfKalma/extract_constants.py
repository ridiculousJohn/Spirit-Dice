import json,struct,hashlib,re
from pathlib import Path
from reference_paths import metadata, output_directory, parser

args = parser('Extract named static integer arrays from IL2CPP metadata.').parse_args()
p = args.input_dir
output_dir = output_directory(args, 'constants')
m = metadata(args.apk_dir)
h=json.loads((p/'metadata-header.json').read_text(encoding='utf-8-sig'))
def cstring(pos): return m[pos:m.index(b'\0',pos)].decode('utf-8')
fields={}
for i in range(h['fieldsSize']//12):
    name,typeid,token=struct.unpack_from('<III',m,h['fieldsOffset']+i*12)
    fields[i]=cstring(h['stringOffset']+name)
blobs={}
for off in range(h['fieldDefaultValuesOffset'],h['fieldDefaultValuesOffset']+h['fieldDefaultValuesSize'],12):
    index,typeid,dataindex=struct.unpack_from('<iii',m,off)
    name=fields[index]
    if not re.fullmatch('[0-9A-F]{64}',name) or dataindex<0: continue
    start=h['fieldAndParameterDefaultValueDataOffset']+dataindex
    for size in range(4,1025,4):
        b=m[start:start+size]
        if hashlib.sha256(b).hexdigest().upper()==name:
            blobs[name]=list(struct.unpack('<'+'i'*(size//4),b))
            break
names={
'maxHandUpgrades':'E846','diceHandMults':'6BB5','diceHandBones':'1C53',
'pileThresholds':'F12A','easyPileThresholds':'E38B','pileHands':'9CCD','pileRerolls':'9CCD',
'dailyHands':'0C8B','dailyRerolls':'0C8B','dailyThresholds':'0D18',
'challengePoints':'FB18','challengeThresholds':'AF62'}
out={k:next((v for hash,v in blobs.items() if hash.startswith(prefix)),None) for k,prefix in names.items()}
out['allStaticBlobs']=blobs
(output_dir/'constants.json').write_text(json.dumps(out,indent=2), encoding='utf-8')
print(json.dumps(out,indent=2))
