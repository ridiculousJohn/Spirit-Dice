<#
.SYNOPSIS
Processes the Pixabay Sunken Larder recordings into the hut's bed, its creaks, the jar one-shots and the rot clips.

.DESCRIPTION
Reads the raw Pixabay downloads listed below from -Source and writes to -Output:
- one stereo 44.1 kHz signed 16-bit PCM WAV ambience loop of the small hearth fire: highpassed at 60 Hz, cut to the
  loop length plus the loop crossfade, closed with an equal-power tail/head crossfade to exactly the loop length and
  brought to the bed's integrated loudness target with one gain step;
- one mono 44.1 kHz signed 16-bit PCM WAV per one-shot clip (creaks, jar clinks, jar murmurs, rot step, withering,
  restoring), sliced at the listed source range with the listed fade in, optionally pitched, brought to -3 dBFS,
  filtered per kind, trimmed of trailing silence with a fade out and peak-normalized to its tier's level. Murmurs
  also get a faint glass ring: a copy through two very short echoes (a comb ringing near 880 Hz and its multiples),
  highpassed and mixed under the voice;
- MP3 auditions to -Audition: the bed loop played twice end to end (to hear the seam), its last and first 8 s, every
  clip, and one combined preview per group;
- processing-report.json (source hashes, ranges, gains, levels, output hashes) and listing.txt in -Output.
The script refuses to replace an existing output file, so verify a recipe change in fresh output folders.
Requires ffmpeg and ffprobe on PATH.

The source records and the clip hashes are in Docs/Reference/Audio/pixabay-sunken-larder/provenance.json.
#>
param(
    [string]$Source = (Join-Path $PSScriptRoot '../../Docs/work/sunken-larder-audio/downloads'),
    [string]$Output = (Join-Path $PSScriptRoot '../../Docs/work/sunken-larder-audio/processed'),
    [string]$Audition = (Join-Path $PSScriptRoot '../../Docs/work/sunken-larder-audio/audition')
)
$ErrorActionPreference = 'Stop'
$Source = [System.IO.Path]::GetFullPath($Source)
$Output = [System.IO.Path]::GetFullPath($Output)
$Audition = [System.IO.Path]::GetFullPath($Audition)
$inv = [System.Globalization.CultureInfo]::InvariantCulture
function F([double]$value) { $value.ToString('0.######', $inv) }

$fire = 'bed-fire-little-flame.mp3'
$building = 'creak-old-wood-building.mp3'
$ship = 'creak-wood-creaking-ship.mp3'
$bottles = 'jar-bottles-clinking.mp3'
$jars = 'jar-glass-jars.mp3'
$mumble = 'murmur-indistinct-deep-mumble.mp3'
$talking = 'murmur-incoherent-talking.mp3'
$rubber = 'rot-rubber-mask-stretch-wet.mp3'
$sponge = 'rot-wet-sponge-squeeze.mp3'
$mush = 'wither-squelch-mush.mp3'
$wetSquelch = 'wither-wet-squelch.mp3'
$seaweed = 'restore-seaweed-dry-crumble.mp3'
$leaf = 'restore-dry-leaf.mp3'

# The bed: the small fire alone, one loop of the loop length closed with the loop crossfade and brought to the
# installed Bound Tree bed's integrated loudness (bound_tree_swamp_bed_with_frogs_loop.wav, -38.9 LUFS). Its source
# window ends before the recording's first big log pop (64.68 s; others at 84.92 and 101.08 s).
$loopSeconds = 60.0
$loopFade = 2.5
$bedName = 'sunken_larder_fire_bed_loop'
$bedUse = 'Small hearth fire (ambience slot)'
$bedSourceRange = @(1.4, 64.0)
$bedFilter = 'highpass=f=60'
$bedTarget = -38.9

