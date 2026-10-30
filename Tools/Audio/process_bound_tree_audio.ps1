<#
.SYNOPSIS
Processes the Pixabay Bound Tree recordings into the swamp night beds, tree creaks, deep groans and the nymph's sobs.

.DESCRIPTION
Reads the raw Pixabay downloads listed below from -Source and writes to -Output:
- two stereo 44.1 kHz signed 16-bit PCM WAV ambience loops: the Kingdoms of the Night swamp bed, built from the
  listed source segments joined by equal-power crossfades around the non-natural events, and the quieter Frogs
  and Crickets layer. Each is highpassed at 60 Hz, closed with an equal-power tail/head loop crossfade and
  brought to its integrated loudness target with one gain step;
- the installed ambience loop, the swamp bed with the frogs layer baked in: the theme plays one ambience loop, so the
  Frogs and Crickets source is re-cut to the swamp bed's exact length with the same highpass and loop crossfade,
  brought to the frogs layer's own target alone and summed sample-aligned with the swamp bed WAV. Both loops have the
  same length, so the sum loops where each does; the two separate loops stay for later swaps;
- one mono 44.1 kHz signed 16-bit PCM WAV per one-shot clip (creaks, groans, sobs), sliced at the listed source
  range, brought to -3 dBFS, band-limited, trimmed of trailing silence with a fade out and peak-normalized to its
  tier's level; groans are also pitched down by a factor of 0.84 (about 3 semitones, about 16 percent slower);
- MP3 previews of every clip to -Output/preview: per clip, a seam audition per loop (its last and first 8 s, the
  combined loop included),
  one combined preview per group, and two passes of the Kingdoms loop mixed with the Frogs loop;
- MP3 audition clips of each flagged event in the Kingdoms source with 1 s of context to -Output/events;
- processing-report.json (source hashes, ranges, gains, levels, output hashes) and listing.txt.
The script refuses to replace an existing output file, so verify a recipe change in a fresh output folder.
Requires ffmpeg and ffprobe on PATH.

The source records and the clip hashes are in Docs/Reference/Audio/pixabay-bound-tree/provenance.json.
#>
param(
    [string]$Source = (Join-Path $PSScriptRoot '../../Docs/work/bound-tree-audio/downloads'),
    [string]$Output = (Join-Path $PSScriptRoot '../../Docs/work/bound-tree-audio/processed')
)
$ErrorActionPreference = 'Stop'
$Source = [System.IO.Path]::GetFullPath($Source)
$Output = [System.IO.Path]::GetFullPath($Output)
$previewFolder = Join-Path $Output 'preview'
$eventFolder = Join-Path $Output 'events'
$inv = [System.Globalization.CultureInfo]::InvariantCulture
function F([double]$value) { $value.ToString('0.######', $inv) }

$kingdoms = 'bed-kingdoms-of-the-night.mp3'
$frogs = 'bed-frogs-and-crickets-excerpt-b.mp3'
$woodCreak = 'creak-wood-creak.mp3'
$woodland = 'creak-creaky-tree-in-woodland.mp3'
$sobbing = 'sob-sobbing-breathing.mp3'
$crying = 'sob-woman-crying-softly.mp3'

