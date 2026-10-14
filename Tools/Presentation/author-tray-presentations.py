"""Author editable encounter tray prefabs, mesh assets, and surface materials.

The output is static Unity content. No authoring formulas run in the player.
The default writes a new ignored preview directory beneath Docs/work.
"""
from pathlib import Path
import argparse
import itertools
import math
import struct
import tempfile
import uuid

ROOT = Path(__file__).resolve().parents[2]
BASE = Path('Dice-Unity/Assets/Dice/Content/Presentation')
SHARED = BASE / 'Shared'
OUTPUT = ROOT
SHADER = '2bf45d368b9b4a21b2da5b3a1c1ed36b'
WOOD = '0edec0dd42ef415bb6a06e07a40e4c5b'
BONE = '4554059d4d75478c8ea275baa3695ef7'
GEOMETRY = 'd9322fa612f94632bf737e9b610dc4de'
PRESENTATION = '70365ad616e9415aa4038f2f3bd30c35'


class Flow(dict):
    pass


def scalar(value):
    if isinstance(value, Flow):
        return '{' + ', '.join(str(k) + ': ' + scalar(v) for k, v in value.items()) + '}'
    if isinstance(value, bool):
        return '1' if value else '0'
    if isinstance(value, float):
        return format(0 if abs(value) < 1e-9 else value, '.8g')
    return str(value)


def yaml(value, indent=0):
    prefix = ' ' * indent
    result = []
    if isinstance(value, dict):
        for key, item in value.items():
            if isinstance(item, (dict, list)) and not isinstance(item, Flow) and item:
                result.append(prefix + str(key) + ':')
                result.extend(yaml(item, indent + 2))
            else:
                result.append(prefix + str(key) + ': ' + ('[]' if item == [] else scalar(item)))
    else:
        for item in value:
            if isinstance(item, dict) and not isinstance(item, Flow):
                lines = yaml(item, indent + 2)
                lines[0] = prefix + '- ' + lines[0][indent + 2:]
                result.extend(lines)
            else:
                result.append(prefix + '- ' + scalar(item))
    return result


def guid(path):
    existing = ROOT / (str(path) + '.meta')
    if existing.exists():
        return next(line.split(':', 1)[1].strip() for line in existing.read_text().splitlines()
                    if line.startswith('guid:'))
    return uuid.uuid5(uuid.NAMESPACE_URL, path.as_posix()).hex


def ref(value=None, file_id=0, kind=2):
    return Flow(fileID=file_id, guid=value, type=kind) if value else Flow(fileID=file_id)


def vec(*values):
    return Flow(zip(('x', 'y', 'z', 'w'), values))


def color(*values):
    return Flow(zip(('r', 'g', 'b', 'a'), values))


def common():
    return dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=ref(),
                m_PrefabInstance=ref(), m_PrefabAsset=ref())


def write(path, objects, importer='NativeFormatImporter', main_id=0):
    target = OUTPUT / path
    target.parent.mkdir(parents=True, exist_ok=True)
    text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
    for cls, file_id, name, data in objects:
        text += '--- !u!' + str(cls) + ' &' + str(file_id) + '\n'
        text += '\n'.join(yaml({name: data})) + '\n'
    target.write_text(text, encoding='utf-8')
    meta = 'fileFormatVersion: 2\nguid: ' + guid(path) + '\n' + importer + ':\n'
    meta += '  externalObjects: {}\n'
    if importer == 'NativeFormatImporter':
        meta += '  mainObjectFileID: ' + str(main_id) + '\n'
    meta += '  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
    Path(str(target) + '.meta').write_text(meta, encoding='utf-8')
    return guid(path)


def folder(path):
    (OUTPUT / path).mkdir(parents=True, exist_ok=True)
    meta = OUTPUT / (str(path) + '.meta')
    if not (ROOT / (str(path) + '.meta')).exists():
        meta.write_text('fileFormatVersion: 2\nguid: ' + guid(path) + '\nfolderAsset: yes\n'
                        'DefaultImporter:\n  externalObjects: {}\n  userData:\n'
                        '  assetBundleName:\n  assetBundleVariant:\n', encoding='utf-8')


def cross(a, b):
    return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])