# Target peak dBFS per tier. Creaks match the Bound Tree's random creak; the jar clinks sit 3 dB under the nymph's
# ambient sobs and the murmurs 2 dB under the clinks; the rot step is quiet (it plays on every reroll), the
# withering is the loudest rot sound and the restoring sits between.
$tiers = @{ creak = -14; clink = -18; murmur = -20; rot = -21; wither = -15; restore = -18 }

# name, kind, tier, source file, source start and end seconds, fade in and fade out seconds, pitch factor, use,
# installed. Each start sits a few milliseconds before the event's measured onset (the Old Wood Creaking Building
# recording is gated to silence between creaks) or, in a continuous recording, in its bed between events.
$clips = @(
    @('sunken_larder_creak_01', 'creak', 'creak', $building, 11.77, 12.95, 0.003, 0.15, 1.0, 'The hut settling on its stilts', $true),
    @('sunken_larder_creak_02', 'creak', 'creak', $building, 18.38, 18.97, 0.003, 0.10, 1.0, 'The hut settling on its stilts', $true),
    @('sunken_larder_creak_03', 'creak', 'creak', $building, 25.32, 27.06, 0.003, 0.20, 1.0, 'The hut settling on its stilts', $true),
    @('sunken_larder_creak_04', 'creak', 'creak', $building, 30.00, 30.58, 0.003, 0.10, 1.0, 'The hut settling on its stilts', $true),
    @('sunken_larder_creak_05', 'creak', 'creak', $building, 48.17, 48.97, 0.003, 0.12, 1.0, 'The hut settling on its stilts', $true),
    @('sunken_larder_creak_06', 'creak', 'creak', $building, 58.28, 60.17, 0.003, 0.20, 1.0, 'The hut settling on its stilts', $true),
    @('sunken_larder_creak_07', 'creak', 'creak', $building, 89.03, 90.60, 0.003, 0.20, 1.0, 'The hut settling on its stilts', $true),
    @('sunken_larder_creak_08', 'creak', 'creak', $building, 111.80, 113.52, 0.003, 0.20, 1.0, 'The hut settling on its stilts', $true),
    # Wood Creaking rocks in groups of short creaks about every 4 s; each slice is one group.
    @('sunken_larder_sway_creak_01', 'creak', 'creak', $ship, 7.95, 9.45, 0.05, 0.15, 1.0, 'The hut swaying on its stilts', $true),
    @('sunken_larder_sway_creak_02', 'creak', 'creak', $ship, 16.55, 18.25, 0.05, 0.15, 1.0, 'The hut swaying on its stilts', $true),
    @('sunken_larder_sway_creak_03', 'creak', 'creak', $ship, 20.68, 21.95, 0.05, 0.15, 1.0, 'The hut swaying on its stilts', $true),
    @('sunken_larder_sway_creak_04', 'creak', 'creak', $ship, 39.15, 40.50, 0.05, 0.15, 1.0, 'The hut swaying on its stilts', $true),
    @('sunken_larder_sway_creak_05', 'creak', 'creak', $ship, 43.30, 44.50, 0.05, 0.15, 1.0, 'The hut swaying on its stilts', $true),
    @('sunken_larder_sway_creak_06', 'creak', 'creak', $ship, 51.40, 52.70, 0.05, 0.15, 1.0, 'The hut swaying on its stilts', $true),
    # Single soft touches of glass, each ending before the next onset; 25.335 is two touches.
    @('sunken_larder_jar_clink_01', 'clink', 'clink', $bottles, 3.44, 4.10, 0.004, 0.12, 1.0, 'Jars clinking, ambient one-shot', $true),
    @('sunken_larder_jar_clink_02', 'clink', 'clink', $bottles, 4.11, 4.52, 0.004, 0.10, 1.0, 'Jars clinking, ambient one-shot', $true),
    @('sunken_larder_jar_clink_03', 'clink', 'clink', $bottles, 10.07, 10.95, 0.004, 0.12, 1.0, 'Jars clinking, ambient one-shot', $true),
    @('sunken_larder_jar_clink_04', 'clink', 'clink', $bottles, 11.60, 12.40, 0.004, 0.12, 1.0, 'Jars clinking, ambient one-shot', $true),
    @('sunken_larder_jar_clink_05', 'clink', 'clink', $bottles, 25.335, 26.20, 0.004, 0.12, 1.0, 'Jars clinking, ambient one-shot', $true),
    @('sunken_larder_jar_clink_06', 'clink', 'clink', $bottles, 32.175, 32.53, 0.004, 0.10, 1.0, 'Jars clinking, ambient one-shot', $true),
    @('sunken_larder_jar_clink_07', 'clink', 'clink', $jars, 0.16, 0.50, 0.004, 0.10, 1.0, 'Jars clinking, ambient one-shot', $true),
    @('sunken_larder_jar_clink_08', 'clink', 'clink', $jars, 4.225, 4.98, 0.004, 0.12, 1.0, 'Jars clinking, ambient one-shot', $true),
    # One phrase each, between pauses, so no word shape survives the lowpass.
    @('sunken_larder_jar_murmur_01', 'murmur', 'murmur', $mumble, 2.10, 3.75, 0.08, 0.40, 1.0, 'A faint murmur from a jar, rare ambient one-shot', $true),
    @('sunken_larder_jar_murmur_02', 'murmur', 'murmur', $talking, 7.55, 9.85, 0.08, 0.40, 1.0, 'A faint murmur from a jar, rare ambient one-shot', $true),
    # The rot presenter's clips: the shortest, least shrill stretch pitched down to a wet creak; the softest, least
    # clicky squelch (lowest brightness and crest factor of the takes measured); a crumble with many small crackles.
    # The Squelch recording has a voice in it from about 13.5 to 14.4 s (periodic above 300 Hz, harmonic stacks in
    # its spectrogram); the withering take at 3.41-4.26 s has neither.
    @('sunken_larder_rot_creak_01', 'rot', 'rot', $rubber, 5.34, 5.70, 0.005, 0.10, 0.85, 'Rot step, quiet wet creak', $true),
    @('sunken_larder_wither_squelch_01', 'wither', 'wither', $mush, 3.41, 4.26, 0.01, 0.20, 1.0, 'Withering, wetter squelch', $true),
    @('sunken_larder_restore_crumble_01', 'restore', 'restore', $seaweed, 8.92, 9.76, 0.005, 0.20, 1.0, 'Restoring, dry crumble', $true),
    # Alternatives for audition, not installed.
    @('sunken_larder_rot_creak_alt_01', 'rot', 'rot', $rubber, 23.01, 23.40, 0.005, 0.10, 0.85, 'Rot step alternative', $false),
    @('sunken_larder_rot_squish_alt_01', 'rot', 'rot', $sponge, 4.29, 4.85, 0.005, 0.15, 0.85, 'Rot step alternative (sponge squeeze)', $false),
    @('sunken_larder_wither_squelch_alt_02', 'wither', 'wither', $wetSquelch, 0.00, 0.72, 0.01, 0.20, 1.0, 'Withering alternative (Wet Squelch)', $false),
    @('sunken_larder_restore_crumble_alt_01', 'restore', 'restore', $seaweed, 34.94, 35.60, 0.005, 0.20, 1.0, 'Restoring alternative', $false),
    @('sunken_larder_restore_crumble_alt_02', 'restore', 'restore', $leaf, 0.23, 0.64, 0.005, 0.12, 1.0, 'Restoring alternative (Dry Leaf)', $false)
)

