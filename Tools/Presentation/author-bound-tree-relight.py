"""Author the Bound Tree's relighting sources from registered imagegen lighting donors.

Splits the painted lighting of the Bound Tree layers (Art/BoundTree) into a moonlit ambient base and a reference lit
by a strong flare of the spirit's glow (scene light 0), following "Relighting sources" in
Docs/Reference/Art/HauntedMerchantHostLayers.md. Stages:

  targets  Write the imagegen edit targets under Docs/work/bound-tree-relight/targets: the plate's face box (unlit
           donor) and, once the build in --from holds them, the unlit plate's glow box (glow donor) with the painted
           plate in that box as its reference image, and the painted nymph over the glow-lit plate (nymph glow donor).
  build    Register the preserved donors (Docs/Reference/Art/BoundTree*Donor.png), derive linear-light ratio maps
           and write BoundTreeAmbientUnlit.png, BoundTreeGlowLit.png, BoundTreeFacelessUnlit.png,
           BoundTreeNymphUnlit.png, BoundTreeNymphGlowLit.png and the response map BoundTreeGlowResponse.png, the
           review images and report.json. A missing glow donor skips everything that depends on it.
  plates   From the textures in --from (default: the installed Relight folder), write the background plates that
           the spirit host's face layer covers: through BoundTreeFaceMask.png alpha (soft edge included) the face
           region of the ambient base and of both light references is replaced by BoundTreeFacelessUnlit.png, so the
           plate shows the faceless trunk wherever the face layer is transparent. BoundTreeFacelessPlateUnlit.png
           (base), BoundTreeFacelessPlateGlow.png (light 0 reference) and BoundTreeFacelessPlateGlowLit.png (light 1
           reference), with review images and report.json.

Python 3.10 or later with numpy, Pillow and scipy (on this machine the venv
Docs/work/merchant-table-clear/.venv/Scripts/python.exe). The registration, ratio-map, highlight-cap and harmonic-fill
functions are imported from author-merchant-relight.py beside this script, so both scenes use one method. Run from
the repository root:

  python Tools/Presentation/author-bound-tree-relight.py targets
  python Tools/Presentation/author-bound-tree-relight.py build                 # fresh Docs/work/bound-tree-relight-*
  python Tools/Presentation/author-bound-tree-relight.py build --write-assets  # also writes Art/BoundTree/Relight
  python Tools/Presentation/author-bound-tree-relight.py plates                # fresh Docs/work/bound-tree-plates-*
  python Tools/Presentation/author-bound-tree-relight.py plates --write-assets # also writes the three plates

The default build output is a fresh ignored Docs/work/bound-tree-relight-* directory (textures under textures/,
review images under review/, report.json); --output chooses an unused directory under Docs/work. --write-assets also
replaces the six textures in Art/BoundTree/Relight, keeps existing .meta files and writes a .meta with a fresh GUID
for a texture that has none (colour: BoundTreeOriginal.png.meta, RGBA: BoundTreeNymph.png.meta, response:
HauntedMerchant/MerchantLanternResponse.png.meta). Source artwork and donors are only read, so repeated runs give
the same textures. `plates` writes a fresh Docs/work/bound-tree-plates-* directory (or --output); its --write-assets
writes the three plates to Art/BoundTree/Relight with a .meta copied, under a fresh GUID, from the texture each
replaces on the background (BoundTreeAmbientUnlit.png, BoundTreeBackground.png and BoundTreeGlowLit.png) and keeps
an existing .meta. It only reads the installed textures, so repeated runs give the same plates.
"""
from pathlib import Path
import argparse
import importlib.util
import json
import tempfile
import uuid

import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import gaussian_filter, distance_transform_edt

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Dice-Unity/Assets/Dice/Art/BoundTree'
OUT = ART / 'Relight'
REF = ROOT / 'Docs/Reference/Art'
WORK = ROOT / 'Docs/work'
TARGETS = WORK / 'bound-tree-relight' / 'targets'
SIZE = (941, 1672)

_spec = importlib.util.spec_from_file_location('author_merchant_relight', Path(__file__).with_name('author-merchant-relight.py'))
MR = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(MR)
to_linear, to_srgb = MR.to_linear, MR.to_srgb

# Edit-target boxes in the concept frame (x0, y0, x1, y1), each a Lanczos 1024 x 1536 upscale.
TARGET_SIZE = (1024, 1536)
PLATE_UNLIT_BOX = (290, 100, 710, 730)    # the face: eyes, brows, split mouth and the bark it lights
PLATE_GLOW_BOX = (40, 60, 800, 1200)      # the face down to the trunk base where the nymph sits
NYMPH_GLOW_BOX = (96, 576, 416, 1056)     # the nymph (BoundTreeLayers.json nymph target box)

DONORS = {
    'plate_unlit': REF / 'BoundTreeRelightUnlitDonor.png',
    'plate_glow': REF / 'BoundTreeRelightGlowDonor.png',
    'nymph_glow': REF / 'BoundTreeRelightNymphGlowDonor.png',
}


def load(path, mode='RGBA'):
    return np.asarray(Image.open(path).convert(mode)).astype(np.float64)


def art(name, mode='RGBA'):
    return load(ART / name, mode)


def save_png(path, array, mode):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(np.clip(np.rint(array), 0, 255).astype(np.uint8), mode).save(path)
    return path


def over(rgba, rgb):
    a = rgba[..., 3:4] / 255.0
    return rgba[..., :3] * a + rgb * (1 - a)


def write_target(rgb, box, path, replace):
    path = Path(path)
    if path.exists() and not replace:
        raise SystemExit(f'Refusing to replace existing {path}; pass --replace.')
    x0, y0, x1, y1 = box
    img = Image.fromarray(np.clip(np.rint(rgb[y0:y1, x0:x1]), 0, 255).astype(np.uint8), 'RGB')
    path.parent.mkdir(parents=True, exist_ok=True)
    img.resize(TARGET_SIZE, Image.LANCZOS).save(path)
    print(path)


