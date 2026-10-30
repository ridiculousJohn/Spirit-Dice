"""Author the Bound Tree's possessing spirit as the encounter host: one lit face layer and its expression mesh.

Python 3.9 or later; no third-party packages. The host has one layer, the spirit's face: the concept plate
BoundTreeBackground.png drawn through BoundTreeFaceMask.png in the 941 x 1672 concept frame, lit per pixel by the
scene's two lights (light 0 the painted glow, light 1 the flare) exactly as the background is. Over the faceless
background plates (author-bound-tree-relight.py plates) the face at full opacity reproduces the resting scene and at
opacity 0 leaves the faceless trunk.

Idle has no timeline: the face is still bark and its only idle life is light 0's glow pulse, which belongs to the
scene lighting. Expressions use one deformation mesh with two shapes over the face only: Brow (the brows drop, the
inner ends most) and MouthWide (the lips of the mouth crack part sideways; a negative weight narrows the crack).
Every vertex within EDGE[0] px of the mask's fully opaque region's boundary has zero displacement, so the face
layer's outline never moves and no seam can open against the plate. This writes the host asset and the mesh; the
artwork, material and portrait prefab are authored separately.
"""
from pathlib import Path
import argparse
import importlib.util
import json
import math
import re
import tempfile

SPEC = importlib.util.spec_from_file_location('spirit_authoring', Path(__file__).with_name('author-spirit-presentations.py'))
A = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(A)
LAYERS_SPEC = importlib.util.spec_from_file_location('bound_tree_layers', Path(__file__).with_name('cut-bound-tree-layers.py'))
L = importlib.util.module_from_spec(LAYERS_SPEC)
LAYERS_SPEC.loader.exec_module(L)
FOLDER = A.PRESENTATION / 'BoundTree'
ART = A.ASSETS / 'Art/BoundTree'
MASK = ART / 'BoundTreeFaceMask.png'
PREFAB = FOLDER / 'BoundTreeHost.prefab'
PORTRAIT_SCRIPT = '08503f4c437f422b807ec20ae4c077c6'
SOURCE = (941, 1672)
# About 7.4 source pixels per cell (cell diagonal 10.5 px); only vertices over the face move.
COLUMNS, ROWS = 128, 224

# Native concept pixels, top-left origin. Distance is measured inward from the boundary of the mask's alpha-1 region
# (its 2 px soft edge lies outside it). Vertex weight is 0 within EDGE[0] px and full beyond EDGE[1] px; EDGE[0] less
# the cell diagonal is the band of face pixels next to the edge that never move.
EDGE = (18.0, 36.0)

# Brow: soft ellipses (centre x, centre y, radius x, radius y) over the bark plates above each eye, full inside
# ELLIPSE_FALLOFF[0] of the radius and none beyond [1]; the drop fades out between the rows in `lower` (from the
# brow's lower edge to just below the eye's centre), so the eye's upper half narrows beneath the brow. The drop is
# BROW_DROP[0] px at the inner end (toward the nose) and BROW_DROP[1] px at the outer end.
BROWS = (dict(ellipse=(445, 214, 60, 28), lower=(226, 252), inner_x=490, outer_x=395),
         dict(ellipse=(570, 242, 46, 24), lower=(252, 282), inner_x=530, outer_x=612))
ELLIPSE_FALLOFF = (.6, 1.5)
BROW_DROP = (4.5, 3.0)

# MouthWide: each lip of the crack moves WIDEN px outward at full weight. The crack is the glow source polygon of
# author-bound-tree-relight.py (CRACK). The dark opening between the lips stretches; beyond each lip the bark moves
# with it for LIP_HOLD px and the motion fades to zero over LIP_FALLOFF px. Rows: none above MOUTH_TOP[0], full from
# MOUTH_TOP[1] to MOUTH_BOTTOM[0], none below MOUTH_BOTTOM[1], so the crack opens most under the nose and its narrow
# lower end, near the mask's tip, stays still.
CRACK = [(455, 335), (478, 322), (506, 318), (528, 330), (532, 380), (525, 440), (518, 500), (512, 560), (506, 610),
         (500, 640), (492, 610), (488, 560), (478, 500), (466, 440), (452, 380)]
WIDEN = 4.0
LIP_HOLD = 4.0
LIP_FALLOFF = 30.0
MOUTH_TOP = (318, 350)
MOUTH_BOTTOM = (520, 600)