def mesh(path, faces):
    vertices, indices = [], []
    for points in faces:
        normal = cross(tuple(points[1][i]-points[0][i] for i in range(3)),
                       tuple(points[2][i]-points[0][i] for i in range(3)))
        length = math.sqrt(sum(n*n for n in normal))
        normal = tuple(n/length for n in normal)
        center = tuple(sum(p[i] for p in points)/len(points) for i in range(3))
        if sum(normal[i]*center[i] for i in range(3)) < 0:
            points = list(reversed(points))
            normal = tuple(-n for n in normal)
        start = len(vertices)
        for p in points:
            dominant = max(range(3), key=lambda i: abs(normal[i]))
            uv = (p[0], p[2]) if dominant == 1 else ((p[0], p[1]) if dominant == 2 else (p[2], p[1]))
            vertices.append((*p, *normal, *uv, uv[0]+.5, uv[1]+.5))
        for i in range(1, len(points)-1):
            indices.extend((start, start+i, start+i+1))
    channels = [dict(stream=0, offset=0, format=0, dimension=0) for _ in range(14)]
    for index, offset, dimension in ((0,0,3),(1,12,3),(4,24,2),(6,32,2)):
        channels[index].update(offset=offset, dimension=dimension)
    blob = b''.join(struct.pack('<10f', *vertex) for vertex in vertices)
    index_blob = struct.pack('<' + 'H'*len(indices), *indices)
    packed_float = dict(m_NumItems=0, m_Range=0, m_Start=0, m_Data='', m_BitSize=0)
    packed_int = dict(m_NumItems=0, m_Data='', m_BitSize=0)
    bounds = dict(m_Center=vec(0,0,0), m_Extent=vec(.5,.5,.5))
    data = common()
    data.update(m_Name=path.stem, serializedVersion=12,
                m_SubMeshes=[dict(serializedVersion=2, firstByte=0, indexCount=len(indices), topology=0,
                                 baseVertex=0, firstVertex=0, vertexCount=len(vertices), localAABB=bounds)],
                m_Shapes=dict(vertices=[], shapes=[], channels=[], fullWeights=[]),
                m_BindPose=[], m_BoneNameHashes='', m_RootBoneNameHash=0, m_BonesAABB=[],
                m_VariableBoneCountWeights=dict(m_Data=''), m_MeshCompression=0,
                m_IsReadable=1, m_KeepVertices=0, m_KeepIndices=0, m_IndexFormat=0,
                m_IndexBuffer=index_blob.hex(),
                m_VertexData=dict(serializedVersion=3, m_VertexCount=len(vertices),
                                  m_Channels=channels, m_DataSize=len(blob), _typelessdata=blob.hex()),
                m_CompressedMesh=dict(m_Vertices=packed_float, m_UV=packed_float,
                                      m_Normals=packed_float, m_Tangents=packed_float,
                                      m_Weights=packed_int, m_NormalSigns=packed_int,
                                      m_TangentSigns=packed_int, m_FloatColors=packed_float,
                                      m_BoneIndices=packed_int, m_Triangles=packed_int, m_UVInfo=0),
                m_LocalAABB=bounds, m_MeshUsageFlags=0, m_BakedConvexCollisionMesh='',
                m_BakedTriangleCollisionMesh='', m_MeshMetrics=[1,1], m_MeshOptimizationFlags=-1,
                m_StreamData=dict(serializedVersion=2, offset=0, size=0, path=''))
    return write(path, [(43,4300000,'Mesh',data)], main_id=4300000)


def bevel_faces():
    faces, inset = [], .46
    for axis in range(3):
        other = [i for i in range(3) if i != axis]
        for sign in (-1,1):
            face = []
            for a,b in ((-1,-1),(1,-1),(1,1),(-1,1)):
                p = [0,0,0]
                p[axis]=sign*.5; p[other[0]]=a*inset; p[other[1]]=b*inset
                face.append(tuple(p))
            faces.append(face)
    for axis in range(3):
        other = [i for i in range(3) if i != axis]
        for a,b in itertools.product((-1,1),repeat=2):
            face=[]
            for longitudinal, first in ((-1,True),(1,True),(1,False),(-1,False)):
                p=[0,0,0]; p[axis]=longitudinal*inset
                p[other[0]]=a*(.5 if first else inset)
                p[other[1]]=b*(inset if first else .5)
                face.append(tuple(p))
            faces.append(face)
    for signs in itertools.product((-1,1),repeat=3):
        faces.append([tuple(signs[i]*(.5 if i==axis else inset) for i in range(3)) for axis in range(3)])
    return faces


