using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Dice.Tests
{
    /**
     * Records the mixed output the audio listener hears, for PortraitCapture.Recorder. Added to the listener's object, its
     * OnAudioFilterRead receives every block of the final mix on the audio thread, copies it and queues it with the time it
     * was mixed; the queue is written as a 32-bit float WAV on the main thread when capture ends. Audio and frames are
     * aligned by mix time: a sound the game starts in a frame is mixed into the next block, so each block's first sample is
     * placed at the real time its block was mixed, measured on the same clock as the recorder's tick 0. The written WAV
     * starts at tick 0, padded with silence or trimmed at its start as that alignment requires. The filter passes the mix
     * through unchanged.
     */
    internal sealed class PortraitAudioCapture : MonoBehaviour
    {
        // Seconds End waits for an audio callback that is copying a block to finish.
        private const double CallbackDrainSeconds = 1;

        private readonly struct Block
        {
            internal readonly float[] Samples;
            internal readonly int Channels;
            internal readonly long Stamp;

            internal Block(float[] samples, int channels, long stamp)
            {
                Samples = samples;
                Channels = channels;
                Stamp = stamp;
            }
        }

        private readonly ConcurrentQueue<Block> blocks = new ConcurrentQueue<Block>();
        private volatile bool capturing;
        private int inCallback;
        private long startStamp;
        private int sampleRate;

        /** Starts queueing the mix; the stamp is the Stopwatch timestamp of the recorder's tick 0. */
        internal void Begin(long tickZeroStamp)
        {
            startStamp = tickZeroStamp;
            sampleRate = AudioSettings.outputSampleRate;
            capturing = true;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            Interlocked.Increment(ref inCallback);
            try
            {
                if (!capturing) return;
                long stamp = Stopwatch.GetTimestamp();
                var copy = new float[data.Length];
                Array.Copy(data, copy, data.Length);
                blocks.Enqueue(new Block(copy, channels, stamp));
            }
            finally
            {
                Interlocked.Decrement(ref inCallback);
            }
        }

        /**
         * Stops queueing, writes the captured mix aligned to tick 0 as a WAV at the path and a description of the capture
         * and its alignment beside it at syncPath, and returns a one-line summary.
         */
        internal string End(string path, string syncPath, string focusNote)
        {
            capturing = false;
            var drain = Stopwatch.StartNew();
            while (Volatile.Read(ref inCallback) != 0 && drain.Elapsed.TotalSeconds < CallbackDrainSeconds) Thread.Yield();

            var captured = new List<Block>();
            while (blocks.TryDequeue(out var block)) captured.Add(block);
            int channels = captured.Count > 0 ? captured[0].Channels : 0;
            foreach (var block in captured)
                if (block.Channels != channels) throw new InvalidOperationException("The listener's channel count changed during capture.");

            // Each block's first sample was mixed at its stamp; the median over blocks places sample 0 against tick 0.
            var offsets = new List<double>(captured.Count);
            long frames = 0;
            foreach (var block in captured)
            {
                offsets.Add((block.Stamp - startStamp) / (double)Stopwatch.Frequency - frames / (double)sampleRate);
                frames += block.Samples.Length / channels;
            }
            offsets.Sort();
            double offset = offsets.Count > 0 ? offsets[offsets.Count / 2] : 0;
            long shift = (long)Math.Round(offset * sampleRate);
            long padFrames = Math.Max(0, shift), dropFrames = Math.Min(frames, Math.Max(0, -shift));
            long writtenFrames = padFrames + frames - dropFrames;

            double peak = 0, sumSquares = 0;
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new BinaryWriter(stream))
            {
                int written = Math.Max(1, channels);
                WriteHeader(writer, written, sampleRate, writtenFrames);
                for (long frame = 0; frame < padFrames * written; ++frame) writer.Write(0f);
                long skip = dropFrames * written;
                foreach (var block in captured)
                    foreach (float sample in block.Samples)
                    {
                        if (skip > 0) { --skip; continue; }
                        writer.Write(sample);
                        peak = Math.Max(peak, Math.Abs(sample));
                        sumSquares += sample * (double)sample;
                    }
            }

            long sampleCount = Math.Max(1, (frames - dropFrames) * Math.Max(1, channels));
            string peakDb = Decibels(peak), rmsDb = Decibels(Math.Sqrt(sumSquares / sampleCount));
            string spread = offsets.Count > 0 ? Seconds(offsets[0]) + " to " + Seconds(offsets[offsets.Count - 1]) : "none";
            var sync = new StringBuilder();
            sync.AppendLine(Path.GetFileName(path) + ": 32-bit float PCM, " + sampleRate + " Hz, " + channels + " channels, " +
                writtenFrames + " frames (" + Seconds(writtenFrames / (double)sampleRate) + " s)");
            sync.AppendLine("Sample 0 of " + Path.GetFileName(path) + " is tick 0 (time 0.000 in frames.csv); mux it with no offset.");
            sync.AppendLine("Captured " + captured.Count + " mix blocks, " + frames + " frames.");
            sync.AppendLine("Mix time of the first captured sample after tick 0: " + Seconds(offset) + " s (median over blocks; " +
                "per-block estimates " + spread + " s).");
            sync.AppendLine("Aligned by " + (padFrames > 0 ? "prepending " + padFrames + " silent frames" :
                dropFrames > 0 ? "dropping the first " + dropFrames + " captured frames" : "no shift") + ".");
            sync.AppendLine("Level: peak " + peakDb + " dBFS, RMS " + rmsDb + " dBFS.");
            AudioSettings.GetDSPBufferSize(out int bufferLength, out int bufferCount);
            sync.AppendLine("DSP buffer " + bufferLength + " x " + bufferCount + ", speaker mode " + AudioSettings.speakerMode + ".");
            sync.AppendLine(focusNote);
            File.WriteAllText(syncPath, sync.ToString());
            return Path.GetFileName(path) + " " + Seconds(writtenFrames / (double)sampleRate) + " s at " + sampleRate + " Hz x " +
                channels + ", first mix " + Seconds(offset) + " s after tick 0, peak " + peakDb + " dBFS, RMS " + rmsDb + " dBFS";
        }

        /** A WAVE_FORMAT_IEEE_FLOAT header with its fact chunk, for the frame count that follows it. */
        private static void WriteHeader(BinaryWriter writer, int channels, int rate, long frames)
        {
            const int FormatLength = 18, FactLength = 4, BytesPerSample = 4;
            long dataLength = frames * channels * BytesPerSample;
            if (dataLength > uint.MaxValue - 64) throw new InvalidOperationException("The captured audio is too long for a WAV file.");
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write((uint)(4 + 8 + FormatLength + 8 + FactLength + 8 + dataLength));
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(FormatLength);
            writer.Write((ushort)3);
            writer.Write((ushort)channels);
            writer.Write(rate);
            writer.Write(rate * channels * BytesPerSample);
            writer.Write((ushort)(channels * BytesPerSample));
            writer.Write((ushort)(BytesPerSample * 8));
            writer.Write((ushort)0);
            writer.Write(Encoding.ASCII.GetBytes("fact"));
            writer.Write(FactLength);
            writer.Write((uint)frames);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write((uint)dataLength);
        }

        private static string Seconds(double value) => value.ToString("F4", CultureInfo.InvariantCulture);

        private static string Decibels(double level)
            => level > 0 ? (20 * Math.Log10(level)).ToString("F1", CultureInfo.InvariantCulture) : "-inf";
    }
}
