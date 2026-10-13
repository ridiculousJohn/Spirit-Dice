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


def track(sequence, layer, prop, fn, channel=0, add=False):
    sequence['Tracks'].append(dict(Layer=layer, Property=prop, Channel=channel, Blend=int(add),
                                   Curve=A.curve(fn, sequence['Duration'], .003 if prop == 6 else .02)))


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
                              (18 + i * 2, (18 + i * 2) * 2 / 3), pivot=foot,
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

    def flutter(t, i):
        angle = t * (1.8 + i * .6) + i * 2.1
        return (lantern[0] + math.cos(angle) * (22 + i * 7) + math.sin(angle * 3) * 4,
                lantern[1] + math.sin(angle) * (15 + i * 5) + math.sin(angle * 2) * 5)

    waiting = A.sequence('Lantern flutter', 1, 2 * math.pi / .6, loop=True)
    for i in range(3):
        track(waiting, i, 0, lambda t, i=i: flutter(t, i)[0])
        track(waiting, i, 1, lambda t, i=i: flutter(t, i)[1])
        track(waiting, i, 4, lambda t, i=i: math.sin(t * (1.8 + i * .6) + i * 2.1) * 20)
        track(waiting, i, 5, lambda t: 1)
        track(waiting, i, 6, lambda t, i=i: math.sin(t * (24 + i * 3)) * .85, channel=1)
        track(waiting, i, 6, lambda t, i=i: math.sin(t * (24 + i * 3)) * .35, channel=0)
    for i in (3, 4, 5):
        track(waiting, i, 5, lambda t: 0)

    arrival = A.sequence('Arrival', 2, 7)

    def path(t):
        circle = A.ease(t, 0, 2.3)
        move = A.ease(t, 2.1, 5.8)
        angle = circle * 2 * math.pi
        return (lantern[0] + 22 * math.cos(angle) * (1 - move) + (rail[0] - lantern[0]) * move,
                lantern[1] + 15 * math.sin(angle) * (1 - move) + (rail[1] - lantern[1]) * move + 34 * math.sin(move * math.pi)
                - 3 * A.turn(t, 5.8, 6.05, 6.35, 6.8))

    def growing_width(t):
        return 18 + (width - 18) * A.ease(t, 2.3, 5.8)

    for i in (0, 3, 4, 5):
        track(arrival, i, 0, lambda t: path(t)[0])
        track(arrival, i, 1, lambda t: path(t)[1])
        track(arrival, i, 2, growing_width)
        track(arrival, i, 3, lambda t: growing_width(t) * 2 / 3)
        track(arrival, i, 4, lambda t: -12 * math.sin(A.ease(t, 1.8, 5.8) * math.pi))
        track(arrival, i, 6, lambda t: math.sin(t * (25 - 18 * A.ease(t, 2.3, 5.8))) * .9 * (1 - A.ease(t, 5.3, 6.5)), channel=1)
        track(arrival, i, 6, lambda t: math.sin(t * (25 - 18 * A.ease(t, 2.3, 5.8))) * .4 * (1 - A.ease(t, 5.3, 6.5)), channel=0)
    track(arrival, 0, 5, lambda t: 1 - A.ease(t, 2.2, 3.05))
    track(arrival, 3, 5, lambda t: .62 * A.ease(t, 2.2, 3.05) * (1 - A.ease(t, 5.0, 6.35)))
    track(arrival, 4, 5, lambda t: A.ease(t, 4.85, 6.3))
    track(arrival, 5, 5, lambda t: A.ease(t, 2.7, 4.0))
    for i in (1, 2):
        track(arrival, i, 0, lambda t, i=i: flutter(t, i)[0])
        track(arrival, i, 1, lambda t, i=i: flutter(t, i)[1])
        track(arrival, i, 4, lambda t, i=i: math.sin(t * (1.8 + i * .6) + i * 2.1) * 20)
        track(arrival, i, 5, lambda t: 1 - A.ease(t, 5.2, 6.2))
        track(arrival, i, 6, lambda t, i=i: math.sin(t * (24 + i * 3)) * .85, channel=1)

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
