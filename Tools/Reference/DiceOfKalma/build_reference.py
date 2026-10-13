import collections
import hashlib
import html
import json
import re
from pathlib import Path

from reference_paths import binary, metadata, output_directory, parser

args = parser('Build the design reference report from the preserved snapshot.').parse_args()
root = args.input_dir
out = output_directory(args, 'design-report')
def read(name):
    return json.loads((root / name).read_text(encoding='utf-8'))
def clean(value):
    return re.sub(r'\s+', ' ', re.sub(r'<[^>]*>', '', str(value))).strip()
def esc(value):
    return html.escape(str(value))
def table(headers, rows, ident=''):
    return '<div class="table-wrap"><table' + (f' id="{ident}"' if ident else '') + '><thead><tr>' + ''.join('<th>' + esc(x) + '</th>' for x in headers) + '</tr></thead><tbody>' + ''.join('<tr>' + ''.join('<td>' + str(x) + '</td>' for x in row) + '</tr>' for row in rows) + '</tbody></table></div>'

assets = read('design-assets.json')
constants = {k: v for k, v in read('constants.json').items() if k != 'allStaticBlobs'}
texts = {name: read(name + '-english.json') for name in ['Skulls', 'GameUI', 'DiceHands', 'Dialogues']}
loc = {r['key']: clean(r['text']) for rows in texts.values() for r in rows}
colors = ['Blue', 'Red', 'Purple', 'Yellow']
phases = ['Play hand', 'Initial roll', 'Player reroll']
groups = collections.defaultdict(list)
for asset in assets:
    if asset['script'] == 'Item':
        d = asset['data']
        groups[d['skullNameKey']].append(dict(
            assetName=d['m_Name'], sourceFile=asset['file'], sourceObjectId=asset['id'],
            color=colors[d['skullColor']], activationPhase=phases[d['activationTime']],
            activationTriggerId=d['activationTrigger'], bones=d['rigidBoneEffect'],
            mult=d['rigidMultEffect'], rerolls=d['rigidRerollRegainEffect'],
            metaProgressLevel=d['metaProgressLevel']))
skulls = []
for key, variants in sorted(groups.items()):
    base = key.removesuffix('.Name')
    skulls.append(dict(name=loc[key].title(), key=base, condition=loc[base + '.Desc'],
        example=loc.get(base + '.DescExample', ''), phase=variants[0]['activationPhase'],
        variants=sorted(variants, key=lambda v: colors.index(v['color']))))

challenge_keys = ['10', '11', '20', '21', '22', '40', '31', '32', '30', '41', '50', '51']
tiers = [0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 4, 4]
challenges = [dict(index=i, localizationKey='GameUI.Challenge' + k,
    condition=loc['GameUI.Challenge' + k], points=constants['challengePoints'][i],
    unlockWinPoints=constants['challengeThresholds'][tiers[i]]) for i, k in enumerate(challenge_keys)]
hand_keys = ['HighDice', 'Pair', 'TwoPair', 'ThreeOfKind', 'Flush', 'FullHouse', 'FlushHouse', 'FourOfKind', 'StraightFlush', 'RoyalFlush']
hands = [dict(index=i, name=loc['DiceHand.' + k + '.Name'].title(),
    condition=loc['DiceHand.' + k + '.Desc'], baseBones=constants['diceHandBones'][i],
    baseMult=constants['diceHandMults'][i], active=i not in [4, 6],
    maximumDisplayedLevel=constants['maxHandUpgrades'][i],
    bonesPerUpgrade=5 if i not in [4, 6] else None,
    multPerUpgrade=2 if i == 9 else 1 if i not in [4, 6] else None)
    for i, k in enumerate(hand_keys)]
achievements = []
for r in texts['GameUI']:
    m = re.fullmatch(r'GameUI.Achievements(\d+(?:_\d+)?)', r['key'])
    if m:
        achievements.append(dict(key=r['key'], name=clean(r['text']).title(),
            condition=loc.get('GameUI.AchievementsDesc' + m[1], '')))
cosmetics = []
for a in assets:
    if a['script'] == 'SkinData':
        d = a['data']
        cosmetics.append(dict(assetName=d['m_Name'], type=['Hat', 'Cape', 'Dice'][d['skinType']],
            unlockKey=d['unlockDescKey'], unlockDescription=loc.get(d['unlockDescKey'], 'Default'),
            unlockAchievementId=d['unlockAchievementId'], dialogueKey=d['dialogueKey']))