def rough_faces():
    outline = [(-.5,-.46),(-.34,-.49),(-.08,-.455),(.16,-.5),(.40,-.47),
               (.5,-.42),(.48,.35),(.5,.46),(.22,.5),(.05,.465),(-.22,.495),(-.5,.43)]
    bottom=[(x,-.5,z) for x,z in outline]
    top=[(x,.5,z) for x,z in outline]
    return [bottom,top] + [[bottom[i],bottom[(i+1)%len(outline)],top[(i+1)%len(outline)],top[i]]
                           for i in range(len(outline))]


def rivet_faces():
    bottom=[(.5*math.cos(i*math.tau/12),-.5,.5*math.sin(i*math.tau/12)) for i in range(12)]
    top=[(.44*math.cos(i*math.tau/12),.5,.44*math.sin(i*math.tau/12)) for i in range(12)]
    return [bottom,top] + [[bottom[i],bottom[(i+1)%12],top[(i+1)%12],top[i]] for i in range(12)]


def material(folder_path, name, tint, kind, seed=0, grain=(8,5,18,0)):
    path=folder_path / (name+'.mat')
    data=common()
    data.update(serializedVersion=8, m_Name=name, m_Shader=ref(SHADER,4800000,3),
                m_Parent=ref(), m_ModifiedSerializedProperties=0, m_ValidKeywords=[], m_InvalidKeywords=[],
                m_LightmapFlags=4, m_EnableInstancingVariants=0, m_DoubleSidedGI=0,
                m_CustomRenderQueue=-1, stringTagMap=Flow(), disabledShaderPasses=[], m_LockedProperties='',
                m_SavedProperties=dict(serializedVersion=3,
                    m_TexEnvs=[{'_SurfaceTexture':dict(m_Texture=ref(WOOD if kind in (1,8) else None,2800000 if kind in (1,8) else 0,3),
                                                     m_Scale=vec(1,1),m_Offset=vec(0,0))}],
                    m_Ints=[],m_Floats=[{key:value} for key,value in
                        (('_SurfaceKind',kind),('_ShowPips',0),('_ShowFaceIcon',0),('_Seed',seed),
                         ('_Smoothness',.23),('_SceneLightingEnabled',0),('_SrcBlend',1),
                         ('_DstBlend',0),('_Cull',2),('_ZWrite',1))],
                    m_Colors=[{'_BaseColor':color(*tint,1)},{'_GrainScale':color(*grain)}]),
                m_BuildTextureStacks=[], m_AllowLocking=1)
    return write(path,[(21,2100000,'Material',data)],main_id=2100000)


