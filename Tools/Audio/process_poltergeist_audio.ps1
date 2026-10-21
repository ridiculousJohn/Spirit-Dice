<#
.SYNOPSIS
Processes the Pixabay poltergeist recordings into the runtime poltergeist clips.

.DESCRIPTION
Reads the raw Pixabay downloads listed below from -Source and writes one mono 44.1 kHz signed 16-bit PCM WAV per
clip to -Output, followed by a JSON report of each clip's source hash, gain, duration and output hash. Each clip
is band-limited, given a light chorus shimmer and a diffuse multi-tap echo tail, trimmed of trailing silence with
a short fade, and peak-normalized to its target level. The script refuses to replace an existing output file, so
verify a recipe change in a fresh output folder. Requires ffmpeg and ffprobe on PATH.

The source records and the hashes of the installed clips are in Docs/Reference/Audio/pixabay-poltergeist/provenance.json.
#>
param(
    [string]$Source = (Join-Path $PSScriptRoot '../../Docs/work/audio-downloads/poltergeist-giggles'),
    [string]$Output = (Join-Path $PSScriptRoot '../../Docs/work/audio-generated/poltergeist-giggles')
)
$ErrorActionPreference = 'Stop'
$Source = [System.IO.Path]::GetFullPath($Source)
$Output = [System.IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Force $Output | Out-Null

# name, source file, target peak dBFS, optional source trim start and end seconds
$clips = @(
    @('poltergeist_giggle_01', 'u_39er8q3as7-giggle-339074.mp3', -6),
    @('poltergeist_giggle_02', 'voiceofruthie-giggle-338193.mp3', -6),
    @('poltergeist_ambient_giggle_01', 'freesound_community-small-giggle-85632.mp3', -12),
    @('poltergeist_ambient_giggle_02', 'freesound_community-small-giggle-104591.mp3', -12),
    # Source seconds 1.12-1.72 hold the giggle; the rest is leading room noise and trailing crackle.
    @('poltergeist_ambient_giggle_03', 'freesound_community-child-giggle-81205.mp3', -12, 1.12, 1.72),
    @('poltergeist_grumble_01', 'voiceofruthie-hmph-338183.mp3', -6)
)

$reportPath = Join-Path $Output 'processing-report.json'
foreach ($path in @($reportPath) + @($clips | ForEach-Object { Join-Path $Output "$($_[0]).wav" })) {
    if (Test-Path $path) { throw "Refusing to replace existing output $path; choose an empty output folder." }
}

# Ethereal treatment: band-limit, light chorus shimmer, diffuse multi-tap echo tail, padded so the tail rings out.
# The reversed silenceremove trims trailing silence and applies a 0.35 second fade out; the final fade in is 8 ms.
$ethereal = 'aformat=sample_rates=44100:channel_layouts=mono,highpass=f=170,lowpass=f=6200,' +
    'chorus=0.7:0.9:38|57:0.32|0.26:0.30|0.42:1.2|1.9,' +
    'apad=pad_dur=1.6,' +
    'aecho=0.85:0.9:47|89|141|197|263|347|449:0.46|0.38|0.31|0.25|0.19|0.14|0.09,' +
    'areverse,silenceremove=start_periods=1:start_threshold=-62dB,afade=t=in:d=0.35,areverse,afade=t=in:d=0.008'

$report = @()
foreach ($clip in $clips) {
    $name, $file, $peak = $clip[0], $clip[1], $clip[2]
    $sourceFile = Join-Path $Source $file
    if (-not (Test-Path $sourceFile)) { throw "Missing source recording $sourceFile." }
    $trim = if ($clip.Count -ge 5) { "atrim=start=$($clip[3]):end=$($clip[4]),asetpts=PTS-STARTPTS,afade=t=out:st=$($clip[4] - $clip[3] - 0.04):d=0.04," } else { '' }
    $raw = Join-Path $Output "$name.tmp.wav"
    & ffmpeg -v error -y -i $sourceFile -af ($trim + $ethereal) -c:a pcm_s16le $raw
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed to process $file." }
    $max = [double](([regex]::Match((& ffmpeg -hide_banner -i $raw -af volumedetect -f null - 2>&1 | Out-String), 'max_volume: (-?[\d.]+) dB')).Groups[1].Value)
    $gain = $peak - $max
    $wav = Join-Path $Output "$name.wav"
    & ffmpeg -v error -i $raw -af "volume=${gain}dB" -ar 44100 -ac 1 -c:a pcm_s16le $wav
    if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed to normalize $name." }
    Remove-Item $raw
    $duration = [double](& ffprobe -v error -show_entries format=duration -of default=nw=1:nk=1 $wav)
    $entry = [ordered]@{ clip = "$name.wav"; source = $file; source_sha256 = (Get-FileHash $sourceFile).Hash.ToLower();
        target_peak_dbfs = $peak; gain_db = [math]::Round($gain, 2); duration_seconds = [math]::Round($duration, 3);
        sha256 = (Get-FileHash $wav).Hash.ToLower() }
    if ($clip.Count -ge 5) { $entry.source_trim_seconds = @($clip[3], $clip[4]) }
    $report += $entry
}
$json = $report | ConvertTo-Json -Depth 3
Set-Content -Path $reportPath -Value $json -Encoding ascii
$json