# Filters per kind after the slice is brought to -3 dBFS. The reversed silenceremove trims trailing silence and
# fades out over the clip's fade-out time, which also ends a slice cut inside continuing sound.
$filters = @{
    creak = 'highpass=f=40,lowpass=f=9000'
    clink = 'highpass=f=150,lowpass=f=7000'
    murmur = 'highpass=f=120,lowpass=f=900,lowpass=f=900'
    rot = 'highpass=f=80,lowpass=f=5000'
    wither = 'highpass=f=60,lowpass=f=6000'
    restore = 'highpass=f=200,lowpass=f=9000'
}
$trimThreshold = @{ creak = -60; clink = -62; murmur = -60; rot = -60; wither = -58; restore = -60 }
# The murmur's glass ring: echoes of 1.13 and 2.27 ms (a comb ringing near 880 Hz and its multiples), highpassed
# so only the ring is added, mixed 14 dB under the voice.
$murmurRing = 'aecho=in_gain=1:out_gain=1:delays=1.13|2.27:decays=0.55|0.3,highpass=f=700,volume=-14dB'
$groups = @(@('creaks', 'creak'), @('jar_clinks', 'clink'), @('jar_murmurs', 'murmur'), @('rot', 'rot'), @('wither', 'wither'), @('restore', 'restore'))