class Prefab:
    def __init__(self, name):
        self.name=name; self.nodes=[]; self.next_id=1000
        self.root=self.node(name)
    def node(self,name,parent=None,position=(0,0,0),size=(1,1,1),angle=0,geometry=None):
        self.next_id+=10
        node=dict(name=name,go=self.next_id,transform=self.next_id+1,components=[],children=[],
                  parent=parent,position=position,size=size,angle=angle,geometry=geometry)
        if parent: parent['children'].append(node)
        if geometry: node['components']=[self.next_id+2,self.next_id+3]
        self.nodes.append(node)
        return node
    def box(self,name,mesh_id,mat_id,position,size,angle=0,parent=None):
        return self.node(name,parent or self.root,position,size,angle,(mesh_id,mat_id))
    def write(self,path,slots,labels,modifier,scale,width,depth,die_size,spacing):
        objects=[]
        for node in self.nodes:
            components=[ref(file_id=node['transform'])]+[ref(file_id=i) for i in node['components']]
            if node is self.root: components.append(ref(file_id=11400000))
            game=common(); game.update(serializedVersion=6,m_Component=[dict(component=c) for c in components],
                                      m_Layer=31,m_Name=node['name'],m_TagString='Untagged',m_Icon=ref(),
                                      m_NavMeshLayer=0,m_StaticEditorFlags=0,m_IsActive=1)
            objects.append((1,node['go'],'GameObject',game))
            rotation=node['angle']*math.pi/360
            transform=common(); transform.update(m_GameObject=ref(file_id=node['go']),serializedVersion=2,
                m_LocalRotation=vec(0,math.sin(rotation),0,math.cos(rotation)),
                m_LocalPosition=vec(*node['position']),m_LocalScale=vec(*node['size']),
                m_ConstrainProportionsScale=0,m_Children=[ref(file_id=n['transform']) for n in node['children']],
                m_Father=ref(file_id=node['parent']['transform'] if node['parent'] else 0),
                m_LocalEulerAnglesHint=vec(0,node['angle'],0))
            objects.append((4,node['transform'],'Transform',transform))
            if node['geometry']:
                mesh_id,mat_id=node['geometry']
                filt=common(); filt.update(m_GameObject=ref(file_id=node['go']),m_Mesh=ref(mesh_id,4300000))
                objects.append((33,node['components'][0],'MeshFilter',filt))
                rend=common(); rend.update(m_GameObject=ref(file_id=node['go']),m_Enabled=1,m_CastShadows=1,
                    m_ReceiveShadows=1,m_DynamicOccludee=1,m_StaticShadowCaster=0,m_MotionVectors=1,
                    m_LightProbeUsage=1,m_ReflectionProbeUsage=1,m_RayTracingMode=2,m_RayTraceProcedural=0,
                    m_RenderingLayerMask=1,m_RendererPriority=0,m_Materials=[ref(mat_id,2100000)],
                    m_StaticBatchInfo=dict(firstSubMesh=0,subMeshCount=0),m_StaticBatchRoot=ref(),
                    m_ProbeAnchor=ref(),m_LightProbeVolumeOverride=ref(),m_ScaleInLightmap=1,
                    m_ReceiveGI=1,m_PreserveUVs=0,m_IgnoreNormalsForChartDetection=0,m_ImportantGI=0,
                    m_StitchLightmapSeams=1,m_SelectedEditorRenderState=3,m_MinimumChartSize=4,
                    m_AutoUVMaxDistance=.5,m_AutoUVMaxAngle=89,m_LightmapParameters=ref(),
                    m_SortingLayerID=0,m_SortingLayer=0,m_SortingOrder=0,m_AdditionalVertexStreams=ref())
                objects.append((23,node['components'][1],'MeshRenderer',rend))
        data=common();data.update(m_GameObject=ref(file_id=self.root['go']),m_Enabled=1,m_EditorHideFlags=0,
            m_Script=ref(GEOMETRY,11500000,3),m_Name='',m_EditorClassIdentifier='',
            DiceSlots=[ref(file_id=n['transform']) for n in slots],
            DiceLabelAnchors=[ref(file_id=n['transform']) for n in labels],DiceScale=scale,
            ModifierRoot=ref(file_id=modifier['transform']),ModifierWidth=width,ModifierDepth=depth,
            ModifierDieSize=die_size,ModifierSlotSpacing=spacing,ModifierVisibleCapacity=9)
        objects.append((114,11400000,'MonoBehaviour',data))
        return write(path,objects,'PrefabImporter')