# Timelines (seconds). Directed states (Arrival, Victory, Defeat, Farewell) are sampled by normalized progress, so
# their durations set only relative timing; Reaction plays in real time.
ARRIVAL = 2.5        # face fades in over ARRIVAL_FADE; the crack opens from MouthWide ARRIVAL_CLOSED to rest
ARRIVAL_FADE = (0.0, 1.8)
ARRIVAL_CLOSED = -1.0
ARRIVAL_OPEN = (0.3, 2.3)
REACTION = 1.4       # brows drop in REACTION_BROW[0..1], hold, return by [3]; the crack widens briefly
REACTION_BROW = (0.0, 0.1, 0.5, 1.3)
REACTION_MOUTH = (0.0, 0.18, 0.35, 0.95)
REACTION_MOUTH_WEIGHT = 0.7
VICTORY = 2.5        # the face sinks back into the bark (opacity 1 to 0)
DEFEAT = 3.0         # a slow, satisfied widening of the crack, then back to rest
DEFEAT_MOUTH = (0.1, 1.5, 1.9, 3.0)
DEFEAT_MOUTH_WEIGHT = 1.0
FAREWELL = 2.5       # the face withdraws into the bark, as for Victory
SETTLE = 0.6         # an interrupted expression relaxes to rest over this time when an outcome starts

ALPHA, DEFORMATION = 5, 6
BROW, MOUTH = 0, 1


def soft_ellipse(x, y, ellipse):
    cx, cy, rx, ry = ellipse
    return 1 - A.ease(math.hypot((x - cx) / rx, (y - cy) / ry), *ELLIPSE_FALLOFF)


class FaceEdge:
    """Distance (px) from pixel centres to the outside of the mask's alpha-1 region; 0 outside it."""

    def __init__(self):
        width, height, channels, pixels = L.read_png(MASK)
        if (width, height) != SOURCE or channels != 4:
            raise SystemExit('BoundTreeFaceMask.png must be a 941 x 1672 RGBA texture.')
        alpha = [pixels[i * 4 + 3] for i in range(width * height)]
        full = [a >= 255 for a in alpha]
        rows = [y for y in range(height) if any(full[y * width:(y + 1) * width])]
        columns = [x for x in range(width) if any(full[y * width + x] for y in rows)]
        self.x0, self.y0 = max(0, columns[0] - 4), max(0, rows[0] - 4)
        self.x1, self.y1 = min(width, columns[-1] + 5), min(height, rows[-1] + 5)
        self.w, self.h = self.x1 - self.x0, self.y1 - self.y0
        self.full = [full[(y + self.y0) * width + x + self.x0] for y in range(self.h) for x in range(self.w)]
        self.alpha = alpha
        self.width = width
        self.distance = L.edt(self.full, self.w, self.h)

    def at(self, x, y):
        """Bilinear distance at a continuous source point; 0 outside the box."""
        fx, fy = x - .5 - self.x0, y - .5 - self.y0
        if fx < 0 or fy < 0 or fx > self.w - 1 or fy > self.h - 1:
            return 0.0
        ix, iy = min(int(fx), self.w - 2), min(int(fy), self.h - 2)
        ax, ay = fx - ix, fy - iy
        d = self.distance
        i = iy * self.w + ix
        top = d[i] * (1 - ax) + d[i + 1] * ax
        bottom = d[i + self.w] * (1 - ax) + d[i + self.w + 1] * ax
        return top * (1 - ay) + bottom * ay

    def pixel(self, px, py):
        """(alpha 0..255, inward distance) of an integer pixel."""
        a = self.alpha[py * self.width + px]
        if not (self.x0 <= px < self.x1 and self.y0 <= py < self.y1):
            return a, 0.0
        return a, self.distance[(py - self.y0) * self.w + px - self.x0]


def edge_weight(x, y):
    return A.ease(EDGE_FIELD.at(x, y), *EDGE)


def crack_span(y):
    """(left, right) x of the crack polygon at row y, or None."""
    xs = []
    n = len(CRACK)
    for i in range(n):
        (x0, y0), (x1, y1) = CRACK[i], CRACK[(i + 1) % n]
        if (y0 <= y < y1) or (y1 <= y < y0):
            xs.append(x0 + (y - y0) * (x1 - x0) / (y1 - y0))
    return (min(xs), max(xs)) if len(xs) >= 2 else None


def brow_pixels(x, y):
    """Displacement (dx right, dy up) in source pixels at the full brow drop."""
    e = edge_weight(x, y)
    if e == 0:
        return 0.0, 0.0
    drop = 0.0
    for brow in BROWS:
        w = soft_ellipse(x, y, brow['ellipse']) * (1 - A.ease(y, *brow['lower']))
        if w <= 0:
            continue
        t = min(1.0, max(0.0, (x - brow['outer_x']) / (brow['inner_x'] - brow['outer_x'])))
        drop = max(drop, w * (BROW_DROP[1] + (BROW_DROP[0] - BROW_DROP[1]) * t))
    return 0.0, -drop * e


