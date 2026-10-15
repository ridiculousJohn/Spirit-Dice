"""Author the crossroads moth's layered host and arrival curves.

Python 3.9 or later; no third-party packages. Artwork stays source-aligned at
1536x1024. The generated assets use the shared spirit renderer and smoke shader.
"""
from pathlib import Path
import argparse
import importlib.util
import math
import tempfile
import uuid

SPEC = importlib.util.spec_from_file_location('spirit_authoring', Path(__file__).with_name('author-spirit-presentations.py'))
A = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(A)
FOLDER = A.PRESENTATION / 'MothCrossroads'
ART = A.ASSETS / 'Art/MothCrossroads'


def track(sequence, layer, prop, fn, channel=0, add=False, entry=None):
    item = dict(Layer=layer, Property=prop, Channel=channel, Blend=int(add),
                Curve=A.curve(fn, sequence['Duration'], .003 if prop == 6 else .02))
    if entry is not None:
        item['EntryWeight'] = A.curve(entry, sequence['Duration'])
    sequence['Tracks'].append(item)


def texture_guid(name):
    path = ART / name
    meta = Path(str(path) + '.meta')
    if meta.exists():
        return next(line.split(':', 1)[1].strip() for line in meta.read_text().splitlines() if line.startswith('guid:'))
    return uuid.uuid5(uuid.NAMESPACE_URL, str(path.relative_to(A.ROOT)).replace('\\', '/')).hex