def stage_targets(args):
    """The plate's face box for the unlit donor; with a build in --from, the unlit plate's glow box for the glow
    donor and the painted nymph over the glow-lit plate for the nymph glow donor."""
    src = Path(args.source) if args.source else OUT
    write_target(art('BoundTreeBackground.png', 'RGB'), PLATE_UNLIT_BOX, TARGETS / 'target-plate-unlit.png', args.replace)
    unlit = src / 'BoundTreeAmbientUnlit.png'
    if unlit.exists():
        write_target(load(unlit, 'RGB'), PLATE_GLOW_BOX, TARGETS / 'target-plate-glow.png', args.replace)
        # Reference image for the glow donor: the painted plate (the glow at rest) in the same box.
        write_target(art('BoundTreeBackground.png', 'RGB'), PLATE_GLOW_BOX, TARGETS / 'reference-plate-glow-rest.png', args.replace)
    glow = src / 'BoundTreeGlowLit.png'
    if glow.exists():
        write_target(over(art('BoundTreeNymph.png'), load(glow, 'RGB')), NYMPH_GLOW_BOX, TARGETS / 'target-nymph-glow.png', args.replace)


# ---------------------------------------------------------------------------------------------------------------
# Glow geometry, concept pixels (top-left origin, pixel centres at +0.5).
# Source: the two eyes (ellipses: centre, radii) and the split mouth (polygon), feathered over SOURCE_FEATHER px.
EYES = (((441.0, 244.0), (17.0, 13.0)), ((568.0, 274.0), (14.0, 10.0)))
CRACK = [(455, 335), (478, 322), (506, 318), (528, 330), (532, 380), (525, 440), (518, 500), (512, 560), (506, 610),
         (500, 640), (492, 610), (488, 560), (478, 500), (466, 440), (452, 380)]
SOURCE_FEATHER = 6.0
# Response: R = min(1, hypot((x - cx) / rx, (y - cy) / ry)), the convention of MerchantLanternResponse.png.
GLOW_CENTRE = (500.0, 420.0)
GLOW_RADIUS = (620.0, 860.0)

# Unlit: the concept's glow is removed within UNLIT_REACH px of the source (full weight within UNLIT_FULL px); the
# plate is byte-identical beyond. The donor's drift is measured on the box beyond UNLIT_REFERENCE px of the source.
UNLIT_FULL = 30.0
UNLIT_REACH = 60.0
UNLIT_REFERENCE = 80.0
# Glow-lit: the donor's added light, weighted by 1 - smoothstep((R - GLOW_FADE) / (1 - GLOW_FADE)) so it reaches
# zero at R = 1. The donor's drift is measured on GLOW_REFERENCE (x0, y0, x1, y1): distant swamp, trees and sky
# left and right of the trunk, which the glow cannot light.
GLOW_FADE = 0.55
GLOW_REFERENCE = ((48, 300, 140, 560), (700, 380, 792, 700), (700, 68, 792, 300))
GLOW_SOURCE_DILATE = 10.0        # registration excludes the source within this distance (its look changes most)
DRIFT_SIGMA = (40.0, 160.0)      # normalized-convolution sigmas of the drift baseline, fine then coarse fallback
# Receivers: the glow lights the tree, its roots, moss and the ground, never the sky, the moon or the distant swamp
# and trees. Those are the plate's blue pixels: in linear light the tree has B / R about 0.3 to 0.8, the distant
# background about 3. Receiver weight = 1 - smoothstep((log2(B / R) - lo) / (hi - lo)) on the unlit plate blurred
# RECEIVER_SIGMA px, times 0 inside the moon ellipse (centre, radii; the moon is neutral grey, not blue).
RECEIVER_LOG2_BR = (0.3, 1.3)
RECEIVER_SIGMA = 2.0
MOON = ((804.0, 184.0), (72.0, 72.0))
# Light colour: the glow is yellow-green, so its gain (ratio - 1, blurred HUE_SIGMA px) raises green about as much
# as red (G / R gain 0.7 to 1.1 on bark, moss and leaves). Gain whose G / R is below HUE_GR is the donor's re-painting
# of dark roots, not glow light; it is faded out by smoothstep((G / R - lo) / (hi - lo)).
HUE_SIGMA = 4.0
HUE_GR = (0.3, 0.6)
NYMPH_DRIFT_RING = (3.0, 40.0)   # plate pixels around the nymph that measure the nymph donor's drift (px)
BOX_INSET = 8
# Beyond the glow donor's box the field continues from the box (blurred, BEYOND_SIGMA px) and fades to the identity
# over BEYOND_FADE px, so the frame edges above, left and right of the box receive the spill without a seam.
BEYOND_SIGMA = 16.0
BEYOND_FADE = 60.0


def smoothstep(t):
    t = np.clip(t, 0, 1)
    return t * t * (3 - 2 * t)


def pixel_grid():
    yy, xx = np.mgrid[0:SIZE[1], 0:SIZE[0]].astype(np.float64)
    return xx + 0.5, yy + 0.5


def polygon_inside(poly, xx, yy):
    inside = np.zeros(xx.shape, bool)
    n = len(poly)
    for i in range(n):
        x0, y0 = poly[i]
        x1, y1 = poly[(i + 1) % n]
        cross = ((y0 > yy) != (y1 > yy)) & (xx < (x1 - x0) * (yy - y0) / (y1 - y0 + 1e-12) + x0)
        inside ^= cross
    return inside


def source_mask():
    """The glow source (eyes and split mouth): 1 inside, smoothstep to 0 over SOURCE_FEATHER px outside."""
    xx, yy = pixel_grid()
    hard = polygon_inside(CRACK, xx, yy)
    for (cx, cy), (rx, ry) in EYES:
        hard |= np.hypot((xx - cx) / rx, (yy - cy) / ry) <= 1
    dist = distance_transform_edt(~hard)
    return 1 - smoothstep(dist / SOURCE_FEATHER), dist


def glow_distance():
    xx, yy = pixel_grid()
    return np.minimum(1.0, np.hypot((xx - GLOW_CENTRE[0]) / GLOW_RADIUS[0], (yy - GLOW_CENTRE[1]) / GLOW_RADIUS[1]))