$reportPath = Join-Path $Output 'processing-report.json'
$listingPath = Join-Path $Output 'listing.txt'
$bedTwice = Join-Path $Audition "${bedName}_twice.mp3"
$outputs = @($reportPath, $listingPath, $bedTwice, (Join-Path $Output "$bedName.wav"), (Join-Path $Audition "${bedName}_seam.mp3")) +
    @($clips | ForEach-Object { Join-Path $Output "$($_[0]).wav"; Join-Path $Audition "$($_[0]).mp3" }) +
    @($groups | ForEach-Object { Join-Path $Audition "sunken_larder_$($_[0])_all.mp3" })
foreach ($path in $outputs) {
    if (Test-Path $path) { throw "Refusing to replace existing output $path; choose empty output folders." }
}
$files = @($fire, $building, $ship, $bottles, $jars, $mumble, $talking, $rubber, $sponge, $mush, $wetSquelch, $seaweed, $leaf)
foreach ($file in $files) {
    if (-not (Test-Path (Join-Path $Source $file))) { throw "Missing source recording $(Join-Path $Source $file)." }
}
New-Item -ItemType Directory -Force $Output, $Audition | Out-Null

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

function Get-Hash([string]$path) { (Get-FileHash $path).Hash.ToLower() }

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

# Closes a loop: the cut S of length L + X becomes S[X, L] followed by S[L, L + X] fading out summed with S[0, X]
# fading in, both quarter-sine (equal power), for a total length L.
function Close-Loop([string]$cut, [double]$fade, [string]$loop) {
    $total = Get-Samples $cut
    $x = [long][math]::Round($fade * 44100)
    $l = $total - $x
    $loopChain = "[0:a]asplit=3[h][b][t];" +
        "[h]atrim=end_sample=$x,asetpts=PTS-STARTPTS,afade=t=in:d=$(F $fade):curve=qsin[hi];" +
        "[b]atrim=start_sample=${x}:end_sample=$l,asetpts=PTS-STARTPTS[bo];" +
        "[t]atrim=start_sample=$l,asetpts=PTS-STARTPTS,afade=t=out:d=$(F $fade):curve=qsin[to];" +
        "[to][hi]amix=inputs=2:normalize=0[x];[bo][x]concat=n=2:v=0:a=1[loop]"
    & ffmpeg -v error -i $cut -filter_complex $loopChain -map '[loop]' -c:a pcm_f32le $loop
    Assert-Ffmpeg "close the loop $loop"
}

$report = [ordered]@{ bed = $null; clips = @() }
$listing = @()

