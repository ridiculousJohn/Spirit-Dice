"""Author the Lantern Keeper and Watchtower presentation assets from their approved motion.

Run with Python 3. No third-party packages are required. Runtime playback uses only
the resulting layer, curve, and mesh assets; these content formulas are authoring input.
"""
from pathlib import Path
import argparse
import json
import math
import tempfile
import uuid

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / 'Dice-Unity/Assets/Dice'
PRESENTATION = ASSETS / 'Content/Presentation'
HOST_SCRIPT = '8fac0627b592494ebcb02707d77b5c91'
OUTPUT = None


def output_path(path):
    return OUTPUT / path.relative_to(ROOT) if OUTPUT is not None else path


class Flow(dict):
    pass


def scalar(value):
    if isinstance(value, Flow):
        return '{' + ', '.join(str(k) + ': ' + scalar(v) for k, v in value.items()) + '}'
    if isinstance(value, str):
        return json.dumps(value)
    if isinstance(value, bool):
        return '1' if value else '0'
    if isinstance(value, float):
        return format(value if abs(value) > 1e-10 else 0, '.8g')
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
    meta = Path(str(path) + '.meta')
    if meta.exists():
        return next(line.split(':', 1)[1].strip() for line in meta.read_text().splitlines() if line.startswith('guid:'))
    value = uuid.uuid5(uuid.NAMESPACE_URL, str(path.relative_to(ROOT)).replace('\\', '/')).hex
    destination = output_path(meta)
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text('fileFormatVersion: 2\nguid: ' + value + '\n', encoding='utf-8')
    return value


def ref(value, file_id=11400000, kind=2):
    return Flow(fileID=file_id, guid=value, type=kind) if value else Flow(fileID=0)


def v2(x, y):
    return Flow(x=x, y=y)


def write_asset(path, script, data):
    destination = output_path(path)
    destination.parent.mkdir(parents=True, exist_ok=True)
    obj = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=ref(None),
               m_PrefabInstance=ref(None), m_PrefabAsset=ref(None), m_GameObject=ref(None),
               m_Enabled=1, m_EditorHideFlags=0, m_Script=ref(script, 11500000, 3),
               m_Name=path.stem, m_EditorClassIdentifier='')
    if script == HOST_SCRIPT:
        obj.update(SmokeShader=ref(guid(ASSETS / 'Presentation/Shared/SpiritSmoke.shader'), 4800000, 3))
    obj.update(data)
    destination.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n' +
                    '\n'.join(yaml({'MonoBehaviour': obj})) + '\n', encoding='utf-8')
    value = guid(path)
    if OUTPUT is not None and Path(str(path) + '.meta').exists():
        Path(str(destination) + '.meta').write_bytes(Path(str(path) + '.meta').read_bytes())
    return value


def ease(t, a, b):
    p = max(0, min(1, (t - a) / (b - a)))
    return p * p * (3 - 2 * p)


def turn(t, a, b, c, d):
    return ease(t, a, b) * (1 - ease(t, c, d))


def curve(fn, duration, tolerance=.00015):
    count = math.ceil(duration * 90)
    samples = [(duration * i / count, fn(duration * i / count)) for i in range(count + 1)]
    step = duration / count / 20
    slopes = [(fn(min(duration, t + step)) - fn(max(0, t - step))) /
              (min(duration, t + step) - max(0, t - step)) for t, _ in samples]
    keep = {0, count}
    pending = [(0, count)]
    while pending:
        a, b = pending.pop()
        if b <= a + 1:
            continue
        ta, va = samples[a]
        tb, vb = samples[b]
        def interpolated(t):
            p = (t - ta) / (tb - ta)
            return ((2 * p ** 3 - 3 * p ** 2 + 1) * va + (p ** 3 - 2 * p ** 2 + p) * (tb - ta) * slopes[a]
                    + (-2 * p ** 3 + 3 * p ** 2) * vb + (p ** 3 - p ** 2) * (tb - ta) * slopes[b])
        error, index = max((abs(v - interpolated(t)), i)
                          for i, (t, v) in enumerate(samples[a + 1:b], a + 1))
        if error > tolerance:
            keep.add(index)
            pending.extend([(a, index), (index, b)])
    keys = []
    for i in sorted(keep):
        t, v = samples[i]
        keys.append(dict(serializedVersion=3, time=t, value=v, inSlope=slopes[i],
                         outSlope=slopes[i], tangentMode=0, weightedMode=0, inWeight=0, outWeight=0))
    return dict(serializedVersion=2, m_Curve=keys, m_PreInfinity=2, m_PostInfinity=2, m_RotationOrder=4)


