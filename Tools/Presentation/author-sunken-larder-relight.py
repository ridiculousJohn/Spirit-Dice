"""Author the Sunken Larder's relighting sources from registered imagegen lighting donors.

Splits the painted lighting of the Sunken Larder layers (Art/SunkenLarder) into an ambient base with the hearth fire
out, under faint cold moonlight through the gap in the boards, and the fire as scene light 0, whose reference is the
painted plate itself (the Bound Tree's light 0 convention): light 0 at 1 reproduces the concept exactly. Follows
"Relighting sources" in Docs/Reference/Art/HauntedMerchantHostLayers.md and "Relighting" in
Docs/Reference/Art/BoundTreeLayers.md. Stages:

  targets  Write the imagegen edit targets under Docs/work/sunken-larder-relight/targets: the plate's upper box
           (unlit donor) and, once the build in --from holds the unlit plate, the painted old woman over the unlit
           plate (her unlit donor).
  build    Register the preserved donors (Docs/Reference/Art/SunkenLarderRelight*Donor.png), derive linear-light
           ratio maps and write SunkenLarderAmbientUnlit.png, SunkenLarderHagUnlit.png and the response map
           SunkenLarderFireResponse.png, the review images and report.json. A missing hag donor skips her texture.

Python 3.10 or later with numpy, Pillow and scipy (on this machine the venv
Docs/work/merchant-table-clear/.venv/Scripts/python.exe). The registration, ratio-map, cap, drift and review functions
are imported from author-bound-tree-relight.py beside this script (which imports author-merchant-relight.py), so
every scene uses one method. Run from the repository root:

  python Tools/Presentation/author-sunken-larder-relight.py targets
  python Tools/Presentation/author-sunken-larder-relight.py targets --from <build>/textures
  python Tools/Presentation/author-sunken-larder-relight.py build                 # fresh Docs/work/sunken-larder-relight-*
  python Tools/Presentation/author-sunken-larder-relight.py build --write-assets  # also writes Art/SunkenLarder/Relight

The default build output is a fresh ignored Docs/work/sunken-larder-relight-* directory (textures under textures/,
review images under review/, report.json); --output chooses an unused directory under Docs/work. --write-assets also
replaces the three textures in Art/SunkenLarder/Relight, keeps existing .meta files and writes a .meta with a fresh
GUID for a texture that has none (colour: SunkenLarderOriginal.png.meta, RGBA: SunkenLarderHag.png.meta, response:
HauntedMerchant/MerchantLanternResponse.png.meta). Source artwork and donors are only read, so repeated runs give the
same textures.
"""
from pathlib import Path
import argparse
import importlib.util
import json
import tempfile

import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter, distance_transform_edt, binary_erosion

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Dice-Unity/Assets/Dice/Art/SunkenLarder'
OUT = ART / 'Relight'
REF = ROOT / 'Docs/Reference/Art'
WORK = ROOT / 'Docs/work'
TARGETS = WORK / 'sunken-larder-relight' / 'targets'

_spec = importlib.util.spec_from_file_location('author_bound_tree_relight', Path(__file__).with_name('author-bound-tree-relight.py'))
BT = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(BT)
MR = BT.MR
to_linear, to_srgb = MR.to_linear, MR.to_srgb
SIZE = BT.SIZE
smoothstep, label, crop2x, row, over, save_png = BT.smoothstep, BT.label, BT.crop2x, BT.row, BT.over, BT.save_png

# Edit-target boxes in the concept frame (x0, y0, x1, y1), each a Lanczos 1024 x 1536 upscale. The plate box is the
# whole frame width at 2:3, from the top (the gap in the boards, the hearth and every shelf) down to row 1412; the
# near tabletop below it takes the field continued from the box. Her box is SunkenLarderLayers.json's target box.
PLATE_UNLIT_BOX = (0, 0, 941, 1412)
HAG_UNLIT_BOX = (80, 230, 920, 1490)

DONORS = {
    'plate_unlit': REF / 'SunkenLarderRelightUnlitDonor.png',
    'hag_unlit': REF / 'SunkenLarderRelightHagUnlitDonor.png',
}


def art(name, mode='RGBA'):
    return BT.load(ART / name, mode)