# The bed: the fire cut from its source range to exactly the loop length plus the loop crossfade, highpassed, closed
# and brought to the bed target with one gain step.
$loopSamples = [long][math]::Round($loopSeconds * 44100)
$cutSamples = $loopSamples + [long][math]::Round($loopFade * 44100)
$fireFile = Join-Path $Source $fire
$cut = Join-Path $Output "$bedName.cut.tmp.wav"
& ffmpeg -v error -i $fireFile -af "aresample=44100,aformat=sample_fmts=fltp:sample_rates=44100:channel_layouts=stereo,atrim=start=$(F $bedSourceRange[0]):end=$(F $bedSourceRange[1]),asetpts=PTS-STARTPTS,$bedFilter,atrim=end_sample=$cutSamples" -c:a pcm_f32le $cut
Assert-Ffmpeg 'cut the bed'
if ((Get-Samples $cut) -ne $cutSamples) { throw "The bed's source range is shorter than the loop length plus the loop crossfade." }
$loop = Join-Path $Output "$bedName.loop.tmp.wav"
Close-Loop $cut $loopFade $loop
Remove-Item $cut
$gain = $bedTarget - (Measure-Level $loop).lufs
$bedWav = Join-Path $Output "$bedName.wav"
& ffmpeg -v error -i $loop -af "volume=$(F $gain)dB" -ar 44100 -ac 2 -c:a pcm_s16le $bedWav
Assert-Ffmpeg 'set the level of the bed'
Remove-Item $loop
if ((Get-Samples $bedWav) -ne $loopSamples) { throw "The bed does not match the loop's $loopSamples samples." }
& ffmpeg -v error -stream_loop 1 -i $bedWav -c:a libmp3lame -q:a 2 $bedTwice
Assert-Ffmpeg 'write the bed played twice'
& ffmpeg -v error -i $bedWav -filter_complex "[0:a]asplit=2[e][s];[e]atrim=start=$(F ($loopSeconds - 8)),asetpts=PTS-STARTPTS[end];[s]atrim=end=8,asetpts=PTS-STARTPTS[start];[end][start]concat=n=2:v=0:a=1[seam]" -map '[seam]' -c:a libmp3lame -q:a 2 (Join-Path $Audition "${bedName}_seam.mp3")
Assert-Ffmpeg 'write the seam audition of the bed'
$level = Measure-Level $bedWav
$report.bed = [ordered]@{ clip = "$bedName.wav"; use = $bedUse; source = $fire; source_sha256 = (Get-Hash $fireFile);
    source_range_seconds = $bedSourceRange; filter = $bedFilter; loop_crossfade_seconds = $loopFade; target_lufs = $bedTarget;
    gain_db = [math]::Round($gain, 2); loop_duration_seconds = $loopSeconds; loop_boundary_delta = (Get-BoundaryDelta $bedWav 2);
    integrated_lufs = $level.lufs; true_peak_dbfs = $level.true_peak; peak_dbfs = [math]::Round($level.peak, 2);
    rms_dbfs = [math]::Round($level.rms, 2); sha256 = (Get-Hash $bedWav) }
$listing += [ordered]@{ file = "$bedName.wav"; purpose = $bedUse; seconds = $loopSeconds; level = "$($level.lufs) LUFS, peak $([math]::Round($level.peak, 1)) dBFS" }