def sequence(name, state, duration, loop=False, idle=False, restart=False, gesture=False, times=()):
    return dict(Id=name, State=state, Duration=duration, Loop=loop, IncludeIdle=idle,
                RestartOnReaction=restart, GestureOnStart=gesture, GestureTimes=list(times), Tracks=[])


def track(seq, layer, prop, fn, channel=0, add=False, entry=None):
    item = dict(Layer=layer, Property=prop, Channel=channel, Blend=int(add),
                Curve=curve(fn, seq['Duration'], .008 if prop in (0, 1, 2, 3) else .00015))
    if entry is not None:
        item['EntryWeight'] = curve(entry, seq['Duration'])
    seq['Tracks'].append(item)


def layer(name, texture, mesh, position, size, pivot=(.5, .5), color=(1, 1, 1, 1), silhouette=False, weights=()):
    return dict(Id=name, Texture=ref(texture, 2800000, 3), Mesh=ref(mesh), Position=v2(*position),
                Size=v2(*size), Pivot=v2(*pivot), Rotation=0,
                Tint=Flow(zip(('r', 'g', 'b', 'a'), color)), Silhouette=silhouette, Weights=list(weights))


def mesh(path, columns, rows, shapes, alpha=None):
    uvs = [(x / columns, y / rows) for y in range(rows + 1) for x in range(columns + 1)]
    data = dict(Columns=columns, Rows=rows, Alpha=[] if alpha is None else [alpha(x, y) for x, y in uvs],
                Shapes=[dict(Id=name, Offsets=[v2(*fn(x, y)) for x, y in uvs]) for name, fn in shapes])
    mesh_script = guid(ASSETS / 'Presentation/SpiritPortraitMeshAsset.cs')
    return write_asset(path, mesh_script, data)


