"""Author the tree nymph's sobbing breath and flinch: one deformation mesh with two shapes over the concept frame.

Python 3.9 or later; no third-party packages. The nymph is one layer, BoundTreeNymph.png in the 941 x 1672 Bound Tree
concept frame, so a single smooth displacement field moves her whole figure and no seam can open. The mesh holds two
shapes on the same grid: Breath (shoulders, upper back and bowed head rise, the back rounds outward) and Flinch
(shoulders hunch up, the upper body draws in toward the tree at image right and down). Both fade to zero down her body,
so her feet, dress hem and ankle bindings stay planted, and both are confined to her alpha region plus a small margin.
The sob and flinch clips that drive the two weights are separate Animation assets; this writes only the mesh.
"""
from pathlib import Path
import argparse
import importlib.util
import json
import math
import tempfile

SPEC = importlib.util.spec_from_file_location('spirit_authoring', Path(__file__).with_name('author-spirit-presentations.py'))
A = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(A)
LAYERS_SPEC = importlib.util.spec_from_file_location('bound_tree_layers', Path(__file__).with_name('cut-bound-tree-layers.py'))
L = importlib.util.module_from_spec(LAYERS_SPEC)
LAYERS_SPEC.loader.exec_module(L)
FOLDER = A.PRESENTATION / 'BoundTree'
ART = A.ASSETS / 'Art/BoundTree'
NYMPH = ART / 'BoundTreeNymph.png'
SOURCE = (941, 1672)
# About 9.8 x 10 source pixels per cell, fine enough that every cell touching her keeps full weight at all four corners.
COLUMNS, ROWS = 96, 168

# Native concept pixels, top-left origin. Her silhouette is every pixel with alpha at or above SILHOUETTE.
SILHOUETTE = .02
# Displacement keeps full weight within MARGIN[0] px outside her silhouette and reaches zero at MARGIN[1] px. The inner
# distance exceeds a cell diagonal (about 14 px), so bilinear interpolation never reduces the motion of her own pixels.
MARGIN = (16, 34)
# The motion fades to zero between these rows: full above the shoulder line, none from the top of the ankle bindings
# down, so the feet, hem and bindings never move.
PLANTED_ROWS = (870, 735)
# Soft ellipses (center x, center y, radius x, radius y): the bowed head with the hands at her face, the shoulder and
# upper back hump, and the curve of the back that rounds outward.
HEAD = (288, 695, 62, 62)
SHOULDERS = (212, 735, 62, 44)
BACK = (160, 790, 52, 72)
ELLIPSE_FALLOFF = (.6, 1.5)

# Breath at full weight: the upper body lifts LIFT px, the shoulders SHOULDER_SHARE more, and the back rounds ROUND px
# outward (image left).
LIFT = 4.0
SHOULDER_SHARE = .25
ROUND = 1.5

# Flinch at full weight: the upper body draws SHIFT px toward the tree (image right) and DROP px down, the head tucks a
# further TUCK px down and the shoulders hunch HUNCH px up.
SHIFT = 4.0
DROP = 1.0
TUCK = 1.5
HUNCH = 3.5


def soft_ellipse(x, y, ellipse):
    cx, cy, rx, ry = ellipse
    return 1 - A.ease(math.hypot((x - cx) / rx, (y - cy) / ry), *ELLIPSE_FALLOFF)


class Silhouette:
    """Distance (px) from pixel centres to her silhouette, inside a box around her; 0 on her, large far away."""

    def __init__(self):
        width, height, channels, pixels = L.read_png(NYMPH)
        if (width, height) != SOURCE or channels != 4:
            raise SystemExit('BoundTreeNymph.png must be a 941 x 1672 RGBA texture.')
        threshold = SILHOUETTE * 255
        inside = [pixels[i * 4 + 3] >= threshold for i in range(width * height)]
        rows = [y for y in range(height) if any(inside[y * width:(y + 1) * width])]
        columns = [x for x in range(width) if any(inside[y * width + x] for y in rows)]
        reach = MARGIN[1] + 8
        self.x0, self.y0 = max(0, columns[0] - reach), max(0, rows[0] - reach)
        self.x1, self.y1 = min(width, columns[-1] + 1 + reach), min(height, rows[-1] + 1 + reach)
        self.w, self.h = self.x1 - self.x0, self.y1 - self.y0
        self.mask = [inside[(y + self.y0) * width + x + self.x0] for y in range(self.h) for x in range(self.w)]
        # Distance from each pixel centre to the nearest silhouette pixel centre.
        self.distance = L.edt([not m for m in self.mask], self.w, self.h)
        self.bounds = (columns[0], rows[0], columns[-1] + 1, rows[-1] + 1)

    def at(self, x, y):
        """Bilinear distance at a continuous source point; far outside the box counts as far away."""
        fx, fy = x - .5 - self.x0, y - .5 - self.y0
        if fx < 0 or fy < 0 or fx > self.w - 1 or fy > self.h - 1:
            return 1e9
        ix, iy = min(int(fx), self.w - 2), min(int(fy), self.h - 2)
        ax, ay = fx - ix, fy - iy
        d = self.distance
        i = iy * self.w + ix
        top = d[i] * (1 - ax) + d[i + 1] * ax
        bottom = d[i + self.w] * (1 - ax) + d[i + self.w + 1] * ax
        return top * (1 - ay) + bottom * ay

    def pixels(self, step=1):
        """Source pixel centres of her silhouette."""
        for y in range(0, self.h, step):
            for x in range(0, self.w, step):
                if self.mask[y * self.w + x]:
                    yield x + self.x0 + .5, y + self.y0 + .5