foreach ($clip in $clips) {
    $name, $kind, $tier, $file, $start, $end, $fadeIn, $fadeOut, $pitch, $use, $installed = $clip
    $sourceFile = Join-Path $Source $file
    $pitchChain = if ($pitch -ne 1.0) { ",asetrate=$([int][math]::Round(44100 * $pitch)),aresample=44100" } else { '' }
    $slice = "aresample=44100,aformat=sample_fmts=fltp:sample_rates=44100:channel_layouts=mono," +
        "atrim=start=$(F $start):end=$(F $end),asetpts=PTS-STARTPTS,afade=t=in:d=$(F $fadeIn)$pitchChain"
    $cut = Join-Path $Output "$name.cut.tmp.wav"
    & ffmpeg -v error -i $sourceFile -af $slice -c:a pcm_f32le $cut
    Assert-Ffmpeg "slice $name"
    $pre = -3 - (Get-MaxVolume $cut)
    $trim = "areverse,silenceremove=start_periods=1:start_threshold=$($trimThreshold[$kind])dB,afade=t=in:d=$(F $fadeOut),areverse"
    $raw = Join-Path $Output "$name.tmp.wav"
    if ($kind -eq 'murmur') {
        & ffmpeg -v error -i $cut -filter_complex "[0:a]volume=$(F $pre)dB,$($filters[$kind]),asplit=2[v][r];[r]$murmurRing[ring];[v][ring]amix=inputs=2:normalize=0,$trim[out]" -map '[out]' -c:a pcm_f32le $raw
    }
    else {
        & ffmpeg -v error -i $cut -af "volume=$(F $pre)dB,$($filters[$kind]),$trim" -c:a pcm_f32le $raw
    }
    Assert-Ffmpeg "process $name"
    $peak = $tiers[$tier]
    $gain = $peak - (Get-MaxVolume $raw)
    $wav = Join-Path $Output "$name.wav"
    & ffmpeg -v error -i $raw -af "volume=$(F $gain)dB" -ar 44100 -ac 1 -c:a pcm_s16le $wav
    Assert-Ffmpeg "normalize $name"
    Remove-Item $cut, $raw
    & ffmpeg -v error -i $wav -c:a libmp3lame -q:a 2 (Join-Path $Audition "$name.mp3")
    Assert-Ffmpeg "write the audition of $name"
    $duration = (Get-Samples $wav) / 44100.0
    $level = Measure-Level $wav
    $entry = [ordered]@{ clip = "$name.wav"; use = $use; installed = $installed; kind = $kind; tier = $tier; source = $file; source_sha256 = (Get-Hash $sourceFile);
        source_range_seconds = @($start, $end); fade_in_seconds = $fadeIn; fade_out_seconds = $fadeOut; target_peak_dbfs = $peak; gain_db = [math]::Round($pre + $gain, 2);
        duration_seconds = [math]::Round($duration, 3); peak_dbfs = [math]::Round($level.peak, 2); rms_dbfs = [math]::Round($level.rms, 2);
        sha256 = (Get-Hash $wav) }
    if ($pitch -ne 1.0) { $entry.pitch_factor = $pitch }
    $report.clips += $entry
    $listing += [ordered]@{ file = "$name.wav"; purpose = $use; seconds = $duration; level = "peak $([math]::Round($level.peak, 1)) dBFS, RMS $([math]::Round($level.rms, 1)) dBFS" }
}

# One audition per group: its clips in order, each followed by 0.8 seconds of silence.
foreach ($group in $groups) {
    $members = @($clips | Where-Object { $_[1] -eq $group[1] })
    $inputs = @(); $chain = ''
    for ($i = 0; $i -lt $members.Count; $i++) {
        $inputs += '-i', (Join-Path $Output "$($members[$i][0]).wav")
        $chain += "[${i}:a]apad=pad_dur=0.8[a$i];"
    }
    $chain += (-join (0..($members.Count - 1) | ForEach-Object { "[a$_]" })) + "concat=n=$($members.Count):v=0:a=1[all]"
    & ffmpeg -v error @inputs -filter_complex $chain -map '[all]' -c:a libmp3lame -q:a 2 (Join-Path $Audition "sunken_larder_$($group[0])_all.mp3")
    Assert-Ffmpeg "write the $($group[0]) audition"
}

$lines = @('Sunken Larder audio for audition. Loop: stereo 44.1 kHz 16-bit; one-shots: mono 44.1 kHz 16-bit.', '',
    ('{0,-40} {1,7}  {2,-36} {3}' -f 'file', 'seconds', 'level', 'purpose'))
foreach ($row in $listing) { $lines += ('{0,-40} {1,7}  {2,-36} {3}' -f $row.file, ($row.seconds.ToString('0.000', $inv)), $row.level, $row.purpose) }
$lines += '', "Auditions in ${Audition}: ${bedName}_twice.mp3 (the loop twice end to end), ${bedName}_seam.mp3 (last 8 s then first 8 s),"
$lines += "<clip>.mp3 and sunken_larder_<group>_all.mp3. Clips marked as alternatives are not installed."
Set-Content -Path $listingPath -Value $lines -Encoding ascii

$json = $report | ConvertTo-Json -Depth 6
Set-Content -Path $reportPath -Value $json -Encoding ascii
$json
