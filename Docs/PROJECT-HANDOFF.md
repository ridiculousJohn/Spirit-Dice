# Spirit Dice: start here

Current state: October 22, 2026. This is a working contract and current-state summary, not a development diary. Verify relevant code, assets, and fresh Git status before acting.

## Working contract

Read this document, [presentation authoring](PRESENTATION-AUTHORING.md), and [Tools/README.md](../Tools/README.md).

- Follow John's approved design. Do not invent screens, confirmation steps, celebrations, delays, rewards, or flow changes. Preserve accepted visuals and pacing unless the requested change requires otherwise.
- Inspect first; explain the cause and smallest complete proposed fix. Wait for implementation approval unless John has already directed the work. Agreement with an observation is not authorization. Historical proposals are not authorization.
- Keep work within scope. Report incidental issues separately. Do not add speculative safeguards, fallback systems, or unrelated refactors.
- Finish authorized implementation and relevant validation without repeatedly asking to continue. Keep progress visible during long work. If blocked, identify the actual blocker and what remains; do not present unfinished work as complete.
- John is a Unity expert. Use normal Unity mechanisms and explain concrete tradeoffs, not programming fundamentals.
- All workspace changes are user-owned, including generated assets and test output. Never restore, discard, delete, or roll back changes without explicit authorization for that action. No worktrees unless requested.
- Preserve existing identifiers and useful comments. Use ASCII comments and strings. Never abbreviate "checkpoint."
- Tests support evidence, not design authority. Check actual player-visible behavior and rendered layouts where relevant. Do not add arbitrary waits to make failures disappear, weaken checks to match a bug, or equate structural assertions with visual correctness. Run appropriate checks, then stop when the requested work is verified.

## Current workspace and outstanding work

- Root: C:/Users/jsp73/Projects/Dice; Unity project: Dice-Unity; branch: main.
- Unity: 6000.6.6f1 at C:/Program Files/Unity/Hub/Editor/6000.6.6f1/Editor/Unity.exe.
- Entry scene: Dice-Unity/Assets/Scenes/Application.unity; menu: Dice > Open Application.
- Confirming a permanent charm unlock persists the selected unlock and proceeds directly to the next offer.
- Open Application opens the existing scene without generating assets. Configure Modifier Addressables supports authored content and Android builds. Edit scenes and prefabs directly; inspect and report missing assets rather than silently recreating them.
- The installed Android build does not include the merchant encounter. On-device gameplay is not visually verified; do not assume workspace changes are installed.
- Chapter 2's merchant encounter is playable through the campaign (see "Merchant encounter" below). Assets/Scenes/AmbientMotionPreview.unity is the merchant art preview scene. Player-built decks, equipment and the other Chapter 2 encounters remain future work.
- This handoff does not authorize further code changes, a commit, or an Android build. Follow John's next request.

### Working practices

- Layout or presentation-only changes: verify with one targeted rendered capture of the affected encounter(s) and review the images. Run full EditMode or PlayMode suites only when John asks.
- Unity CLI runs need the editor closed; if it is open, ask John to close it. Give every CLI test run a timeout.
- Rendered PlayMode tests that wait for rendered frames (WaitForEndOfFrame, e.g. SpiritFeedbackPlayTests) hang under -batchmode; run them windowed. Windowed runs on this machine currently fail on an editor ProjectBrowser/ExposablePopupMenu NullReferenceException counted as an unhandled log; batch-safe fixtures and captures run with -batchmode and graphics. Rendered runs while the machine is in Modern Standby stall frames about 0.5 s and fail timing-sensitive tests; keep the display awake.
- QualitySettings.asset stays at Ultra antiAliasing 0; URP rewrites it during runs and it has no effect.
- Image generation: use the project's imagegen skill (C:/Users/jsp73/.claude/skills/imagegen), which drives Codex's built-in image tool through `codex exec` and copies the results into the project. Generated edits drift, so install them only through registration and masked transfer, never wholesale; keep donors and prompts with the art provenance.
- Play-mode lighting control: with the game running in the editor, the encounter screen's Inspector (Editor/SpiritEncounterViewEditor.cs) shows "Testing: scene lights" with an On toggle and a 0-2 slider per scene light (merchant: light 0 lantern, light 1 candle) and "Release overrides". A held override replaces the ambient activity's multiplier; it is not saved and is released when the table is reconfigured or presentation resets. In Edit Mode, Dice > Encounter Lighting Preview has per-light sliders.

### Merchant encounter (The Unquiet Cart)

Design, script and rules are under "Chapter 2 and future mechanics" below. Details of the merchant art, masks, relighting sources and props: [merchant asset details](Reference/Art/HauntedMerchantHostLayers.md). Authoring: [presentation authoring](PRESENTATION-AUTHORING.md) ("Lit layers", "Ambient activity").

