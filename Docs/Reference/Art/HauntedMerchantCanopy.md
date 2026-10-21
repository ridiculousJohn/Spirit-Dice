# Canopy prototype artwork

Generated with the built-in image generation tool on 2026-10-20. These are layered prototype assets for the merchant scene.

- Fabric: `Dice-Unity/Assets/Dice/Art/HauntedMerchant/Canopy.png` (941 x 1671, transparent).
- Underlay: `Dice-Unity/Assets/Dice/Art/HauntedMerchant/MerchantCanopyPlate.png` (941 x 1672).
- Source for both edits: `Dice-Unity/Assets/Dice/Art/HauntedMerchant/MerchantAmbientPlate.png`.
- Both layers use the same 540 x 960 authored rectangle. The extracted fabric differs by one pixel in canvas height; its registration and support alignment are checked in the composed Unity preview.
- Original cup-prototype artwork is retained. The ambient preview now references the canopy underlay, with the fabric and hanging cup as separate child prefabs.

## Fabric extraction prompt

Use case: background-extraction. Asset type: registered transparent canopy layer for Unity 2D mesh animation. Edit target is the supplied941x1672 portrait. Extract ONLY all visible fabric of the cart canopy: the faded red and cream striped upper canvas, its ragged hanging fringe, the dark fabric underside and the torn hanging fabric panel at the canopy's right rear. Preserve every fabric pixel's original position, scale, perspective, tears, holes, shape and lighting as closely as possible. Keep EXACT same full941x1672 canvas and camera framing; this is a registered layer, NOT a cropped or centered isolated product. Everything except the canopy FABRIC must be fully transparent: remove merchant, wooden poles/struts, lantern, ropes not part of cloth, shelves, bottles, trees, swamp and table. Leave openings and tears transparent. Preserve the original top-left clipping at the image edges. Do not invent extra cloth, do not recenter, do not zoom or resize the canopy. Real alpha transparency, no painted checkerboard, no shadows on background.

## Underlay prompt

Use case: precise-object-edit. Asset type: clean background plate behind an animated canopy. Edit target is the supplied941x1672 merchant scene. Remove ALL canopy FABRIC from the upper left: the faded red/cream striped canvas, dark fabric underside and ragged hanging rear flap, including tiny hanging scraps. Reveal the wooden cart support frame, swamp trees and night sky behind the removed cloth. Preserve the existing cart poles and supports, especially the upper RIGHT canopy attachment at pixel approximately(650,235) and the upper LEFT post. Preserve lantern at left, shelf bottles, wares, merchant identity and pose, cart wheel, mud, table, candle, swamp and lighting exactly. Keep original camera, image size941x1672 and registration. Fill newly revealed pixels naturally; no replacement cloth, no hanging cookware. This is an UNDERLAY for compositing the original canopy fabric back on top as an animated layer, so preserve every visible pixel outside the removed cloth and expose enough background at the ragged edges for slight cloth movement. No other changes, no text.