dialogue_groups = collections.Counter(re.sub(r'\d+$', '', r['key']) for r in texts['Dialogues'])
abilities = [dict(key=k, name=loc['GameUI.RerollAbility' + k].title(),
    description=loc['GameUI.RerollAbilityDesc' + k], availability='Classic is active; alternate selection not confirmed')
    for k in ['Classic', 'Sticky', 'Twin', 'Chaotic', 'Chained', 'Weighed', 'Cogwheel', 'Reverse', 'Sorting']]
hashes = {name: hashlib.sha256((args.apk_dir / name).read_bytes()).hexdigest()
    for name in ['base.apk', 'split_config.arm64_v8a.apk']}
hashes['global-metadata.dat'] = hashlib.sha256(metadata(args.apk_dir)).hexdigest()
hashes['libil2cpp.so'] = hashlib.sha256(binary(args.apk_dir)).hexdigest()
data = dict(
    title='Dice of Kalma - reconstructed design reference',
    version='1.6.3', versionCode=644, menuBuild='1.6.3-15092026-A',
    inspectedDate='2026-10-06', package='fi.Pepperbox.DiceOfKalma',
    scope='Installed Android build. Complete extracted Item inventory; not the original internal design document or exhaustive runtime verification.',
    counts=dict(skullTypes=len(skulls), itemVariants=sum(len(s['variants']) for s in skulls),
        activeHandCategories=8, challenges=len(challenges), achievementTextDefinitions=len(achievements),
        cosmeticAssets=len(cosmetics), dialogueTextRows=len(texts['Dialogues'])),
    skulls=skulls, hands=hands, challenges=challenges, constants=constants,
    achievements=achievements, cosmetics=cosmetics, rerollAbilities=abilities,
    dialogueGroups=dict(dialogue_groups),
    localizedDesignText={k: [dict(key=r['key'], text=clean(r['text'])) for r in v] for k, v in texts.items()},
    sourceSha256=hashes)
(out / 'dice-of-kalma-design-data.json').write_text(json.dumps(data, indent=2, ensure_ascii=True), encoding='utf-8')

parts = []
def add(value):
    parts.append(value)
def section(ident, title, text):
    add(f'<section id="{ident}"><h2>{title}</h2>{text}</section>')