def box_mask(box, inset=BOX_INSET):
    m = np.zeros((SIZE[1], SIZE[0]), bool)
    x0, y0, x1, y1 = box
    m[y0 + inset:y1 - inset, x0 + inset:x1 - inset] = True
    return m


def beyond_box(field, interior):
    """Return (field, weight): outside `interior` the field is blurred (its nearest-interior extension) and the
    weight falls from 1 at the box edge to 0 at BEYOND_FADE px."""
    d = distance_transform_edt(~interior)
    blurred = gaussian_filter(field, (BEYOND_SIGMA, BEYOND_SIGMA, 0))
    t = smoothstep(d / 8.0)[..., None]
    return field * (1 - t) + blurred * t, 1 - smoothstep(d / BEYOND_FADE)


def glow_hue(ratio):
    gain = gaussian_filter(np.maximum(ratio - 1, 0), (HUE_SIGMA, HUE_SIGMA, 0))
    gr = gain[..., 1] / np.maximum(gain[..., 0], 1e-4)
    lo, hi = HUE_GR
    w = smoothstep((gr - lo) / (hi - lo))
    return 1 + (ratio - 1) * w[..., None]


def receivers(unlit):
    lin = gaussian_filter(to_linear(unlit), (RECEIVER_SIGMA, RECEIVER_SIGMA, 0))
    br = np.log2(np.maximum(lin[..., 2], 1e-5) / np.maximum(lin[..., 0], 1e-5))
    lo, hi = RECEIVER_LOG2_BR
    w = 1 - smoothstep((br - lo) / (hi - lo))
    xx, yy = pixel_grid()
    (cx, cy), (rx, ry) = MOON
    w *= np.hypot((xx - cx) / rx, (yy - cy) / ry) > 1
    return gaussian_filter(w, RECEIVER_SIGMA)


def drift_baseline(ratio, reference):
    """Per-channel multiplicative drift of a registered donor: exp of the normalized Gaussian average of log(ratio)
    over the reference pixels (fine sigma where enough reference is near, the coarse one elsewhere)."""
    w = reference.astype(np.float64)
    out = np.empty_like(ratio)
    fine, coarse = DRIFT_SIGMA
    wf, wc = gaussian_filter(w, fine), gaussian_filter(w, coarse)
    blend = smoothstep(wf / 0.25)
    for ch in range(ratio.shape[-1]):
        lr = np.log(np.maximum(ratio[..., ch], 1e-6)) * w
        f = gaussian_filter(lr, fine) / np.maximum(wf, 1e-9)
        c = gaussian_filter(lr, coarse) / np.maximum(wc, 1e-9)
        out[..., ch] = np.exp(blend * f + (1 - blend) * c)
    return out


def rgba_with(rgb, alpha):
    return np.concatenate([rgb, alpha[..., None]], -1)


def register_donor(path, shown_rgb, region, box, report, key, save_dir):
    """Register a donor regenerating `box` of `shown_rgb`; `region` (bool) drives the fit."""
    donor = load(path, 'RGB')
    du, dv, reg = MR.register(donor, shown_rgb, np.where(region, 255.0, 0.0), box)
    reg['donor'] = Path(path).resolve().relative_to(ROOT).as_posix()
    report[f'registration_{key}'] = reg
    registered = MR.sample_donor(donor, du, dv)
    if save_dir is not None:
        save_png(save_dir / f'{key}-registered.png', registered, 'RGB')
    return registered


def field_for(registered, shown_rgb, interior, sigma=MR.RATIO_SIGMA):
    """Ratio (3) and highlight/saturation cap (4) fields of the merchant method for a registered donor over the
    pixels it regenerates (`interior`, bool), extended past them from the nearest interior value."""
    host = rgba_with(shown_rgb, np.where(interior, 255.0, 0.0))
    return MR.ratio_map(registered, host, sigma), MR.highlight_cap(registered, host)


def blend_field(ratio, cap, weight):
    """Fade a ratio/cap field to the identity (ratio 1, no cap) as weight goes to 0."""
    w = weight[..., None]
    return np.concatenate([1 + (ratio - 1) * w, cap[..., :3] * w + (1 - w), cap[..., 3:] * w + (1 - w)], -1)


def relit(rgb, field):
    """Linear relit RGB of an RGB image through a 7-channel field (author-merchant-relight.py relit_linear)."""
    return MR.relit_linear(rgba_with(rgb, np.full(rgb.shape[:2], 255.0)), field)


def encode(lin, source_rgb, changed):
    """sRGB bytes: the relit value where `changed`, the source bytes elsewhere."""
    out = source_rgb.copy()
    out[changed] = np.rint(to_srgb(lin))[changed]
    return out