# Events in the Kingdoms recording that do not belong to the insect, frog and water texture, found from band
# levels against a 10 s running median, spectral flux and spectrograms. id, start and end seconds, whether the
# bed excludes it, reason. Each audition clip adds 1 s of context on both sides.
$events = @(
    @('E1', 4.40, 6.75, $true, 'Abrupt broadband block with hard edges at about 4.55 s and 6.45 s (+25 dB at 3-6 kHz and +22 dB above 8 kHz over the local median) and isolated tonal partials near 1.9 and 2.8 kHz at 5.0 s. The 4.4 s before it is a different scene without the insect band.'),
    @('E2', 35.25, 37.40, $true, 'Smooth 1.7 s broadband swell (+15.6 dB above 7.5 kHz, +10 dB at 60-250 Hz) with evenly spaced comb notches sweeping across the spectrum, a moving-reflection signature unlike the insect and frog texture.'),
    @('E3', 64.20, 66.50, $true, 'Broadband swell of the same comb-swept kind as E2, weaker (+8.6 dB above 7.5 kHz, +9 dB at 60-250 Hz).'),
    @('E4', 108.15, 110.45, $true, 'Broadband swell of the same comb-swept kind as E2, the strongest (+16.5 dB above 7.5 kHz, +15 dB at 60-250 Hz).'),
    @('E5', 92.65, 93.50, $true, 'Single 0.3 s broadband thump (+11 dB above 7.5 kHz, +9 dB at 60-250 Hz) with a low tail: a handling bump or knock, possibly a splash.'),
    @('E6', 118.00, 120.20, $true, 'Three steady tonal glides near 400 Hz (118.3, 118.9 and 119.6 s) over a low rumble (+18 dB at 400-3000 Hz, +10 dB at 60-250 Hz): a bullfrog or a hinge squeal, uncertain.'),
    @('M1', 52.90, 53.30, $false, 'Single short broadband click with a low tail inside the bed window; likely a drip or a frog. Kept in the bed; listed for confirmation.')
)

# Ambience loops. name, source file, use, source segments (start and end seconds), crossfade seconds at each cut,
# loop crossfade seconds, integrated loudness target LUFS. The Kingdoms segments skip E3 and E5; E1, E2, E4 and E6
# lie outside them. The Frogs layer sits 8 LU below the Kingdoms bed so it can play with or instead of it.
$beds = @(
    @('bound_tree_swamp_bed_loop', $kingdoms, 'Swamp night bed (ambience slot)', @(@(37.40, 64.20), @(66.50, 92.65), @(93.50, 108.15)), 1.0, 2.5, -39.5),
    @('bound_tree_frogs_layer_loop', $frogs, 'Quiet frog and cricket layer, with or instead of the swamp bed', @(, @(0.50, 79.00)), 1.0, 2.5, -47.5)
)

# The installed ambience loop: name, use, and the Frogs source start in seconds. The frogs are re-cut from that start
# to the swamp bed's length plus the loop crossfade, closed with the frogs layer's loop crossfade and set to its target.
$bedWithFrogs = @('bound_tree_swamp_bed_with_frogs_loop', 'Swamp night bed with the quiet frog and cricket layer (ambience slot)', 0.50)

# Target peak dBFS per tier: the random creak matches the original room creak, the mark/bind groan sits between
# the prop burst and hit tiers, ambient sobs sit 3 dB under the ambient giggles and the beat sob is louder.
$tiers = @{ creak = -14; groan = -12; sob = -15; sob_cue = -12 }

