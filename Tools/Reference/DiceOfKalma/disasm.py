import sys, json, re, struct, bisect, io
from pathlib import Path
from reference_paths import binary, output_directory, parser

cli = parser('Disassemble named ARM64 methods using a local Il2CppDumper script.json.')
cli.add_argument('--script-json', type=Path, required=True, help='Local Il2CppDumper script.json output.')
cli.add_argument('patterns', nargs='+', help='Regular expressions matching method names.')
args = cli.parse_args()
root = output_directory(args, 'disassembly')
from capstone import Cs, CS_ARCH_ARM64, CS_MODE_ARM
from elftools.elf.elffile import ELFFile
data=binary(args.apk_dir)
f=io.BytesIO(data)
elf=ELFFile(f)
segments=[(s['p_vaddr'],s['p_offset'],s['p_filesz']) for s in elf.iter_segments() if s['p_type']=='PT_LOAD']
relocs={}
for section in elf.iter_sections():
    if section['sh_type']=='SHT_RELA':
        for reloc in section.iter_relocations():
            if reloc['r_info_type']==1027:
                relocs[reloc['r_offset']]=reloc['r_addend']
def at(va, n):
    for base,off,size in segments:
        if base <= va < base+size: return data[off+va-base:off+va-base+n]
    return b''
d=json.loads(args.script_json.read_text(encoding='utf-8-sig'))
names={x['Address']: x.get('Name',x.get('Value','')) for key in ['ScriptMethod','ScriptMetadata','ScriptMetadataMethod','ScriptString'] for x in d[key]}
addrs=sorted(set(d['Addresses'])|{x['Address'] for x in d['ScriptMethod']})
md=Cs(CS_ARCH_ARM64,CS_MODE_ARM)
def disasm(start,end):
    regs={}
    out=[]
    for ins in md.disasm(at(start,end-start),start):
        note=''
        args=ins.op_str
        if ins.mnemonic=='adrp':
            m=re.match(r'(x\d+), #0x([0-9a-f]+)',args)
            if m: regs[m[1]]=int(m[2],16)
        elif ins.mnemonic=='add':
            m=re.match(r'(x\d+), (x\d+), #0x([0-9a-f]+)$',args)
            if m and m[2] in regs:
                regs[m[1]]=regs[m[2]]+int(m[3],16)
                addr=regs[m[1]]
                note=f'addr {addr:x} '+names.get(addr,'')
        elif ins.mnemonic in ['ldr','ldur']:
            m=re.match(r'([xwqds]\d+), \[(x\d+)(?:, #(0x[0-9a-f]+|[0-9]+))?\]',args)
            if m and m[2] in regs:
                addr=regs[m[2]]+int(m[3] or '0',0)
                raw=at(addr,16 if m[1][0]=='q' else 8)
                note=f'[{addr:x}] '+names.get(addr,'')+' bytes='+raw.hex()
                regs.pop(m[1],None)
                if addr in relocs:
                    value=relocs[addr]
                    note+=f' -> {value:x} '+names.get(value,'')
                    regs[m[1]]=value
        elif ins.mnemonic in ['b','bl'] and args.startswith('#0x'):
            note=names.get(int(args[1:],16),'')
        out.append(f'{ins.address:08x}: {ins.mnemonic:8} {args:42} {note}')
    return '\n'.join(out)

if __name__=='__main__':
    for pattern in args.patterns:
        found=[x for x in d['ScriptMethod'] if re.search(pattern,x['Name'])]
        for x in found:
            start=x['Address']
            following=bisect.bisect_right(addrs,start)
            if following == len(addrs):
                raise ValueError('No end address available for method: ' + x['Name'])
            end=addrs[following]
            s=x['Name']+'\n'+disasm(start,end)
            path=root/('asm-'+re.sub(r'[^a-zA-Z0-9_.-]','_',x['Name'])+'.txt')
            path.write_text(s,encoding='utf-8')
            print(path.name,hex(start),hex(end),end-start)