def stage_targets(args):
    """The plate's upper box for the unlit donor; with a build in --from, the painted old woman over the unlit plate
    for her unlit donor."""
    src = Path(args.source) if args.source else OUT
    BT.write_target(art('SunkenLarderBackground.png', 'RGB'), PLATE_UNLIT_BOX, TARGETS / 'target-plate-unlit.png', args.replace)
    unlit = src / 'SunkenLarderAmbientUnlit.png'
    if unlit.exists():
        BT.write_target(over(art('SunkenLarderHag.png'), BT.load(unlit, 'RGB')), HAG_UNLIT_BOX,
                        TARGETS / 'target-hag-unlit.png', args.replace)


# ---------------------------------------------------------------------------------------------------------------
# Fire geometry, concept pixels (top-left origin, pixel centres at +0.5).
# Source: the flames and embers under the cauldron and in the hearth's fire opening (polygons), feathered over
# SOURCE_FEATHER px.
FIRE_POT = [(36, 700), (52, 690), (64, 676), (74, 662), (84, 668), (92, 680), (110, 682), (132, 680), (150, 684),
            (166, 690), (166, 704), (140, 710), (100, 712), (60, 712), (36, 708)]
FIRE_OPENING = [(104, 792), (124, 786), (142, 792), (152, 806), (156, 836), (156, 870), (152, 898), (104, 898),
                (102, 860), (102, 820)]
SOURCE_FEATHER = 6.0
# Response: R = min(1, hypot((x - cx) / rx, (y - cy) / ry)), the convention of MerchantLanternResponse.png, centred
# on the fire under the cauldron; the far right of the room (x 941 at the fire's height) is at R 0.97.
FIRE_CENTRE = (85.0, 690.0)
FIRE_RADIUS = (880.0, 1000.0)
# The gap in the boards at the upper right: the moonlit swamp the fire cannot reach (polygon inside the opening).
# It is the drift region of the plate donor, and the unlit field is the identity inside it (faded over GAP_FEATHER px
# outside), so the swamp stays byte-identical.
GAP = [(613, 84), (628, 84), (632, 92), (664, 92), (665, 384), (613, 384)]
GAP_FEATHER = 4.0
GAP_DRIFT_ERODE = 3                # drift is measured this many px inside the gap polygon
FIRE_SOURCE_DILATE = 10.0          # registration excludes the source within this distance (its look changes most)
HAG_DRIFT_RING = (3.0, 40.0)       # unlit plate pixels around her that measure her donor's drift (px)
# The scene lighting asset's light 0 settings, used for the intensity strips (SceneLighting.shader ResponseScale).
DISTANCE_FALLOFF = 0.8
SOURCE_RESPONSE = 0.5
STRIP = (0.0, 0.5, 1.0, 1.3)


def source_mask():
    """The fire source: 1 inside, smoothstep to 0 over SOURCE_FEATHER px outside."""
    xx, yy = BT.pixel_grid()
    hard = BT.polygon_inside(FIRE_POT, xx, yy) | BT.polygon_inside(FIRE_OPENING, xx, yy)
    dist = distance_transform_edt(~hard)
    return 1 - smoothstep(dist / SOURCE_FEATHER), dist


def fire_distance():
    xx, yy = BT.pixel_grid()
    return np.minimum(1.0, np.hypot((xx - FIRE_CENTRE[0]) / FIRE_RADIUS[0], (yy - FIRE_CENTRE[1]) / FIRE_RADIUS[1]))


def gap_mask():
    xx, yy = BT.pixel_grid()
    return BT.polygon_inside(GAP, xx, yy)


def constant_drift(ratio, reference):
    """Per-channel multiplicative drift of a registered donor over a small reference region: exp of the mean log
    ratio there, applied to the whole frame."""
    lr = np.log(np.maximum(ratio[reference], 1e-6))
    return np.exp(lr.mean(0))