add('''<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Dice of Kalma | Design reference</title><style>
:root{--bg:#10141c;--panel:#19202b;--text:#e9edf3;--muted:#aeb9c9;--line:#334052;--blue:#62d4dc;--red:#ff929b;--purple:#c5a7ff;--yellow:#f5ca6b}
*{box-sizing:border-box}html{scroll-behavior:smooth}body{margin:0;background:var(--bg);color:var(--text);font:16px/1.65 system-ui,Segoe UI,sans-serif}main{max-width:1240px;margin:auto;padding:48px 32px 90px}h1{font-size:clamp(36px,6vw,62px);line-height:1.1;margin:12px 0 20px;letter-spacing:-2px}h2{font-size:30px;margin:0 0 16px;line-height:1.2}h3{font-size:21px;margin:28px 0 10px}p{max-width:1000px;margin:12px 0}a{color:var(--blue)}.eyebrow{color:var(--yellow);letter-spacing:2px;font-size:12px;font-weight:700;text-transform:uppercase}.lede{font-size:21px;color:#cbd5e3;max-width:920px}.muted,.source{color:var(--muted)}.source{font-size:12px}.stats{display:grid;grid-template-columns:repeat(4,1fr);gap:12px;margin:30px 0}.stat{background:var(--panel);padding:20px;border:1px solid var(--line);border-radius:12px}.stat strong{font-size:32px;display:block}.stat span{color:var(--muted)}nav{display:flex;flex-wrap:wrap;gap:8px;margin:24px 0 36px}nav a{border:1px solid var(--line);border-radius:30px;padding:7px 16px;text-decoration:none;font-size:14px}section{border-top:1px solid var(--line);padding-top:34px;margin-top:46px;scroll-margin-top:20px}.callout{background:var(--panel);border-left:3px solid var(--yellow);padding:15px 22px;margin:20px 0}.flow{display:grid;grid-template-columns:repeat(3,1fr);gap:12px;counter-reset:steps}.step{padding:18px;background:var(--panel);border-radius:10px;counter-increment:steps}.step:before{content:counter(steps,decimal-leading-zero);display:block;font-size:13px;color:var(--yellow);font-weight:700}.step strong{display:block;margin:4px 0}.step p{font-size:14px;margin:4px 0;color:#cbd5e3}.table-wrap{overflow:auto;border:1px solid var(--line);border-radius:10px;margin:18px 0}table{border-collapse:collapse;width:100%;text-align:left;font-size:14px}th,td{padding:12px 14px;border-bottom:1px solid var(--line);vertical-align:top}th{background:#222c3b;font-size:12px;letter-spacing:.4px;color:#cad4e2}tr:last-child td{border-bottom:0}tbody tr:nth-child(even){background:#151c26}code,pre{font-family:Consolas,monospace;background:#222c3b}code{padding:2px 5px;border-radius:4px}pre{padding:22px;overflow:auto;font-size:15px;border-radius:10px}.blue{color:var(--blue)}.red{color:var(--red)}.purple{color:var(--purple)}.yellow{color:var(--yellow)}.controls{display:flex;gap:12px;flex-wrap:wrap;margin:24px 0 12px}.controls input{flex:1;min-width:220px}.controls input,.controls select{background:#222c3b;color:var(--text);border:1px solid #52627a;padding:13px;border-radius:8px;font:inherit}.skull-name{min-width:125px}.skull-condition{min-width:300px}.effect{white-space:nowrap;font-weight:650}details{margin:18px 0}summary{cursor:pointer;color:var(--blue);font-weight:650}td details{margin:8px 0 0}td summary{font-size:12px}td details p{font-size:13px;max-width:440px}.tag{display:inline-block;font-size:11px;color:#cbd5e3;background:#273244;border-radius:6px;padding:2px 7px;margin-top:6px}.small{font-size:13px}.columns{display:grid;grid-template-columns:1fr 1fr;gap:24px}.count{font-size:13px;color:var(--muted)}ul,ol{padding-left:24px}li{margin:8px 0}footer{border-top:1px solid var(--line);padding-top:25px;margin-top:45px;color:var(--muted);font-size:13px}
@media(max-width:750px){main{padding:28px 18px}.stats{grid-template-columns:1fr 1fr}.flow,.columns{grid-template-columns:1fr}h2{font-size:26px}.skull-condition{min-width:260px}}
@media print{body{background:white;color:#111;font-size:10pt}main{padding:0;max-width:none}nav,.controls{display:none}section{break-before:auto}.stat,.step,.callout,th,tbody tr:nth-child(even){background:#f1f3f5;color:#111}a,.blue,.red,.purple,.yellow,.muted,.source,.lede,.tag,.step p{color:#333}table{font-size:9pt}tr{break-inside:avoid}pre,code{background:#eee;color:#111}.table-wrap{overflow:visible}h2,h3{break-after:avoid}}
</style></head><body><main>
<div class="eyebrow">Reference study / Android build 1.6.3 / 06 October 2026</div>
<h1>Dice of Kalma</h1><p class="lede">A reconstructed game-design reference: run structure, scoring, every extracted skull, challenge modifiers, progression, and content present in the build but not confirmed in the menu.</p>
<div class="stats"><div class="stat"><strong>74</strong><span>skull types, including Blank</span></div><div class="stat"><strong>296</strong><span>color variants</span></div><div class="stat"><strong>8</strong><span>active hand categories</span></div><div class="stat"><strong>12</strong><span>challenge modifiers</span></div></div>
<div class="callout"><strong>What is complete:</strong> the 296 Item definitions referenced by the installed game's database, including all names, conditions, examples, activation phases and effect values. <strong>What this is:</strong> a reconstruction from shipped assets, localized rules, selected native code paths and live menu inspection. It is not the developer's original design document, and individual skull predicates have not all been exercised in play.</div>
<nav><a href="#flow">Game flow</a><a href="#scoring">Scoring</a><a href="#rewards">Rewards</a><a href="#skulls">Skull catalog</a><a href="#levels">Level tables</a><a href="#modes">Modes & challenges</a><a href="#events">Events</a><a href="#progression">Progression</a><a href="#unused">Other shipped content</a><a href="#evidence">Evidence & limits</a></nav>
''')