def body_weight(x, y):
    return 1 - A.ease(SHAPE.at(x, y), *MARGIN)


def breath_pixels(x, y):
    """Displacement (dx right, dy up) in source pixels at full inhale."""
    rows = A.ease(y, *PLANTED_ROWS)
    if rows == 0:
        return 0.0, 0.0
    weight = rows * body_weight(x, y)
    lift = LIFT * (1 + SHOULDER_SHARE * soft_ellipse(x, y, SHOULDERS))
    return -ROUND * soft_ellipse(x, y, BACK) * weight, lift * weight


def flinch_pixels(x, y):
    """Displacement (dx right, dy up) in source pixels at the full flinch."""
    rows = A.ease(y, *PLANTED_ROWS)
    if rows == 0:
        return 0.0, 0.0
    weight = rows * body_weight(x, y)
    up = -DROP - TUCK * soft_ellipse(x, y, HEAD) + HUNCH * soft_ellipse(x, y, SHOULDERS)
    return SHIFT * weight, up * weight


def normalized(field):
    """A bottom-left UV field for the mesh: the source displacement divided by the frame size."""
    def offset(u, v):
        dx, dy = field(u * SOURCE[0], (1 - v) * SOURCE[1])
        return dx / SOURCE[0], dy / SOURCE[1]
    return offset


def vertex_offsets(field):
    """Per-vertex source-pixel displacements in the mesh's row-major bottom-left order."""
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


def check(name, offsets):
    """Magnitudes, planting, containment, strain and interpolation error of one rendered field over her pixels."""
    largest = max(math.hypot(*o) for o in offsets)
    farthest = 0.0
    for index, o in enumerate(offsets):
        if o != (0.0, 0.0):
            row, column = divmod(index, COLUMNS + 1)
            farthest = max(farthest, SHAPE.at(column / COLUMNS * SOURCE[0], (1 - row / ROWS) * SOURCE[1]))
    planted = moving = 0.0
    strain = 0.0
    folded = 0
    for x, y in SHAPE.pixels(2):
        dx, dy = interpolated(offsets, x, y)
        magnitude = math.hypot(dx, dy)
        if y >= PLANTED_ROWS[0]:
            planted = max(planted, magnitude)
        moving = max(moving, magnitude)
        # Jacobian of the mapping p -> p + d (image y down, so the up displacement enters negated).
        rx, ry = interpolated(offsets, x + 2, y), interpolated(offsets, x, y + 2)
        jxx, jyx = 1 + (rx[0] - dx) / 2, -(rx[1] - dy) / 2
        jxy, jyy = (ry[0] - dx) / 2, 1 - (ry[1] - dy) / 2
        strain = max(strain, abs(jxx - 1), abs(jyy - 1), abs(jxy), abs(jyx))
        folded += jxx * jyy - jxy * jyx <= 0
    return {'shape': name,
            'largest vertex displacement px': round(largest, 3),
            'largest displacement on her pixels px': round(moving, 3),
            'largest displacement on her pixels at or below row %d px' % PLANTED_ROWS[0]: round(planted, 4),
            'farthest moving vertex outside her silhouette px': round(farthest, 2),
            'largest strain on her pixels (Jacobian deviation)': round(strain, 4),
            'folded samples': folded}


def sample(field, points):
    return {label: [round(v, 3) for v in field(x, y)] for label, (x, y) in points.items()}


def author():
    global SHAPE
    SHAPE = Silhouette()
    mesh = A.mesh(FOLDER / 'NymphBreathMesh.asset', COLUMNS, ROWS,
                  [('Breath', normalized(breath_pixels)), ('Flinch', normalized(flinch_pixels))])
    breath, flinch = vertex_offsets(breath_pixels), vertex_offsets(flinch_pixels)
    both = [(a[0] + b[0], a[1] + b[1]) for a, b in zip(breath, flinch)]
    points = {'crown (270, 645)': (270, 645), 'face (300, 715)': (300, 715), 'shoulder (205, 730)': (205, 730),
              'upper back (160, 780)': (160, 780), 'knees (300, 805)': (300, 805), 'shins (320, 850)': (320, 850),
              'ankle bindings (320, 895)': (320, 895), 'hem (170, 960)': (170, 960), 'toes (360, 985)': (360, 985)}
    report = {
        'mesh guid': mesh,
        'grid': [COLUMNS, ROWS],
        'silhouette bounds (x0, y0, x1, y1)': list(SHAPE.bounds),
        'margin px': list(MARGIN),
        'planted rows': list(PLANTED_ROWS),
        'breath at full weight (dx right, dy up) px': sample(breath_pixels, points),
        'flinch at full weight (dx right, dy up) px': sample(flinch_pixels, points),
        'checks': [check('Breath', breath), check('Flinch', flinch), check('Breath + Flinch', both)],
    }
    text = json.dumps(report, indent=2)
    if A.OUTPUT is not None:
        (A.OUTPUT / 'report.json').write_text(text + '\n', encoding='ascii')
    print(text)
    print('NymphBreathMesh GUID: ' + mesh)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument('--write-assets', action='store_true', help='Reauthor the production nymph deformation mesh in place.')
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
            A.OUTPUT = Path(tempfile.mkdtemp(prefix='bound-tree-presentation-', dir=scratch))
    author()
    print('Authored the nymph deformation mesh in ' + str(A.OUTPUT or A.ROOT))