def build(out_dir, write_assets, save_registered):
    report = {'ratio_sigma_px': MR.RATIO_SIGMA, 'ratio_clamp': MR.RATIO_CLAMP, 'cap_gain': MR.CAP_GAIN,
              'saturation_gain': MR.SAT_GAIN}
    tex_dir = out_dir / 'textures'
    reg_dir = out_dir / 'registered' if save_registered else None
    plate = art('BoundTreeBackground.png', 'RGB')
    faceless = art('BoundTreeFaceless.png', 'RGB')
    face_mask = art('BoundTreeFaceMask.png')[..., 3]
    nymph = art('BoundTreeNymph.png')
    src, src_dist = source_mask()
    R = glow_distance()
    outputs = {}

    # 1. Unlit plate: the concept's glow removed near the source, darkening only, drift-normalized.
    region = box_mask(PLATE_UNLIT_BOX) & (src_dist > GLOW_SOURCE_DILATE)
    reg = register_donor(DONORS['plate_unlit'], plate, region, PLATE_UNLIT_BOX, report, 'plate_unlit', reg_dir)
    interior = box_mask(PLATE_UNLIT_BOX)
    ratio, cap = field_for(reg, plate, interior)
    drift = drift_baseline(ratio, interior & (src_dist > UNLIT_REFERENCE))
    report['plate_unlit_drift_at_source_rgb'] = [float(v) for v in drift[src > 0.5].mean(0)]
    ratio = np.minimum(ratio / drift, 1.0)
    w_unlit = 1 - smoothstep((src_dist - UNLIT_FULL) / (UNLIT_REACH - UNLIT_FULL))
    u_field = blend_field(ratio, cap, w_unlit)
    unlit_changed = w_unlit > 0
    unlit_lin = np.minimum(relit(plate, u_field), to_linear(plate))
    unlit = encode(unlit_lin, plate, unlit_changed)
    outputs['BoundTreeAmbientUnlit.png'] = unlit

    # 2. Faceless unlit: the plate's unlit field, harmonically filled (log space) over the face transfer region
    # (BoundTreeFaceMask.png alpha 1, which holds every pixel where the faceless trunk differs), so no plate value
    # from inside the face (the dark eyes and mouth) reaches the plain bark.
    unknown = (face_mask >= 255) | np.any(faceless != plate, -1)
    f_field = MR.membrane_fill(u_field, unknown)
    f_changed = unlit_changed | unknown
    faceless_lin = np.minimum(relit(faceless, f_field), to_linear(faceless))
    faceless_unlit = encode(faceless_lin, faceless, f_changed)
    outputs['BoundTreeFacelessUnlit.png'] = faceless_unlit

    # 3. Nymph unlit: the same field (identity wherever the concept's glow does not reach her).
    n_rgb = nymph[..., :3]
    n_inside = nymph[..., 3] > 0
    n_unlit_lin = np.minimum(relit(n_rgb, u_field), to_linear(n_rgb))
    nymph_unlit = nymph.copy()
    nymph_unlit[..., :3] = encode(n_unlit_lin, n_rgb, unlit_changed & n_inside)
    outputs['BoundTreeNymphUnlit.png'] = nymph_unlit

    # 4. Response map: R the elliptical distance from the glow, G the source mask.
    response = np.zeros((SIZE[1], SIZE[0], 3))
    response[..., 0] = R * 255
    response[..., 1] = src * 255
    outputs['BoundTreeGlowResponse.png'] = response
    report['response'] = {'formula': 'R = min(1, hypot((x - cx) / rx, (y - cy) / ry)), x, y continuous concept pixels '
                          '(pixel centres at +0.5); G = source mask (eyes and split mouth, feathered); B = 0',
                          'centre_px': list(GLOW_CENTRE), 'radius_px': list(GLOW_RADIUS),
                          'source_feather_px': SOURCE_FEATHER}

    glow = None
    if DONORS['plate_glow'].exists():
        # 5. Glow-lit plate: the flare added to the unlit plate. Added light only (ratio >= 1), drift-normalized,
        # faded to zero at R = 1; inside the source the registered donor's own emission is the floor.
        region = box_mask(PLATE_GLOW_BOX) & (src_dist > GLOW_SOURCE_DILATE)
        reg = register_donor(DONORS['plate_glow'], unlit, region, PLATE_GLOW_BOX, report, 'plate_glow', reg_dir)
        interior = box_mask(PLATE_GLOW_BOX)
        ratio, cap = field_for(reg, unlit, interior)
        ref = np.zeros_like(interior)
        for x0, y0, x1, y1 in GLOW_REFERENCE:
            ref[y0:y1, x0:x1] = True
        ref &= interior
        report['plate_glow_drift_reference_pixels'] = int(ref.sum())
        drift = drift_baseline(ratio, ref)
        report['plate_glow_drift_at_source_rgb'] = [float(v) for v in drift[src > 0.5].mean(0)]
        ratio = glow_hue(np.maximum(ratio / drift, 1.0))
        rc, w_box = beyond_box(np.concatenate([ratio, cap], -1), interior)
        ratio, cap = rc[..., :3], rc[..., 3:]
        w_recv = receivers(unlit)
        w_glow = (1 - smoothstep((R - GLOW_FADE) / (1 - GLOW_FADE))) * w_box * w_recv
        g_field = blend_field(ratio, cap, w_glow)
        base = to_linear(unlit)
        emission = to_linear(reg) / drift * src[..., None]
        glow_lin = np.maximum(np.maximum(relit(unlit, g_field), emission), base)
        glow_changed = (w_glow > 0) | (src > 0)
        glow = encode(glow_lin, unlit, glow_changed)
        glow = np.maximum(glow, unlit)
        outputs['BoundTreeGlowLit.png'] = glow
        report['plate_glow_fade_R'] = GLOW_FADE
        save_png(out_dir / 'fields' / 'plate-glow-ratio-x8.png', to_srgb(np.clip((g_field[..., :3] - 1) / 8, 0, 1)), 'RGB')

    nymph_glow = None
    if glow is not None and DONORS['nymph_glow'].exists():
        # 6. Glow-lit nymph: her donor regenerates her (as painted, = unlit) over the glow-lit plate. Its drift is
        # measured on the plate around her, which the target already shows glow-lit.
        shown = over(nymph_unlit, glow)
        n_alpha = nymph[..., 3]
        region = box_mask(NYMPH_GLOW_BOX) & (n_alpha > 250)
        reg = register_donor(DONORS['nymph_glow'], shown, region, NYMPH_GLOW_BOX, report, 'nymph_glow', reg_dir)
        interior = box_mask(NYMPH_GLOW_BOX)
        host_interior = interior & (n_alpha > 250)
        ratio, cap = field_for(reg, nymph_unlit[..., :3], host_interior)
        out_d = distance_transform_edt(n_alpha == 0)
        ring = interior & (out_d >= NYMPH_DRIFT_RING[0]) & (out_d <= NYMPH_DRIFT_RING[1])
        plate_ratio = (gaussian_filter(to_linear(reg) * ring[..., None], (2, 2, 0)) /
                       np.maximum(gaussian_filter(to_linear(shown) * ring[..., None], (2, 2, 0)), 1e-6))
        drift = drift_baseline(plate_ratio, ring)
        report['nymph_glow_drift_on_her_rgb'] = [float(v) for v in drift[n_alpha > 250].mean(0)]
        ratio = glow_hue(np.maximum(ratio / drift, 1.0))
        w_glow = 1 - smoothstep((R - GLOW_FADE) / (1 - GLOW_FADE))
        ng_field = blend_field(ratio, cap, w_glow)
        base = to_linear(nymph_unlit[..., :3])
        ng_lin = np.maximum(relit(nymph_unlit[..., :3], ng_field), base)
        nymph_glow = nymph_unlit.copy()
        nymph_glow[..., :3] = encode(ng_lin, nymph_unlit[..., :3], n_inside & (w_glow > 0))
        nymph_glow[..., :3] = np.maximum(nymph_glow[..., :3], nymph_unlit[..., :3])
        outputs['BoundTreeNymphGlowLit.png'] = nymph_glow

    # Write, read back and verify.
    for name, img in outputs.items():
        save_png(tex_dir / name, img, 'RGBA' if img.shape[2] == 4 else 'RGB')
    disk = {name: load(tex_dir / name, 'RGBA' if outputs[name].shape[2] == 4 else 'RGB') for name in outputs}
    report['verification'] = verify(disk, plate, faceless, face_mask, nymph, unlit_changed, f_changed)
    if 'BoundTreeGlowLit.png' in disk:
        report['resting'] = resting(disk, plate, nymph)
    review(out_dir / 'review', disk, plate, nymph, src, R, report.get('resting'))
    if write_assets:
        report['installed'] = install(disk)
    (out_dir / 'report.json').write_text(json.dumps(report, indent=2), encoding='ascii')
    return report


