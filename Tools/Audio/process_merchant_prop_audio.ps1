<#
.SYNOPSIS
Slices and processes the Pixabay prop recordings into the haunted merchant's runtime prop and burst clips.

.DESCRIPTION
Reads the raw Pixabay downloads listed below from -Source and writes one mono 44.1 kHz signed 16-bit PCM WAV per
clip to -Output, an MP3 preview of each clip and one concatenated MP3 preview with short gaps to -Output/preview,
followed by a JSON report of each clip's source hash, source range, gain, duration, peak, RMS and output hash.
Each clip is cut from its recording at the listed source range with a 3 ms fade in and the listed fade out, then
brought to -3 dBFS, given a gentle highpass and lowpass and a short room tail, trimmed of trailing silence with a short fade, and
peak-normalized to its tier's level so idle clips sit below hits. The script refuses to replace an existing output
file, so verify a recipe change in a fresh output folder. Requires ffmpeg and ffprobe on PATH.

The source records and the hashes of the installed clips are in Docs/Reference/Audio/pixabay-merchant-props/provenance.json.
#>
param(
    [string]$Source = (Join-Path $PSScriptRoot '../../Docs/work/audio-downloads/merchant-props'),
    [string]$Output = (Join-Path $PSScriptRoot '../../Docs/work/audio-generated/merchant-props')
)
$ErrorActionPreference = 'Stop'
$Source = [System.IO.Path]::GetFullPath($Source)
$Output = [System.IO.Path]::GetFullPath($Output)
$previewFolder = Join-Path $Output 'preview'
New-Item -ItemType Directory -Force $Output, $previewFolder | Out-Null

$pots = 'freesound_community-small-metal-pots-rattling-76063.mp3'
$wobble = 'floraphonic-empty-mug-table-foley-wobble-1-239010.mp3'
$lid = 'freesound_community-lid-of-chest-hitting_northern87-81041.mp3'
$slam = 'freesound_community-chest-slam-85122.mp3'
$rumble = 'alex_jauk-loud-rumbling-noise-199419.mp3'

# Target peak dBFS per tier: idle nudges sit well below disturbance hits, the defeat knock is loudest,
# and the disturbance burst sits between.
$tiers = @{ idle = -18; hit = -9; defeat = -6; burst = -14 }

# name, tier, source file, source start and end seconds, fade out seconds. Each start sits a few milliseconds
# before the event's onset measured from the recording; each end follows the event's natural decay or stops
# just before the next event.
$clips = @(
    @('merchant_cookware_idle_01', 'idle', $pots, 3.885, 4.200, 0.05),
    @('merchant_cookware_idle_02', 'idle', $pots, 4.268, 4.580, 0.05),
    @('merchant_cookware_idle_03', 'idle', $pots, 5.066, 5.375, 0.05),
    @('merchant_cookware_idle_04', 'idle', $pots, 10.404, 10.730, 0.05),
    @('merchant_cookware_hit_01', 'hit', $pots, 0.402, 0.760, 0.06),
    @('merchant_cookware_hit_02', 'hit', $pots, 5.560, 6.100, 0.06),
    @('merchant_cookware_hit_03', 'hit', $pots, 10.010, 10.400, 0.04),
    @('merchant_cookware_hit_04', 'hit', $pots, 11.442, 12.100, 0.08),
    @('merchant_cookware_hit_05', 'hit', $pots, 7.092, 7.780, 0.06),
    @('merchant_cookware_hit_06', 'hit', $pots, 8.554, 8.930, 0.05),
    # The wobble's first rocking ticks are the idle nudges; the whole rock to rest is the hit.
    @('merchant_bottle_idle_01', 'idle', $wobble, 0.232, 0.745, 0.06),
    @('merchant_bottle_idle_02', 'idle', $wobble, 0.232, 0.520, 0.04),
    @('merchant_bottle_hit_01', 'hit', $wobble, 0.232, 2.130, 0.05),
    # The lid recording holds three separate drops; the second drop's first impact is a softer idle nudge.
    @('merchant_lid_idle_01', 'idle', $lid, 0.178, 0.460, 0.05),
    @('merchant_lid_idle_02', 'idle', $lid, 1.238, 1.430, 0.04),
    @('merchant_lid_hit_01', 'hit', $lid, 1.238, 1.620, 0.06),
    @('merchant_lid_hit_02', 'hit', $lid, 2.735, 3.150, 0.06),
    @('merchant_lid_defeat_01', 'defeat', $slam, 0.355, 0.850, 0.06),
    # The rumble's clatter peaks three times; each burst runs from one peak's onset to the next.
    @('poltergeist_burst_01', 'burst', $rumble, 0.012, 0.605, 0.08),
    @('poltergeist_burst_02', 'burst', $rumble, 0.605, 1.310, 0.08),
    @('poltergeist_burst_03', 'burst', $rumble, 1.310, 2.040, 0.10)
)