def build(out_dir, write_assets, save_registered):
    report = {'ratio_sigma_px': MR.RATIO_SIGMA, 'ratio_clamp': MR.RATIO_CLAMP, 'cap_gain': MR.CAP_GAIN,
              'saturation_gain': MR.SAT_GAIN}
    tex_dir = out_dir / 'textures'
    reg_dir = out_dir / 'registered' if save_registered else None
    plate = art('SunkenLarderBackground.png', 'RGB')
    hag = art('SunkenLarderHag.png')
    src, src_dist = source_mask()
    R = fire_distance()
    gap = gap_mask()
    outputs = {}

    # 1. Unlit plate: the fire's light removed over the whole frame, darkening only, drift-normalized on the swamp
    # seen through the gap. Beyond the box (the near tabletop) the field continues blurred from the box.
    region = BT.box_mask(PLATE_UNLIT_BOX) & (src_dist > FIRE_SOURCE_DILATE)
    reg = BT.register_donor(DONORS['plate_unlit'], plate, region, PLATE_UNLIT_BOX, report, 'plate_unlit', reg_dir)
    interior = BT.box_mask(PLATE_UNLIT_BOX)
    ratio, cap = BT.field_for(reg, plate, interior)
    drift_ref = binary_erosion(gap, iterations=GAP_DRIFT_ERODE) & interior
    drift = constant_drift(ratio, drift_ref)
    report['plate_unlit_drift'] = {'region': 'gap polygon eroded %d px' % GAP_DRIFT_ERODE, 'pixels': int(drift_ref.sum()),
                                   'rgb': [float(v) for v in drift],
                                   'raw_ratio_p10_p50_p90_rgb_in_region': [[float(v) for v in np.percentile(ratio[drift_ref][:, c], (10, 50, 90))] for c in range(3)]}
    ratio = np.minimum(ratio / drift, 1.0)
    rc, _ = BT.beyond_box(np.concatenate([ratio, cap], -1), interior)
    ratio, cap = rc[..., :3], rc[..., 3:]
    gap_out = distance_transform_edt(~gap)
    w_unlit = smoothstep(gap_out / GAP_FEATHER) * (~gap)
    u_field = BT.blend_field(ratio, cap, w_unlit)
    unlit_changed = w_unlit > 0
    unlit_lin = np.minimum(BT.relit(plate, u_field), to_linear(plate))
    unlit = BT.encode(unlit_lin, plate, unlit_changed)
    outputs['SunkenLarderAmbientUnlit.png'] = unlit
    save_png(out_dir / 'fields' / 'plate-unlit-ratio.png', to_srgb(np.clip(u_field[..., :3], 0, 1)), 'RGB')

    # 2. Response map: R the elliptical distance from the fire, G the source mask.
    response = np.zeros((SIZE[1], SIZE[0], 3))
    response[..., 0] = R * 255
    response[..., 1] = src * 255
    outputs['SunkenLarderFireResponse.png'] = response
    report['response'] = {'formula': 'R = min(1, hypot((x - cx) / rx, (y - cy) / ry)), x, y continuous concept pixels '
                          '(pixel centres at +0.5); G = source mask (flames and embers under the cauldron and in the '
                          'hearth opening, feathered); B = 0',
                          'centre_px': list(FIRE_CENTRE), 'radius_px': list(FIRE_RADIUS),
                          'source_feather_px': SOURCE_FEATHER}

    # 3. Unlit old woman: her donor regenerates her (fire-lit, as painted) over the unlit plate with the fire out.
    # Its drift is measured on the plate around her, which the target already shows unlit.
    if DONORS['hag_unlit'].exists():
        shown = over(hag, unlit)
        h_alpha = hag[..., 3]
        h_inside = h_alpha > 0
        region = BT.box_mask(HAG_UNLIT_BOX) & (h_alpha > 250)
        reg = BT.register_donor(DONORS['hag_unlit'], shown, region, HAG_UNLIT_BOX, report, 'hag_unlit', reg_dir)
        interior = BT.box_mask(HAG_UNLIT_BOX)
        ratio, cap = BT.field_for(reg, hag[..., :3], interior & (h_alpha > 250))
        out_d = distance_transform_edt(h_alpha == 0)
        ring = interior & (out_d >= HAG_DRIFT_RING[0]) & (out_d <= HAG_DRIFT_RING[1])
        plate_ratio = (gaussian_filter(to_linear(reg) * ring[..., None], (2, 2, 0)) /
                       np.maximum(gaussian_filter(to_linear(shown) * ring[..., None], (2, 2, 0)), 1e-6))
        hdrift = BT.drift_baseline(plate_ratio, ring)
        report['hag_unlit_drift'] = {'region': 'unlit plate %g to %g px outside her alpha' % HAG_DRIFT_RING,
                                     'pixels': int(ring.sum()),
                                     'mean_on_her_rgb': [float(v) for v in hdrift[h_alpha > 250].mean(0)]}
        ratio = np.minimum(ratio / hdrift, 1.0)
        h_field = BT.blend_field(ratio, cap, np.ones(h_alpha.shape))
        h_lin = np.minimum(BT.relit(hag[..., :3], h_field), to_linear(hag[..., :3]))
        hag_unlit = hag.copy()
        hag_unlit[..., :3] = BT.encode(h_lin, hag[..., :3], h_inside)
        outputs['SunkenLarderHagUnlit.png'] = hag_unlit

    # Write, read back and verify.
    for name, img in outputs.items():
        save_png(tex_dir / name, img, 'RGBA' if img.shape[2] == 4 else 'RGB')
    disk = {name: BT.load(tex_dir / name, 'RGBA' if outputs[name].shape[2] == 4 else 'RGB') for name in outputs}
    report['verification'] = verify(disk, plate, hag, gap, unlit_changed)
    review(out_dir / 'review', disk, plate, hag, src, R, gap)
    if write_assets:
        report['installed'] = install(disk)
    (out_dir / 'report.json').write_text(json.dumps(report, indent=2), encoding='ascii')
    return report