def verify(disk, plate, faceless, face_mask, nymph, unlit_region, faceless_region):
    v = {}
    unlit = disk['BoundTreeAmbientUnlit.png']
    changed = np.any(unlit != plate, -1)
    v['unlit_vs_background'] = {'pixels_changed': int(changed.sum()), 'declared_region_pixels': int(unlit_region.sum()),
                                'changed_outside_declared_region': int((changed & ~unlit_region).sum()),
                                'channel_values_above_background': int((unlit > plate).sum())}
    fu = disk['BoundTreeFacelessUnlit.png']
    outside = face_mask == 0
    v['faceless_unlit'] = {
        'pixels_differing_from_unlit_plate_outside_face_mask': int((np.any(fu != unlit, -1) & outside).sum()),
        'pixels_differing_from_unlit_plate_where_face_mask_below_1': int((np.any(fu != unlit, -1) & (face_mask < 255)).sum()),
        'faceless_differs_from_background_where_face_mask_below_1': int((np.any(faceless != plate, -1) & (face_mask < 255)).sum()),
        'pixels_changed_vs_faceless': int(np.any(fu != faceless, -1).sum()),
        'changed_vs_faceless_outside_declared_region': int((np.any(fu != faceless, -1) & ~faceless_region).sum()),
        'channel_values_above_faceless': int((fu > faceless).sum())}
    nu = disk['BoundTreeNymphUnlit.png']
    v['nymph_unlit'] = {'alpha_equal': bool(np.array_equal(nu[..., 3], nymph[..., 3])),
                        'rgb_pixels_changed_vs_nymph': int(np.any(nu[..., :3] != nymph[..., :3], -1).sum())}
    if 'BoundTreeGlowLit.png' in disk:
        g = disk['BoundTreeGlowLit.png']
        gch = np.any(g != unlit, -1)
        v['glow_vs_unlit'] = {'channel_values_below_unlit': int((g < unlit).sum()), 'pixels_changed': int(gch.sum()),
                              'changed_pixel_bbox_xyxy': bbox(gch)}
    if 'BoundTreeNymphGlowLit.png' in disk:
        ng = disk['BoundTreeNymphGlowLit.png']
        v['nymph_glow_vs_unlit'] = {'channel_values_below_unlit': int((ng[..., :3] < nu[..., :3]).sum()),
                                    'alpha_equal_to_nymph': bool(np.array_equal(ng[..., 3], nymph[..., 3])),
                                    'alpha_equal_to_unlit': bool(np.array_equal(ng[..., 3], nu[..., 3])),
                                    'pixels_changed': int(np.any(ng[..., :3] != nu[..., :3], -1).sum())}
    resp = disk['BoundTreeGlowResponse.png']
    v['response'] = {'R_range': [float(resp[..., 0].min()), float(resp[..., 0].max())],
                     'G_pixels_gt_0': int((resp[..., 1] > 0).sum()), 'B_max': float(resp[..., 2].max())}
    return v


def bbox(mask):
    ys, xs = np.nonzero(mask)
    return [int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1] if len(xs) else None


def mix(unlit, glow, k):
    """Linear light: unlit + k * (glowlit - unlit), as sRGB values."""
    u, g = to_linear(unlit), to_linear(glow)
    return to_srgb(u + k * (g - u))