$reportPath = Join-Path $Output 'processing-report.json'
$allPreview = Join-Path $previewFolder 'merchant_props_all.mp3'
$outputs = @($reportPath, $allPreview) + @($clips | ForEach-Object { Join-Path $Output "$($_[0]).wav"; Join-Path $previewFolder "$($_[0]).mp3" })
foreach ($path in $outputs) {
    if (Test-Path $path) { throw "Refusing to replace existing output $path; choose an empty output folder." }
}

# Room treatment for physical props: gentle band-limit and a short, quiet multi-tap room tail with no chorus,
# padded so the tail rings out. The reversed silenceremove trims trailing silence and applies a 0.12 second fade out.
$room = 'highpass=f=60,lowpass=f=9000,' +
    'apad=pad_dur=0.6,' +
    'aecho=0.9:0.8:19|31|47|67|89|113|151|197|241|293|353:0.30|0.25|0.21|0.17|0.13|0.10|0.07|0.05|0.04|0.03|0.02,' +
    'areverse,silenceremove=start_periods=1:start_threshold=-60dB,afade=t=in:d=0.12,areverse'

function Get-MaxVolume([string]$path) {
    [double](([regex]::Match((& ffmpeg -hide_banner -i $path -af volumedetect -f null - 2>&1 | Out-String), 'max_volume: (-?[\d.]+) dB')).Groups[1].Value)
}

function Measure-Level([string]$path) {
    $stats = & ffmpeg -hide_banner -i $path -af astats=measure_perchannel=none -f null - 2>&1 | Out-String
    [ordered]@{
        peak = [double]([regex]::Match($stats, 'Peak level dB: (-?[\d.]+)').Groups[1].Value)
        rms = [double]([regex]::Match($stats, 'RMS level dB: (-?[\d.]+)').Groups[1].Value)
    }
}

$report = @()
foreach ($clip in $clips) {
    $name, $tier, $file, $start, $end, $fade = $clip
    $sourceFile = Join-Path $Source $file
    if (-not (Test-Path $sourceFile)) { throw "Missing source recording $sourceFile." }
    $length = $end - $start
    $slice = "aresample=44100,aformat=sample_fmts=fltp:sample_rates=44100:channel_layouts=mono," +
        "atrim=start=${start}:end=${end},asetpts=PTS-STARTPTS,afade=t=in:d=0.003,afade=t=out:st=$($length - $fade):d=${fade},"
    # The slice is brought to -3 dBFS before the room so every clip's tail rings out to the same depth below its peak.
    $cut = Join-Path $Output "$name.cut.tmp.wav"
    & ffmpeg -v error -y -i $sourceFile -af $slice.TrimEnd(',') -c:a pcm_f32le $cut
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed to slice $name." }
    $pre = -3 - (Get-MaxVolume $cut)
    $raw = Join-Path $Output "$name.tmp.wav"
    & ffmpeg -v error -y -i $cut -af ("volume=${pre}dB," + $room) -c:a pcm_f32le $raw
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed to process $name." }
    $peak = $tiers[$tier]
    $gain = $peak - (Get-MaxVolume $raw)
    $wav = Join-Path $Output "$name.wav"
    & ffmpeg -v error -i $raw -af "volume=${gain}dB" -ar 44100 -ac 1 -c:a pcm_s16le $wav
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed to normalize $name." }
    Remove-Item $cut, $raw
    & ffmpeg -v error -i $wav -c:a libmp3lame -q:a 2 (Join-Path $previewFolder "$name.mp3")
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed to write the preview of $name." }
    $duration = [double](& ffprobe -v error -show_entries format=duration -of default=nw=1:nk=1 $wav)
    $level = Measure-Level $wav
    $report += [ordered]@{ clip = "$name.wav"; tier = $tier; source = $file; source_sha256 = (Get-FileHash $sourceFile).Hash.ToLower();
        source_range_seconds = @($start, $end); fade_out_seconds = $fade; target_peak_dbfs = $peak; gain_db = [math]::Round($pre + $gain, 2);
        duration_seconds = [math]::Round($duration, 3); peak_dbfs = [math]::Round($level.peak, 2); rms_dbfs = [math]::Round($level.rms, 2);
        sha256 = (Get-FileHash $wav).Hash.ToLower() }
}

# One preview of every clip in order, each followed by 0.6 seconds of silence.
$inputs = @(); $chain = ''
for ($i = 0; $i -lt $clips.Count; $i++) {
    $inputs += '-i', (Join-Path $Output "$($clips[$i][0]).wav")
    $chain += "[${i}:a]apad=pad_dur=0.6[a$i];"
}
$chain += (0..($clips.Count - 1) | ForEach-Object { "[a$_]" }) -join ''
$chain += "concat=n=$($clips.Count):v=0:a=1[all]"
& ffmpeg -v error @inputs -filter_complex $chain -map '[all]' -c:a libmp3lame -q:a 2 $allPreview
if ($LASTEXITCODE -ne 0) { throw 'ffmpeg failed to write the combined preview.' }

$json = $report | ConvertTo-Json -Depth 3
Set-Content -Path $reportPath -Value $json -Encoding ascii
$json
