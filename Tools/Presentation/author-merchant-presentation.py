"""Author the haunted merchant's living-still host: one breathing field shared by every layer.

Python 3.9 or later; no third-party packages. Every merchant layer keeps the native 941 x 1672
source frame and its fitted registration, so a single normalized displacement field deforms the
torso, head, both arms and the blink overlay identically and overlapping layers stay registered.
Artwork, masks and the portrait prefab are authored separately; this writes only the host and
its shared deformation mesh.
"""
from pathlib import Path
import argparse
import importlib.util
import math
import tempfile

SPEC = importlib.util.spec_from_file_location('spirit_authoring', Path(__file__).with_name('author-spirit-presentations.py'))
A = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(A)
FOLDER = A.PRESENTATION / 'Ambient'
ART = A.ASSETS / 'Art/HauntedMerchant'
SOURCE = (941, 1672)
# A coarse grid suffices for the smooth field and keeps per-frame vertex rebuilding below the earlier rig.
COLUMNS, ROWS = 48, 84

# Native source pixels, top-left origin. Full inhale lifts the shoulders and chest by LIFT.
LIFT = 6.0
# The head, hat and the hand under the cheek rise together by this share of the shoulder lift.
HEAD_SHARE = .7
# Lateral chest expansion at full inhale, measured at the outer shirt.
SPREAD = 2.0
# The lift fades to zero between these rows, leaving the forearms, elbows and hands on the table fixed.
TABLE_ROWS = (950, 760)
# Soft ellipses (center x, center y, radius x, radius y) that move with the head.
HEAD_GROUP = ((338, 615, 95, 110), (318, 540, 175, 112), (262, 680, 55, 55))
CHEST = (360, 820, 260, 170)

IDLE_BREATHS = ((4.4, 1.0), (4.9, .86), (4.5, .94))

# Per-pixel lighting. The source frame registers to the background plate at plate px = (0.9136 x + 21, 0.94 y + 28.8),
# top-left origin. The candle's piece light maps plate UV (bottom-left) to the receiver rig as x = -3.4 + 10 (u - 0.163656),
# y = -1.5 + 8 (v - 0.336124) (MerchantUnquietCartTable.asset), and frame positions are relative to its host lighting
# position (0, 1, 0).
PLATE = (941, 1672)
REGISTRATION = (.9136, 21, .94, 28.8)
CANDLE_RIG = ((.163656, .336124), (-3.4, -1.5), (10, 8))
HOST_LIGHTING_POSITION = (0, 1, 0)
# Rig Z at depth 0 (torso back) and depth 1 (hands on the table); the candle sits at Z -1 in front of the table.
DEPTH_RANGE = (3.5, 1.5)
# Scales the candle's receiver light (intensity 8 x receiver intensity 0.1) to the painted candle warmth on the hands and forearms.
CANDLE_STRENGTH = 50
IDLE_BLINKS = (1.55, 5.45, 11.25)


def soft_ellipse(x, y, ellipse, inner, outer):
    cx, cy, rx, ry = ellipse
    return 1 - A.ease(math.hypot((x - cx) / rx, (y - cy) / ry), inner, outer)


def breathing_field(u, v):
    """Normalized bottom-left UV displacement at full inhale for a source-frame point."""
    x, y = u * SOURCE[0], (1 - v) * SOURCE[1]
    table = A.ease(y, *TABLE_ROWS)
    head = max(soft_ellipse(x, y, ellipse, .8, 1.4) for ellipse in HEAD_GROUP)
    lift = LIFT * table * (1 - (1 - HEAD_SHARE) * head)
    spread = SPREAD * (x - 340) / 200 * table * soft_ellipse(x, y, CHEST, .4, 1.3)
    return spread / SOURCE[0], lift / SOURCE[1]


def breath(t, period, amplitude):
    """One breath from rest: a measured inhale, a brief turn, a longer passive exhale, then a pause."""
    if t <= 0 or t >= period:
        return 0
    return amplitude * A.ease(t, 0, .40 * period) * (1 - A.ease(t, .44 * period, .88 * period))


def idle_breathing(t):
    start = 0
    for period, amplitude in IDLE_BREATHS:
        if t < start + period:
            return breath(t - start, period, amplitude)
        start += period
    return 0