def resting(disk, plate, nymph):
    """The resting intensity k (least squares in linear light against the plate) and the composite errors."""
    u = to_linear(disk['BoundTreeAmbientUnlit.png'])
    g = to_linear(disk['BoundTreeGlowLit.png'])
    p = to_linear(plate)
    d = g - u
    k = float(np.clip((d * (p - u)).sum() / max((d * d).sum(), 1e-12), 0, 1))
    out = {'k': k, 'fit': 'least squares over every pixel and channel in linear light against BoundTreeBackground.png'}
    comp = np.rint(mix(disk['BoundTreeAmbientUnlit.png'], disk['BoundTreeGlowLit.png'], k))
    changed = np.any(disk['BoundTreeGlowLit.png'] != disk['BoundTreeAmbientUnlit.png'], -1) | \
        np.any(disk['BoundTreeAmbientUnlit.png'] != plate, -1)
    err = np.abs(comp - plate)
    out['plate_mae_levels_full_frame'] = float(err.mean())
    out['plate_mae_levels_in_lit_region'] = float(err[changed].mean())
    out['plate_max_abs_levels'] = float(err.max())
    if 'BoundTreeNymphGlowLit.png' in disk:
        nu, ng = disk['BoundTreeNymphUnlit.png'], disk['BoundTreeNymphGlowLit.png']
        ncomp = nu.copy()
        ncomp[..., :3] = np.rint(mix(nu[..., :3], ng[..., :3], k))
        scene = over(ncomp, comp)
        painted = over(nymph, plate)
        a = nymph[..., 3] > 0
        nerr = np.abs(np.rint(scene) - np.rint(painted))
        out['nymph_over_plate_mae_levels_inside_her_alpha'] = float(nerr[a].mean())
        out['nymph_over_plate_mae_levels_full_frame'] = float(nerr.mean())
    # Comparison: least-squares k over the regions where the concept's glow is, and the k on a 0.01 grid that
    # minimizes the mean absolute error in sRGB levels; the error at a few intensities.
    src = disk['BoundTreeGlowResponse.png'][..., 1] > 127
    unlit_region = np.any(disk['BoundTreeAmbientUnlit.png'] != plate, -1)
    regions = {'full_frame': np.ones(src.shape, bool), 'lit_region': changed, 'unlit_region': unlit_region,
               'source_G_gt_0.5': src}
    out['regions'] = {}
    for name, m in regions.items():
        dm, em = d[m], (p - u)[m]
        grid = [(float(np.abs(np.rint(to_srgb(u[m] + kk * dm)) - plate[m]).mean()), float(kk))
                for kk in np.round(np.arange(0, 1.0001, 0.01), 2)]
        best = min(grid)
        out['regions'][name] = {'pixels': int(m.sum()),
                                'k_least_squares_linear': float((dm * em).sum() / max((dm * dm).sum(), 1e-12)),
                                'k_min_mae_srgb': best[1], 'mae_levels_at_min': best[0],
                                'mae_levels_at_k': float(err[m].mean()),
                                'mae_levels_at_0': float(np.abs(plate[m] - disk['BoundTreeAmbientUnlit.png'][m]).mean())}
    return out


# ---------------------------------------------------------------------------------------------------------------
# Review images.
def label(img, text):
    im = Image.fromarray(np.clip(np.rint(img), 0, 255).astype(np.uint8), 'RGB')
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, 8 + 6 * len(text), 14], fill=(0, 0, 0))
    d.text((4, 1), text, fill=(255, 255, 255))
    return np.asarray(im).astype(np.float64)


def crop2x(img, box, scale=2):
    x0, y0, x1, y1 = box
    im = Image.fromarray(np.clip(np.rint(img[y0:y1, x0:x1]), 0, 255).astype(np.uint8), 'RGB')
    return np.asarray(im.resize(((x1 - x0) * scale, (y1 - y0) * scale), Image.LANCZOS)).astype(np.float64)


def row(images, gap=6):
    h = max(i.shape[0] for i in images)
    parts = []
    for i, img in enumerate(images):
        pad = np.zeros((h, img.shape[1], 3))
        pad[:img.shape[0]] = img
        parts.append(pad)
        if i < len(images) - 1:
            parts.append(np.full((h, gap, 3), 40.0))
    return np.concatenate(parts, 1)


FACE_BOX = (330, 150, 680, 680)
NYMPH_BOX = (90, 600, 420, 1020)