section('flow', '01 / The run loop', '''
<p>The central loop is a five-die, poker-like score challenge. Each level gives a threshold, a limited number of hands and a shared pool of dice rerolls. The growing skull deck rewards particular roll patterns, positions and resource decisions. Hand upgrades increase the value of selected categories throughout the run.</p>
<div class="flow">
<div class="step"><strong>Select a mode</strong><p>Start a run or continue a saved run. Standard has 25 levels. Daily uses a shorter, prescribed setup.</p></div>
<div class="step"><strong>Begin a level and roll</strong><p>Load the level's threshold, hands and rerolls. Resolve skulls associated with the initial roll.</p></div>
<div class="step"><strong>Choose dice to reroll</strong><p>Spend one reroll action on the selected dice. Resolve reroll-triggered skulls. Repeat while resources and strategy permit.</p></div>
<div class="step"><strong>Play the hand</strong><p>Resolve the best hand category, upgrades and eligible skulls. Multiply Bones by Mult, apply the result to the remaining threshold, and consume a hand.</p></div>
<div class="step"><strong>Continue or clear</strong><p>If the threshold remains and hands remain, begin another hand. If the threshold is cleared, enter the reward flow. Running out of hands before clearing normally loses the run.</p></div>
<div class="step"><strong>Build the deck and advance</strong><p>Choose a skull and choose a hand upgrade between levels in the ordinary run. The next level raises the score requirement. Standard ends in victory at level 25.</p></div>
</div>
<h3>State and resource boundaries</h3>
<ul><li><strong>Hand:</strong> five dice, rerolls spent on that hand, dice changed since the initial roll, score accumulated from earlier skull activations, and once-per-hand skull eligibility.</li>
<li><strong>Level:</strong> remaining threshold, hands, rerolls, and once-per-level eligibility. Dice rerolls are shared across hands; yellow skulls can replenish them.</li>
<li><strong>Run:</strong> skull inventory, hand upgrade levels, level depth, selected challenges, separate skull-selection reroll currency, score and run statistics.</li>
<li><strong>Profile:</strong> achievements, cosmetics, records and mode/challenge unlocks. A saved run can resume both play and reward-selection states.</li></ul>
<p>Dice order does not change the hand category. It does change many skull conditions: endpoints, the middle die, adjacent dice and left/right sums are all part of the design. Holding the Play Hand control exposes a score preview.</p>
<p class="source">Evidence: tutorial localization; RunSaveData fields; BattleHandler.CheckHand, DoPreviewCalc, PointsCountCycle; GameState values Playing, LootingSkulls and LootingHands.</p>''')

hand_rows=[]
for h in hands:
    if h['active']:
        hand_rows.append([esc(h['name']),esc(h['condition']),h['baseBones'],h['baseMult'],
            '+5 Bones / +' + str(h['multPerUpgrade']) + ' Mult',
            str(h['baseBones'] + 30) + ' Bones / ' + str(h['baseMult'] + 6*h['multPerUpgrade']) + ' Mult'])
section('scoring', '02 / Scoring and hand upgrades', '''
<pre>Bones = sum of all five dice + category Bones + upgrade Bones + skull Bones
Mult  = category Mult + upgrade Mult + skull Mult
Hand total = Bones * Mult
Remaining threshold -= Hand total</pre>
<p>Skull Bones and Mult include effects accumulated during initial rolls/rerolls and effects earned when playing. Blue adds Bones, red adds Mult, purple adds both, and yellow restores rerolls. There is no extra universal +1 Mult on top of the category's value.</p>
''' + table(['Category','Definition','Base Bones','Base Mult','Each upgrade','At level 7'], hand_rows) + '''
<p>Categories start at displayed level 1 and cap at level 7: six upgrades. An upgrade adds 5 Bones and 1 Mult; Five of a Kind adds 5 Bones and 2 Mult. The best matching category supplies the hand bonus. Flush and Flush House have leftover data but are not returned by the inspected hand classifier.</p>
<p><strong>Example:</strong> an unupgraded Full House of 2,2,2,5,5 scores (16 + 20) * 6 = <strong>216</strong> without skull effects. One upgrade changes this to (16 + 25) * 7 = <strong>287</strong>.</p>
<p class="source">Evidence: ConstantStorage arrays; CheckHand at native VA 0x18ad8dc; DoPreviewCalc at 0x18c164c; SelectHandsForUpgrade at 0x18fffb8. The formulas above describe ordinary scoring; challenge modifiers can remove either upgrade contribution.</p>''')

section('rewards', '03 / Reward and build economy', '''
<div class="columns"><div><h3>Skull offers</h3>
<p>The ordinary offer has three skulls and one choice. The extracted four-step color cycle is <strong>three yellow options, then one blue/red/purple option for each of the next three offers</strong>. The generator indexes this cycle using (level depth - 1) modulo 4. The initial table entry therefore corresponds to depth 1, followed by 5, 9, 13, 17 and 21 for yellow rounds.</p>
<p>Endless switches to blue/red/purple offers after level 25. The inventory accumulates chosen skulls. The UI explicitly recognizes owning the same skull type in another color; color variants should not be treated as mutually exclusive types.</p>
<p>Offer generation filters excluded inventory items, excluded loot and the current offer. This is not an unrestricted independent draw from all 296 assets. The complete probability distribution and exclusion-history lifetime are not reconstructed here.</p></div>
<div><h3>Upgrade offers and rerolls</h3>
<p>Hand-upgrade selection normally offers three categories; a challenge reduces it to two. Fully upgraded categories and the two inactive categories are excluded. The generator suppresses the previous three upgrades through depth 16, the previous two through depth 21, and then removes that recent-history restriction. There is a fallback when the candidate pool becomes too small.</p>
<p>Rerolling a skull offer uses a separate currency from rerolling dice. Normal initialization assigns one skull reroll, with overrides for tutorial/special setup. The inspected ordinary clear path adds one skull reroll. Although a leftover-hands reward path exists in the binary, its controlling flag is set false in the inspected path; leftover hands should not be assumed to multiply this reward.</p></div></div>
<p class="source">Evidence: InventoryVisualizer.GenerateLoot and TakeLoot; DatabaseObject.ReturnRandomItemOfColor; HandsSyncToUI.SelectHandsForUpgrade; ResetRunSaveDataToDefaults; PointsCountCycle. Offer depth-to-screen timing and all exceptional mode branches have not been played through end to end.</p>''')