Engine and mechanical content:
- Table pieces with pluggable interaction kinds (Engine/TablePieces.cs; first kind: position), `InteractWithPieceAction`, `DieInterferenceRuleDefinition` (haunting) and `BayWardRuleDefinition` (Engine/DieInterference.cs), a separate rule random stream (seeded only for encounters with a dice-disturbing rule, otherwise 0), disturbed-dice masks; save format 6 / rules version 8 with codec defaults for older formats. UnityContent authoring assets: PositionInteractionAsset, DieInterferenceRuleAsset, BayWardRuleAsset; catalog `pieces`.
- `encounter.merchant.unquiet_cart` in Content/Mechanical/Standard.asset uses Moth's ten-stage curve as a placeholder; reward pools copy Moth's ordinary pools plus the three merchant families; six Base/Mult unlocks at milestones 3/6/10. MerchantHaunting.asset (70%, reroll/flip 1:1, both triggers), MerchantCandleWard.asset, MerchantCandlePosition.asset (neutral start, movable during the hand), ProgressionRewards/MerchantFamilyDiscovery.asset.
- Chapter 2 content in Content/Presentation/HauntedMerchant: DrownedRoadChapter.asset ("THE DROWNED ROAD", MerchantBackground.png art), MerchantJourney.asset (requires story.fox.chapter-one-return), Poltergeist.asset and CandleWard.asset rule cards with icons PoltergeistRuleIcon.png and CandleWardRuleIcon.png. Registered in SpiritHouse/SpiritJourneyCatalog.asset. Lines beyond the accepted script (invitation line, continue labels, discovery flavour sentence, abandon text, completion clue, candle card wording) are editable placeholders.

Scene, layout and HUD:
- Scenes/MerchantEncounter.unity (Addressables group "Dice Presentation Scenes", key dice.scene.merchant), table presentation HauntedMerchant/MerchantUnquietCartTable.asset, merchant-only compact tray (MerchantTrayPresentation.asset, MerchantTrayGeometry.prefab, Rigs/MerchantTrayPresentationRig.prefab and its render texture), MerchantHost.prefab as host, UI/MerchantHostSigil.prefab (the HUD requires a host sigil).
- The art is zoomed 1.12 so the merchant's face clears the HUD; narrow screens crop the right side. The board is centered, short and spans the table width (bay pitch 1.25, 1024x512 tray texture). The candle's warded positions sit above each die (bayPlacement (0.5, 1.08)), clear of the HANDS/REROLLS row; its neutral home is the table's top-left corner; it is scaled 0.86.
- The "Merchant Table Pieces" layer is a child of the HUD's PlayingPanel directly after DiceTrayFrame, so the candle draws above the tray and dice, below the score panel, buttons, stage intro and every popup, and hides with the play area. TargetPanel and the HANDS/REROLLS row precede PlayingPanel in the shared HUD, so a candle being dragged across them draws over them; resting positions never touch them. Reordering the shared HUD would fix it but changes draw order in every encounter.
- HUD in every encounter: the selection label is empty while choosing dice; selected dice are marked by the tray outline and frame highlight; the score panel uses SpiritGameplayHud.prefab ScorePreview top anchor .392. Reroll help text may be designed later elsewhere.

Merchant host:
- Layers (torso, head/hat, two forearms, blink overlay) share one breathing deformation mesh (Ambient/MerchantBreathMesh.asset) through SpiritHostPortrait timelines: a "living still" with gentle breathing and blinks. Tools/Presentation/author-merchant-presentation.py generates MerchantHost.asset; re-run it rather than hand-editing. Validate moving poses as well as rest alignment.
- 2D rigging was considered and deferred: Unity's 2D Animation and 2D PSD Importer packages (not installed) with bones/weights generated by editor script, rendered to a render texture because Sprite Skin does not deform UI images; estimated half a day to a day per character.

Lighting:
- Lit layers (Presentation/SceneLitLayer.cs, SceneLitLayerMaterial.cs, SceneLitImage.cs, Shared/SceneLitImage.shader + SceneLitImage.cginc) light foreground images per pixel from the scene lights: an unlit base texture; each fixed light (the lantern) adds max(lit reference - base, 0) with the background's ResponseScale math so it dims and flickers identically; each moving light (the candle) is computed per pixel from distance using a depth map and a frame-to-rig mapping. MapRect selects the sub-rectangle of full-frame response/depth maps for cropped images. Hosts opt in per layer; Fox, Owl and Moth keep the uniform receiver tint. The encounter view feeds lit layers from its own SceneLightingState.
- UGUI draws every pass of a UI material's shader, so SceneLitImage.shader keeps exactly one pass; the preview pass used by tests is the hidden SceneLitImagePreview.shader.
- The merchant, canopy (striped cloth from CanopyUnlit/CanopyLanternLit; the background plates paint a plain roof and must not be used for it), hanging cup and props are lit layers. Relight textures are in Art/HauntedMerchant/Relight, built by Tools/Presentation/author-merchant-relight.py from donors in Docs/Reference/Art. Candle on the merchant: Strength 50, DepthRange (3.5, 1.5). The lit merchant reads cooler than the concept art because much of the painting's warmth was an overall grade.
- The candle's light is a tight pool: CandleLightFootprintCentered.png and CandleLightResponseCentered.png are built by Tools/Presentation/author-candle-light-footprint.py from the preserved sources in Docs/Reference/Art (REACH 0.8); light 1 ReceiverRange 8. The candle body is not lit by the scene lighting.