def author(name,folder_name,width,spacing,scale,style,pitch,composition,distance=10):
    destination=BASE/folder_name
    wood_tint=(1,.96,.89) if style=='fox' else ((.55,.47,.35) if style=='moth' else (.46,.48,.47))
    dark_tint=(.63,.52,.40) if style=='fox' else ((.30,.28,.23) if style=='moth' else (.32,.35,.35))
    surface_kind=1 if style=='fox' else (8 if style=='moth' else 7)
    grain=(8,5,18,0) if style=='fox' else (1,1,1,0)
    wood=material(destination,name+'Wood',wood_tint,surface_kind,11,grain)
    dark=material(destination,name+'DarkWood',dark_tint,surface_kind,37,grain)
    metal=material(destination,name+('Brass' if style=='fox' else 'Iron'),
                   (.66,.43,.16) if style=='fox' else (.18,.20,.22),3,19)
    felt=None
    if style!='moth':
        felt=material(destination,name+'Felt',(.085,.12,.067) if style=='fox' else (.23,.25,.24),
                      2 if style=='fox' else 7,23,grain)
    p=Prefab(name+'TrayGeometry')
    dice_z=.45; modifier_z=-1.04; front=-1.56; rear=1.54
    surface=.04 if style!='moth' else .0
    rough=guid(SHARED/'SpiritTrayRoughSlatMesh.asset')
    smooth=guid(SHARED/'SpiritTrayBeveledTimberMesh.asset')
    rivet=guid(SHARED/'SpiritTrayRivetMesh.asset')
    if style=='fox':
        p.box('Walnut Foundation',smooth,wood,(0,-.09,0),(width+.22,.20,3.15))
        p.box('Front Walnut Rail',smooth,wood,(0,.15,front),(width,.32,.19))
        p.box('Back Walnut Rail',smooth,wood,(0,.18,rear),(width,.38,.22))
        for x in (-width/2,width/2):
            p.box('Walnut Side Rail',smooth,wood,(x,.18,0),(3.12,.37,.22),90)
            p.box('Side Brass Inlay',smooth,metal,(x,.371,0),(3.02,.012,.018),90)
        for z in (front,rear):
            p.box('Brass Rail Inlay',smooth,metal,(0,.318 if z==front else .378,z),(width-.22,.012,.018))
        for x in (-width/2+.12,width/2-.12):
            for z in (front,rear):
                h=.32 if z==front else .38
                p.box('Brass Corner Plate',smooth,metal,(x,h,z),(.25,.014,.18))
                for offset in (-.07,.07):
                    p.box('Brass Corner Rivet',rivet,metal,(x+offset,h+.015,z),(.048,.025,.048))
    elif style=='watchtower':
        for i,z in enumerate((-.99,-.38,.23,.84,1.38)):
            p.box('Weathered Base Plank '+str(i+1),rough,dark if i%2 else wood,(0,-.08,z),(width+.12,.16,.59))
        for z in (front,rear):
            p.box('Weathered End Rail',rough,wood,(0,.13,z),(width,.30,.16))
        for x in (-width/2,width/2):
            p.box('Iron Edge Strap',smooth,metal,(x,.07,0),(3.10,.30,.10),90)
            for z in (-1.3,0,1.3):
                p.box('Iron Strap Nail',rivet,metal,(x,.225,z),(.072,.025,.072))
    else:
        # Separate bay slats leave the painted cart floor visible between the loose timbers.
        for bay in range(5):
            for i,z in enumerate((-.16,.45,1.06)):
                p.box('Cart Bay '+str(bay+1)+' Floor Slat '+str(i+1),rough,wood if (bay+i)%3 else dark,
                      ((bay-2)*spacing,-.085,z),(spacing-.16,.16,.47),(.8 if (bay+i)%2 else -.8))
        for i,z in enumerate((-1.21,-.88)):
            p.box('Modifier Floor Slat '+str(i+1),rough,wood if i else dark,
                  (0,-.085,z),(width-.18,.16,.23),(.15 if i else -.15))
        p.box('Low Rear Cross Slat',rough,wood,(0,.14,rear),(width-.12,.30,.11),-.45)
        p.box('Worn Front Stop Slat',rough,dark,(0,.07,front),(width-.08,.18,.095),.35)
        for x in (-width/2,width/2):
            for z in (-1.25,1.25):
                p.box('Cart Peg',rivet,metal,(x,.232,z),(.05,.018,.05))
    divider_mesh=rough if style!='fox' else smooth
    divider_h=.22 if style=='moth' else .30
    for index in range(4):
        x=(index-1.5)*spacing
        p.box('Dice Bay Divider '+str(index+1),divider_mesh,wood,(x,divider_h/2,dice_z),
              (1.93,divider_h,.065 if style=='moth' else .095),90+(.8*(index%2) if style=='moth' else 0))
        if style=='fox':
            p.box('Divider Brass Inlay '+str(index+1),smooth,metal,(x,divider_h+.005,dice_z),
                  (1.83,.010,.018),90)
    p.box('Modifier Bay Cross Divider',divider_mesh,wood,(0,.105,-.57),
          (width-.13,.22,.07 if style=='moth' else .12))
    if style=='fox':
        p.box('Modifier Divider Brass Inlay',smooth,metal,(0,.221,-.57),(width-.22,.01,.018))
    slots=[];labels=[]
    for index in range(5):
        x=(index-2)*spacing
        if felt:
            p.box('Dice Bay Felt '+str(index+1),smooth,felt,(x,.026,dice_z),
                  (spacing-.13,.028,1.90))
        slots.append(p.node('Dice '+str(index+1)+' Surface',p.root,(x,surface,dice_z)))
        labels.append(p.node('Dice '+str(index+1)+' Label',p.root,(x,.34,-.47)))
    modifier_width=width-.43
    if felt:
        p.box('Modifier Bay Felt',smooth,felt,(0,.026,modifier_z),(modifier_width,.028,.78))
    modifier=p.node('Modifier Surface',p.root,(0,surface,modifier_z))
    die_size=.44 if style=='moth' else .49
    modifier_spacing=.59 if style=='moth' else .67
    prefab=destination/(name+'TrayGeometry.prefab')
    prefab_id=p.write(prefab,slots,labels,modifier,scale,modifier_width,.75,die_size,modifier_spacing)
    path=destination/(name+'TrayPresentation.asset')
    rad=pitch*math.pi/180
    data=common();data.update(m_GameObject=ref(),m_Enabled=1,m_EditorHideFlags=0,
        m_Script=ref(PRESENTATION,11500000,3),m_Name=path.stem,m_EditorClassIdentifier='',
        DiceShader=ref(SHADER,4800000,3),BoneTexture=ref(BONE,2800000,3),WoodTexture=ref(WOOD,2800000,3),
        GeometryPrefab=ref(prefab_id,11400000,3),
        CameraPosition=vec(0,math.sin(rad)*distance,-math.cos(rad)*distance),
        CameraEulerAngles=vec(pitch,0,0),Orthographic=False,FieldOfView=27,OrthographicSize=4,
        NearClipPlane=.1,FarClipPlane=30,FallbackLightEulerAngles=vec(65,-32,0),
        FallbackLightColor=color(1,.94,.8,1),FallbackLightIntensity=1.2,TextureSize=vec(1024,512),
        CompositionRect=Flow(serializedVersion=2,x=composition[0],y=composition[1],width=composition[2],height=composition[3]))
    asset_id=write(path,[(114,11400000,'MonoBehaviour',data)],main_id=11400000)
    print(path.as_posix(),asset_id)
    print(prefab.as_posix(),prefab_id)