skull_rows=[]
for s in skulls:
    effects={v['color']:v for v in s['variants']}
    b,r,p,y=[effects[c] for c in colors]
    effect_cells=[f'<span class="effect blue">+{b["bones"]} Bones</span>',
        f'<span class="effect red">+{r["mult"]} Mult</span>',
        f'<span class="effect purple">+{p["bones"]} Bones<br>+{p["mult"]} Mult</span>',
        f'<span class="effect yellow">+{y["rerolls"]} rerolls</span>']
    skull_rows.append([f'<strong>{esc(s["name"])}</strong><br><span class="tag">{esc(s["phase"])}</span>',
        esc(s['condition']) + '<details><summary>Example & source key</summary><p>' + esc(s['example']) + '</p><code>' + esc(s['key']) + '</code></details>',*effect_cells])
section('skulls', '04 / Complete extracted skull catalog', '''
<p><strong>74 types = 73 functional types + Blank.</strong> Every type has four serialized color variants. All 296 are referenced by the game's item database. All have metaProgressLevel = 0, so these definitions do not specify per-skull progression gates.</p>
<p>The catalog preserves current English condition text, including once-per-hand and once-per-level limits. Values are taken from each actual color asset. They are not inferred from a general color formula. Blank has no effect and is used by challenge replacement rules; its presence in the database does not establish it as an ordinary random reward.</p>
<p class="small muted">Activation phases: 54 types on playing a hand (including Blank), 7 on the initial roll, and 13 on player rerolls. Some internal trigger enum names are stale; current condition text takes precedence in this reference. Expand a row for the game's example.</p>
<div class="controls"><input id="skull-search" type="search" placeholder="Search skulls, conditions or examples" aria-label="Search skull catalog"><select id="phase-filter" aria-label="Filter activation phase"><option value="">All activation phases</option><option>Play hand</option><option>Initial roll</option><option>Player reroll</option></select></div><p id="skull-count" class="count" aria-live="polite">74 of 74 skull types</p>
''' + table(['Skull / phase','Activation condition','Blue','Red','Purple','Yellow'], skull_rows, 'skull-table') + '''
<p class="source">Evidence: 296 Item assets in sharedassets0.assets and Skulls localization. Asset suffix _green maps to the enum/display color Purple, not a fifth green skull family. Scholar and Merchant share an internal trigger ID; their current descriptions and effects are retained separately.</p>''')

standard_rows=[[i+1, f'{constants["easyPileThresholds"][i]:,}',f'{constants["pileThresholds"][i]:,}',constants['pileHands'][i],constants['pileRerolls'][i]] for i in range(25)]
daily_rows=[[i+1, f'{constants["dailyThresholds"][i]:,}',constants['dailyHands'][i],constants['dailyRerolls'][i]] for i in range(10)]
extended_rows=[[i+1,f'{v:,}', f'{constants["pileThresholds"][i]:,}' if i<len(constants['pileThresholds']) else 'No entry',constants['pileHands'][i],constants['pileRerolls'][i]] for i,v in enumerate(constants['easyPileThresholds'])]
section('levels', '05 / Level progression and exact thresholds', '''
<p>Default Standard uses the table internally called <code>easyPileThresholds</code>. The challenge advertised as "10% harder" switches to <code>pileThresholds</code>. Use the actual values below rather than multiplying the default table by exactly 1.1.</p>
<p>The initial budget is 3 hands and 3 rerolls. It rises to 4 and 4 at level 11, then 5 and 5 at level 21. These are level-start budgets before challenge/reroll-ability adjustments or skull replenishment.</p>
''' + table(['Level','Default threshold','Harder challenge','Hands','Rerolls'], standard_rows) + '<h3>Daily</h3>' + table(['Level','Threshold','Hands','Rerolls'],daily_rows) + '''
<details><summary>All extracted extended threshold entries</summary><p>The default array has 111 entries; the harder array has 73. Their final values (99,999,999 and 999,999) look like terminal/sentinel values. This table reports stored values, not a claim that every row is reachable or that the terminal behavior is known. No extrapolation beyond the arrays is supplied.</p>''' + table(['Index + 1','Default / Endless table','Harder table','Hands','Rerolls'],extended_rows) + '</details><p class="source">Evidence: ConstantStorage static data, SHA-256-matched against metadata-backed initialization blobs; runtime table selection in ResetRunSaveDataToDefaults.</p>')