Props, cup and ambient activity:
- Movable props (Art/HauntedMerchant/Props, props.json, Tools/Presentation/cut-merchant-props.py): HangingPot and HangingPan (separate swinging pieces, pot drawn first), ShelfBottle (rocks on two base contacts), TrunkLid (thumps on its hinge). The background everywhere uses MerchantAmbientUnlitPropsRemoved.png and MerchantLanternOnlyPropsRemoved.png. Props are RawImages with SceneLitImage and Ambient/<Name>Lit.mat in both MerchantEncounter.unity and AmbientMotionPreview.unity, drawn before the canopy, cup and merchant.
- The hanging cup is lit through a SceneLitImage and HangingCupLit.mat added on its scene instances; HangingCup.prefab stays RawImage-only. Its idle sway animates its Swing child while ambient motion moves its root.
- Ambient activity is reusable and presentation-only: Presentation/EncounterAmbientActivity.cs (own random, ticked by SpiritEncounterView, pauses on focus loss), EncounterAmbientActivityAsset.cs (light and prop actors, authored on EncounterTablePresentationAsset.ambientActivity; prop actors bind by id in the encounter screen's Ambient Props list), AmbientMotionAsset.cs with Swing/Wobble/Thump kinds. A motion records the prop's rest pose when it starts, writes rest plus offset (RectTransforms as anchored positions) and writes rest back when it ends. Hand start begins the activity; each disturbance makes the lantern stutter and one prop jolt; a blocked attempt adds nothing; victory and abandonment settle everything with the lantern steady; defeat knocks one prop hard and stops. It keeps running through stage intros.
- Merchant values (HauntedMerchant/MerchantAmbientActivity.asset): lantern outages every 20-40 s, dark 1.5-4 s, sputters of 10-16 flickers of 0.06-0.3 s each way, disturbance stutters of 6-10 flickers; each prop idles every 8-20 s with swings and rocks of about 7-10 degrees and a visible lid hop; disturbance strength 1.6, defeat 2.5. Flicker steps cut between levels; fading between levels is a possible refinement.

Audio:
- Poltergeist voice (Presentation/Shared/SpiritAudio/Poltergeist, ethereal treatment): two disturbance giggles, a blocked "Hmph!", three ambient giggles played at random (volume 0.3, 25-60 s). Provenance: Docs/Reference/Audio/pixabay-poltergeist/provenance.json; recipe Tools/Audio/process_poltergeist_audio.ps1. Ambient one-shots share the four cue voices, so a busy moment can drop one.
- Prop sounds (Presentation/Shared/SpiritAudio/MerchantProps, 21 clips sliced from five Pixabay recordings, light room tail): prop actors carry Idle/Hit/Defeat clip lists (defeat falls back to hit), and the activity plays a rumble Burst once per disturbance. They play on three Prop voices in SpiritEncounterScreen.prefab, never the cue voices, under the encounter's mute, focus and pause, without repeating a prop's last clip. Provenance: Docs/Reference/Audio/pixabay-merchant-props/provenance.json; recipe Tools/Audio/process_merchant_prop_audio.ps1. The Chest Slam (lid defeat) recording is clipped at the source.

Disturbance playback:
- A forced reroll rolls the die; a flip turns it to the opposite face over 0.5 s; the merchant reacts, a giggle plays, the lantern stutters and a prop jolts. On Play Hand, playback waits 0.25 s after the commit before a disturbance or blocked attempt, and the score panel shows the rank of the hand about to be scored once the disturbed die lands. A reroll's disturbance follows its roll at once.
- A blocked attempt flares the candle (flame 1.45 wide at full height, light 3x) and plays Flare Sparks (SpiritFlareSparks on the candle: a warm glow and ten near-white sparks thrown mostly sideways), plus the grumble.
- Captures: MerchantReviewCapturePlayTests and MerchantDisturbanceCapturePlayTests (flip after reroll, forced reroll on Play Hand, block, candle comparison stills) run only when DICE_SPIRIT_CAPTURE_ROOT is set.

Open items:
- Tune the merchant's stage targets, hands and rerolls (Moth's placeholder curve) and possibly the 70% haunting chance, which John thinks may be a bit high.
- Not yet reviewed by John at length: prop motion strength, the prop sounds in game, and the lit merchant's look. The lantern changes the face and shirt little while the candle sits at its neutral home; candle Strength/DepthRange may need tuning in the lighting preview. The cup's lantern warmth leans slightly rose; a slight dark speckle remains under the left hat brim in the unlit textures.
- The merchant's acknowledgment reaction is barely visible at phone size; John left it for now, as he did the flip speed.
- The full EditMode and PlayMode suites have not been run against the current merchant work; the re-capture of the disturbance clips and candle comparison stills is pending.

## Architecture and coding boundaries

Mechanics, mechanical content, progression, presentation, and persistence remain separate. The engine must support a different skin or a larger game. Mechanical content contains rules and values, not display names, artwork, dialogue, or audio.

Each modifier has its own mechanical ScriptableObject and separate presentation asset. Conditions have individual evaluation classes with companion helpers where appropriate. Do not add central evaluation switches or presentation lookup dictionaries. Use explicit references or Addressables; nothing belongs in Resources. Modifier discovery labels are dice.modifiers and dice.theme.spirit-house.modifiers.

Every screen, popup, card, button, host and tray hierarchy is authored in a scene or prefab. Runtime code binds data/state, animates, and instantiates explicitly referenced prefabs for dynamic lists. Do not construct replacement hierarchies, choose styles by object name, or overwrite authored layout/typography during binding. Edit assets in place, preserving GUIDs, references and unrelated overrides. Unity Editor API operations through CLI/tooling are acceptable; retained generators are not necessary.

Before adding a system, identify its state, responsibility, dependencies and lifetime, then decide whether it belongs to an existing owner. Do not accumulate unrelated responsibilities in a controller/view or extract classes without a useful boundary. Shared encounter logic must not branch on fox/owl/moth or specific encounter IDs.

Paths below are relative to Dice-Unity/Assets/Dice.