# name, kind, tier, source file, source start and end seconds, fade in and fade out seconds, use. Each start sits a
# few milliseconds before the event's measured onset (creaks in the near-silent Wood Creak recording) or at a
# quiet point between phrases. The woodland creaks rise out of continuous wind, so they fade in over 0.15 s.
$clips = @(
    @('bound_tree_creak_01', 'creak', 'creak', $woodCreak, 0.57, 1.55, 0.003, 0.12, 'Random tree creak'),
    @('bound_tree_creak_02', 'creak', 'creak', $woodCreak, 10.39, 11.85, 0.003, 0.15, 'Random tree creak'),
    @('bound_tree_creak_03', 'creak', 'creak', $woodCreak, 12.27, 13.55, 0.003, 0.15, 'Random tree creak'),
    @('bound_tree_creak_04', 'creak', 'creak', $woodCreak, 13.75, 14.78, 0.003, 0.12, 'Random tree creak'),
    @('bound_tree_creak_05', 'creak', 'creak', $woodCreak, 14.95, 15.52, 0.003, 0.10, 'Random tree creak'),
    @('bound_tree_creak_06', 'creak', 'creak', $woodCreak, 17.45, 19.48, 0.003, 0.20, 'Random tree creak'),
    @('bound_tree_creak_07', 'creak', 'creak', $woodCreak, 19.97, 20.50, 0.003, 0.10, 'Random tree creak'),
    @('bound_tree_creak_08', 'creak', 'creak', $woodCreak, 21.59, 22.88, 0.003, 0.15, 'Random tree creak'),
    @('bound_tree_creak_09', 'creak', 'creak', $woodCreak, 24.25, 24.82, 0.003, 0.10, 'Random tree creak'),
    @('bound_tree_creak_10', 'creak', 'creak', $woodCreak, 29.31, 30.98, 0.003, 0.20, 'Random tree creak'),
    @('bound_tree_creak_11', 'creak', 'creak', $woodCreak, 31.39, 32.45, 0.003, 0.15, 'Random tree creak'),
    @('bound_tree_tree_creak_01', 'creak', 'creak', $woodland, 2.00, 4.20, 0.15, 0.20, 'Random tree creak, woodland alternative'),
    @('bound_tree_tree_creak_02', 'creak', 'creak', $woodland, 17.00, 18.80, 0.15, 0.20, 'Random tree creak, woodland alternative'),
    @('bound_tree_tree_creak_03', 'creak', 'creak', $woodland, 20.80, 22.40, 0.15, 0.20, 'Random tree creak, woodland alternative'),
    @('bound_tree_tree_creak_04', 'creak', 'creak', $woodland, 27.20, 29.60, 0.15, 0.20, 'Random tree creak, woodland alternative'),
    @('bound_tree_tree_creak_05', 'creak', 'creak', $woodland, 35.00, 37.60, 0.15, 0.20, 'Random tree creak, woodland alternative'),
    @('bound_tree_tree_creak_06', 'creak', 'creak', $woodland, 47.60, 50.00, 0.15, 0.20, 'Random tree creak, woodland alternative'),
    # The deepest Wood Creak events, pitched down so they read as the giant trunk.
    @('bound_tree_groan_01', 'groan', 'groan', $woodCreak, 12.27, 13.55, 0.003, 0.25, 'Deep groan on mark and bind'),
    @('bound_tree_groan_02', 'groan', 'groan', $woodCreak, 17.45, 19.48, 0.003, 0.25, 'Deep groan on mark and bind'),
    @('bound_tree_groan_03', 'groan', 'groan', $woodCreak, 29.31, 30.98, 0.003, 0.25, 'Deep groan on mark and bind'),
    # Each sob phrase is an in-breath followed by its sobbing out-breath.
    @('bound_tree_sob_01', 'sob', 'sob', $sobbing, 0.72, 2.15, 0.008, 0.35, 'Nymph sob, ambient one-shot'),
    @('bound_tree_sob_02', 'sob', 'sob', $sobbing, 2.15, 3.90, 0.008, 0.35, 'Nymph sob, ambient one-shot'),
    @('bound_tree_sob_03', 'sob', 'sob', $sobbing, 4.25, 5.35, 0.008, 0.35, 'Nymph sob, ambient one-shot'),
    @('bound_tree_sob_04', 'sob', 'sob', $sobbing, 5.35, 7.70, 0.008, 0.35, 'Nymph sob, ambient one-shot'),
    @('bound_tree_sob_05', 'sob', 'sob', $sobbing, 7.95, 10.30, 0.008, 0.35, 'Nymph sob, ambient one-shot (voiced, the most intense)'),
    @('bound_tree_sob_06', 'sob', 'sob', $sobbing, 11.05, 12.75, 0.008, 0.35, 'Nymph sob, ambient one-shot'),
    @('bound_tree_sob_07', 'sob', 'sob', $sobbing, 12.75, 14.40, 0.008, 0.35, 'Nymph sob, ambient one-shot (fading whimper)'),
    @('bound_tree_sob_roots_01', 'sob', 'sob_cue', $sobbing, 4.25, 7.70, 0.008, 0.35, 'Nymph sob, At the roots beat cue'),
    @('bound_tree_sob_soft_01', 'sob', 'sob', $crying, 0.10, 1.95, 0.008, 0.35, 'Nymph sob, soft crying alternative'),
    @('bound_tree_sob_soft_02', 'sob', 'sob', $crying, 2.15, 3.45, 0.008, 0.35, 'Nymph sob, soft crying alternative'),
    @('bound_tree_sob_soft_03', 'sob', 'sob', $crying, 3.85, 5.15, 0.008, 0.35, 'Nymph sob, soft crying alternative'),
    @('bound_tree_sob_soft_04', 'sob', 'sob', $crying, 5.40, 8.05, 0.008, 0.35, 'Nymph sob, soft crying alternative')
)