def author(foot, width):
    texture, smoke = texture_guid('MothHost.png'), texture_guid('MothWingSmoke.png')
    height = width * 2 / 3
    lantern, rail = (213.3, 753.6), (270, 609.6)

    def wing(x, y):
        return A.ease(abs(x - .5), .1, .34) * A.ease(y, foot[1] + .02, foot[1] + .16)

    # Wings move about the central body; the contact band leaves the feet planted.
    shapes = [('Wing lift', lambda x, y: (0, wing(x, y) * .05)),
              ('Wing fold', lambda x, y: (-(x - .5) * wing(x, y) * .13, 0)),
              ('Breathing', lambda x, y: ((x - .5) * (1 - A.ease(abs(x - .5), .07, .17)) * A.ease(y, foot[1] + .04, foot[1] + .25) * .006,
                                        (1 - A.ease(abs(x - .5), .07, .17)) * A.ease(y, foot[1] + .04, foot[1] + .25) * .002))]
    body_mesh = A.mesh(FOLDER / 'MothBodyMesh.asset', 32, 24, shapes)
    layers = []
    for i in range(3):
        layers.append(A.layer('Lantern moth ' + str(i + 1), texture, body_mesh, lantern,
                              (22 + i * 2, (22 + i * 2) * 2 / 3), pivot=foot,
                              color=(1, .82, .55, 0), weights=(0, 0, 0)))
    layers.append(A.layer('Spectral arrival', texture, body_mesh, rail, (width, height), pivot=foot,
                          color=(.32, .72, 1, 0), silhouette=True, weights=(0, 0, 0)))
    layers.append(A.layer('Body', texture, body_mesh, rail, (width, height), pivot=foot, weights=(0, 0, 0)))
    layers.append(A.layer('Wing smoke', smoke, body_mesh, rail, (width, height), pivot=foot, weights=(0, 0, 0)))
    for layer in layers:
        layer['ReceiveSceneLighting'] = layer['Id'] != 'Wing smoke'
    layers[5]['Smoke'] = dict(Enabled=True, MotionMask=A.ref(None), FlowNoise=A.ref(None),
                             Distortion=.035, Speed=.7, Opacity=.6, Flow=A.v2(0, .018))

    idle = A.sequence('Resting wings', 0, 2 * math.pi / .8, loop=True)
    for i in (3, 4, 5):
        track(idle, i, 6, lambda t: .055 * math.sin(t * .8), channel=0)
        track(idle, i, 6, lambda t: .06 * math.sin(t * .8), channel=1)
        track(idle, i, 6, lambda t: .65 * math.sin(t * .8), channel=2)

    # Repeated points hold briefly; short unequal legs make bounded darts and reversals.
    routes = [
        [(0, 12, 8, -12), (.35, 35, 24, -42), (.62, 35, 24, -18),
         (1.05, -9, 4, 40), (1.32, -9, 4, 8), (1.65, 8, -23, -36),
         (2.2, 17, -27, -8), (2.46, -24, -8, 46), (2.73, -24, -8, 12),
         (3.18, 4, 11, -28), (3.9, 7, 13, -4), (4.16, -36, 27, 42),
         (4.36, -36, 27, 16), (4.92, -13, -4, -38), (5.45, -9, -1, 4),
         (5.8, 32, -11, -45), (6.16, 32, -11, -12), (6.57, 5, 19, 38),
         (7.08, 8, 22, 8), (7.34, -23, 4, 48), (7.61, -23, 4, 4),
         (8.05, 14, -29, -42), (8.52, 18, -27, -8), (8.87, 42, 3, -32),
         (9.12, 42, 3, -8), (9.64, -3, 9, 40), (10.18, 0, 10, 4),
         (10.8, 12, 8, -12)],
        [(0, -18, -14, 18), (.52, 8, 5, -40), (.78, 8, 5, -6),
         (1.02, -31, 22, 45), (1.38, -31, 22, 12), (1.96, -11, -5, -30),
         (2.62, -8, -2, -4), (2.91, 27, 30, -46), (3.18, 27, 30, -8),
         (3.65, 5, 3, 36), (4.05, 8, 4, 2), (4.36, -42, -18, 48),
         (4.7, -42, -18, 12), (5.08, -7, 2, -38), (5.6, -4, 4, -4),
         (5.87, 21, -32, -42), (6.13, 21, -32, -8), (6.62, 29, 7, 32),
         (7.18, 26, 9, 4), (7.47, -12, 31, 44), (7.8, -12, 31, 8),
         (8.3, 3, 5, -34), (8.86, 5, 7, 0), (9.17, -35, -8, 42),
         (9.48, -35, -8, 10), (10.05, -15, -16, -24), (10.8, -18, -14, 18)],
        [(0, 23, -7, -20), (.28, -14, 18, 46), (.58, -14, 18, 10),
         (1.12, 5, 2, -30), (1.73, 7, 4, -4), (1.99, 38, 29, -44),
         (2.29, 38, 29, -10), (2.78, -3, 7, 38), (3.22, 0, 9, 4),
         (3.49, -27, -29, 46), (3.81, -27, -29, 8), (4.25, 5, -3, -38),
         (4.87, 7, -1, -4), (5.14, -38, 17, 48), (5.48, -38, 17, 12),
         (5.97, -8, 2, -36), (6.45, -5, 4, -4), (6.73, 34, -24, -48),
         (7.03, 34, -24, -12), (7.52, 7, 5, 38), (8.1, 10, 7, 4),
         (8.39, -20, 34, 44), (8.72, -20, 34, 8), (9.2, 3, 8, -32),
         (9.73, 5, 10, -2), (10.09, 26, -9, -38), (10.8, 23, -7, -20)]]
    duration = 10.8
    phases = (0, 3.1, 6.7)

    def waypoint(t, points):
        for a, b in zip(points, points[1:]):
            if t <= b[0]:
                blend = A.ease(t, a[0], b[0])
                return tuple(x + (y - x) * blend for x, y in zip(a[1:], b[1:]))
        return points[-1][1:]

    def flutter(t, i):
        x, y, bank = waypoint((t + phases[i]) % duration, routes[i])
        return lantern[0] + x, lantern[1] + y, bank

    def wing_flutter(t, i, channel):
        cycles = (76, 83, 91)[i]
        return math.sin(t * cycles * 2 * math.pi / duration + i * 1.7 + channel * .65) * (.65 if channel == 0 else 1.25)

    waiting = A.sequence('Lantern flutter', 1, duration, loop=True)
    for i in range(3):
        track(waiting, i, 0, lambda t, i=i: flutter(t, i)[0])
        track(waiting, i, 1, lambda t, i=i: flutter(t, i)[1])
        track(waiting, i, 4, lambda t, i=i: flutter(t, i)[2])
        track(waiting, i, 5, lambda t: 1)
        track(waiting, i, 6, lambda t, i=i: wing_flutter(t, i, 1), channel=1)
        track(waiting, i, 6, lambda t, i=i: wing_flutter(t, i, 0), channel=0)
    for i in (3, 4, 5):
        track(waiting, i, 5, lambda t: 0)

    arrival = A.sequence('Arrival', 2, 7)

    departure = [(0, *flutter(0, 0)), (.45, lantern[0] + 49, lantern[1] + 33, -46),
                 (.64, lantern[0] + 49, lantern[1] + 33, -12),
                 (1.06, lantern[0] - 12, lantern[1] + 3, 48),
                 (1.34, lantern[0] - 12, lantern[1] + 3, 10),
                 (1.8, lantern[0] + 33, lantern[1] + 23, -42),
                 (2.15, lantern[0] + 45, lantern[1] + 2, -22),
                 (2.85, 300, 726, -18), (3.55, 303, 698, 12),
                 (4.5, 281, 655, 6), (5.8, *rail, 0), (7, *rail, 0)]

    def path(t):
        x, y, bank = waypoint(t, departure)
        return x, y - 3 * A.turn(t, 5.8, 6.05, 6.35, 6.8), bank

    def growing_width(t):
        return 22 + (width - 22) * A.ease(t, 2.3, 5.8)

    # Entry offsets vanish while the moth is still warm, before aligned blue layers appear.
    def entry(t):
        return 1 - A.ease(t, 0, .8)

    def arrival_flutter(t, channel):
        return wing_flutter(t, 0, channel) * (1 - A.ease(t, 3.5, 6.5))

    for i in (0, 3, 4, 5):
        continuity = entry if i == 0 else None
        track(arrival, i, 0, lambda t: path(t)[0], entry=continuity)
        track(arrival, i, 1, lambda t: path(t)[1], entry=continuity)
        track(arrival, i, 2, growing_width)
        track(arrival, i, 3, lambda t: growing_width(t) * 2 / 3)
        track(arrival, i, 4, lambda t: path(t)[2], entry=continuity)
        track(arrival, i, 6, lambda t: arrival_flutter(t, 1), channel=1, entry=continuity)
        track(arrival, i, 6, lambda t: arrival_flutter(t, 0), channel=0, entry=continuity)
    track(arrival, 0, 5, lambda t: 1 - A.ease(t, 2.2, 3.05))
    track(arrival, 3, 5, lambda t: .62 * A.ease(t, 2.2, 3.05) * (1 - A.ease(t, 5.0, 6.35)))
    track(arrival, 4, 5, lambda t: A.ease(t, 4.85, 6.3))
    track(arrival, 5, 5, lambda t: A.ease(t, 2.7, 4.0))
    for i in (1, 2):
        track(arrival, i, 0, lambda t, i=i: flutter(t, i)[0], entry=entry)
        track(arrival, i, 1, lambda t, i=i: flutter(t, i)[1], entry=entry)
        track(arrival, i, 4, lambda t, i=i: flutter(t, i)[2], entry=entry)
        track(arrival, i, 5, lambda t: 1 - A.ease(t, 5.2, 6.2))
        track(arrival, i, 6, lambda t, i=i: wing_flutter(t, i, 1), channel=1, entry=entry)
        track(arrival, i, 6, lambda t, i=i: wing_flutter(t, i, 0), channel=0, entry=entry)

    reaction = A.sequence('Wing acknowledgment', 3, 1.5, idle=True)
    for i in (3, 4, 5):
        track(reaction, i, 6, lambda t: .14 * A.turn(t, 0, .35, .65, 1.5), channel=0, add=True)
        track(reaction, i, 6, lambda t: .2 * A.turn(t, 0, .35, .65, 1.5), channel=1, add=True)
    host = A.write_asset(FOLDER / 'MothCrossroadsHost.asset', A.HOST_SCRIPT,
                        dict(ReferenceSize=A.v2(540, 960), Scale=1, Offset=A.v2(0, 0), Layers=layers,
                             Sequences=[idle, waiting, arrival, reaction], SigilLines=[], SigilWidth=.035))
    print('Host GUID: ' + host)
    print('MothHost.png GUID: ' + texture)
    print('MothWingSmoke.png GUID: ' + smoke)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument('--write-assets', action='store_true', help='Reauthor the production moth host and mesh in place.')
    mode.add_argument('--output', type=Path, help='New preview directory beneath Docs/work.')
    parser.add_argument('--foot-x', type=float, default=.5, help='Source UV foot contact x, bottom-left origin.')
    parser.add_argument('--foot-y', type=float, default=.28, help='Source UV foot contact y, bottom-left origin.')
    parser.add_argument('--host-width', type=float, default=370, help='Canvas width in the 540x960 reference portrait.')
    args = parser.parse_args()
    if not all(math.isfinite(v) for v in (args.foot_x, args.foot_y, args.host_width)) or not (0 <= args.foot_x <= 1 and 0 <= args.foot_y <= 1 and args.host_width > 0):
        parser.error('Foot UV must be within 0..1 and host width must be positive, with finite values.')
    if not args.write_assets:
        scratch = (A.ROOT / 'Docs/work').resolve()
        scratch.mkdir(parents=True, exist_ok=True)
        if args.output:
            A.OUTPUT = args.output.resolve()
            if not A.OUTPUT.is_relative_to(scratch) or A.OUTPUT == scratch:
                parser.error('--output must be a new directory beneath Docs/work.')
            A.OUTPUT.mkdir(parents=True, exist_ok=False)
        else:
            A.OUTPUT = Path(tempfile.mkdtemp(prefix='moth-authoring-', dir=scratch))
    author((args.foot_x, args.foot_y), args.host_width)
    print('Authored moth presentation and deformation mesh in ' + str(A.OUTPUT or A.ROOT))