| Owner | Responsibility |
| --- | --- |
| Engine/EncounterSession.cs, conditions, reward generator | State, deterministic resolution, modifier evaluation, offers |
| UnityContent/ModifierAsset.cs, catalog and loader | Authored mechanics and labeled discovery |
| App/DiceBootstrap.cs, DiceApplicationConfig.cs | Content/theme loading and lifetime before presentation is ready |
| App/SpiritApplicationPresentation.cs | Addressable frontend/encounter scene lifetime and binding; state remains in Application |
| App/SpiritEncounterController.cs, EncounterApplication.cs | Live run, actions, busy state, coordinated transitions and notifications |
| App/EncounterJourneyNavigation.cs | Journey/chapter selection, prerequisites, beat transitions and follow-up focus; no persistence/gameplay mutation |
| App/PlayerProgression.cs | Profile, grants, visits/completion and ordered run-before-profile writes; no view/controller reference |
| App/SpiritCampaignController.cs, CampaignSaveSlots.cs, CampaignRecoveryStore.cs | Main menu, slot mapping and recoverable campaign storage |
| Presentation/SpiritEncounterView.cs | Coordinates authored screen components and lifecycle |
| Presentation/SpiritGameplayHud.cs, SpiritScorePlayback.cs | HUD binding and ordered resolution presentation |
| Rewards, owned charms, popup, collection, journey and ending components | Their respective screen state and serialized references |

Install replacement run state and refresh caches before publishing notifications. Journey selection can differ from the retained run; visiting the board/story must not destroy a resumable encounter. Each presentation component owns its lifecycle; hiding gameplay must not reset an ending in progress.

## Current game design

The Old Road chapter is Fox, Owl, Moth, then a story-only return to the fox. Three encounters have ten stages each; the standard 25-stage encounter remains separate. Completed encounters remain replayable. Targets and tier weights are provisional.

| Encounter | Always-active rule |
| --- | --- |
| The Last Lantern / fox | First reroll of each hand spends no paid reroll |
| The Watchtower / owl | Adds one multiplier per distinct scored face |
| The Unmarked Fork / moth | Favored face cycles 1 through 6 by encounter hand count; matching scored dice add +2 multiplier each |

Chapters supply ordered journey references, title, description and artwork. Journey prerequisites determine availability; chapters do not duplicate progress flags. The board defaults to the latest available incomplete chapter, or latest available chapter if all are complete. Earlier chapters remain selectable. The return story saves gifts and completion on final Continue; interrupted/repeated stories are safe and grants are idempotent.

Main menu order is **Continue, Load Game, Chapter, New Game, Options**. Continue is disabled without a resumable save. Load Game selects one of three independent campaigns; empty slots are disabled. Chapter restores the saved run behind the board so "Continue at the table" resumes it. New Game selects a slot and warns before replacing an occupied campaign. Options remains a placeholder.

### Charms and progression

- Run offers use encounter-authored ordinary pools plus optional encounter-only additions, with tier weighting. Future player decks replace only the ordinary pool; deck building is not implemented.
- Milestones at stages 3, 6 and 10 offer up to three unowned individual charms from an explicit UnlockRewardModifierIds list. Select one permanent unlock, not a family. Show fewer if fewer remain; exhausted lists show "All charms collected" and Continue.
- Ownership persists after defeat/restart. It makes a charm eligible for applicable ordinary pools; it does not equip it. Pending choices/continuations are saved. Final unlock selection precedes the ending.
- Fox awards six Base/Mult variants across Open Gate, Still Reflection and Rising Sun. Owl/moth also use six Base/Mult variants from their authored milestone families. Some other locked variants have no current unlock source; do not silently add sources.
- Family discovery and individual ownership are separate. Starter/owned families are implicitly discovered. Invitations discover their authored unlock families; fox also grants Open Gate / Base. The return reveals Sheltering Branch and Boundary Stone and grants their authored Base and Mult charms.
- Story/beat grants use ProgressionRewardAsset; repeated grants are idempotent. Failed grant persistence blocks continuation without granting ownership. Presentation references do not grant charms.
- Only moth currently adds an encounter-only family: Lantern Seekers, triggered by at least two favored-face dice. Its introduction explains the additions; encounters without additions skip that screen. These are excluded from permanent Collection.
- Collection filters: Owned (default), Discovered, All. One row per family shares its trigger and shows four variants with ownership/availability. Undiscovered families are anonymous locked rows. Charms shows the current run's equipped modifiers.

### Chapter 2 and future mechanics

These are design decisions and open directions, not implementation authorization. Chapter 2, THE DROWNED ROAD, follows the return to the fox and the old bridge crossing; its final revelation remains open.

1. Haunted traveling merchant: a merchant has set up his wares cart on a road beside a dark swamp. A poltergeist haunts the cart and interferes while the player plays with him, using the haunting rule below. The merchant leads the player into the swamp afterward. This is the first encounter to develop; the accepted working dialogue and rewards are below.
2. Bound tree nymph: on a small patch of dry land in the swamp stands a huge black, withered tree. A sobbing dryad/tree nymph sits at its base, trapped and tormented by the evil spirit possessing her tree. She reveals that the spirit is bound by challenge terms; it would not volunteer this information. Once the player challenges it, the spirit must comply and abide by the result. Play against the spirit using binding; victory banishes it, releases the tree and nymph, and lets her provide information needed to continue. The nymph's knowledge and her mid-encounter gift of charms make her an active participant in her rescue. The approved design is under "Tree nymph encounter" below.
3. Rot encounter: the third encounter uses rot. A swamp hag is a favored starting possibility, with a decaying hut, preserved things, and knowledge of the traveler's passage or the route onward. She can be unsettling and transactional rather than another spirit to banish. Her exact identity, location, bargain, and revelation remain open.