# Filters per kind after the slice is brought to -3 dBFS. The reversed silenceremove trims trailing silence and
# fades out over the clip's fade-out time, which also ends a slice cut inside continuing sound.
$filters = @{
    creak = 'highpass=f=40,lowpass=f=9000'
    groan = 'highpass=f=40,lowpass=f=9000'
    sob = 'highpass=f=120,lowpass=f=7500'
}
$trimThreshold = @{ creak = -60; groan = -60; sob = -62 }
$pitchFactor = 0.84
$groups = @(@('creaks', 'creak', $woodCreak), @('tree_creaks', 'creak', $woodland), @('groans', 'groan', $null), @('sobs', 'sob', $sobbing), @('sobs_soft', 'sob', $crying))

$reportPath = Join-Path $Output 'processing-report.json'
$listingPath = Join-Path $Output 'listing.txt'
$mixPreview = Join-Path $previewFolder 'bound_tree_swamp_bed_with_frogs_mix.mp3'
function EventName($e) { 'kingdoms_event_{0}_{1}-{2}s.mp3' -f $e[0], (F $e[1]), (F $e[2]) }
$outputs = @($reportPath, $listingPath, $mixPreview) +
    @(@($beds | ForEach-Object { $_[0] }) + $bedWithFrogs[0] | ForEach-Object { Join-Path $Output "$_.wav"; Join-Path $previewFolder "${_}_seam.mp3" }) +
    @($clips | ForEach-Object { Join-Path $Output "$($_[0]).wav"; Join-Path $previewFolder "$($_[0]).mp3" }) +
    @($groups | ForEach-Object { Join-Path $previewFolder "bound_tree_$($_[0])_all.mp3" }) +
    @($events | ForEach-Object { Join-Path $eventFolder (EventName $_) })
foreach ($path in $outputs) {
    if (Test-Path $path) { throw "Refusing to replace existing output $path; choose an empty output folder." }
}
foreach ($file in @($kingdoms, $frogs, $woodCreak, $woodland, $sobbing, $crying)) {
    if (-not (Test-Path (Join-Path $Source $file))) { throw "Missing source recording $(Join-Path $Source $file)." }
}
New-Item -ItemType Directory -Force $Output, $previewFolder, $eventFolder | Out-Null

function Assert-Ffmpeg([string]$what) {
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed to $what." }
}

function Get-MaxVolume([string]$path) {
    [double](([regex]::Match((& ffmpeg -hide_banner -i $path -af volumedetect -f null - 2>&1 | Out-String), 'max_volume: (-?[\d.]+) dB')).Groups[1].Value)
}

function Measure-Level([string]$path) {
    $stats = & ffmpeg -hide_banner -i $path -af astats=measure_perchannel=none -f null - 2>&1 | Out-String
    $loud = & ffmpeg -hide_banner -nostats -i $path -af ebur128=peak=true -f null - 2>&1 | Out-String
    $summary = $loud.Substring($loud.LastIndexOf('Summary:'))
    [ordered]@{
        peak = [double]([regex]::Match($stats, 'Peak level dB: (-?[\d.]+)').Groups[1].Value)
        rms = [double]([regex]::Match($stats, 'RMS level dB: (-?[\d.]+)').Groups[1].Value)
        lufs = [double]([regex]::Match($summary, 'I:\s+(-?[\d.]+) LUFS').Groups[1].Value)
        true_peak = [double]([regex]::Match($summary, 'Peak:\s+(-?[\d.]+) dBFS').Groups[1].Value)
    }
}