def blink(t, times):
    # About 120 ms closed: a quick fade in, a short hold, a slightly slower fade out.
    return max((A.ease(t, at, at + .04) * (1 - A.ease(t, at + .085, at + .14)) for at in times), default=0)


def track(sequence, layer, prop, fn, channel=0, add=False, entry=None):
    item = dict(Layer=layer, Property=prop, Channel=channel, Blend=int(add),
                Curve=A.curve(fn, sequence['Duration'], .0005 if prop == 6 else .00015))
    if entry is not None:
        item['EntryWeight'] = A.curve(entry, sequence['Duration'])
    sequence['Tracks'].append(item)


def texture_guid(name):
    meta = Path(str(ART / name) + '.meta')
    if not meta.exists():
        raise SystemExit('Missing imported artwork: ' + str(meta.relative_to(A.ROOT)))
    return A.guid(ART / name)


def frame_mapping():
    """Receiver rig position of the source frame's bottom-left corner and its full-width and full-height axes."""
    (reference_u, reference_v), (rig_x, rig_y), (scale_x, scale_y) = CANDLE_RIG
    sx, ox, sy, oy = REGISTRATION
    origin_u = ox / PLATE[0]
    origin_v = 1 - (sy * SOURCE[1] + oy) / PLATE[1]
    origin = (rig_x + scale_x * (origin_u - reference_u) - HOST_LIGHTING_POSITION[0],
              rig_y + scale_y * (origin_v - reference_v) - HOST_LIGHTING_POSITION[1], -HOST_LIGHTING_POSITION[2])
    axis_x = scale_x * sx * SOURCE[0] / PLATE[0]
    axis_y = scale_y * sy * SOURCE[1] / PLATE[1]
    return origin, axis_x, axis_y


def lighting(unlit, lantern_lit):
    """Lantern 0 by reference through the merchant-frame response; candle 1 computed from depth."""
    relight = 'Relight/'
    origin, axis_x, axis_y = frame_mapping()
    return dict(
        BaseTexture=A.ref(texture_guid(relight + unlit), 2800000, 3),
        Lights=[dict(LightIndex=0, Mode=0, Reference=A.ref(texture_guid(relight + lantern_lit), 2800000, 3),
                     ResponseMap=A.ref(texture_guid(relight + 'MerchantLanternResponseHost.png'), 2800000, 3), Strength=1),
                dict(LightIndex=1, Mode=1, Reference=A.ref(None), ResponseMap=A.ref(None), Strength=CANDLE_STRENGTH)],
        Depth=A.ref(texture_guid(relight + 'MerchantDepth.png'), 2800000, 3),
        DepthRange=A.v2(*DEPTH_RANGE),
        FrameOrigin=A.Flow(x=origin[0], y=origin[1], z=origin[2]),
        FrameAxisX=A.Flow(x=axis_x, y=0, z=0),
        FrameAxisY=A.Flow(x=0, y=axis_y, z=0),
        MapRect=dict(serializedVersion=2, x=0, y=0, width=1, height=1),
        Wrap=1)