def author_lantern():
    folder = PRESENTATION / 'SpiritHouse'
    def chest(x, y):
        return ease(x, .33, .41) * (1 - ease(x, .55, .63)) * ease(y, .32, .46) * (1 - ease(y, .65, .74))
    def tail_mask(x, y):
        return ease(x, .72, .82) * ease(y, .3, .45)
    def tail(x, y):
        return tail_mask(x, y) * ease(x, .72, .97) * ease(y, .3, .68)
    def ears(x, y):
        left = ease(x, .12, .22) * (1 - ease(x, .42, .5)) * ease(y, .62, .9)
        right = ease(x, .57, .67) * (1 - ease(x, .88, .96)) * ease(y, .62, .9)
        return ((right - left) * .01, (left + right) * .0035)
    body_shapes = [('Breathing', lambda x, y: (chest(x, y) * (x - .46) * 2.5 / 460, chest(x, y) * .9 / (460 * 2 / 3))),
                   ('Accent', lambda x, y: (tail(x, y) * 8 / 460, tail(x, y) * 3 / (460 * 2 / 3)))]
    body_mesh = mesh(folder / 'LanternKeeperBodyMesh.asset', 24, 18, body_shapes)
    accent_mesh = mesh(folder / 'LanternKeeperAccentMesh.asset', 24, 18, body_shapes, tail_mask)
    face_mesh = mesh(folder / 'LanternKeeperFaceMesh.asset', 24, 18, [('Accent', ears)])
    textures = ['08217cb586464a53a1dbb7a0f2e858a6', '8ae3013c2027a8f468aa392a983c3918', '283d515401fbea140b4b8fa1802d4b07']
    cyan, gold = (.46, .88, 1, 0), (1, .76, .37, 0)
    layers = [layer('Gold Tail', textures[0], accent_mesh, (300, 724), (460, 460 * 2 / 3), color=gold, silhouette=True, weights=(0, 0)),
              layer('Cyan Tail', textures[0], accent_mesh, (300, 724), (460, 460 * 2 / 3), color=cyan, silhouette=True, weights=(0, 0)),
              layer('Body', textures[0], body_mesh, (300, 724), (460, 460 * 2 / 3), weights=(0, 0))]
    for name, tex, pos, tint, spectral in [('Gold Front', 1, (234, 791), gold, True), ('Gold Turn', 2, (234, 791), gold, True),
                                          ('Cyan Front', 1, (232, 792), cyan, True), ('Cyan Turn', 2, (232, 792), cyan, True),
                                          ('Physical Front', 1, (232, 792), (1, 1, 1, 1), False),
                                          ('Physical Turn', 2, (232, 792), (1, 1, 1, 0), False)]:
        layers.append(layer(name, textures[tex], face_mesh, pos, (190, 190), color=tint, silhouette=spectral, weights=(0,)))
    breath = sequence('Breathing', 0, 2 * math.pi / 1.15, True)
    track(breath, 2, 6, lambda t: math.sin(t * 1.15) * .65)
    for i in range(3, 9):
        track(breath, i, 1, lambda t: math.sin(t * 1.15) * .65, add=True)
        track(breath, i, 4, lambda t: math.sin(t * 1.15) * .18, add=True)
    def gesture(t, length):
        return math.sin(t / length * 2 * math.pi) * math.sin(t / length * math.pi) if 0 < t < length else 0
    accent = sequence('Brief accent', 0, 18.3, True, restart=True)
    for i in (7, 8):
        track(accent, i, 6, lambda t: sum(gesture(t - start, .52) * .55 for start in (4.1, 9.4, 15.5)))
    sweep = sequence('Long accent', 0, 35.7, True, restart=True, times=(9.2, 20, 31.9))
    def idle_tail(t, spectral):
        return sum(gesture(t - start - (0 if spectral else .18), 1.7) * .55 for start in (9.2, 20, 31.9))
    for i in (0, 1, 2):
        track(sweep, i, 6, lambda t, i=i: idle_tail(t, i != 2), channel=1)
    for i, intensity in ((0, .075), (1, .16)):
        track(sweep, i, 5, lambda t, intensity=intensity: abs(idle_tail(t, True) - idle_tail(t, False)) * intensity)
    def reaction(state, duration):
        seq = sequence('Arrival' if state == 2 else 'Reaction', state, duration, idle=True, gesture=True)
        def pose(t, spectral):
            return turn(t, 0 if spectral else .18, .3 if spectral else .48, 1.15 if spectral else 1.33, 1.55 if spectral else 1.73)
        def scored_tail(t):
            return .9 * ease(t, 0, .28) - 1.22 * ease(t, .36, .74) + .32 * ease(t, .86, 1.3)
        def alpha(t):
            return max(turn(t, 0, .05, .3, .6), turn(t, 1.15, 1.2, 1.63, 1.8))
        for i in (0, 1, 2):
            delay = .18 if i == 2 else 0
            track(seq, i, 6, lambda t, d=delay: scored_tail(t - d), channel=1,
                  entry=lambda t, d=delay: 1 - ease(t - d, 0, .28))
        for i, intensity in ((0, .11), (1, .23)):
            track(seq, i, 5, lambda t, intensity=intensity: abs(scored_tail(t) - scored_tail(t - .18)) * ease(t, 0, .05) * intensity)
        for i in range(3, 9):
            spectral = i < 7
            def x(t, s=spectral):
                return 12 * pose(t, s) + (6 * (pose(t, True) - pose(t, False)) if s else 0)
            def y(t, s=spectral):
                return pose(t, s) + (abs(pose(t, True) - pose(t, False)) * 5 if s else 0)
            def angle(t, s=spectral):
                return -6 * pose(t, s) - (3 * (pose(t, True) - pose(t, False)) if s else 0)
            entry = lambda t, s=spectral: 1 - ease(t, 0 if s else .18, .3 if s else .48)
            track(seq, i, 0, x, add=True, entry=entry)
            track(seq, i, 1, y, add=True, entry=entry)
            track(seq, i, 4, angle, add=True, entry=entry)
            is_front = i in (3, 5, 7)
            tint_alpha = .12 if i < 5 else .24 if i < 7 else 1
            track(seq, i, 5, lambda t, s=spectral, front=is_front, a=tint_alpha:
                  ((1 - pose(t, s)) if front else pose(t, s)) * (alpha(t) * a if s else 1), entry=entry)
            if not spectral:
                track(seq, i, 6, lambda t: gesture(t - .18, .65) * .7, entry=lambda t: 1 - ease(t, 0, .22))
        return seq
    def outcome(name, state, duration, tilt, dip, shift):
        seq = sequence(name, state, duration, idle=True)
        track(seq, 2, 6, lambda t: 0, add=True, entry=lambda t: 1 - ease(t, 0, duration * .3))
        def pose(t, spectral):
            delay = 0 if spectral else .15
            return turn(t - delay, 0, duration * .2, duration * .46, duration - .16)
        for i in range(3, 9):
            spectral, front = i < 7, i in (3, 5, 7)
            entry = lambda t, s=spectral: 1 - ease(t, 0 if s else .15, duration * .2 + (0 if s else .15))
            track(seq, i, 0, lambda t, s=spectral: shift * pose(t, s), add=True, entry=entry)
            track(seq, i, 1, lambda t, s=spectral: -dip * pose(t, s), add=True, entry=entry)
            track(seq, i, 4, lambda t, s=spectral: tilt * pose(t, s), add=True, entry=entry)
            intensity = .10 if i < 5 else .20
            track(seq, i, 5, lambda t, s=spectral, f=front, a=intensity:
                  abs(pose(t, True) - pose(t, False)) * a if s and f else 1 if not s and f else 0,
                  entry=entry)
            if not spectral:
                track(seq, i, 6, lambda t: 0, entry=entry)
        for i in (0, 1, 2):
            track(seq, i, 6, lambda t: 0, channel=1, entry=lambda t: 1 - ease(t, 0, duration * .3))
            if i != 2:
                track(seq, i, 5, lambda t: 0, entry=lambda t: 1 - ease(t, 0, duration * .3))
        return seq
    write_asset(folder / 'LanternKeeperHost.asset', HOST_SCRIPT,
                dict(ReferenceSize=v2(540, 960), Scale=1.16, Offset=v2(270 - 270 * 1.16, 595.2 - 624 * 1.16),
                     Layers=layers, Sequences=[breath, accent, sweep, reaction(2, 2), reaction(3, 1.8),
                                              outcome('Victory', 4, 1.4, -2.2, 4, .5),
                                              outcome('Defeat', 5, 1.2, 3.5, 1.2, -1),
                                              outcome('Farewell', 6, .5, -1, 1, .2)]))