function Get-Samples([string]$path) {
    [long](& ffprobe -v error -select_streams a:0 -show_entries stream=duration_ts -of default=nw=1:nk=1 $path)
}

# Difference between the loop's first and last 16-bit sample, per channel (first minus last).
function Get-BoundaryDelta([string]$wav, [int]$channels) {
    $raw = "$wav.raw.tmp"
    & ffmpeg -v error -i $wav -f s16le -c:a pcm_s16le $raw
    Assert-Ffmpeg 'read the loop samples'
    $bytes = [System.IO.File]::ReadAllBytes($raw)
    Remove-Item $raw
    $frame = 2 * $channels
    $last = $bytes.Length - $frame
    @(0..($channels - 1) | ForEach-Object { [BitConverter]::ToInt16($bytes, 2 * $_) - [BitConverter]::ToInt16($bytes, $last + 2 * $_) })
}

# Closes a loop: the joined segment S of length L + X becomes S[X, L] followed by S[L, L + X] fading out summed with
# S[0, X] fading in, both quarter-sine (equal power), for a total length L.
function Close-Loop([string]$joined, [double]$loopFade, [string]$loop) {
    $total = Get-Samples $joined
    $x = [long][math]::Round($loopFade * 44100)
    $l = $total - $x
    $loopChain = "[0:a]asplit=3[h][b][t];" +
        "[h]atrim=end_sample=$x,asetpts=PTS-STARTPTS,afade=t=in:d=$(F $loopFade):curve=qsin[hi];" +
        "[b]atrim=start_sample=${x}:end_sample=$l,asetpts=PTS-STARTPTS[bo];" +
        "[t]atrim=start_sample=$l,asetpts=PTS-STARTPTS,afade=t=out:d=$(F $loopFade):curve=qsin[to];" +
        "[to][hi]amix=inputs=2:normalize=0[x];[bo][x]concat=n=2:v=0:a=1[loop]"
    & ffmpeg -v error -i $joined -filter_complex $loopChain -map '[loop]' -c:a pcm_f32le $loop
    Assert-Ffmpeg "close the loop $loop"
}

# The loop's last 8 s followed by its first 8 s, to audition the seam.
function Write-SeamAudition([string]$wav, [string]$name) {
    $duration = (Get-Samples $wav) / 44100.0
    & ffmpeg -v error -i $wav -filter_complex "[0:a]asplit=2[e][s];[e]atrim=start=$(F ($duration - 8)),asetpts=PTS-STARTPTS[end];[s]atrim=end=8,asetpts=PTS-STARTPTS[start];[end][start]concat=n=2:v=0:a=1[seam]" -map '[seam]' -c:a libmp3lame -q:a 2 (Join-Path $previewFolder "${name}_seam.mp3")
    Assert-Ffmpeg "write the seam audition of $name"
}

$report = [ordered]@{ events = @(); beds = @(); clips = @() }
$listing = @()

# Event audition clips: the unprocessed source with 1 s of context on each side.
foreach ($e in $events) {
    $start = [math]::Max(0, $e[1] - 1); $end = $e[2] + 1
    & ffmpeg -v error -i (Join-Path $Source $kingdoms) -af "atrim=start=$(F $start):end=$(F $end),asetpts=PTS-STARTPTS" -c:a libmp3lame -q:a 2 (Join-Path $eventFolder (EventName $e))
    Assert-Ffmpeg "cut event $($e[0])"
    $report.events += [ordered]@{ id = $e[0]; source = $kingdoms; range_seconds = @($e[1], $e[2]); excluded_from_bed = $e[3]; reason = $e[4];
        audition_clip = "events/$(EventName $e)"; audition_range_seconds = @($start, $end) }
}

