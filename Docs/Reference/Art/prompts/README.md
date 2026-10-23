# Haunted merchant generation prompts

One file per generated edit used for installed merchant art or for a preserved donor. Each file holds the prompt text, then a note naming the input image(s), the donor and the asset it was transferred into. Files marked (summary) hold a recorded summary because the verbatim prompt is not preserved. Asset paths are under `Dice-Unity/Assets/Dice/Art/HauntedMerchant` unless stated.

- [MerchantBackground-removal.txt](MerchantBackground-removal.txt) -> intermediate clean plate for MerchantBackground.png (summary)
- [MerchantBackground-correction.txt](MerchantBackground-correction.txt) -> MerchantBackground.png (summary)
- [MerchantHost-extraction.txt](MerchantHost-extraction.txt) -> MerchantHost.png (summary)
- [ProtectiveCandle.txt](ProtectiveCandle.txt) -> ProtectiveCandle.png (summary)
- [MerchantHeadRepaired-donor.txt](MerchantHeadRepaired-donor.txt) -> Docs/Reference/Art/HauntedMerchantHeadRepairDonor.png, transferred into MerchantHeadRepaired.png (summary)
- [MerchantTorsoAnimation-donor.txt](MerchantTorsoAnimation-donor.txt) -> Docs/Reference/Art/HauntedMerchantTorsoRepairDonor.png, transferred into MerchantTorsoAnimation.png (summary)
- [MerchantBlinkEyes-donor.txt](MerchantBlinkEyes-donor.txt) -> blink donor, transferred into MerchantBlinkEyes.png
- [MerchantAmbientUnlit.txt](MerchantAmbientUnlit.txt) -> MerchantAmbientUnlit.png (summary)
- [MerchantLanternOnly.txt](MerchantLanternOnly.txt) -> MerchantLanternOnly.png (summary)
- [MerchantAmbientUnlit-table-clear.txt](MerchantAmbientUnlit-table-clear.txt) -> lower-left tabletop of MerchantAmbientUnlit.png and MerchantLanternOnly.png
- [MerchantHostUnlit-relight.txt](MerchantHostUnlit-relight.txt) -> Docs/Reference/Art/HauntedMerchantRelightUnlitDonor.png, transferred into the Relight/*Unlit.png merchant textures
- [MerchantHostLanternLit-relight.txt](MerchantHostLanternLit-relight.txt) -> Docs/Reference/Art/HauntedMerchantRelightLanternDonor.png, transferred into the Relight/*LanternLit.png merchant textures
- [CanopyUnlit.txt](CanopyUnlit.txt) -> Docs/Reference/Art/HauntedMerchantRelightCanopyUnlitDonor.png, transferred into Relight/CanopyUnlit.png
- [HangingCupUnlit.txt](HangingCupUnlit.txt) -> Docs/Reference/Art/HauntedMerchantRelightCupUnlitDonor.png, transferred into Relight/HangingCupUnlit.png
- [HangingCupLanternLit.txt](HangingCupLanternLit.txt) -> Docs/Reference/Art/HauntedMerchantRelightCupLanternDonor.png, transferred into Relight/HangingCupLanternLit.png
- [PropsRemoved-PotPanCage.txt](PropsRemoved-PotPanCage.txt) -> Props/*PropsRemoved.png behind the pot and pan and over the removed cage
- [HangingPot-completion.txt](HangingPot-completion.txt) -> hidden part of Props/HangingPot*.png
- [PropsRemoved-ShelfBottle.txt](PropsRemoved-ShelfBottle.txt) -> Props/*PropsRemoved.png behind the bottle
- [PropsRemoved-TrunkLid.txt](PropsRemoved-TrunkLid.txt) -> Props/*PropsRemoved.png behind the trunk lid

Prompts committed elsewhere:

- [../HauntedMerchantConceptPrompt.txt](../HauntedMerchantConceptPrompt.txt) -> MerchantOriginal.png, the original scene concept
- [../HauntedMerchantOriginalOverhangPrompts.md](../HauntedMerchantOriginalOverhangPrompts.md) -> background fill behind the canopy overhang (Docs/Reference/Art/HauntedMerchantOverhangFill.png)
- [../HauntedMerchantAmbient.md](../HauntedMerchantAmbient.md), "Hanging cup prompt" -> HangingCup.png

## Bound Tree

Same format; asset paths are under `Dice-Unity/Assets/Dice/Art/BoundTree`. The layers are described in [../BoundTreeLayers.md](../BoundTreeLayers.md).

- [BoundTreeNymph-extraction.txt](BoundTreeNymph-extraction.txt) -> Docs/Reference/Art/BoundTreeNymphDonor.png, registered and colour-matched into BoundTreeNymph.png
- [BoundTreeBackground-nymph-removal.txt](BoundTreeBackground-nymph-removal.txt) -> Docs/Reference/Art/BoundTreeBackgroundDonor.png, transferred into BoundTreeBackground.png
- [BoundTreeFaceless-face-removal.txt](BoundTreeFaceless-face-removal.txt) -> Docs/Reference/Art/BoundTreeFacelessDonor.png, transferred into BoundTreeFaceless.png
- [BoundTreeAmbientUnlit-relight.txt](BoundTreeAmbientUnlit-relight.txt) -> Docs/Reference/Art/BoundTreeRelightUnlitDonor.png, transferred into Relight/BoundTreeAmbientUnlit.png, Relight/BoundTreeFacelessUnlit.png and Relight/BoundTreeNymphUnlit.png
- [BoundTreeGlowLit-relight.txt](BoundTreeGlowLit-relight.txt) -> Docs/Reference/Art/BoundTreeRelightGlowDonor.png, transferred into Relight/BoundTreeGlowLit.png
- [BoundTreeNymphGlowLit-relight.txt](BoundTreeNymphGlowLit-relight.txt) -> Docs/Reference/Art/BoundTreeRelightNymphGlowDonor.png, transferred into Relight/BoundTreeNymphGlowLit.png
- [../BoundTreeConceptPrompt.txt](../BoundTreeConceptPrompt.txt) -> BoundTreeOriginal.png, the scene concept