Each encounter should advance the search for the traveler as well as lead to the next location. The chapter's final revelation is undecided. Carry-over has no assigned place in this three-encounter outline; do not force it into the chapter or implement it as part of the merchant encounter.

John explicitly chose to flesh out the merchant's story and exact rules before implementation. He accepted this working premise: the tired, frightened merchant freely confirms seeing the traveler enter the swamp and can guide the player along its obscured paths. The game-obsessed poltergeist meddles in games; beating the merchant despite its cheating buys a temporary respite long enough for him to leave the cart and guide the player. It is not permanently banished. The merchant lends a protective candle already used for his wares. Exact dialogue remains a draft.

#### Merchant reward charms

John selected Draining Bowl (played dice sum below the opening sum), Returning Echo (same hand rank as the previous played hand), and Counted Coins (played dice sum divisible by five) as the merchant's reward families. Use their existing Base and Mult variants as six permanent unlocks, with ten stages and unlock milestones at 3, 6, and 10. Stage targets remain to be tuned. These six assets already require permanent ownership; giving them an unlock source does not itself reduce the current starter pool.

John considers the beginning charm pool too large. As encounters are added, prefer using existing families as progression rewards where suitable, and consider moving appropriate starter families behind later unlocks to simplify early play. This is a progression-design direction; specific starter-pool removals have not been selected or authorized.

#### Merchant scene and working script

Working title: The Unquiet Cart. The following dialogue is accepted as the working draft.

The merchant is a human traveler wearing a formerly white shirt, suspenders, brown trousers, and a battered hat. The scene is nighttime on a muddy road beside a dark swamp/marsh, with tall black trees, old moss, and sagging limbs. His broken-down cart has a broken wheel and a formerly colorful canopy that is now torn and dingy.

- Arrival: the road narrows beside dark water. A merchant's cart stands beneath a sagging canvas cover. A hanging cup turns against the breeze as the player approaches.
- Merchant: "Looking for someone? Yes, they passed this way. Left the road and went into the marsh." Then: "I know the path they took. But I can't leave my wares with this thing awake." Something knocks beneath the table; he presses a hand against it.
- Bargain: "It likes watching us play. Likes cheating more." Then: "Beat me despite its meddling, and it sulks for a while. Long enough for me to show you the way."
- Candle: he takes a short candle from beside a wooden chest. "This keeps its fingers off things. Set it beside what you want left alone." Keep the mechanical explanation separate from dialogue: drag the candle onto a bay to protect that die, move it whenever the game accepts input, and expect possible interference after Reroll or Play Hand. The invitation introduces the three selected families using existing discovery and milestone-unlock behavior.
- Victory: the hanging cup settles; the merchant pauses. "Quiet. At last." Then: "Come. There's dry ground around the great tree. That's where I saw your traveler heading." He secures the cart and leads the player toward the swamp.
- Defeat: a sharp knock comes from beneath the board. "Pleased with itself, isn't it?" Then: "Take a breath. We can try again."

#### System boundaries and protection

John emphasized that these are reusable systems. Haunting belongs within the existing always-active encounter-rule system used by Fox, Owl, and Moth, extending its action-resolution behavior as needed. Do not build a separate merchant-specific haunting framework. The encounter supplies its rule configuration; the merchant and poltergeist supply narrative and presentation.

Protection is a separate capability reusable independently of haunting. The candle is the merchant's presentation of a ward, not a mechanic that requires knowledge of the merchant. Protection adds no hand phase: the opening roll stays automatic, and interacting with the candle is an additional action available during a hand.

Table pieces are the reusable mechanism for objects an encounter places on the table:

- The encounter authors its pieces alongside its rules. A piece has an id, a presentation prop, and zero or more interactions. A piece is not assumed to be draggable.
- Each interaction is a reusable kind with its own authored settings, state, validation, saved data and input handler. Kinds are individual classes; do not add a central interaction switch. A new kind must not require changes to pieces, rules, other kinds or the save format.
- One generic action applies a typed input to a named piece interaction and reports the change through a generic event. Piece interactions are accepted only during a hand; the controller's existing busy state blocks them while Reroll or Play Hand resolves. Snapshots expose each interaction's kind, state and current availability.
- Rules read piece state through its interaction kind and reference pieces by id. Catalog construction validates those references. Interacting with a piece is not a committed action for haunting and cannot trigger interference.
- Rules moving or changing pieces themselves is not current scope; the event shape should not prevent it.

The merchant requires only the position interaction kind. A position is the neutral slot or one of the five dice bays. The player drags the prop and releases it to snap to the nearest allowed position; dropping it elsewhere returns it. Dragging never changes dice selection. Do not add a screen, popup or confirmation. Bay snap points come from the tray's dice slots; each encounter scene authors its neutral point. The position kind authors its allowed positions, starting position and movement timing. Timing is not a fixed property of the mechanic: the merchant allows movement whenever the game accepts input during a hand, while another encounter could lock a piece after the first reroll of each hand. Positions persist in the run save.

- The merchant authors one piece, the candle, with a position interaction starting in the neutral slot at the candle's current tabletop position. It keeps its position between hands and stages.
- A ward rule protects the bay occupied by a named piece against named effects. The merchant's ward names the candle and prevents forced rerolls and opposite-face flips.
- The neutral slot protects nothing. It is a valid choice, not a removal, and may matter in later encounters where being haunted benefits some charms.