# Ambience loops, each joined from its source segments and closed by Close-Loop.
foreach ($bed in $beds) {
    $name, $file, $use, $segments, $cutFade, $loopFade, $targetLufs = $bed
    $sourceFile = Join-Path $Source $file
    $count = $segments.Count
    $chain = "[0:a]aresample=44100,aformat=sample_fmts=fltp:sample_rates=44100:channel_layouts=stereo,highpass=f=60,asplit=$count" + (-join (0..($count - 1) | ForEach-Object { "[s$_]" })) + ';'
    for ($i = 0; $i -lt $count; $i++) {
        $chain += "[s$i]atrim=start=$(F $segments[$i][0]):end=$(F $segments[$i][1]),asetpts=PTS-STARTPTS[a$i];"
    }
    $last = 'a0'
    for ($i = 1; $i -lt $count; $i++) {
        $chain += "[$last][a$i]acrossfade=d=$(F $cutFade):c1=qsin:c2=qsin[j$i];"
        $last = "j$i"
    }
    $chain += "[$last]anull[joined]"
    $joined = Join-Path $Output "$name.joined.tmp.wav"
    & ffmpeg -v error -i $sourceFile -filter_complex $chain -map '[joined]' -c:a pcm_f32le $joined
    Assert-Ffmpeg "join the segments of $name"
    $loop = Join-Path $Output "$name.loop.tmp.wav"
    Close-Loop $joined $loopFade $loop
    $gain = $targetLufs - (Measure-Level $loop).lufs
    $wav = Join-Path $Output "$name.wav"
    & ffmpeg -v error -i $loop -af "volume=$(F $gain)dB" -ar 44100 -ac 2 -c:a pcm_s16le $wav
    Assert-Ffmpeg "set the level of $name"
    Remove-Item $joined, $loop
    $duration = (Get-Samples $wav) / 44100.0
    Write-SeamAudition $wav $name
    $level = Measure-Level $wav
    $cuts = @()
    for ($i = 1; $i -lt $count; $i++) { $cuts += , @($segments[$i - 1][1], $segments[$i][0]) }
    $report.beds += [ordered]@{ clip = "$name.wav"; use = $use; source = $file; source_sha256 = (Get-FileHash $sourceFile).Hash.ToLower();
        source_segments_seconds = $segments; removed_ranges_seconds = $cuts; cut_crossfade_seconds = $cutFade; loop_crossfade_seconds = $loopFade;
        target_lufs = $targetLufs; gain_db = [math]::Round($gain, 2); loop_duration_seconds = [math]::Round($duration, 6);
        loop_boundary_delta = (Get-BoundaryDelta $wav 2); integrated_lufs = $level.lufs; true_peak_dbfs = $level.true_peak;
        peak_dbfs = [math]::Round($level.peak, 2); rms_dbfs = [math]::Round($level.rms, 2); sha256 = (Get-FileHash $wav).Hash.ToLower() }
    $listing += [ordered]@{ file = "$name.wav"; purpose = $use; seconds = $duration; level = "$($level.lufs) LUFS, peak $([math]::Round($level.peak, 1)) dBFS" }
}

# Two passes of the swamp bed with the Frogs layer looped beside it, as two independent sources would play.
$bedWav = Join-Path $Output "$($beds[0][0]).wav"
$frogWav = Join-Path $Output "$($beds[1][0]).wav"
& ffmpeg -v error -stream_loop 1 -i $bedWav -stream_loop -1 -i $frogWav -filter_complex '[0:a][1:a]amix=inputs=2:normalize=0:duration=first[mix]' -map '[mix]' -c:a libmp3lame -q:a 2 $mixPreview
Assert-Ffmpeg 'mix the bed preview'
$mixLevel = Measure-Level $mixPreview
$report.bed_mix_preview = [ordered]@{ file = 'preview/bound_tree_swamp_bed_with_frogs_mix.mp3'; integrated_lufs = $mixLevel.lufs; true_peak_dbfs = $mixLevel.true_peak }