def mouth_pixels(x, y):
    """Displacement (dx right, dy up) in source pixels with the crack fully widened."""
    rows = A.ease(y, *MOUTH_TOP) * (1 - A.ease(y, *MOUTH_BOTTOM))
    if rows == 0:
        return 0.0, 0.0
    e = edge_weight(x, y)
    if e == 0:
        return 0.0, 0.0
    span = crack_span(y)
    if span is None:
        return 0.0, 0.0
    left, right = span
    centre, half = (left + right) / 2, max((right - left) / 2, 1.0)
    u = (x - centre) / half
    if abs(u) <= 1:
        g = u * (1.5 - .5 * u * u)
    else:
        g = math.copysign(1 - A.ease(abs(x - centre) - half, LIP_HOLD, LIP_HOLD + LIP_FALLOFF), u)
    return WIDEN * g * rows * e, 0.0


def normalized(field):
    """A bottom-left UV field for the mesh: the source displacement divided by the frame size."""
    def offset(u, v):
        dx, dy = field(u * SOURCE[0], (1 - v) * SOURCE[1])
        return dx / SOURCE[0], dy / SOURCE[1]
    return offset


def vertex_offsets(field):
    return [field(column / COLUMNS * SOURCE[0], (1 - row / ROWS) * SOURCE[1])
            for row in range(ROWS + 1) for column in range(COLUMNS + 1)]


def interpolated(offsets, x, y):
    """The displacement the rendered grid gives a source point (bilinear between its cell's vertices)."""
    gx, gy = x / SOURCE[0] * COLUMNS, (1 - y / SOURCE[1]) * ROWS
    column, row = min(int(gx), COLUMNS - 1), min(int(gy), ROWS - 1)
    ax, ay = gx - column, gy - row
    i = row * (COLUMNS + 1) + column
    a, b, c, d = offsets[i], offsets[i + 1], offsets[i + COLUMNS + 1], offsets[i + COLUMNS + 2]
    return tuple((p * (1 - ax) + q * ax) * (1 - ay) + (r * (1 - ax) + s * ax) * ay
                 for p, q, r, s in zip(a, b, c, d))