def main():
    global OUTPUT
    parser=argparse.ArgumentParser()
    mode=parser.add_mutually_exclusive_group()
    mode.add_argument('--write-assets',action='store_true',help='Reauthor the checked-in tray assets in place.')
    mode.add_argument('--output',type=Path,help='Unused preview directory beneath Docs/work.')
    args=parser.parse_args()
    if not args.write_assets:
        scratch=(ROOT/'Docs/work').resolve()
        scratch.mkdir(parents=True,exist_ok=True)
        if args.output:
            OUTPUT=args.output.resolve()
            if not OUTPUT.is_relative_to(scratch) or OUTPUT==scratch:
                parser.error('--output must be a new directory beneath Docs/work.')
            OUTPUT.mkdir(parents=True,exist_ok=False)
        else:
            OUTPUT=Path(tempfile.mkdtemp(prefix='tray-authoring-',dir=scratch))
    folder(SHARED)
    mesh(SHARED/'SpiritTrayBeveledTimberMesh.asset',bevel_faces())
    mesh(SHARED/'SpiritTrayRoughSlatMesh.asset',rough_faces())
    mesh(SHARED/'SpiritTrayRivetMesh.asset',rivet_faces())
    author('Lantern','SpiritHouse',7.70,1.45,.88,'fox',54,(.03,.285,.94,.237))
    author('Watchtower','Watchtower',7.40,1.38,.82,'watchtower',52,(.03,.275,.94,.225))
    author('MothCrossroads','MothCrossroads',6.20,1.15,.70,'moth',59,(.03,.273,.94,.28),8.65)
    print('Authored three encounter trays in '+str(OUTPUT))


if __name__=='__main__':
    main()