def response_scale(k, resp):
    """SceneLighting.shader ResponseScale for light 0 at intensity k, per pixel from the response map bytes."""
    if k <= 0:
        return np.zeros(resp.shape[:2])
    r, g = np.clip(resp[..., 0] / 255.0, 0, 1), np.clip(resp[..., 1] / 255.0, 0, 1)
    surface = k * np.power(min(k, 1.0), r * DISTANCE_FALLOFF)
    source = k ** SOURCE_RESPONSE
    return surface + (source - surface) * g


def lit(base, reference, scale):
    """Linear light: base + max(reference - base, 0) * scale, as sRGB values (SceneLighting.shader, tint white)."""
    b, r = to_linear(base), to_linear(reference)
    return to_srgb(b + np.maximum(r - b, 0) * scale[..., None])


def composite(disk, plate, hag, k):
    """The plate and her lit layer at light 0 = k, her drawn over the plate in linear light."""
    scale = response_scale(k, disk['SunkenLarderFireResponse.png'])
    p = lit(disk['SunkenLarderAmbientUnlit.png'], plate, scale)
    h = disk.get('SunkenLarderHagUnlit.png', hag)
    hc = lit(h[..., :3], hag[..., :3], scale)
    a = hag[..., 3:4] / 255.0
    return to_srgb(to_linear(hc) * a + to_linear(p) * (1 - a)), p, hc


def verify(disk, plate, hag, gap, unlit_region):
    v = {}
    unlit = disk['SunkenLarderAmbientUnlit.png']
    changed = np.any(unlit != plate, -1)
    lum = np.array([0.2126, 0.7152, 0.0722])
    v['unlit_vs_background'] = {'pixels_changed': int(changed.sum()), 'declared_region_pixels': int(unlit_region.sum()),
                                'changed_outside_declared_region': int((changed & ~unlit_region).sum()),
                                'gap_pixels': int(gap.sum()), 'gap_pixels_changed': int((changed & gap).sum()),
                                'channel_values_above_background': int((unlit > plate).sum()),
                                'mean_linear_luminance_ratio_unlit_over_background':
                                    float((to_linear(unlit) @ lum).mean() / (to_linear(plate) @ lum).mean())}
    concept = art('SunkenLarderOriginal.png', 'RGB')
    painted = np.rint(to_srgb(to_linear(hag[..., :3]) * (hag[..., 3:4] / 255.0) + to_linear(plate) * (1 - hag[..., 3:4] / 255.0)))
    comp, p1, h1 = composite(disk, plate, hag, 1.0)
    v['light0_at_1'] = {
        'plate_max_abs_levels_vs_background': float(np.abs(np.rint(p1) - plate).max()),
        'plate_pixels_differing_from_background': int(np.any(np.rint(p1) != plate, -1).sum()),
        'composite_max_abs_levels_vs_hag_over_background': float(np.abs(np.rint(comp) - painted).max()),
        'composite_vs_concept_mae_levels_full_frame': float(np.abs(np.rint(comp) - concept).mean()),
        'hag_over_background_vs_concept_mae_levels_full_frame': float(np.abs(painted - concept).mean()),
        'formula': 'base + max(reference - base, 0) * ResponseScale(1) in linear light; ResponseScale(1) = 1 for '
                   'every pixel; her layer over the plate in linear light'}
    if 'SunkenLarderHagUnlit.png' in disk:
        hu = disk['SunkenLarderHagUnlit.png']
        a = hag[..., 3] > 0
        v['light0_at_1']['hag_layer_max_abs_levels_vs_hag'] = float(np.abs(np.rint(h1) - hag[..., :3])[a].max())
        v['hag_unlit'] = {'alpha_equal': bool(np.array_equal(hu[..., 3], hag[..., 3])),
                          'channel_values_above_hag_inside_alpha': int((hu[..., :3] > hag[..., :3])[a].sum()),
                          'rgb_pixels_changed_vs_hag': int(np.any(hu[..., :3] != hag[..., :3], -1).sum()),
                          'rgb_changed_where_alpha_0': int((np.any(hu[..., :3] != hag[..., :3], -1) & ~a).sum()),
                          'mean_linear_luminance_ratio_on_her_alpha_gt_250':
                              float((to_linear(hu[..., :3]) @ lum)[hag[..., 3] > 250].mean() /
                                    (to_linear(hag[..., :3]) @ lum)[hag[..., 3] > 250].mean())}
    resp = disk['SunkenLarderFireResponse.png']
    xs = [0, 470, 940]
    v['response'] = {'R_range': [float(resp[..., 0].min()), float(resp[..., 0].max())],
                     'R_at_x_0_470_940_on_row_690': [float(resp[690, x, 0]) / 255 for x in xs],
                     'R_at_x_940_rows_200_400_900_1300': [float(resp[y, 940, 0]) / 255 for y in (200, 400, 900, 1300)],
                     'G_pixels_gt_0': int((resp[..., 1] > 0).sum()), 'G_pixels_eq_255': int((resp[..., 1] >= 255).sum()),
                     'B_max': float(resp[..., 2].max())}
    return v


