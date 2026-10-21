# Spirit Dice: start here

Current state: October 20, 2026. This is a working contract and current-state summary, not a development diary. Verify relevant code, assets, and fresh Git status before acting.

## Working contract

Read [John's global instructions](C:/Users/jsp73/.codex/AGENTS.md), this document, [presentation authoring](PRESENTATION-AUTHORING.md), and [Tools/README.md](../Tools/README.md).

- Follow John's approved design. Do not invent screens, confirmation steps, celebrations, delays, rewards, or flow changes. Preserve accepted visuals and pacing unless the requested change requires otherwise.
- Inspect first; explain the cause and smallest complete proposed fix. Wait for implementation approval unless John has already directed the work. Agreement with an observation is not authorization. Historical proposals are not authorization.
- Keep work within scope. Report incidental issues separately. Do not add speculative safeguards, fallback systems, or unrelated refactors.
- Finish authorized implementation and relevant validation without repeatedly asking to continue. Keep progress visible during long work. If blocked, identify the actual blocker and what remains; do not present unfinished work as complete.
- John is a Unity expert. Use normal Unity mechanisms and explain concrete tradeoffs, not programming fundamentals.
- All workspace changes are user-owned, including generated assets and test output. Never restore, discard, delete, or roll back changes without explicit authorization for that action. No worktrees unless requested.
- Preserve existing identifiers and useful comments. Use ASCII comments and strings. Never abbreviate "checkpoint."
- Prefer Astra for orchestration, architecture, integration, and review; use GPT-6.1 Sol with high reasoning for bounded coding/test assignments. Delegation is authorized within approved work. Assign separate responsibilities and review the combined result.
- Tests support evidence, not design authority. Check actual player-visible behavior and rendered layouts where relevant. Do not add arbitrary waits to make failures disappear, weaken checks to match a bug, or equate structural assertions with visual correctness. Run appropriate checks, then stop when the requested work is verified.

## Current workspace and outstanding work

- Merchant presentation has four portrait parts and separate candle body/wick and flame in AmbientMotionPreview. SceneLightingPresentation binds the existing shared lighting state to the independent lantern and movable candle; the candle's footprint and receivers follow its scene transform, with its old glare removed from the ambient plate. See [installed layers and source notes](Reference/Art/HauntedMerchantHostLayers.md). The supporting hand and bare forearm move independently over a fixed sleeve and reconstructed torso/neck backing; the Head includes repaired cheek/ear artwork underneath. Existing SpiritHostPortrait meshes and timelines provide a quiet idle, hand-release/nod reaction and short outcome gestures. Validate the authored moving poses, not only the resting composite. Protective-candle gameplay remains unfinished. Uniform receiver tint retains some painted warm highlights; do not claim spatial relighting of the merchant. The earlier [requirements audit](Reference/Art/HauntedMerchantRequirements.md) records the original failures and acceptance criteria; its historical failure table is not the current implementation state.

- Root: C:/Users/jsp73/Projects/Dice; Unity project: Dice-Unity; branch: main.
- Unity: 6000.6.6f1 at C:/Program Files/Unity/Hub/Editor/6000.6.6f1/Editor/Unity.exe.
- Entry scene: Dice-Unity/Assets/Scenes/Application.unity; menu: Dice > Open Application.
- The scene/prefab migration and removal of the unsolicited unlock notice are committed in 7d04b58. Check fresh Git status for subsequent work; do not discard any changes.
- Confirming a permanent charm unlock now proceeds directly to the next offer, with no extra "CHARM UNLOCKED" notice or timed pause. The selected unlock is still persisted.
- Editor migration generators (Spirit*Authoring, related helpers, and repair commands) and the modifier snapshot importer are removed. Open Application opens the existing scene without generating assets. Configure Modifier Addressables remains available for authored content and Android builds. Edit authored scenes and prefabs directly; inspect and report missing assets rather than silently recreating them.
- Android APK built from 04b7c2b with the migration and unlock fix; signature verified. Installed on the connected SM_S947U with app data cleared at John's request, then launched. The process and foreground activity were verified; gameplay has not been visually checked on-device. Do not assume later workspace edits are installed.
- Player-built decks, further chapters, balance changes, and additional encounter gameplay are deferred. Chapter 2 has design notes and an isolated merchant ambient-motion prototype; its gameplay systems are not implemented.
- This handoff update does not authorize further code changes, a commit, or an Android build. Follow John's next request.

## Architecture and coding boundaries

Mechanics, mechanical content, progression, presentation, and persistence remain separate. The engine must support a different skin or a larger game. Mechanical content contains rules and values, not display names, artwork, dialogue, or audio.

Each modifier has its own mechanical ScriptableObject and separate presentation asset. Conditions have individual evaluation classes with companion helpers where appropriate. Do not reintroduce central evaluation switches or presentation lookup dictionaries. Use explicit references or Addressables; nothing belongs in Resources. Modifier discovery labels are dice.modifiers and dice.theme.spirit-house.modifiers.

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

### Chapter 2 design exploration

#### Chapter outline and next encounter

John approved the following starting points and asked to begin the first encounter. Chapter 2 follows the Chapter 1 return to the fox and the crossing of the old bridge. This records the agreed outline; exact dialogue, rewards, balance, and presentation still need definition.

1. Haunted traveling merchant: a merchant has set up his wares cart on a road beside a dark swamp. A poltergeist haunts the cart and interferes while the player plays with him, using the haunting rule below. The merchant leads the player into the swamp afterward. This is the first encounter to develop; the accepted working dialogue and rewards are below.
2. Bound tree nymph: on a small patch of dry land in the swamp stands a huge black, withered tree. A sobbing dryad/tree nymph sits at its base, trapped and tormented by the evil spirit possessing her tree. She reveals that the spirit is bound by challenge terms; it would not volunteer this information. Once the player challenges it, the spirit must comply and abide by the result. Play against the spirit using binding; victory banishes it, releases the tree and nymph, and lets her provide information needed to continue. The nymph's knowledge makes her an active participant in her rescue.
3. Rot encounter: the third encounter uses rot. A swamp hag is a favored starting possibility, with a decaying hut, preserved things, and knowledge of the traveler's passage or the route onward. She can be unsettling and transactional rather than another spirit to banish. Her exact identity, location, bargain, and revelation remain open.

Each encounter should advance the search for the traveler as well as lead to the next location. The chapter's final revelation is undecided. Carry-over has no assigned place in this three-encounter outline; do not force it into the chapter or implement it as part of the merchant encounter.

John explicitly chose to flesh out the merchant's story and exact rules before implementation. He accepted this working premise: the tired, frightened merchant freely confirms seeing the traveler enter the swamp and can guide the player along its obscured paths. The game-obsessed poltergeist meddles in games; beating the merchant despite its cheating buys a temporary respite long enough for him to leave the cart and guide the player. It is not permanently banished. The merchant lends a protective candle already used for his wares. Exact dialogue remains a draft.

John selected Draining Bowl (played dice sum below the opening sum), Returning Echo (same hand rank as the previous played hand), and Counted Coins (played dice sum divisible by five) as the merchant's reward families. Use their existing Base and Mult variants as six permanent unlocks, with ten stages and unlock milestones at 3, 6, and 10. Stage targets remain to be tuned. These six assets already require permanent ownership; giving them an unlock source does not itself reduce the current starter pool.

John considers the beginning charm pool too large. As encounters are added, prefer using existing families as progression rewards where suitable, and consider moving appropriate starter families behind later unlocks to simplify early play. This is a progression-design direction; specific starter-pool removals have not been selected or authorized.

John accepted the following working dialogue for now, under the working title The Unquiet Cart:

Scene-first direction: John wants the encounter's look established before implementing the gameplay systems. The merchant is a human traveler wearing a formerly white shirt, suspenders, brown trousers, and a battered hat. The scene is nighttime on a muddy road beside a dark swamp/marsh, with tall black trees, old moss, and sagging limbs. His broken-down cart has a broken wheel and a formerly colorful canopy that is now torn and dingy. The first generated visual concept and its prompt are under ignored Docs/work/merchant-concept-20261020. The composition remains provisional.

John approved reusable ambient prototypes before preparing the full scene. Assets/Scenes/AmbientMotionPreview.unity renders MerchantBackground.png at native 941x1672, with the merchant and candle removed from the background and displayed as separate transparent Merchant and Protective Candle children. The generated layers are close derivatives, not pixel-exact copies; source art remains preserved. See [host and candle layers](Reference/Art/HauntedMerchantHostLayers.md). MerchantRegisteredRoofPlate.png remains the roof-registration source. The cloth mask preserves the complete original front seam and rear attachment folds, correcting the front overreach and rear gap. See [attachment repair](Reference/Art/HauntedMerchantAttachmentRepair.md). The Fixed Canopy gameobject is disabled: its complementary mask and the earlier MerchantOriginalRoofPlate.png retained fragments around the removed cloth. The animated fabric continues to use exact original RGB from MerchantOriginal.png through CanopyOriginalOverhangMask.png, with CanopyOriginalOverhang.png retained as the transparent extraction. Canopy.prefab retains its full-frame SpiritPortraitImage, 96x192 mesh, two localized shapes and irregular 31-second native animation. The extra cup is disabled because the clean background contains cookware. Validate the actual scene with Canopy hidden before judging animation; rest reconstruction alone concealed the defective background. This preview is not playable or registered in Addressables/build scenes. See [current layers and validation](Reference/Art/HauntedMerchantRegisteredCanopy.md).

- Arrival: the road narrows beside dark water. A merchant's cart stands beneath a sagging canvas cover. A hanging cup turns against the breeze as the player approaches.
- Merchant: "Looking for someone? Yes, they passed this way. Left the road and went into the marsh." Then: "I know the path they took. But I can't leave my wares with this thing awake." Something knocks beneath the table; he presses a hand against it.
- Bargain: "It likes watching us play. Likes cheating more." Then: "Beat me despite its meddling, and it sulks for a while. Long enough for me to show you the way."
- Candle: he takes a short candle from beside a wooden chest. "This keeps its fingers off things. Set it beside what you want left alone." Keep the mechanical explanation separate from dialogue: place the candle before the initial roll, protect its bay for the hand, and expect possible interference after Reroll or Play Hand. The invitation introduces the three selected families using existing discovery and milestone-unlock behavior.
- Victory: the hanging cup settles; the merchant pauses. "Quiet. At last." Then: "Come. There's dry ground around the great tree. That's where I saw your traveler heading." He secures the cart and leads the player toward the swamp.
- Defeat: a sharp knock comes from beneath the board. "Pleased with itself, isn't it?" Then: "Take a breath. We can try again."

#### System boundaries and hand preparation

John emphasized that these are reusable systems. Haunting belongs within the existing always-active encounter-rule system used by Fox, Owl, and Moth, extending its action-resolution behavior as needed. Do not build a separate merchant-specific haunting framework. The encounter supplies its rule configuration; the merchant and poltergeist supply narrative and presentation.

Protection is a separate capability reusable independently of haunting. Candle placement belongs to interaction before the initial roll, alongside choosing a carry-over die for the upcoming hand. Treat these as different uses of a shared hand-preparation opportunity rather than unrelated encounter flows. The current StartHand immediately rolls all dice. John accepted tapping a bay to position the candle, then pressing Roll in the existing action-button area; placement does not automatically roll. The candle remains fixed for the entire hand and can be repositioned before the next initial roll. Persist this preparation state as part of the hand lifecycle; do not add a separate screen or confirmation popup.

An interference attempt chooses its target, checks applicable protection, then applies the effect or reports a block. Protection should specify which effects it prevents rather than implicitly blocking every future hostile mechanic. The candle is presentation for a protective capability, not a mechanic that requires knowledge of the merchant. Carry-over's actual gameplay implementation remains outside the merchant's scope even though its future preparation needs inform the shared design.

#### Selected mechanic directions

John selected four encounter mechanic directions: carry-over, binding, rot, and haunting, plus the three carry-over charm families below. These are recorded design directions, not authorization to implement. For carry-over, the proposed flow chooses one die from a played hand to retain its face and position in the next opening roll; the other four roll normally. Rerolling the carried die permanently removes its carried status for that hand, even if the same face returns.

- Keepsake: play a hand containing a carried die.
- Kindred Spirits: play a hand where at least two other dice match the carried die.
- Lone Traveler: play a hand where no other die matches the carried die.

Names are provisional. Each family can use the usual Base, Mult, combined, and reroll effects. All three require surviving carried status. Initially they are encounter-only and do not enter ordinary pools or permanent Collection; the future equipment/deck direction below could open them to other encounters. John likes their independence from existing mechanics. Crossing Stones was suggested but is not among the three selected families. Exact values, selection interaction, and stage-boundary behavior remain to be settled.

John also wants hostile encounter rules for corrupted spirits, ghosts, or a swamp hag. He likes binding and considers the following rot design solid. These are design directions, not implementation authorization.

- Binding supports any number of bound dice. Bound dice score normally but cannot be rerolled; bindings clear when the hand is played. John likes both one binding per hand and one additional binding per reroll, so the encounter controls the binding schedule and count. The proposed affected die is visible before committing the reroll, then binds at its rolled result. Selection and avoidance rules remain unresolved.
- Rot separates its decay trigger from its endpoint behavior. The current design decays kept dice by one per player reroll. When a die would decay below one, the encounter can wither it, automatically reroll it, clamp it at one, or wrap it to six. Withered dice contribute no value and participate in no hand combinations, including matches with other withered dice; a normal player-selected reroll restores them. Clamp intentionally permits accumulation at one; it is unsuitable when that strategy should be prevented.
- Resolve each die's decay once per player action. An endpoint reroll has no additional reroll cost and does not trigger more decay. A one rolled at the endpoint survives until the next decay event.

John favors haunting as unpredictable interference by a third participant at the table. A possible encounter is a scared or lost merchant whose wares cart is haunted by a poltergeist; the merchant has something the player needs, with the reward and story still undecided. This direction replaces the earlier repeated-hand-category penalty proposal.

- Interference actions and timing are independent. The merchant encounter uses dice interference (forced rerolls or flips, including kept dice) and candle protection. John explicitly excludes charm knockout from this encounter to limit the number of new mechanics introduced together.
- Knocking an equipped charm out of its bay remains a future haunting action: suppress it for the current hand and return it next hand without loss of ownership. Preserve this as a system extension possibility, not an implementation requirement for the merchant.
- Interference can occur after committing a player reroll or after committing Play Hand, before scoring resolves. On Play Hand, visibly perform the disturbance, then evaluate the resulting hand and qualifying charms without giving the player a repair opportunity. A straight can be broken or an otherwise ready charm activation lost; accidental improvements are possible.
- Surprise and misfortune are intentional. The player can know the encounter's possible tricks without knowing the next trigger or target. Do not turn every disturbance into a telegraphed choice. Merely replacing random opening faces with other random faces is not the intended pressure.
- John accepted a starting balance of 20% interference chance per eligible committed action, equally likely forced reroll or opposite-face flip, with at most one disturbance per action. After a player reroll, target a kept die; if all five were rerolled, there is no eligible target. After Play Hand, any die can be targeted. These are authored tuning values, not fixed engine constants. Forced rerolls cost no player reroll, cannot recursively trigger interference, and remain distinct from player rerolls for charm triggers.
- Effect, target selection, and disclosure are independent. The merchant gives no advance warning of the next target or outcome. A future encounter could announce a target and deterministic flip, letting the player exploit it; do not implement that future encounter as part of the merchant.
- John likes protection as counterplay: the merchant lends a movable talisman or candle to ward a die. The candle wards one bay against forced rerolls and flips for the hand, with placement before the initial roll as described under system boundaries. An attack on that protected bay is visibly repelled rather than redirected to an unprotected target.
- Temporary protective buffs or protection for additional board elements are possible later extensions, not committed scope. The merchant's selected starting behavior is one protected bay, placement before the opening roll, and no movement for the rest of that hand; broader protection variants remain undecided.

Haunting and protection are recorded design directions only; no implementation is authorized yet.

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
- Further binding/haunting charm triggers: scoring with bound dice, matching around a bound or disturbed die, playing before binding occurs, or scoring while a charm is suppressed. No families or values are selected.
- Other haunting suggestions: an apparition moving between bays and disabling the occupied die, a remembered face returning on a later reroll, suppression moving between charms, or fulfilling a visible request to banish a haunting. These are alternatives, not part of the selected poltergeist specification.
- Protection extensions: temporary additional wards, charm protection, or blocking the next interference. The moving candle is the concrete starting concept.
- The Chapter 2 outline above selects the merchant, tree nymph/possessing spirit, and a rot encounter in that order. Exact rewards, dialogue, chapter title, and the final revelation remain open; the hag's details are provisional.

### Presentation behavior

Portrait painted backgrounds/hosts combine with physical 3D dice and bays. Each encounter authors its camera, geometry, materials, lighting and host sequences. Keep narration separate from mechanical details; highlight gifted charm names in narration and show effects separately.

Intro Continue/Accept are available immediately during animation. Advancing cancels the previous beat safely; an interrupted automatic beat must not advance again.

Charm and hand-upgrade choices require selection followed by confirmation. Family artwork is shared; effect-category icons/colors distinguish variants. Trigger phrases have consistent colors independent of effect category.

Scoring accumulates dice sum, complete upgraded hand-rank values, qualifying modifiers, then Base times Mult. Upgrades are part of rank evaluation, not a separate step. Roll-triggered bonuses may accumulate beforehand. The small tray previews ready Play Hand modifiers; it is not a history tray. Tapping a tile opens a compact popup for that exact instance, dismissed by any tap without passing input through. Charms opens the full owned list. Action-button cues show transient activations.

## Persistence constraints

Use Unity's Application.persistentDataPath. Do not inspect or reject parent directories, redirected profiles or platform-managed links. Deletion checks stay within the application's owned campaign subtree.

Newtonsoft JSON stores campaign indexes, profiles and run saves. Run saves preserve RNG and pending offers; fresh runs choose new seeds, with fixed seeds only for debugging. Loading a run must not erase permanent progress. Keep current codec migrations; do not infer incompatible formats from old discussions.

Consumed unlock offers are saved to the run before the profile grant, preventing duplicate choices after interruption. Pending writes must finish before advancing/replacing the run. Autosaved journey actions persist before presentation. Continue restores state without replaying the opening roll or consuming RNG.

Corrupt slot indexes reconstruct uniquely identified campaigns; unassigned readable campaigns remain available through Load Game > Recover saves. Keep only the latest generated corrupt-index copy after successful reconstruction. Confirmed New Game replacement removes only the replaced campaign; recovering into an occupied slot preserves the displaced campaign for recovery. Ordinary access errors are not treated as corruption.

## Validation and tools

Follow [Tools/README.md](../Tools/README.md). Before CLI Unity runs, check whether Dice-Unity is open and ask John to close it if necessary. Do not close it yourself or affect other projects. Use the documented escalated, hidden Unity process; track its PID and inspect results XML. Use isolated test saves. Do not introduce delays merely to make tests pass.

Latest relevant evidence is under ignored Docs/work/unity-tests:

- Merchant/candle separation: 30/30 focused EditMode tests passed in merchant-layers-49ed02f134be46f590112455c402f938. Background-only rendering matches the installed clean plate exactly at six sizes; merchant and candle are independently hidden and rendered. Native composite versus unmodified original: 1,498,255 differing pixels, mean absolute RGB channel error 11.1211/255. This comparison measures generated-art differences, not exact reconstruction. No full-suite or Android run was performed.
- Canopy edge filtering: 30/30 focused EditMode tests passed in canopy-edges-dcb9962750b143f5b781923700ccade8. Bilinear mask sampling retains fractional alpha; coverage affects alpha only to avoid dark fringes. A GPU regression compares fractional coverage with default UI alpha blending. Native rest/gust renders and an original/before/after edge comparison were visually inspected. Attachment and hidden-background checks pass at six sizes. No full-suite or Android run was performed.
- Ambient cup/canopy: 29/29 focused EditMode tests passed in canopy-attachments-7c73848ebeb94366979b44ba182b674a. The front seam mask and rear attachment folds were recovered from original pixels; the clean roof edge was registered to the source curve. Hidden-canopy renders match the registered background at six resolutions. Native rest, gust and background-only captures were independently inspected: rear gap closed, front seam aligned, no background fragments. The native 12-second canopy-attachments.webm is in that run. Source provenance and repair anchors are in Reference/Art/HauntedMerchantAttachmentRepair.md. No full-suite or Android run was performed.
- Generator removal: 73/73 targeted EditMode tests passed in generators-removed-a55f046e1be94921956e74ea7783a19e, covering authored presentation assets, explicit references, diagnostic scene, application configuration, modifier Addressables, tray composition previews, and encounter lighting previews. Unity compiled the remaining editor tools successfully. Full suites and Android builds were not rerun for this editor-only removal.
- Migration EditMode: full run 1,653/1,657, followed by 118/118 targeted checks covering all four failures. No single clean full-suite run is claimed. Evidence: authored-style-final-4e3901d37ef04c968abc437cc0ca2b28 and authored-edit-regressions-5bead733e73c4b2dbcf27302cf9a1823. One filesystem recovery fixture failed moving its temporary directory and passed on rerun; the external cause is unproven.
- Migration PlayMode: all 89 exercised cases pass on their latest results across full/focused runs. Final evidence: authored-play-ui-final-74c601e298954429a236a75c9aeb3e67 and authored-scene-resume-ca1e9c880cc748e6a767762818713a1a. Rendered portrait captures were inspected for scenes, previews, endings, rewards, hand upgrades, owned charms and Collection. This does not substitute for John's playtest or Android verification.
- Latest unlock fix: 3/3 progression presentation tests passed in unlock-direct-7e1ec2d311f74de68e820de56169dbf4. The nonfinal unlock test asserts the next offer and available input immediately after confirmation, without yielding a frame, plus persisted ownership. The full suites were not rerun after this narrow change.

Build/install Android only when requested. Generated output stays in ignored Docs/work; unexpected generated changes still require permission before deletion. Historical test results do not validate subsequent edits.

Latest Android artifact: Docs/work/android-build-eb051dca34334e4d97cfc82a36b1ff0e/SpiritDice.apk (219,717,854 bytes), ARM64 IL2CPP development build, package net.ridiculousgaming.diceprototype. Unity 6000.6.6f1 exited 0; APK v2 signature verified. Build log contains SpiritDiceSurface.shader warnings about pow with a potentially negative base. No tracked files were changed by the build. Installation and app-data reset succeeded; launch process and foreground activity were verified, with no error matches in the sampled startup log. On-device gameplay remains unverified.

## Reference documents

- [Presentation assets and direct editing](PRESENTATION-AUTHORING.md)
- [Host, lighting and composition tools](../Tools/Presentation/README.md)
- [Collection and unlock authoring](../Dice-Unity/Assets/Dice/Content/Mechanical/COLLECTION-AUTHORING.md)
- [Extracted reference mechanics](outputs/dice-of-kalma-design-reference.html) and [reference inventory](Reference/README.md)

Use Git history for past changes. Do not restore the chronological progress log to this handoff.