An interference attempt chooses its target, checks applicable protection, then applies the effect or reports a block. Protection should specify which effects it prevents rather than implicitly blocking every future hostile mechanic.

Pre-roll hand preparation is postponed. John rejected candle placement before the opening roll: every bay is equivalent before the dice are seen, so the choice has no strategic value. A shared pre-roll opportunity remains a possible mechanism for carry-over, where the carried die provides information before the roll. Design it with carry-over, not as part of the merchant; carry-over implementation remains outside merchant scope.

#### Tree nymph encounter (The Bound Tree)

John approved this design for the second Chapter 2 encounter. It is not implemented, and this section does not authorize implementation.

Structure and rewards:
- The player plays the possessing spirit, which is the encounter's host. The nymph is a separate figure in the scene. The merchant leads the player to the edge of the dry ground and returns to his cart.
- Ten stages with unlock milestones at 3, 6 and 10. John might change all of Chapter 2 to 15 stages; that is undecided. Stage targets use Moth's curve as a placeholder until tuned.
- Ordinary reward pools copy Moth's ordinary pools plus the three unlock families. The invitation discovers the three unlock families.
- Unlocks: the Base and Mult variants of Great Oak (the three smallest dice sum to no more than the largest), Close Branches (each pair of neighboring dice differs by at most 1) and Untouched Snow (play a hand without rerolling). Each milestone offers individual charms from that list as usual; combined and reroll variants stay locked without a source. Confirm the condition wording against the condition classes when authoring.
- Untouched Snow is renamed Unturned Leaf: display names only, identifiers unchanged. Its icon (Art/ModifierFamilies/untouched-snow.png) is a snowflake and needs a new glyph in the same style, distinct from Falling Leaf's.

Binding rule, an always-active encounter rule within the existing rule system like haunting, using the rule random stream:
- After the opening roll and after each player reroll resolves, the spirit marks one unbound die chosen at random. Marking never changes a face, so the opening roll is still never disturbed. With all five dice bound, nothing is marked.
- A marked die binds at its result after the next player reroll, whether or not the player selected it. Rerolling it gambles on its final face; leaving it keeps its current face. A mark that bound only rerolled dice would simply be avoided.
- Bound dice score normally and cannot be selected for reroll. Bindings and any outstanding mark clear when the hand is played.
- Nothing wards against or releases a binding in this encounter. A once-per-hand un-bind granted by the nymph was considered and set aside; it could be added later without changing the rule.
- Presentation: a marked die shows a faint creeping-root outline; when it binds, roots close around it and the spirit reacts; bindings release when the hand is played. No text popups. Tapping a bound die does nothing and opens no popup.

Grafted Branch (provisional name), the nymph's encounter-only family:
- Trigger: at least three dice show the face of a bound die, so a bound die becomes an anchor to build around. The three-dice threshold is provisional until John sees how it plays.
- Variants as Lantern Seekers: +10 Base, +2 Mult, +5 Base and +1 Mult, +1 reroll; each tier 1 and once per hand. Excluded from permanent Collection. Provisionally reuses an existing family's artwork until dedicated art is approved.
- The nymph introduces it at the stage 3 clear, after the permanent unlock choice and before that clear's charm offer, which is the first offer that can contain it. The introduction uses the existing encounter-charms screen with a narration line from the nymph above the family, appears on every run, and allows Continue immediately.
- This needs a first eligible stage for an encounter's additions (defaulting to the first stage, so moth is unchanged) and the first story moment inside an encounter, including resuming a save interrupted on that screen.

Working script (draft accepted; minor lines such as continue labels and abandon text are editable placeholders):
- Arrival: the merchant leads the player along the hidden path to a rise of dry ground. A huge black tree fills it, its bark split and its branches bare. He stops at the edge. "This is as far as I go. Mind what you say near it." He turns back toward his cart.
- The nymph: at the base of the tree a young woman sits with her knees drawn up, weeping. Her skin is the grey of old bark. "You shouldn't be here. It hears everything." The tree creaks above her.
- Reveal: "Something crept into my tree when the water rose. It holds me the way it holds the roots." Then: "Your traveler came this way. I saw where they went, but it won't let me speak of it." Barely a whisper: "It is bound by the old terms. Challenge it, and it must play, and it must honor the result."
- Challenge: the bark splits into a face. The spirit is patient and possessive. "She talks too much." Then: "Play, then. Whatever you leave in my reach, I keep."
- Binding rule card: "After each roll, the spirit marks a die. Its next result is bound and cannot be rerolled until the hand is played."
- Stage 3, on the encounter-charms screen: "It keeps what it takes. So build on what it keeps."
- Victory: the roots loosen with a sound like a long breath, and the face sinks back into the bark. The nymph stands as the black fades toward silver. "It's gone. It had to keep its word." Then: "Your traveler went deeper, toward the smoke. A woman lives there who keeps things. Whatever she knows, she'll want something for it."
- Defeat: the roots draw tighter, and the nymph flinches. The spirit: "Again? I have nothing but time." The nymph: "It can't refuse you. Try again."

#### Selected mechanic directions

John selected four encounter mechanic directions: carry-over, binding, rot, and haunting, plus the three carry-over charm families below. These are recorded design directions, not authorization to implement. For carry-over, the proposed flow chooses one die from a played hand to retain its face and position in the next opening roll; the other four roll normally. Rerolling the carried die permanently removes its carried status for that hand, even if the same face returns.