# ---------------------------------------------------------------------------------------------------------------
# Review images.
FIRE_BOX = (0, 560, 260, 920)
FACE_BOX = (300, 280, 680, 640)
HAG_BOX = (100, 260, 900, 940)


def half(img):
    im = Image.fromarray(np.clip(np.rint(img), 0, 255).astype(np.uint8), 'RGB')
    return np.asarray(im.resize((SIZE[0] // 2, SIZE[1] // 2), Image.LANCZOS)).astype(np.float64)


def review(rdir, disk, plate, hag, src, R, gap):
    rdir.mkdir(parents=True, exist_ok=True)
    unlit = disk['SunkenLarderAmbientUnlit.png']
    hu = disk.get('SunkenLarderHagUnlit.png')
    concept = art('SunkenLarderOriginal.png', 'RGB')
    scenes = {k: composite(disk, plate, hag, k)[0] for k in STRIP}
    her = 'her unlit' if hu is not None else 'her painted (no unlit yet)'
    save_png(rdir / 'plates-full.png', row([label(half(concept), 'concept'), label(half(unlit), 'unlit plate'),
                                            label(half(scenes[0.0]), f'unlit composite ({her})'),
                                            label(half(scenes[1.0]), 'light 0 = 1')]), 'RGB')
    save_png(rdir / 'intensity-strip.png', row([label(half(scenes[k]), f'light 0 = {k:g}') for k in STRIP]), 'RGB')
    for name, box in (('fire', FIRE_BOX), ('face', FACE_BOX)):
        save_png(rdir / f'intensity-strip-{name}-2x.png',
                 row([label(crop2x(scenes[k], box), f'light 0 = {k:g}') for k in STRIP]), 'RGB')
    pairs = [label(crop2x(over(hag, plate), HAG_BOX, 1), 'painted her over plate (light 0 = 1)')]
    if hu is not None:
        pairs.insert(0, label(crop2x(over(hu, unlit), HAG_BOX, 1), 'her unlit over unlit plate'))
    save_png(rdir / 'hag-pairs.png', row(pairs), 'RGB')
    if hu is not None:
        save_png(rdir / 'hag-pairs-face-2x.png', row([label(crop2x(over(hu, unlit), FACE_BOX), 'her unlit over unlit plate'),
                                                     label(crop2x(over(hag, plate), FACE_BOX), 'painted')]), 'RGB')
    contrib = to_srgb(np.clip((to_linear(scenes[1.0]) - to_linear(scenes[0.0])) * 2, 0, 1))
    save_png(rdir / 'fire-contribution-x2.png', label(contrib, 'light 0 = 1 minus light 0 = 0, composite, linear x2'), 'RGB')
    resp = disk['SunkenLarderFireResponse.png']
    rv = np.repeat(resp[..., :1], 3, -1)
    contours = np.abs(((R * 10) % 1) - 0.5) > 0.47
    rv[contours] = (255, 200, 0)
    gv = over(BT.rgba_with(np.stack([np.zeros_like(src), resp[..., 1], np.zeros_like(src)], -1), resp[..., 1]), plate * 0.5)
    gapv = plate * 0.5
    edge = np.zeros(gap.shape, bool)
    edge[1:-1, 1:-1] = gap[1:-1, 1:-1] & ~(gap[:-2, 1:-1] & gap[2:, 1:-1] & gap[1:-1, :-2] & gap[1:-1, 2:])
    gapv[edge] = (0, 255, 255)
    save_png(rdir / 'response.png', row([label(half(rv), 'R (0.1 contours)'), label(half(gv), 'G over plate x0.5'),
                                         label(crop2x(gv, FIRE_BOX), 'G, fire 2x'),
                                         label(crop2x(gapv, (560, 30, 720, 420)), 'gap (drift) region, 2x')]), 'RGB')


# ---------------------------------------------------------------------------------------------------------------
# Installation.
TEMPLATES = {
    'SunkenLarderAmbientUnlit.png': ART / 'SunkenLarderOriginal.png.meta',
    'SunkenLarderHagUnlit.png': ART / 'SunkenLarderHag.png.meta',
    'SunkenLarderFireResponse.png': ROOT / 'Dice-Unity/Assets/Dice/Art/HauntedMerchant/MerchantLanternResponse.png.meta',
}


def install(disk):
    used = BT.used_guids()
    created = []
    folder_meta = Path(str(OUT) + '.meta')
    OUT.mkdir(parents=True, exist_ok=True)
    if not folder_meta.exists():
        lines = (ART.parent / 'SunkenLarder.meta').read_text(encoding='ascii').splitlines()
        lines = ['guid: ' + BT.fresh_guid(used) if ln.startswith('guid:') else ln for ln in lines]
        folder_meta.write_text('\n'.join(lines) + '\n', encoding='ascii', newline='\n')
        created.append(folder_meta.relative_to(ROOT).as_posix())
    for name, img in disk.items():
        mode = 'RGBA' if img.shape[2] == 4 else 'RGB'
        path = save_png(OUT / name, img, mode)
        meta = Path(str(path) + '.meta')
        if not meta.exists():
            lines = TEMPLATES[name].read_text(encoding='ascii').splitlines()
            lines = ['guid: ' + BT.fresh_guid(used) if ln.startswith('guid:') else ln for ln in lines]
            meta.write_text('\n'.join(lines) + '\n', encoding='ascii', newline='\n')
            created.append(meta.relative_to(ROOT).as_posix())
        if not np.array_equal(BT.load(path, mode), img):
            raise SystemExit(f'{path} does not read back equal to the build output.')
    return {'folder': OUT.relative_to(ROOT).as_posix(), 'created_meta': created}


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('stage', choices=['targets', 'build'])
    parser.add_argument('--replace', action='store_true', help='targets: allow replacing earlier edit targets.')
    parser.add_argument('--from', dest='source', help='targets: folder holding a build\'s textures (default: the installed Relight folder).')
    parser.add_argument('--output', help='build: unused directory under Docs/work (default: a fresh sunken-larder-relight-* directory).')
    parser.add_argument('--write-assets', action='store_true', help='build: also write the textures to Art/SunkenLarder/Relight.')
    parser.add_argument('--save-registered', action='store_true', help='build: also write the registered donors.')
    parser.add_argument('--donor-unlit', help='build: substitute a candidate plate unlit donor.')
    parser.add_argument('--donor-hag', help='build: substitute a candidate unlit donor for her.')
    args = parser.parse_args()
    if args.stage == 'targets':
        stage_targets(args)
        return
    if args.output:
        out_dir = (Path.cwd() / args.output).resolve()
        if WORK.resolve() not in out_dir.parents:
            raise SystemExit('--output must be under Docs/work.')
        out_dir.mkdir(parents=True, exist_ok=False)
    else:
        WORK.mkdir(parents=True, exist_ok=True)
        out_dir = Path(tempfile.mkdtemp(prefix='sunken-larder-relight-', dir=WORK))
    if args.write_assets and (args.donor_unlit or args.donor_hag):
        raise SystemExit('--write-assets installs only from the preserved donors.')
    for key, value in (('plate_unlit', args.donor_unlit), ('hag_unlit', args.donor_hag)):
        if value:
            DONORS[key] = (Path.cwd() / value).resolve()
    report = build(out_dir, args.write_assets, args.save_registered)
    print(json.dumps({k: report[k] for k in report if k.startswith(('registration', 'verification', 'plate_unlit_drift',
                                                                    'hag_unlit_drift', 'installed'))}, indent=2))
    print(f'Wrote {out_dir}')


if __name__ == '__main__':
    main()