mode_rows=[['Standard','Available','25 levels','Ordinary skull and hand-upgrade progression.'],
['Endless','Win Standard','Continues after level 25','No more yellow skull offers or additional level-start hands/rerolls after 25. Some records/statistics are excluded.'],
['Challenge','Win Standard','Standard-length challenge run','Choose restrictions; win with enough challenge points to unlock more. Total score bonus: +15% for each 5 selected challenge points.'],
['Daily','Reach level 15','10 levels','Starts with eight prescribed skulls; all hands locked to level 2. A new daily challenge replaces it each day. Some records/statistics are excluded.']]
challenge_rows=[[c['index'],esc(c['condition']),c['points'],c['unlockWinPoints']] for c in challenges]
section('modes','06 / Modes and all challenge modifiers',table(['Mode','Unlock','Structure','Special rules'],mode_rows) + '''
<p>The four mode entries and their unlock gates were checked on the connected phone. This profile currently has the additional modes locked, so their full runs were not exercised. Their rules above are the shipped menu descriptions, corroborated where possible by constants and code.</p>
<h3>Challenge selection</h3><p>Unlock requirements are points on a winning challenge run, not spendable currency. A requirement of 0 means the first tier after Challenge mode itself is unlocked. Two different modifiers each remove one level-start reroll; selecting both is intended to stack the restriction.</p>
''' + table(['Internal index','Modifier','Challenge points','Win points needed to unlock'],challenge_rows) + '''
<p>The data maps index 5 to "no upgrade Mult" and index 8 to "no upgrade Bones", despite the localization keys suggesting a different ordering. The table follows actual scene event bindings and native scoring checks. The score bonus is a run-score modifier; it should not be assumed to increase the hand score applied to the level threshold.</p>
<p class="source">Evidence: live mode menu; MenuHandler.gameModeParents; Challenge event bindings; challengePoints and challengeThresholds arrays; GameUI mode descriptions; DoPreviewCalc challenge branches. Daily reset timezone and exact seed distribution were not established.</p>''')

event_rows=[
['First-run teaching','Tutorial sequence','Introduces dice selection, scoring, category ranks, hand limits and skull rewards. Three of the 22 text slots are marked REMOVED.'],
['Run entrance / continuation','Intro and resume dialogue','Presentation on entering or resuming a run; multiple text alternatives.'],
['Initial roll','7 skull types','Evaluate initial-roll conditions, including hand/level progress conditions. Exact conditions are in the skull catalog.'],
['Player reroll','13 skull types','Evaluate selected/rerolled dice conditions; catalog wording contains repeat limits.'],
['Play hand','53 functional skull types plus Blank','Evaluate dice patterns, sums, ordering, hand types and resource history.'],
['Level clear','Reward transition','Skull selection and hand upgrading; subsequent level budgets and threshold load.'],
['Level 11 / 21 escalation','FirstIncrease / SecondIncrease / IncreaseEnd','Higher thresholds plus one more starting hand and reroll at each boundary.'],
['Scoring reactions','Five praise pools','Bad, Okay, Decent, Good and Great response families. Exact numeric selection cutoffs not decoded.'],
['Ordinary loss','Five death pools','Feedback varies by progress/score; the exact pool boundaries are not decoded.'],
['Second chance','Offer and restore pools','Restart-level interaction, with a free once-per-day option in mobile text. Official 1.6.2 notes describe an offer after level 5, once per run. Eligibility exceptions and retry-state restoration were not fully traced.'],
['Victory / endless / daily','Mode-specific endings','Ending dialogue, results, records and mode progression.'],
['Reaper interaction / hints','PokeDialogue, Hint and LossSkullHint','Flavor reactions and advice, including a hint to reroll poor skull offers.'],
['Cosmetics','Hat/Cape dialogue','Reaper comments associated with cosmetic choices.'],
['Cursed mode','Intro and rib-related dialogue','Shipped but not present in the inspected mode selector. See the unused/unconfirmed section.']]
group_rows=[[esc(k),v] for k,v in sorted(dialogue_groups.items())]
section('events','07 / Events and encounter structure', '''
<p><strong>No independent random encounter/event-choice catalog was found</strong> in the decoded gameplay assets and localization. The visible design is built around skull activation events, level transitions, challenge restrictions and reactive Reaper dialogue. This is evidence about this build, not proof that no hidden branch exists.</p>
''' + table(['Event / trigger','Content','Role'],event_rows) + '''
<p>The localization contains 424 dialogue rows, including unused/demo/Cursed material. These are text variants, not 424 distinct gameplay events. The companion JSON retains every dialogue key and its English text so the full inventory can be inspected without expanding the main report into a script transcript.</p>
<p class="source">Local evidence: Dialogues table and DialogueHandler/BattleHandler metadata. External cross-check for second chance only: <a href="https://steamcommunity.com/app/3885520/allnews/">official Dice of Kalma update announcements</a>.</p>
<details><summary>Complete dialogue-family inventory and row counts</summary>''' + table(['Localization family','Rows'],group_rows) + '</details>')