def author():
    mesh = A.mesh(FOLDER / 'MerchantBreathMesh.asset', COLUMNS, ROWS, [('Breath', breathing_field)])

    def layer(name, texture, position, color=(1, 1, 1, 1), lit=None):
        item = A.layer(name, texture_guid(texture), mesh, position, SOURCE,
                       pivot=(position[0] / SOURCE[0], position[1] / SOURCE[1]), color=color, weights=(0,))
        item['ReceiveSceneLighting'] = True
        item['Smoke'] = dict(Enabled=False, MotionMask=A.ref(None), FlowNoise=A.ref(None),
                             Distortion=.035, Speed=.7, Opacity=.6, Flow=A.v2(0, .018))
        item['Lighting'] = lighting(*lit)
        return item

    # Layer order matches the prefab's authored layers; the blink overlay draws directly above the head.
    layers = [layer('Torso', 'MerchantTorsoAnimation.png', (385, 697),
                    lit=('MerchantTorsoAnimationUnlit.png', 'MerchantTorsoAnimationLanternLit.png')),
              layer('Head', 'MerchantHeadRepaired.png', (370, 934),
                    lit=('MerchantHeadRepairedUnlit.png', 'MerchantHeadRepairedLanternLit.png')),
              layer('Resting Arm', 'MerchantHost.png', (500, 992), lit=('MerchantHostUnlit.png', 'MerchantHostLanternLit.png')),
              layer('Supporting Arm', 'MerchantHost.png', (220, 972), lit=('MerchantHostUnlit.png', 'MerchantHostLanternLit.png')),
              layer('Blink', 'MerchantBlinkEyes.png', (370, 934), color=(1, 1, 1, 0),
                    lit=('MerchantBlinkEyesUnlit.png', 'MerchantBlinkEyesLanternLit.png'))]
    blink_layer = 4
    bodies = range(len(layers))

    idle = A.sequence('Living still', 0, sum(period for period, _ in IDLE_BREATHS), loop=True)
    for i in bodies:
        track(idle, i, 6, idle_breathing)
    track(idle, blink_layer, 5, lambda t: blink(t, IDLE_BLINKS))

    def cue(name, state, fn, entry_time, gesture=False):
        seq = A.sequence(name, state, 3, idle=True, gesture=gesture)
        entry = lambda t: 1 - A.ease(t, 0, entry_time)
        for i in bodies:
            track(seq, i, 6, fn, add=True, entry=entry)
        if state != 3:
            # Directed states restart the idle clock; a blink in progress fades out instead of popping.
            track(seq, blink_layer, 5, lambda t: 0, add=True, entry=lambda t: 1 - A.ease(t, 0, .1))
        return seq

    def swell_and_settle(t, swell, rise, settle, fall, recover):
        """Additive breath weight: swell to `swell`, settle to `settle`, then return to rest."""
        up = A.ease(t, *rise)
        down = A.ease(t, *fall)
        back = A.ease(t, *recover)
        return swell * up * (1 - down) + settle * down * (1 - back)

    sequences = [
        idle,
        cue('Arrival breath', 2, lambda t: swell_and_settle(t, .45, (.15, 1.05), -.12, (1.35, 2.2), (2.2, 2.95)), .8),
        # A quick, sharper intake, then the whole figure settles slightly below rest and recovers.
        cue('Reaction breath', 3, lambda t: swell_and_settle(t, .6, (0, .28), -.25, (.42, 1.25), (1.5, 2.7)), .35, gesture=True),
        cue('Victory breath', 4, lambda t: swell_and_settle(t, .9, (.1, 1.15), -.35, (1.35, 2.3), (2.3, 3)), .8),
        cue('Defeat sigh', 5, lambda t: swell_and_settle(t, .5, (.1, .9), -.5, (1, 2.2), (2.3, 3)), .8),
        cue('Farewell settle', 6, lambda t: swell_and_settle(t, .15, (.05, .6), -.3, (.6, 1.6), (1.9, 2.9)), .6),
    ]
    host = A.write_asset(FOLDER / 'MerchantHost.asset', A.HOST_SCRIPT, dict(
        PortraitPrefab=A.ref(A.guid(FOLDER / 'MerchantHost.prefab'), 1002, 3),
        SigilPrefab=A.ref(A.guid(A.PRESENTATION / 'UI/MerchantHostSigil.prefab'), 6469703378660953074, 3),
        SilhouetteShader=A.ref(None), SmokeShader=A.ref(None),
        LitLayerShader=A.ref(A.guid(A.ASSETS / 'Presentation/Shared/SceneLitImage.shader'), 4800000, 3),
        ReferenceSize=A.v2(*SOURCE), Scale=1, Offset=A.v2(0, 0), Layers=layers, Sequences=sequences,
        SigilLines=[], SigilWidth=.035))
    print('Host GUID: ' + host)
    print('MerchantBreathMesh GUID: ' + mesh)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument('--write-assets', action='store_true', help='Reauthor the production merchant host and breathing mesh in place.')
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
            A.OUTPUT = Path(tempfile.mkdtemp(prefix='merchant-authoring-', dir=scratch))
    author()
    print('Authored merchant presentation and breathing mesh in ' + str(A.OUTPUT or A.ROOT))