def check(name, offsets, scale=1.0):
    """Magnitudes, edge stillness, strain and folds of one rendered field (times scale) over the frame's pixels."""
    offsets = [(o[0] * scale, o[1] * scale) for o in offsets]
    moving = [i for i, o in enumerate(offsets) if o != (0.0, 0.0)]
    rows = [i // (COLUMNS + 1) for i in moving]
    columns = [i % (COLUMNS + 1) for i in moving]
    # Pixels that can move: those inside the bounding box of the moving vertices' cells.
    x0 = max(0, int((min(columns) - 1) / COLUMNS * SOURCE[0]) - 1)
    x1 = min(SOURCE[0], int((max(columns) + 1) / COLUMNS * SOURCE[0]) + 2)
    y0 = max(0, int((1 - (max(rows) + 1) / ROWS) * SOURCE[1]) - 1)
    y1 = min(SOURCE[1], int((1 - (min(rows) - 1) / ROWS) * SOURCE[1]) + 2)
    largest = edge_largest = 0.0
    still_band = EDGE[0] - math.hypot(SOURCE[0] / COLUMNS, SOURCE[1] / ROWS)
    strain = 0.0
    folded = moved_pixels = 0
    nearest_moving = 1e9
    for py in range(y0, y1):
        for px in range(x0, x1):
            x, y = px + .5, py + .5
            dx, dy = interpolated(offsets, x, y)
            magnitude = math.hypot(dx, dy)
            alpha, inward = EDGE_FIELD.pixel(px, py)
            if magnitude > 0:
                moved_pixels += 1
                nearest_moving = min(nearest_moving, inward)
            if alpha < 255 or inward <= still_band:
                edge_largest = max(edge_largest, magnitude)
            largest = max(largest, magnitude)
            if magnitude == 0:
                continue
            rx, ry = interpolated(offsets, x + 1, y), interpolated(offsets, x, y + 1)
            jxx, jyx = 1 + (rx[0] - dx), -(rx[1] - dy)
            jxy, jyy = (ry[0] - dx), 1 - (ry[1] - dy)
            strain = max(strain, abs(jxx - 1), abs(jyy - 1), abs(jxy), abs(jyx))
            folded += jxx * jyy - jxy * jyx <= 0
    return {'shape': name,
            'largest vertex displacement px': round(max(math.hypot(*o) for o in offsets), 3),
            'largest pixel displacement px': round(largest, 3),
            'pixels that move': moved_pixels,
            'nearest moving pixel to the alpha-1 boundary px': round(nearest_moving, 2),
            'largest displacement at or outside the mask edge band (alpha < 1 or within %.1f px) px' % still_band:
                edge_largest,
            'largest strain (Jacobian deviation)': round(strain, 4),
            'folded pixels': folded}


def sample(field, points):
    return {label: [round(v, 3) for v in field(x, y)] for label, (x, y) in points.items()}


def portrait_reference():
    """The prefab's SpiritHostPortrait component, or an empty reference before the prefab exists."""
    if not PREFAB.exists():
        print('BoundTreeHost.prefab does not exist yet; PortraitPrefab is left empty.')
        return A.ref(None)
    text = PREFAB.read_text(encoding='utf-8')
    for block in re.split(r'^--- ', text, flags=re.M):
        match = re.match(r'!u!114 &(-?\d+)', block)
        if match and 'guid: ' + PORTRAIT_SCRIPT in block:
            return A.ref(A.guid(PREFAB), int(match.group(1)), 3)
    raise SystemExit('BoundTreeHost.prefab has no SpiritHostPortrait component.')


def texture_guid(path):
    meta = Path(str(path) + '.meta')
    if not meta.exists():
        raise SystemExit('Missing imported artwork: ' + str(meta.relative_to(A.ROOT)))
    return A.guid(path)


def lighting():
    """Both lights by reference through the glow response, exactly as the background plate draws them."""
    relight = ART / 'Relight'
    response = A.ref(texture_guid(relight / 'BoundTreeGlowResponse.png'), 2800000, 3)
    return dict(
        BaseTexture=A.ref(texture_guid(relight / 'BoundTreeAmbientUnlit.png'), 2800000, 3),
        Lights=[dict(LightIndex=0, Mode=0, Reference=A.ref(texture_guid(ART / 'BoundTreeBackground.png'), 2800000, 3),
                     ResponseMap=response, Strength=1),
                dict(LightIndex=1, Mode=0, Reference=A.ref(texture_guid(relight / 'BoundTreeGlowLit.png'), 2800000, 3),
                     ResponseMap=response, Strength=1)],
        Depth=A.ref(None),
        DepthRange=A.v2(0, -1),
        FrameOrigin=A.Flow(x=0, y=0, z=0),
        FrameAxisX=A.Flow(x=1, y=0, z=0),
        FrameAxisY=A.Flow(x=0, y=1, z=0),
        MapRect=dict(serializedVersion=2, x=0, y=0, width=1, height=1),
        Wrap=1)


def track(sequence, prop, fn, channel=0, add=False, entry=None):
    item = dict(Layer=0, Property=prop, Channel=channel, Blend=int(add),
                Curve=A.curve(fn, sequence['Duration'], .0005 if prop == DEFORMATION else .00015))
    if entry is not None:
        item['EntryWeight'] = A.curve(entry, sequence['Duration'])
    sequence['Tracks'].append(item)


def turn(t, times):
    return A.ease(t, times[0], times[1]) * (1 - A.ease(t, times[2], times[3]))


def sequences():
    settle = lambda t: 1 - A.ease(t, 0, SETTLE)
    waiting = A.sequence('Faceless bark', 1, 1, idle=True)
    track(waiting, ALPHA, lambda t: 0)

    arrival = A.sequence('Face splits open', 2, ARRIVAL, idle=True)
    track(arrival, ALPHA, lambda t: A.ease(t, *ARRIVAL_FADE))
    track(arrival, DEFORMATION, lambda t: 0, BROW)
    track(arrival, DEFORMATION, lambda t: ARRIVAL_CLOSED * (1 - A.ease(t, *ARRIVAL_OPEN)), MOUTH)

    # Additive over the resting face; a reaction restarted mid-expression blends from its current pose.
    reaction = A.sequence('Brows drop', 3, REACTION, idle=True, gesture=True)
    restart = lambda t: 1 - A.ease(t, 0, .1)
    track(reaction, DEFORMATION, lambda t: turn(t, REACTION_BROW), BROW, add=True, entry=restart)
    track(reaction, DEFORMATION, lambda t: REACTION_MOUTH_WEIGHT * turn(t, REACTION_MOUTH), MOUTH, add=True, entry=restart)

    def sink(name, state, duration):
        seq = A.sequence(name, state, duration, idle=True)
        track(seq, ALPHA, lambda t: 1 - A.ease(t, 0, duration))
        track(seq, DEFORMATION, lambda t: 0, BROW, entry=settle)
        track(seq, DEFORMATION, lambda t: 0, MOUTH, entry=settle)
        return seq

    defeat = A.sequence('Satisfied grin', 5, DEFEAT, idle=True)
    track(defeat, DEFORMATION, lambda t: 0, BROW, entry=settle)
    track(defeat, DEFORMATION, lambda t: DEFEAT_MOUTH_WEIGHT * turn(t, DEFEAT_MOUTH), MOUTH, entry=settle)
    return [waiting, arrival, reaction, sink('Face sinks into the bark', 4, VICTORY), defeat,
            sink('Face withdraws', 6, FAREWELL)]


def author():
    global EDGE_FIELD
    EDGE_FIELD = FaceEdge()
    mesh = A.mesh(FOLDER / 'BoundTreeFaceMesh.asset', COLUMNS, ROWS,
                  [('Brow', normalized(brow_pixels)), ('MouthWide', normalized(mouth_pixels))])
    layer = A.layer('Face', texture_guid(ART / 'BoundTreeBackground.png'), mesh, (SOURCE[0] / 2, SOURCE[1] / 2), SOURCE,
                    weights=(0, 0))
    layer['ReceiveSceneLighting'] = True
    layer['Smoke'] = dict(Enabled=False, MotionMask=A.ref(None), FlowNoise=A.ref(None),
                          Distortion=.035, Speed=.7, Opacity=.6, Flow=A.v2(0, .018))
    layer['Lighting'] = lighting()
    host = A.write_asset(FOLDER / 'BoundTreeHost.asset', A.HOST_SCRIPT, dict(
        PortraitPrefab=portrait_reference(),
        SigilPrefab=A.ref(A.guid(A.PRESENTATION / 'UI/BoundTreeHostSigil.prefab'), 9079639759384952736, 3),
        SmokeShader=A.ref(None),
        LitLayerShader=A.ref(A.guid(A.ASSETS / 'Presentation/Shared/SceneLitImage.shader'), 4800000, 3),
        ReferenceSize=A.v2(*SOURCE), Scale=1, Offset=A.v2(0, 0), Layers=[layer], Sequences=sequences()))

    brow, mouth = vertex_offsets(brow_pixels), vertex_offsets(mouth_pixels)
    both = [(a[0] + b[0], a[1] + b[1]) for a, b in zip(brow, mouth)]
    points = {'left brow inner (480, 228)': (480, 228), 'left brow outer (405, 210)': (405, 210),
              'left eye (441, 244)': (441, 244), 'right brow inner (535, 248)': (535, 248),
              'right brow outer (600, 240)': (600, 240), 'right eye (568, 274)': (568, 274),
              'crack left lip (455, 380)': (455, 380), 'crack right lip (531, 380)': (531, 380),
              'crack left lip (478, 500)': (478, 500), 'crack right lip (518, 500)': (518, 500),
              'crack tip (500, 630)': (500, 630)}
    report = {
        'host guid': host, 'mesh guid': mesh, 'grid': [COLUMNS, ROWS], 'edge px': list(EDGE),
        'brow at full weight (dx right, dy up) px': sample(brow_pixels, points),
        'mouth wide at full weight (dx right, dy up) px': sample(mouth_pixels, points),
        'checks': [check('Brow', brow), check('MouthWide', mouth), check('MouthWide at %g (arrival start)' % ARRIVAL_CLOSED,
                                                                          mouth, ARRIVAL_CLOSED),
                   check('Brow + MouthWide', both)],
    }
    text = json.dumps(report, indent=2)
    if A.OUTPUT is not None:
        (A.OUTPUT / 'report.json').write_text(text + '\n', encoding='ascii')
    print(text)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument('--write-assets', action='store_true', help='Reauthor the production host and face mesh in place.')
    mode.add_argument('--output', type=Path, help='New preview directory beneath Docs/work.')
    args = parser.parse_args()
    if not args.write_assets:
        scratch = (A.ROOT / 'Docs/work').resolve()
        scratch.mkdir(parents=True, exist_ok=True)
        if args.output:
            A.OUTPUT = args.output.resolve()
            if not A.OUTPUT.is_relative_to(scratch) or A.OUTPUT == scratch:
                parser.error('--output must be a new directory beneath Docs/work.')
            A.OUTPUT.mkdir(parents=True, exist_ok=False)
        else:
            A.OUTPUT = Path(tempfile.mkdtemp(prefix='bound-tree-host-', dir=scratch))
    author()
    print('Authored the Bound Tree host and face mesh in ' + str(A.OUTPUT or A.ROOT))