achievement_rows=[[esc(a['name']),esc(a['condition']),'<code>'+esc(a['key'])+'</code>'] for a in achievements]
cosmetic_rows=[[esc(c['type']),esc(c['assetName']),esc(c['unlockDescription']),c['unlockAchievementId']] for c in sorted(cosmetics,key=lambda c:(c['type'],int(c['assetName'].split('_')[0]))) ]
section('progression','08 / Profile progression, cosmetics and commercial layer', '''
<p>The extracted progression inventory contains <strong>114 achievement name/description pairs</strong> and <strong>76 cosmetic assets: 27 hats, 27 capes and 22 dice skins</strong>. Achievement text spans tutorial completion, level milestones, wins, score/build feats, hand-use feats and individual skull activations. Text presence alone does not prove every achievement is exposed on every platform.</p>
<p>Cosmetic assets link to achievement requirements or supporter entitlements. Sapphire, Emerald and Ruby supporter tiers have matching cosmetic sets. The mobile UI also contains ad removal, temporary ad removal via rewarded ads, and second-chance messaging. Store prices, current availability, ad timing and entitlement behavior were not tested.</p>
<p>Recorded statistics include runs started/won, best total score, fastest victory, best hand, victories with the fewest hands or rerolls, and skull-activation records. Endless and Daily explicitly exclude some statistics; the precise exclusion matrix has not been reconstructed.</p>
<details><summary>All 114 achievement definitions</summary>''' + table(['Name','Requirement','Source key'],achievement_rows) + '''</details>
<details><summary>All 76 cosmetic definitions and unlock labels</summary><p>Asset names are shown because many cosmetics are identified by their asset rather than a separate localized name. Negative achievement IDs encode special handling; do not interpret them as literal achievement indices. Colored dice variants retain the same displayed unlock description but may have additional entitlement handling.</p>''' + table(['Type','Asset','Displayed unlock requirement','Raw unlock ID'],cosmetic_rows) + '</details><p class="source">Evidence: GameUI achievement/store/statistic text; SkinData assets and their unlock keys. No purchases, ad views or profile modifications were made.</p>')

ability_rows=[[esc(a['name']),esc(a['description'])] for a in abilities]
section('unused','09 / Other content shipped in the build', '''
<div class="callout"><strong>Availability boundary:</strong> these entries are present in the installed files. They are not all confirmed as selectable gameplay in this release. The live selector exposes only Standard, Endless, Challenge and Daily.</div>
<h3>Cursed mode</h3>
<p>A fifth enum value, native branches and localized rules exist for Cursed mode. Its described loop gives temporary skulls for a level that add directly to Total. At the end of most levels, the player takes a cursed skull; activating cursed skulls loses Ribs. Zero Ribs loses the run. Failure to clear a level halves remaining Ribs but allows the run to continue, while clearing restores some Ribs.</p>
<p>The initial Ribs, regeneration amount, temporary-skull distributions and exact accessibility are not established. This should be treated as shipped design material, not a confirmed fifth playable mode.</p>
<h3>Reroll abilities</h3><p>Nine reroll ability definitions exist. Classic is the ordinary selected-dice reroll. The inspected scene uses ability value 0 (Classic); an accessible selector for the other eight was not found. Descriptions below are extracted rules, not a claim of release availability.</p>
''' + table(['Ability','Shipped description'],ability_rows) + '''
<h3>Inactive hand categories</h3><p>Flush is described as five dice of the same parity, with a stored base of 15 Bones and 4 Mult. Flush House is a full house of the same parity, stored as 20 Bones and 7 Mult. Both have zero upgrade caps and are skipped by the inspected classifier and upgrade selection, so neither belongs in the active eight-category scoring table.</p>
<p class="source">Evidence: GameMode/RerollAbilityType/DiceHand enums; menu scene wiring; BattleHandler serialized rerollAbility; English localization; hand-classifier native return values.</p>''')