- Keepsake: play a hand containing a carried die.
- Kindred Spirits: play a hand where at least two other dice match the carried die.
- Lone Traveler: play a hand where no other die matches the carried die.

Names are provisional. Each family can use the usual Base, Mult, combined, and reroll effects. All three require surviving carried status. Initially they are encounter-only and do not enter ordinary pools or permanent Collection; the future equipment/deck direction below could open them to other encounters. John likes their independence from existing mechanics. Crossing Stones was suggested but is not among the three selected families. Exact values, selection interaction, and stage-boundary behavior remain to be settled.

John also wants hostile encounter rules for corrupted spirits, ghosts, or a swamp hag. He likes binding and considers the following rot design solid. These are design directions, not implementation authorization.

- Binding supports any number of bound dice. Bound dice score normally but cannot be rerolled; bindings clear when the hand is played. John likes both one binding per hand and one additional binding per reroll, so the encounter controls the binding schedule and count. The proposed affected die is visible before committing the reroll, then binds at its rolled result. The tree nymph encounter's schedule, selection and binding behavior are specified under "Tree nymph encounter" above.
- Rot separates its decay trigger from its endpoint behavior. The current design decays kept dice by one per player reroll. When a die would decay below one, the encounter can wither it, automatically reroll it, clamp it at one, or wrap it to six. Withered dice contribute no value and participate in no hand combinations, including matches with other withered dice; a normal player-selected reroll restores them. Clamp intentionally permits accumulation at one; it is unsuitable when that strategy should be prevented.
- Resolve each die's decay once per player action. An endpoint reroll has no additional reroll cost and does not trigger more decay. A one rolled at the endpoint survives until the next decay event.

Haunting is unpredictable interference by a third participant at the table. The merchant encounter uses the premise and rewards specified above.

- Interference actions and timing are independent. The merchant encounter uses dice interference (forced rerolls or flips, including kept dice) and candle protection. John explicitly excludes charm knockout from this encounter to limit the number of new mechanics introduced together.
- Knocking an equipped charm out of its bay remains a future haunting action: suppress it for the current hand and return it next hand without loss of ownership. Preserve this as a system extension possibility, not an implementation requirement for the merchant.
- Interference can occur after committing a player reroll or after committing Play Hand, before scoring resolves. The automatic opening roll is never disturbed. On Play Hand, visibly perform the disturbance, then evaluate the resulting hand and qualifying charms without giving the player a repair opportunity. A straight can be broken or an otherwise ready charm activation lost; accidental improvements are possible.
- Surprise and misfortune are intentional. The player can know the encounter's possible tricks without knowing the next trigger or target. Do not turn every disturbance into a telegraphed choice. Merely replacing random opening faces with other random faces is not the intended pressure.
- The merchant's interference chance is 70% per eligible committed action (John may lower it after more play), equally likely forced reroll or opposite-face flip, with at most one disturbance per action. After a player reroll, target a kept die; if all five were rerolled, there is no eligible target. After Play Hand, any die can be targeted. These are authored tuning values, not fixed engine constants. Forced rerolls cost no player reroll, cannot recursively trigger interference, and remain distinct from player rerolls for charm triggers.
- Effect, target selection, and disclosure are independent. The merchant gives no advance warning of the next target or outcome. A future encounter could announce a target and deterministic flip, letting the player exploit it; do not implement that future encounter as part of the merchant.
- Resolution order: after a player reroll, the disturbance resolves once the rerolled dice land and before reroll-triggered charms evaluate. On Play Hand it resolves before scoring. Charms always evaluate the post-disturbance faces.
- Disturbed dice do not count as rerolled or changed for charm triggers or reroll history. Record which dice were disturbed separately, because future charms may respond to haunted dice; no such charm is selected.
- A flip turns the die to its opposite face (faces summing to seven) and always changes it. A forced reroll may land on the same face; it still counts as the action's disturbance and is still presented.
- Haunting draws from its own deterministic random stream, separate from the dice and reward streams, and that stream persists in the run save.
- Presentation: a forced reroll visibly rolls the die and a flip visibly turns it; a blocked attempt shows the candle flaring and sparking. The merchant reacts, and a poltergeist sound plays: a giggle on a disturbance, a grumble when blocked. No text popup. The implemented presentation is described under "Merchant encounter" above.
- John likes protection as counterplay: the merchant lends a movable talisman or candle to ward a die. The candle wards one bay against forced rerolls and flips; movement, timing and the neutral slot are described under system boundaries. An attack on that protected bay is visibly repelled rather than redirected to an unprotected target.
- Temporary protective buffs or protection for additional board elements are possible later extensions, not committed scope. The merchant's selected behavior is one ward, movable whenever the game accepts input during a hand; broader protection variants remain undecided.

Haunting and protection are implemented for the merchant as specified above; the extensions listed remain design directions only.

#### Future equipment, decks, and specialized strategies

John proposed a basic inventory slot system in which equipped talismans grant ongoing abilities, such as carry-over. This could arrive alongside deck building and let currently encounter-only charm families become usable elsewhere. This is a future direction, not a finalized equipment specification or current implementation task.