def review(rdir, disk, plate, nymph, src, R, rest):
    rdir.mkdir(parents=True, exist_ok=True)
    unlit = disk['BoundTreeAmbientUnlit.png']
    glow = disk.get('BoundTreeGlowLit.png')
    nu = disk['BoundTreeNymphUnlit.png']
    ng = disk.get('BoundTreeNymphGlowLit.png')
    concept = art('BoundTreeOriginal.png', 'RGB')
    k = rest['k'] if rest else None

    def scene(i):
        p = mix(unlit, glow, i) if glow is not None else unlit
        if ng is not None:
            n = nu.copy()
            n[..., :3] = mix(nu[..., :3], ng[..., :3], i)
            return over(n, p)
        return over(nu, p)

    def half(img):
        im = Image.fromarray(np.clip(np.rint(img), 0, 255).astype(np.uint8), 'RGB')
        return np.asarray(im.resize((SIZE[0] // 2, SIZE[1] // 2), Image.LANCZOS)).astype(np.float64)

    panels = [label(half(concept), 'concept'), label(half(over(nu, unlit)), 'unlit (nymph unlit over unlit plate)')]
    if glow is not None:
        panels.append(label(half(scene(1.0)), 'glow-lit (1)'))
        panels.append(label(half(scene(k)), f'resting k={k:.3f}'))
    save_png(rdir / 'plates-full.png', row(panels), 'RGB')
    for name, box in (('face', FACE_BOX), ('nymph', NYMPH_BOX)):
        panels = [label(crop2x(concept, box), 'concept'), label(crop2x(over(nu, unlit), box), 'unlit')]
        if glow is not None:
            panels.append(label(crop2x(scene(1.0), box), 'glow-lit'))
            panels.append(label(crop2x(scene(k), box), f'resting k={k:.3f}'))
        save_png(rdir / f'{name}-2x.png', row(panels), 'RGB')
    if glow is not None:
        strip = [label(half(scene(i)), f'intensity {i:.3f}') for i in (0.0, k, 1.0, 1.5)]
        save_png(rdir / 'intensity-strip-plate.png', row(strip), 'RGB')
        strip = [label(crop2x(scene(i), NYMPH_BOX), f'intensity {i:.3f}') for i in (0.0, k, 1.0, 1.5)]
        save_png(rdir / 'intensity-strip-nymph-2x.png', row(strip), 'RGB')
        strip = [label(crop2x(scene(i), FACE_BOX), f'intensity {i:.3f}') for i in (0.0, k, 1.0, 1.5)]
        save_png(rdir / 'intensity-strip-face-2x.png', row(strip), 'RGB')
        contrib = to_srgb(np.clip((to_linear(glow) - to_linear(unlit)) * 4, 0, 1))
        save_png(rdir / 'glow-contribution-x4.png', label(contrib, 'glow-lit - unlit, linear x4'), 'RGB')
    pairs = [label(crop2x(over(nu, unlit), NYMPH_BOX), 'nymph unlit over unlit plate')]
    if ng is not None:
        pairs.append(label(crop2x(over(ng, glow), NYMPH_BOX), 'nymph glow-lit over glow-lit plate'))
        pairs.append(label(crop2x(over(nymph, plate), NYMPH_BOX), 'painted nymph over plate'))
    save_png(rdir / 'nymph-pairs-2x.png', row(pairs), 'RGB')
    fu = disk['BoundTreeFacelessUnlit.png']
    diff = np.any(fu != unlit, -1)
    dimg = np.zeros_like(unlit)
    dimg[diff] = (255, 0, 255)
    save_png(rdir / 'faceless-unlit-vs-unlit.png', row([label(half(unlit), 'unlit plate'), label(half(fu), 'faceless unlit'),
                                                        label(half(dimg), 'pixels differing (magenta)')]), 'RGB')
    save_png(rdir / 'faceless-unlit-face-2x.png', row([label(crop2x(unlit, FACE_BOX), 'unlit plate'),
                                                       label(crop2x(fu, FACE_BOX), 'faceless unlit')]), 'RGB')
    resp = disk['BoundTreeGlowResponse.png']
    rv = np.repeat(resp[..., :1], 3, -1)
    contours = np.abs(((R * 10) % 1) - 0.5) > 0.47
    rv[contours] = (255, 200, 0)
    gv = over(rgba_with(np.stack([np.zeros_like(src), resp[..., 1], np.zeros_like(src)], -1), resp[..., 1]), plate * 0.5)
    save_png(rdir / 'response.png', row([label(half(rv), 'R (0.1 contours)'), label(half(gv), 'G over plate x0.5'),
                                         label(crop2x(gv, FACE_BOX), 'G, face 2x')]), 'RGB')


# ---------------------------------------------------------------------------------------------------------------
# Installation.
TEMPLATES = {
    'BoundTreeAmbientUnlit.png': ART / 'BoundTreeOriginal.png.meta',
    'BoundTreeGlowLit.png': ART / 'BoundTreeOriginal.png.meta',
    'BoundTreeFacelessUnlit.png': ART / 'BoundTreeOriginal.png.meta',
    'BoundTreeNymphUnlit.png': ART / 'BoundTreeNymph.png.meta',
    'BoundTreeNymphGlowLit.png': ART / 'BoundTreeNymph.png.meta',
    'BoundTreeGlowResponse.png': ROOT / 'Dice-Unity/Assets/Dice/Art/HauntedMerchant/MerchantLanternResponse.png.meta',
}


def used_guids():
    used = set()
    for meta in (ROOT / 'Dice-Unity/Assets').rglob('*.meta'):
        for line in meta.read_text(encoding='utf-8', errors='ignore').splitlines()[:3]:
            if line.startswith('guid:'):
                used.add(line.split(':', 1)[1].strip())
    return used


def fresh_guid(used):
    guid = uuid.uuid4().hex
    while guid in used:
        guid = uuid.uuid4().hex
    used.add(guid)
    return guid


def install(disk):
    used = used_guids()
    created = []
    folder_meta = Path(str(OUT) + '.meta')
    OUT.mkdir(parents=True, exist_ok=True)
    if not folder_meta.exists():
        lines = (ART.parent / 'BoundTree.meta').read_text(encoding='ascii').splitlines()
        lines = ['guid: ' + fresh_guid(used) if ln.startswith('guid:') else ln for ln in lines]
        folder_meta.write_text('\n'.join(lines) + '\n', encoding='ascii', newline='\n')
        created.append(folder_meta.relative_to(ROOT).as_posix())
    for name, img in disk.items():
        path = save_png(OUT / name, img, 'RGBA' if img.shape[2] == 4 else 'RGB')
        meta = Path(str(path) + '.meta')
        if not meta.exists():
            lines = TEMPLATES[name].read_text(encoding='ascii').splitlines()
            lines = ['guid: ' + fresh_guid(used) if ln.startswith('guid:') else ln for ln in lines]
            meta.write_text('\n'.join(lines) + '\n', encoding='ascii', newline='\n')
            created.append(meta.relative_to(ROOT).as_posix())
        if not np.array_equal(load(path, 'RGBA' if img.shape[2] == 4 else 'RGB'), img):
            raise SystemExit(f'{path} does not read back equal to the build output.')
    return {'folder': OUT.relative_to(ROOT).as_posix(), 'created_meta': created}


# ---------------------------------------------------------------------------------------------------------------
# Faceless background plates under the spirit host's face layer.
# Each plate is lerp(source, BoundTreeFacelessUnlit.png, BoundTreeFaceMask.png alpha) in linear light; where the mask
# is 0 it keeps the source bytes and where it is 1 it holds the faceless bytes. The face layer draws the source
# textures through the same mask, so at full opacity it covers the faceless region and at 0 the faceless trunk shows.
PLATES = {
    # plate: (source texture, its folder); the source's .meta is the plate's import template
    'BoundTreeFacelessPlateUnlit.png': ('BoundTreeAmbientUnlit.png', 'relight'),
    'BoundTreeFacelessPlateGlow.png': ('BoundTreeBackground.png', 'art'),
    'BoundTreeFacelessPlateGlowLit.png': ('BoundTreeGlowLit.png', 'relight'),
}
PLATE_BASE = 'BoundTreeFacelessPlateUnlit.png'


def faceless_plates(relight_dir):
    """The three plates (sRGB bytes) and their checks, from the textures in `relight_dir` and Art/BoundTree."""
    mask = art('BoundTreeFaceMask.png')[..., 3] / 255.0
    faceless = load(relight_dir / 'BoundTreeFacelessUnlit.png', 'RGB')
    faceless_lin = to_linear(faceless)
    m = mask[..., None]
    sources, plates = {}, {}
    for name, (source, folder) in PLATES.items():
        src = load((relight_dir if folder == 'relight' else ART) / source, 'RGB')
        mixed = np.rint(to_srgb(to_linear(src) * (1 - m) + faceless_lin * m))
        mixed = np.where(m <= 0, src, np.where(m >= 1, faceless, mixed))
        sources[name], plates[name] = src, mixed
    base = plates[PLATE_BASE]
    report = {'formula': 'plate = lerp(source, BoundTreeFacelessUnlit.png, BoundTreeFaceMask.png alpha) in linear light, '
                         'source bytes where the mask is 0, faceless bytes where it is 1',
              'mask_pixels_gt_0': int((mask > 0).sum()), 'mask_pixels_eq_1': int((mask >= 1).sum())}
    for name in PLATES:
        if name == PLATE_BASE:
            continue
        below = plates[name] < base
        report[name + ' channel_values_raised_to_base'] = int(below.sum())
        plates[name] = np.maximum(plates[name], base)
    for name, plate in plates.items():
        outside = mask <= 0
        report[name] = {
            'source': PLATES[name][0],
            'pixels_differing_from_source_where_mask_is_0': int((np.any(plate != sources[name], -1) & outside).sum()),
            'pixels_differing_from_faceless_where_mask_is_1': int((np.any(plate != faceless, -1) & (mask >= 1)).sum()),
            'pixels_differing_from_source': int(np.any(plate != sources[name], -1).sum()),
            'channel_values_below_base': int((plate < base).sum())}
    return plates, sources, faceless, mask, report


def stage_plates(args):
    src = Path(args.source).resolve() if args.source else OUT
    if args.output:
        out_dir = (Path.cwd() / args.output).resolve()
        if WORK.resolve() not in out_dir.parents:
            raise SystemExit('--output must be under Docs/work.')
        out_dir.mkdir(parents=True, exist_ok=False)
    else:
        WORK.mkdir(parents=True, exist_ok=True)
        out_dir = Path(tempfile.mkdtemp(prefix='bound-tree-plates-', dir=WORK))
    plates, sources, faceless, mask, report = faceless_plates(src)
    report['from'] = src.relative_to(ROOT).as_posix() if ROOT in src.parents else str(src)
    for name, plate in plates.items():
        save_png(out_dir / 'textures' / name, plate, 'RGB')
    rdir = out_dir / 'review'
    rdir.mkdir(parents=True, exist_ok=True)
    for name, plate in plates.items():
        diff = np.abs(plate - sources[name]).max(-1)
        dimg = to_srgb(np.clip(np.repeat(diff[..., None], 3, -1) / 255.0 * 4, 0, 1))
        save_png(rdir / name.replace('.png', '-face-2x.png'),
                 row([label(crop2x(sources[name], FACE_BOX), PLATES[name][0]), label(crop2x(plate, FACE_BOX), name),
                      label(crop2x(dimg, FACE_BOX), 'difference x4')]), 'RGB')
    if args.write_assets:
        used = used_guids()
        templates = {name: ((OUT if folder == 'relight' else ART) / source).with_suffix('.png.meta')
                     for name, (source, folder) in PLATES.items()}
        created = []
        for name, plate in plates.items():
            path = save_png(OUT / name, plate, 'RGB')
            meta = Path(str(path) + '.meta')
            if not meta.exists():
                lines = templates[name].read_text(encoding='ascii').splitlines()
                lines = ['guid: ' + fresh_guid(used) if ln.startswith('guid:') else ln for ln in lines]
                meta.write_text('\n'.join(lines) + '\n', encoding='ascii', newline='\n')
                created.append(meta.relative_to(ROOT).as_posix())
            if not np.array_equal(load(path, 'RGB'), plate):
                raise SystemExit(f'{path} does not read back equal to the build output.')
        report['installed'] = {'folder': OUT.relative_to(ROOT).as_posix(), 'created_meta': created}
    (out_dir / 'report.json').write_text(json.dumps(report, indent=2), encoding='ascii')
    print(json.dumps(report, indent=2))
    print(f'Wrote {out_dir}')


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('stage', choices=['targets', 'build', 'plates'])
    parser.add_argument('--replace', action='store_true', help='targets: allow replacing earlier edit targets.')
    parser.add_argument('--from', dest='source', help='targets, plates: folder holding a build\'s textures (default: the installed Relight folder).')
    parser.add_argument('--output', help='build, plates: unused directory under Docs/work (default: a fresh bound-tree-relight-* or bound-tree-plates-* directory).')
    parser.add_argument('--write-assets', action='store_true', help='build, plates: also write the textures to Art/BoundTree/Relight.')
    parser.add_argument('--save-registered', action='store_true', help='build: also write the registered donors.')
    parser.add_argument('--donor-unlit', help='build: substitute a candidate plate unlit donor.')
    parser.add_argument('--donor-glow', help='build: substitute a candidate plate glow donor.')
    parser.add_argument('--donor-nymph-glow', help='build: substitute a candidate nymph glow donor.')
    args = parser.parse_args()
    if args.stage == 'targets':
        stage_targets(args)
        return
    if args.stage == 'plates':
        stage_plates(args)
        return
    if args.output:
        out_dir = (Path.cwd() / args.output).resolve()
        if WORK.resolve() not in out_dir.parents:
            raise SystemExit('--output must be under Docs/work.')
        out_dir.mkdir(parents=True, exist_ok=False)
    else:
        WORK.mkdir(parents=True, exist_ok=True)
        out_dir = Path(tempfile.mkdtemp(prefix='bound-tree-relight-', dir=WORK))
    for key, value in (('plate_unlit', args.donor_unlit), ('plate_glow', args.donor_glow), ('nymph_glow', args.donor_nymph_glow)):
        if value:
            DONORS[key] = (Path.cwd() / value).resolve()
    report = build(out_dir, args.write_assets, args.save_registered)
    print(json.dumps({k: report[k] for k in ('verification', 'resting', 'installed') if k in report}, indent=2))
    print(f'Wrote {out_dir}')


if __name__ == '__main__':
    main()