source_rows=[['Installed package','fi.Pepperbox.DiceOfKalma, version 1.6.3, versionCode 644, menu build 1.6.3-15092026-A.'],
['Runtime','Unity 6000.0.58f2, Android ARM64 IL2CPP.'],
['Assets','Unity bundle inside base.apk; custom MonoBehaviour type trees reconstructed from the native metadata. 1,648 custom objects decoded, including all 296 Item definitions.'],
['Rules text','233 Skulls rows, 440 GameUI rows, 21 DiceHands rows and 424 Dialogues rows.'],
['Native checks','Hand classification, score preview, upgrade caps, challenge score branches, table selection, offer color cadence and menu wiring.'],
['Live inspection','Main menu and Standard/Endless/Challenge/Daily selector, including locked-mode requirements. No run was started for the menu verification.']]
section('evidence','10 / Evidence, confidence and remaining limits', '''
<p><strong>High confidence:</strong> asset counts, every catalog value, current localized condition wording, the eight returned hand categories, base and upgrade values, challenge bindings, stored threshold arrays, and the mode gates observed on the phone.</p>
<p><strong>Reconstructed from selected code paths:</strong> overall state flow, offer cadence and filtering, reward currency, recent-upgrade suppression and formula application. These were not validated by a complete playthrough of every mode.</p>
<p><strong>Still unverified:</strong> every individual predicate against its description, all activation/reset edge cases, exact offer probabilities and exclusion history, daily seed/reset details, score-reaction cutoffs, exhaustive retry eligibility/state restoration, late-Endless terminal behavior and accessibility of unexposed content. A LuckAssist code path also exists; its actual bias and all call-site conditions were not fully characterized.</p>
<p>Some names retained in enums conflict with the current localized rules. This reference deliberately preserves the current condition wording and actual serialized values. In particular, positional tests and phrases such as "only once per hand" should be implemented from verified behavior if a byte-for-byte simulation is ever required.</p>
''' + table(['Source','Coverage'],source_rows) + '''
<h3>Machine-readable companion</h3><p><a href="dice-of-kalma-design-data.json">dice-of-kalma-design-data.json</a> contains the complete skull/variant catalog with source object IDs, active/inactive hand data, challenges, all extracted progression arrays, all achievement and cosmetic definitions, reroll abilities and the full English design-text inventory.</p>
<details><summary>Source fingerprints (SHA-256)</summary>''' + table(['File','SHA-256'],[[esc(k),'<code>'+v+'</code>'] for k,v in hashes.items()]) + '''</details>
<h3>Design implications</h3><p>This section is interpretation. Color separates an activation condition from its reward, allowing the same tactical preference to contribute Bones, Mult or reroll economy. Position-sensitive skulls create a second objective alongside order-independent poker categories. Yellow skulls can turn scoring conditions into additional opportunities to improve or activate a hand. Hand upgrades reward specialization while offer-history suppression encourages some breadth. Challenges remove pieces of those feedback loops without needing separate content sets.</p>''')

add('''<footer>Prepared from the Android installation supplied for this reference study. Game names, item text and underlying design belong to their respective creators. This document describes the inspected build, with uncertainties identified in place.</footer>
</main><script>
const search=document.getElementById('skull-search');
const phase=document.getElementById('phase-filter');
const rows=[...document.querySelectorAll('#skull-table tbody tr')];
for(const row of rows){row.dataset.search=row.textContent.toLowerCase();row.cells[0].classList.add('skull-name');row.cells[1].classList.add('skull-condition');}
function filter(){const query=search.value.toLowerCase().trim();let count=0;for(const row of rows){const match=row.dataset.search.includes(query)&&(!phase.value||row.querySelector('.tag').textContent===phase.value);row.hidden=!match;if(match)count++;}document.getElementById('skull-count').textContent=count+' of '+rows.length+' skull types';}
search.addEventListener('input',filter);phase.addEventListener('change',filter);
</script></body></html>''')
(out/'dice-of-kalma-design-reference.html').write_text('\n'.join(parts),encoding='utf-8')
assert len(skulls)==74 and all(len(s['variants'])==4 for s in skulls)
assert len(achievements)==114 and all(a['condition'] for a in achievements)
assert len(cosmetics)==76 and len(challenges)==12
print(json.dumps(data['counts'],indent=2))
print(out/'dice-of-kalma-design-reference.html')