- Proposed separation: equipped talismans provide abilities; decks select charms eligible for ordinary encounter offers; charms actually acquired during the encounter provide their effects. Permanent ownership, equipment, deck membership, and current-run charms remain distinct.
- Specialized families could eventually require an available mechanic rather than a particular spirit. An encounter's native carry-over rule or an equipped carry-over talisman could support the same families. Ownership alone does not supply the ability.
- Possible progression: first experience a mechanic and its charms in an encounter, then gain a way to bring it elsewhere. Talisman reward sources and whether an encounter awards its own ability are undecided.
- Equipment questions remain open: slot count and types, whether abilities and protection compete for slots, when equipment can change, what happens when equipment duplicates an encounter ability, and how decks handle unavailable required mechanics. Interactions between carry-over, binding, rot, haunting, and wards need explicit design; do not assume stacking behavior.

John is interested in decks built around withered dice, turning a disadvantage into a deliberate strategy. Withered dice still contribute nothing to ordinary scoring or hand combinations, but charms can explicitly reward their state. Two proposed strategies are preserving withered dice for charm benefits and repeatedly withering/restoring dice for transition benefits.

Illustrative charm ideas, with unselected names and untuned effects:

- Ash Offering: gain Base for each withered die when playing.
- Last Ember: gain Mult when exactly one die remains active.
- New Growth: gain a bonus when a withered die is restored by rerolling it.

Such a deck needs a dependable source of withering outside rot encounters. Suggested talismans could allow voluntary withering or apply rot with the wither endpoint. Neither method is selected. The broader opportunity is to let players invest in abilities and charms that make a hostile mechanic useful, while retaining its underlying cost.

#### Mentioned but not selected or developed

- Debt: borrow a reroll now in exchange for one fewer next hand.
- Promises: accept a hand condition for a bonus. Cost-free acceptance with no failure penalty has no reason to be declined; a meaningful commitment or an automatic bonus objective would be needed. Neither version is selected.
- Further binding/haunting charm triggers beyond Grafted Branch: scoring with bound dice, matching around a disturbed die, playing before binding occurs, or scoring while a charm is suppressed. No families or values are selected.
- Other haunting suggestions: an apparition moving between bays and disabling the occupied die, a remembered face returning on a later reroll, suppression moving between charms, or fulfilling a visible request to banish a haunting. These are alternatives, not part of the selected poltergeist specification.
- Protection extensions: temporary additional wards, charm protection, or blocking the next interference. The moving candle is the concrete starting concept.
- The Chapter 2 outline above selects the merchant, tree nymph/possessing spirit, and a rot encounter in that order. The rot encounter's rewards and dialogue and the chapter's final revelation remain open; the hag's details are provisional beyond the nymph's clue.

### Presentation behavior

Portrait painted backgrounds/hosts combine with physical 3D dice and bays. Each encounter authors its camera, geometry, materials, lighting and host sequences. Keep narration separate from mechanical details; highlight gifted charm names in narration and show effects separately.

Intro Continue/Accept are available immediately during animation. Advancing cancels the previous beat safely; an interrupted automatic beat must not advance again.

Charm and hand-upgrade choices require selection followed by confirmation. Family artwork is shared; effect-category icons/colors distinguish variants. Trigger phrases have consistent colors independent of effect category.

Scoring accumulates dice sum, complete upgraded hand-rank values, qualifying modifiers, then Base times Mult. Upgrades are part of rank evaluation, not a separate step. Roll-triggered bonuses may accumulate beforehand. The small tray previews ready Play Hand modifiers; it is not a history tray. Tapping a tile opens a compact popup for that exact instance, dismissed by any tap without passing input through. Charms opens the full owned list. Action-button cues show transient activations.

## Persistence constraints

Use Unity's Application.persistentDataPath. Do not inspect or reject parent directories, redirected profiles or platform-managed links. Deletion checks stay within the application's owned campaign subtree.

Newtonsoft JSON stores campaign indexes, profiles and run saves. Run saves preserve RNG and pending offers; fresh runs choose new seeds, with fixed seeds only for debugging. Loading a run must not erase permanent progress. Keep current codec migrations.

Consumed unlock offers are saved to the run before the profile grant, preventing duplicate choices after interruption. Pending writes must finish before advancing/replacing the run. Autosaved journey actions persist before presentation. Continue restores state without replaying the opening roll or consuming RNG.

Corrupt slot indexes reconstruct uniquely identified campaigns; unassigned readable campaigns remain available through Load Game > Recover saves. Keep only the latest generated corrupt-index copy after successful reconstruction. Confirmed New Game replacement removes only the replaced campaign; recovering into an occupied slot preserves the displaced campaign for recovery. Ordinary access errors are not treated as corruption.

## Validation and tools

Follow [Tools/README.md](../Tools/README.md). Before CLI Unity runs, check whether Dice-Unity is open and ask John to close it if necessary. Do not close it yourself or affect other projects. Use the documented escalated, hidden Unity process; track its PID and inspect results XML. Use isolated test saves. Do not introduce delays merely to make tests pass.

Validate the installed scene and moving poses when changing presentation.

Build/install Android only when requested. Generated output stays in ignored Docs/work; unexpected generated changes require permission before deletion.

## Reference documents

- [Presentation assets and direct editing](PRESENTATION-AUTHORING.md)
- [Host, lighting and composition tools](../Tools/Presentation/README.md)
- [Collection and unlock authoring](../Dice-Unity/Assets/Dice/Content/Mechanical/COLLECTION-AUTHORING.md)
- [Extracted reference mechanics](outputs/dice-of-kalma-design-reference.html) and [reference inventory](Reference/README.md)

Use Git history for past changes; this handoff holds current state and design only.