def author_watchtower():
    folder = PRESENTATION / 'Watchtower'
    pivots = [(.5, .075), (.43, .11), (.51, .055)]
    turns = [(.53, .69), (.43, .35), (.51, .57)]
    bounds = [(.26, .67, .52, .32), (.31, .34, .25, .31), (.40, .56, .24, .32)]
    aspects = [2 / 3, 1.5, 1.5]
    names = ['Rest', 'Arrival', 'Settle']
    textures = ['85a75b59e31c4b169bddb725c4401003', '85a75b59e31c4b169bddb725c4401004', '85a75b59e31c4b169bddb725c4401005']
    layers = []
    for spectral in (True, False):
        for index, name in enumerate(names):
            foot = pivots[index]
            pivot = turns[index]
            bx, by, bw, bh = bounds[index]
            aspect = aspects[index]
            def contact(x, y):
                return ease(y, foot[1] + .025, foot[1] + .17)
            def head(x, y):
                return ease(x, bx - .035, bx + .035) * (1 - ease(x, bx + bw - .035, bx + bw + .035)) * ease(y, by - .04, by + .04) * (1 - ease(y, by + bh, by + bh + .045))
            def chest(x, y):
                return contact(x, y) * (1 - head(x, y)) * (1 - ease(abs(x - .5), .12, .32))
            def edge(x, y):
                return contact(x, y) * (1 - head(x, y)) * ease(abs(x - .5), .14, .38)
            shapes = [('Breathing', lambda x, y: (chest(x, y) * (x - .5) * .012 * contact(x, y), chest(x, y) * .0025 * contact(x, y))),
                      ('Turn Cosine', lambda x, y: ((x - pivot[0]) * head(x, y) * contact(x, y), (y - pivot[1]) * head(x, y) * contact(x, y))),
                      ('Turn Sine', lambda x, y: (-(y - pivot[1]) / aspect * head(x, y) * contact(x, y), (x - pivot[0]) * aspect * head(x, y) * contact(x, y))),
                      ('Edge Motion', lambda x, y: (0, edge(x, y) * .036 * contact(x, y))),
                      ('Offset', lambda x, y: (head(x, y) * .0015 * contact(x, y) if spectral else 0, 0))]
            # A separate static shape leaves the physical artwork untouched.
            shapes.append(('Drift', lambda x, y: (0, edge(x, y) * .007 * contact(x, y) if spectral else 0)))
            def registration(x, y):
                if index == 1:
                    return ((x - .43) * .10 * head(x, y), (y - .47) * .10 * head(x, y))
                if index != 2:
                    return (0, 0)
                body = 1 - ease(abs(x - .51), .15, .28)
                return ((x - .51) * .08 * head(x, y),
                        (.078 * body + (y - .72) * .12 * head(x, y)) * contact(x, y))
            shapes.append(('Landing Registration', registration))
            shapes.append(('Grounded Settle', lambda x, y: (0, -.016 * contact(x, y))))
            def mask(x, y):
                return max(head(x, y), ease(abs(x - .5), .13, .35) * (1 - head(x, y))) * ease(y, foot[1] + .04, foot[1] + .2)
            kind = 'Spirit' if spectral else 'Physical'
            mesh_guid = mesh(folder / ('Watcher' + kind + name + 'Mesh.asset'), 30, 36, shapes, mask if spectral else None)
            height = (.31 if index != 1 else .26) * 960 / (1 - foot[1])
            layers.append(layer(kind + ' ' + name, textures[index], mesh_guid, (.5 * 540, .612 * 960), (height * aspect, height),
                                pivot=foot, color=(.42, .83, 1, .025 if index == 0 else 0) if spectral else (1, 1, 1, 1 if index == 0 else 0),
                                silhouette=spectral, weights=(0, 0, 0, 0, 0, 1 if spectral else 0, 1 if index != 0 else 0, 0)))
    breath = sequence('Breathing', 0, 2 * math.pi / 1.2, True)
    feather = sequence('Edge drift', 0, 2 * math.pi / .82, True)
    glance = sequence('Watchful turn', 0, 15.6, True)
    def idle_tilt(t, spectral):
        direction = 1 if t < 7.8 else -1
        return direction * 5 * turn(t % 7.8 - 3.7 - (0 if spectral else .18), 0, .28, .85, 1.3)
    def add_angle(seq, i, fn, entry=None):
        track(seq, i, 6, lambda t: math.cos(math.radians(fn(t))) - 1, channel=1, entry=entry)
        track(seq, i, 6, lambda t: math.sin(math.radians(fn(t))), channel=2, entry=entry)
        if i < 3:
            track(seq, i, 6, fn, channel=4, entry=entry)
    for i in range(6):
        spectral = i < 3
        track(breath, i, 6, lambda t: math.sin(t * 1.2) * .7)
        track(feather, i, 6, lambda t, s=spectral: math.sin((t + (.18 if s else 0)) * .82) * .07, channel=3)
        add_angle(glance, i, lambda t, s=spectral: idle_tilt(t, s))
    track(feather, 0, 5, lambda t: abs(math.sin((t + .18) * .82) - math.sin(t * .82)) * .07 * .12, add=True)
    track(glance, 0, 5, lambda t: min(1, abs(idle_tilt(t, True) - idle_tilt(t, False)) / 7) * .3, add=True)
    waiting = sequence('Waiting placement', 1, 1, idle=True)
    for i in range(6):
        height = .062 * 960 / (1 - pivots[i % 3][1])
        for prop, val in ((0, .814 * 540), (1, .884 * 960), (2, height * aspects[i % 3]), (3, height)):
            track(waiting, i, prop, lambda t, value=val: value)
    arrival = sequence('Arrival', 2, 4.5, gesture=True)
    def approach(t):
        p = t / 4.5
        travel = ease(p, .24, .76)
        return (.814 + (.5 - .814) * travel - .035 * math.sin(travel * math.pi),
                .884 + (.612 - .884) * travel - .015 * math.sin(travel * math.pi), travel)
    def weights(t):
        p = t / 4.5
        opened, landed, folded = ease(p, .22, .26), ease(p, .72, .76), ease(p, .88, .94)
        second, third = opened * (1 - landed), opened * landed * (1 - folded)
        return (1 - second - third, second, third)
    def arrival_tilt(t, spectral):
        return 11 * (turn(t / 4.5, .01, .10, .27, .40) if spectral else turn(t / 4.5, .065, .16, .325, .455))
    def edge_motion(t, spectral):
        return math.sin(ease(t / 4.5 + (.025 if spectral else 0), .24, .76) * math.pi * 8) * weights(t)[1]
    def size(t, index):
        x, y, travel = approach(t)
        height = (.062 + ((.31 if index == 0 else .26) - .062) * travel) if index < 2 else .31
        height *= 960 / (1 - pivots[index][1])
        if index == 1:
            width = min((x - .02) / pivots[index][0], (.98 - x) / (1 - pivots[index][0])) * 540
            height = min(height, max(1, width) / aspects[index])
            face_y = y * 960 + size(t, 0)[1] * (.80 - pivots[0][1] - .016 * settle(t))
            height = min(height, max(1, .99 * 960 - face_y) / (1 - .47))
        return height * aspects[index], height
    def settle(t):
        return turn(t / 4.5, .755, .79, .79, .96)
    def position(t, index):
        x, y, _ = approach(t)
        if index == 1:
            # Airborne feet follow the crouched pose; its face follows the upright body trajectory.
            y += (size(t, 0)[1] * (.80 - pivots[0][1])
                  - size(t, 1)[1] * (.47 - pivots[1][1]) - size(t, 0)[1] * .016 * settle(t)) / 960
        return x * 540, y * 960
    for i in range(6):
        spectral, index = i < 3, i % 3
        track(arrival, i, 0, lambda t, index=index: position(t, index)[0])
        track(arrival, i, 1, lambda t, index=index: position(t, index)[1])
        track(arrival, i, 2, lambda t, index=index: size(t, index)[0])
        track(arrival, i, 3, lambda t, index=index: size(t, index)[1])
        track(arrival, i, 5, lambda t, index=index, s=spectral: weights(t)[index] *
              (.025 + min(1, abs(arrival_tilt(t, True) - arrival_tilt(t, False)) / 7) * .3 + abs(edge_motion(t, True) - edge_motion(t, False)) * .12 if s else 1))
        add_angle(arrival, i, lambda t, s=spectral: arrival_tilt(t, s))
        track(arrival, i, 6, lambda t, s=spectral: edge_motion(t, s), channel=3)
        track(arrival, i, 6, lambda t, index=index: settle(t) if index != 1 else 0, channel=7)
    reaction = sequence('Reaction', 3, 1.8, idle=True, gesture=True)
    def reaction_tilt(t, spectral):
        return -10 * turn(t - (0 if spectral else .18), 0, .3, 1.1, 1.55)
    for i in range(6):
        spectral = i < 3
        add_angle(reaction, i, lambda t, s=spectral: reaction_tilt(t, s),
                  entry=lambda t, s=spectral: 1 - ease(t - (0 if s else .18), 0, .3))
        track(reaction, i, 6, lambda t, s=spectral: .22 * turn(t - (0 if s else .18), 0, .3, .7, 1.4), channel=3, add=True)
    # The visible spectral layer carries the anticipation alpha; other poses remain transparent.
    track(reaction, 0, 5, lambda t: .025 + min(1, abs(reaction_tilt(t, True) - reaction_tilt(t, False)) / 7) * .3
          + abs(.22 * turn(t, 0, .3, .7, 1.4) - .22 * turn(t - .18, 0, .3, .7, 1.4)) * .12)
    def outcome(name, state, duration, angle, compression, glow):
        seq = sequence(name, state, duration, idle=True)
        def pose(t, spectral):
            return turn(t - (0 if spectral else .18), 0, duration * .22, duration * .46, duration - .2)
        for i in range(6):
            spectral = i < 3
            entry = lambda t, s=spectral: 1 - ease(t - (0 if s else .18), 0, duration * .22)
            add_angle(seq, i, lambda t, s=spectral: angle * pose(t, s), entry=entry)
            track(seq, i, 6, lambda t: 0, add=True, entry=entry)
            track(seq, i, 6, lambda t: 0, channel=3, add=True, entry=entry)
            track(seq, i, 6, lambda t, s=spectral: compression * pose(t, s), channel=7, add=True, entry=entry)
        track(seq, 0, 5, lambda t: abs(pose(t, True) - pose(t, False)) * glow,
              add=True, entry=lambda t: 1 - ease(t, 0, duration * .22))
        return seq
    write_asset(folder / 'WatchtowerHost.asset', HOST_SCRIPT,
                dict(ReferenceSize=v2(540, 960), Scale=1, Offset=v2(0, 0), Layers=layers,
                     Sequences=[breath, feather, glance, waiting, arrival, reaction,
                                outcome('Victory', 4, 1.5, 6, .06, .18),
                                outcome('Defeat', 5, 1.2, -3.5, 0, .10),
                                outcome('Farewell', 6, .5, 1.2, .18, .06)]))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument('--write-assets', action='store_true', help='Reauthor the checked-in Unity assets in place.')
    mode.add_argument('--output', type=Path, help='Unused preview directory beneath Docs/work.')
    args = parser.parse_args()
    if not args.write_assets:
        scratch = (ROOT / 'Docs/work').resolve()
        scratch.mkdir(parents=True, exist_ok=True)
        if args.output:
            OUTPUT = args.output.resolve()
            if not OUTPUT.is_relative_to(scratch) or OUTPUT == scratch:
                parser.error('--output must be a new directory beneath Docs/work.')
            OUTPUT.mkdir(parents=True, exist_ok=False)
        else:
            OUTPUT = Path(tempfile.mkdtemp(prefix='spirit-authoring-', dir=scratch))
    author_lantern()
    author_watchtower()
    print('Authored both spirit presentations and nine deformation meshes in ' + str(OUTPUT or ROOT))
