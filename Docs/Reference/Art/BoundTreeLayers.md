# Bound Tree layers

Reference for the tree nymph encounter's scene art: what each texture and mask is for, how the layers register, and how to rebuild them. Artwork is under `Dice-Unity/Assets/Dice/Art/BoundTree` (paths below are relative to it unless stated). Coordinates are concept pixels with a top-left origin. These are the first cuts; lighting splits, moss, mist and creature layers are not built yet.

## Textures

Every texture is 941 x 1672 in the concept's frame, so all layers register by drawing them over the same rect; no offsets or scales are involved. They import like BoundTreeOriginal.png.meta (sRGB, bilinear, clamped, uncompressed, no mipmaps or non-power-of-two scaling, alpha is transparency).

| Texture | Format | Content |
| --- | --- | --- |
| BoundTreeOriginal.png | RGB | The approved scene concept. Source of every layer; never edited. |
| BoundTreeBackground.png | RGB | Clean plate: the concept with the nymph, her crown, bindings and shadow removed and the trunk base, roots, fallen leaves and swamp behind her reconstructed within 22 px of her silhouette (full weight within 14 px, so her idle and flinch motions uncover only reconstruction). Identical to the concept outside that declared region. |
| BoundTreeNymph.png | RGBA | The nymph: a generated derivative, not concept pixels. Her transparent extraction (Docs/Reference/Art/BoundTreeNymphDonor.png) registered to the concept frame, its alpha her matte (soft on hair strands and crown twigs) and its colour low-frequency matched to the concept on her visible pixels. It holds her hair, leaf crown, garment, the root bindings on her wrists and ankles, and the hem and feet the scenery hides in the concept: the hem resting on the ground and both feet with their toes. Roots lying on the ground stay on the plate. |
| BoundTreeFaceless.png | RGB | BoundTreeBackground.png with the spirit's face (eyes, brows, nose ridge, cheekbones, split mouth and its green glow) replaced by plain split bark, for the victory fade. Identical to BoundTreeBackground.png outside the declared face region. |
| BoundTreeFaceMask.png | RGBA, white | Alpha 1 over the whole face transfer region with a 2 px soft edge outside it. |

Draw order: BoundTreeFaceless.png, then the face layer (BoundTreeBackground.png through BoundTreeFaceMask.png alpha, for example SourceMaskedImage.shader, which reads the mask's alpha), then BoundTreeNymph.png. With the face layer at full opacity this reproduces BoundTreeBackground.png exactly, because the mask is 1 wherever the two differ; fading its opacity to 0 sinks the face into the bark. The nymph composited over the plate reads as the concept at rest; it differs from it where her generated extraction differs in fine detail and where she completes what the concept hides (the hem and toes over the leaves), within the error reported by the script.

## Generated edits

The plate and faceless reconstructions follow "Generated edits" in [HauntedMerchantHostLayers.md](HauntedMerchantHostLayers.md): the generated image is a donor, registered to the concept frame by an affine fit, and only pixels inside the declared region are transferred, with the donor's low-frequency colour matched to the surrounding image (masked Gaussian averages at 24, 10 and 4 px, coarse to fine) and a linear blend from full weight near the edited shape to zero at the declared edge. Pixels outside a declared region stay byte-identical. The nymph is a generated transparent extraction, made the way MerchantHost.png was (see "Scene plates and sources" there), and installed whole after registration and colour matching.

| Donor | Edit target | Installed as | Region |
| --- | --- | --- | --- |
| Docs/Reference/Art/BoundTreeNymphDonor.png | concept box (96, 576)-(416, 1056) | BoundTreeNymph.png | her alpha |
| Docs/Reference/Art/BoundTreeBackgroundDonor.png | concept box (20, 520)-(460, 1180) | BoundTreeBackground.png | within 22 px of the nymph silhouette, full weight within 14 px |
| Docs/Reference/Art/BoundTreeFacelessDonor.png | concept box (290, 100)-(710, 730) | BoundTreeFaceless.png | within 12 px of the face polygon, full weight within 4 px |

Each edit target is a Lanczos 1024 x 1536 upscale of its concept box. Registration maps donor pixels from concept pixels by `donor = M (p - c) + t` (c the box centre), fitted by normalized cross-correlation of high-passed luminance on a 3 px grid inside the box: for the nymph on her interior (at least 6 px inside her silhouette under the upscale's own mapping), for the plate and face donors excluding pixels within 26 px (nymph silhouette) or 16 px (face polygon) of the edit. The fitted values are in the script's report.json. The prompts are in [prompts/](prompts/README.md); the other candidate of each generation and the edit targets are working files under `Docs/work/bound-tree-layers`.

## Nymph

- Matte: the donor's own alpha, rescaled so its levels 2 and 250 map to 0 and 1 (the extraction's interior sits at 250 to 253), warped with the registration. Hair strands and crown twigs keep the extraction's soft alpha.
- Colour: the donor's colour, unpremultiplied where her alpha is above 0.05 and spread outward over a few px (normalized Gaussian, sigma 3 px) so bilinear filtering at her edge does not mix in black. Its low-frequency colour is matched to the concept (sigmas 24, 10 and 4 px) on her visible pixels: at least 4 px inside her alpha-0.5 silhouette, excluding pixels that still differ from the concept by 40 levels or more after a 24 px match (leaves in front of her hem and feet in the concept). Elsewhere, including the completed hem and toes, the match is extrapolated from those pixels.
- Silhouette: every pixel with alpha at or above 0.02. The plate is reconstructed at full weight within 14 px of it; the motion margin the script checks is 12 px, which covers the 1.03 scale about her seat (about 11 px at the top of her head) and small offsets.

## Rebuilding

`Tools/Presentation/cut-bound-tree-layers.py` (Python 3.9+, standard library only) rebuilds all four textures from the concept, the three donors and BoundTreeLayers.json:

```
python Tools/Presentation/cut-bound-tree-layers.py                 # fresh Docs/work/bound-tree-layers-* folder
python Tools/Presentation/cut-bound-tree-layers.py --write-assets  # also replaces the installed textures
```

The output folder holds the textures under `layers/`, review images under `review/` (plate, concept beside nymph over plate, nymph on grey and checker, matte, composite error, the nymph moved 4 px left and right, 6 px up and scaled 1.03 about her seat over the plate, faceless trunk, face over faceless at full and half opacity, and a difference image for each transfer with the declared edge in cyan) and `report.json` with the registrations and checks. To change a region, edit its polygon or distances in BoundTreeLayers.json, rebuild into Docs/work and review before writing assets. A replacement donor must be an edit of the same target box at 1024 x 1536 (RGBA with a transparent background for the nymph).