# The swamp bed with the frogs layer baked in, at the swamp bed's exact length.
$name, $use, $frogStart = $bedWithFrogs
$frogFile, $frogLoopFade, $frogTarget = $beds[1][1], $beds[1][5], $beds[1][6]
$frogSourceFile = Join-Path $Source $frogFile
$bedSamples = Get-Samples $bedWav
$startSample = [long][math]::Round($frogStart * 44100)
$endSample = $startSample + $bedSamples + [long][math]::Round($frogLoopFade * 44100)
$frogCut = Join-Path $Output "$name.frogs.tmp.wav"
& ffmpeg -v error -i $frogSourceFile -af "aresample=44100,aformat=sample_fmts=fltp:sample_rates=44100:channel_layouts=stereo,highpass=f=60,atrim=start_sample=${startSample}:end_sample=$endSample,asetpts=PTS-STARTPTS" -c:a pcm_f32le $frogCut
Assert-Ffmpeg "cut the frogs layer of $name"
$frogLoop = Join-Path $Output "$name.frogs.loop.tmp.wav"
Close-Loop $frogCut $frogLoopFade $frogLoop
if ((Get-Samples $frogLoop) -ne $bedSamples) { throw "The frogs layer of $name does not match the swamp bed's $bedSamples samples." }
$frogLufs = (Measure-Level $frogLoop).lufs
$frogGain = $frogTarget - $frogLufs
$wav = Join-Path $Output "$name.wav"
& ffmpeg -v error -i $bedWav -i $frogLoop -filter_complex "[1:a]volume=$(F $frogGain)dB[f];[0:a][f]amix=inputs=2:normalize=0[mix]" -map '[mix]' -ar 44100 -ac 2 -c:a pcm_s16le $wav
Assert-Ffmpeg "mix $name"
Remove-Item $frogCut, $frogLoop
if ((Get-Samples $wav) -ne $bedSamples) { throw "$name does not match the swamp bed's $bedSamples samples." }
$duration = $bedSamples / 44100.0
Write-SeamAudition $wav $name
$level = Measure-Level $wav
$report.bed_with_frogs = [ordered]@{ clip = "$name.wav"; use = $use; bed = "$($beds[0][0]).wav"; bed_sha256 = (Get-FileHash $bedWav).Hash.ToLower();
    frogs_source = $frogFile; frogs_source_sha256 = (Get-FileHash $frogSourceFile).Hash.ToLower();
    frogs_source_range_seconds = @($frogStart, [math]::Round($endSample / 44100.0, 6)); frogs_loop_crossfade_seconds = $frogLoopFade;
    frogs_target_lufs = $frogTarget; frogs_gain_db = [math]::Round($frogGain, 2); loop_duration_seconds = [math]::Round($duration, 6);
    loop_boundary_delta = (Get-BoundaryDelta $wav 2); integrated_lufs = $level.lufs; true_peak_dbfs = $level.true_peak;
    peak_dbfs = [math]::Round($level.peak, 2); rms_dbfs = [math]::Round($level.rms, 2); sha256 = (Get-FileHash $wav).Hash.ToLower() }
$listing += [ordered]@{ file = "$name.wav"; purpose = $use; seconds = $duration; level = "$($level.lufs) LUFS, peak $([math]::Round($level.peak, 1)) dBFS" }

foreach ($clip in $clips) {
    $name, $kind, $tier, $file, $start, $end, $fadeIn, $fadeOut, $use = $clip
    $sourceFile = Join-Path $Source $file
    $pitch = if ($kind -eq 'groan') { ",asetrate=$([int][math]::Round(44100 * $pitchFactor)),aresample=44100" } else { '' }
    $slice = "aresample=44100,aformat=sample_fmts=fltp:sample_rates=44100:channel_layouts=mono," +
        "atrim=start=$(F $start):end=$(F $end),asetpts=PTS-STARTPTS,afade=t=in:d=$(F $fadeIn)$pitch"
    $cut = Join-Path $Output "$name.cut.tmp.wav"
    & ffmpeg -v error -i $sourceFile -af $slice -c:a pcm_f32le $cut
    Assert-Ffmpeg "slice $name"
    $pre = -3 - (Get-MaxVolume $cut)
    $raw = Join-Path $Output "$name.tmp.wav"
    $treatment = "volume=$(F $pre)dB,$($filters[$kind])," +
        "areverse,silenceremove=start_periods=1:start_threshold=$($trimThreshold[$kind])dB,afade=t=in:d=$(F $fadeOut),areverse"
    & ffmpeg -v error -i $cut -af $treatment -c:a pcm_f32le $raw
    Assert-Ffmpeg "process $name"
    $peak = $tiers[$tier]
    $gain = $peak - (Get-MaxVolume $raw)
    $wav = Join-Path $Output "$name.wav"
    & ffmpeg -v error -i $raw -af "volume=$(F $gain)dB" -ar 44100 -ac 1 -c:a pcm_s16le $wav
    Assert-Ffmpeg "normalize $name"
    Remove-Item $cut, $raw
    & ffmpeg -v error -i $wav -c:a libmp3lame -q:a 2 (Join-Path $previewFolder "$name.mp3")
    Assert-Ffmpeg "write the preview of $name"
    $duration = (Get-Samples $wav) / 44100.0
    $level = Measure-Level $wav
    $entry = [ordered]@{ clip = "$name.wav"; use = $use; kind = $kind; tier = $tier; source = $file; source_sha256 = (Get-FileHash $sourceFile).Hash.ToLower();
        source_range_seconds = @($start, $end); fade_in_seconds = $fadeIn; fade_out_seconds = $fadeOut; target_peak_dbfs = $peak; gain_db = [math]::Round($pre + $gain, 2);
        duration_seconds = [math]::Round($duration, 3); peak_dbfs = [math]::Round($level.peak, 2); rms_dbfs = [math]::Round($level.rms, 2);
        sha256 = (Get-FileHash $wav).Hash.ToLower() }
    if ($kind -eq 'groan') { $entry.pitch_factor = $pitchFactor }
    $report.clips += $entry
    $listing += [ordered]@{ file = "$name.wav"; purpose = $use; seconds = $duration; level = "peak $([math]::Round($level.peak, 1)) dBFS, RMS $([math]::Round($level.rms, 1)) dBFS" }
}

# One preview per group: its clips in order, each followed by 0.8 seconds of silence.
foreach ($group in $groups) {
    $members = @($clips | Where-Object { $_[1] -eq $group[1] -and ($null -eq $group[2] -or $_[3] -eq $group[2]) })
    $inputs = @(); $chain = ''
    for ($i = 0; $i -lt $members.Count; $i++) {
        $inputs += '-i', (Join-Path $Output "$($members[$i][0]).wav")
        $chain += "[${i}:a]apad=pad_dur=0.8[a$i];"
    }
    $chain += (-join (0..($members.Count - 1) | ForEach-Object { "[a$_]" })) + "concat=n=$($members.Count):v=0:a=1[all]"
    & ffmpeg -v error @inputs -filter_complex $chain -map '[all]' -c:a libmp3lame -q:a 2 (Join-Path $previewFolder "bound_tree_$($group[0])_all.mp3")
    Assert-Ffmpeg "write the $($group[0]) preview"
}

$lines = @('Bound Tree audio for audition. Loops: stereo 44.1 kHz 16-bit; one-shots: mono 44.1 kHz 16-bit.', '',
    ('{0,-40} {1,7}  {2,-36} {3}' -f 'file', 'seconds', 'level', 'purpose'))
foreach ($row in $listing) { $lines += ('{0,-40} {1,7}  {2,-36} {3}' -f $row.file, ($row.seconds.ToString('0.000', $inv)), $row.level, $row.purpose) }
$lines += '', "Mix preview: preview/bound_tree_swamp_bed_with_frogs_mix.mp3, two passes of the swamp bed with the frogs layer, $($mixLevel.lufs) LUFS, true peak $($mixLevel.true_peak) dBFS."
$lines += 'Previews: preview/<clip>.mp3, preview/<loop>_seam.mp3 (last 8 s then first 8 s), preview/bound_tree_<group>_all.mp3.'
$lines += 'Flagged Kingdoms events with 1 s of context: events/.'
Set-Content -Path $listingPath -Value $lines -Encoding ascii

$json = $report | ConvertTo-Json -Depth 5
Set-Content -Path $reportPath -Value $json -Encoding ascii
$json
