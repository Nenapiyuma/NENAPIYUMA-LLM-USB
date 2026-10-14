using System.Text;

namespace NENAPIYUMA.Shared;

/// <summary>
/// Offline procedural WAV music generator. This creates actual audio locally without a neural audio model.
/// It must not be described as neural/AI audio synthesis.
/// </summary>
public static class AudioGenerationService
{
    public const int SampleRate = 44100;

    public static async Task<string> GenerateWavAsync(
        string prompt,
        string outputPath,
        int durationSeconds = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Describe the music or sound first.", nameof(prompt));
        if (prompt.Length > 4000)
            throw new ArgumentException("Audio prompt is limited to 4,000 characters.", nameof(prompt));
        if (durationSeconds is < 5 or > 60)
            throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Duration must be between 5 and 60 seconds.");

        var fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var seed = StringComparer.Ordinal.GetHashCode(prompt);
        var random = new Random(seed);
        var tempo = ParseTempo(prompt);
        var minor = ContainsAny(prompt, "sad", "dark", "minor", "දුක්", "ශෝක", "අඳුරු");
        var ambient = ContainsAny(prompt, "ambient", "rain", "ocean", "relax", "සන්සුන්", "වැස්ස", "මුහුද");
        var beatSeconds = 60.0 / tempo;
        var frames = checked(SampleRate * durationSeconds);
        var bytesPerFrame = 4;
        var pcmBytes = checked(frames * bytesPerFrame);

        await using var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + pcmBytes);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)2);
        writer.Write(SampleRate);
        writer.Write(SampleRate * bytesPerFrame);
        writer.Write((short)bytesPerFrame);
        writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(pcmBytes);

        var majorScale = new[] { 0, 2, 4, 7, 9, 12, 14, 16, 19 };
        var minorScale = new[] { 0, 2, 3, 5, 7, 8, 10, 12, 14 };
        var scale = minor ? minorScale : majorScale;
        var rootMidi = ContainsAny(prompt, "bass", "low", "ගැඹුරු") ? 43 : 60;
        var melodyNotes = Enumerable.Range(0, Math.Max(1, (int)Math.Ceiling(durationSeconds / beatSeconds)))
            .Select(_ => rootMidi + scale[random.Next(scale.Length)] + (ambient ? 0 : random.Next(2) * 12))
            .ToArray();
        var chordNotes = minor ? new[] { rootMidi, rootMidi + 3, rootMidi + 7 } : new[] { rootMidi, rootMidi + 4, rootMidi + 7 };
        var samples = new byte[SampleRate * bytesPerFrame * 2];

        for (var start = 0; start < frames; start += SampleRate / 2)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(SampleRate / 2, frames - start);
            for (var i = 0; i < count; i++)
            {
                var frame = start + i;
                var time = frame / (double)SampleRate;
                var beat = time / beatSeconds;
                var noteIndex = Math.Min(melodyNotes.Length - 1, (int)beat);
                var noteAge = beat - Math.Floor(beat);
                var melodyFreq = MidiToHz(melodyNotes[noteIndex]);
                var chord = 0.0;
                foreach (var midi in chordNotes)
                    chord += Math.Sin(2 * Math.PI * MidiToHz(midi) * time) / chordNotes.Length;

                var noteEnvelope = Math.Min(1.0, noteAge * 12) * Math.Min(1.0, (1.0 - noteAge) * 8);
                var melody = Math.Sin(2 * Math.PI * melodyFreq * time) * noteEnvelope;
                var bass = Math.Sin(2 * Math.PI * MidiToHz(rootMidi - 12) * time) * 0.18;
                var pad = chord * (ambient ? 0.20 : 0.09);
                var beatPosition = time % beatSeconds;
                var kick = beatPosition < 0.09 ? Math.Sin(2 * Math.PI * (70 - beatPosition * 350) * time) * (0.16 * (1 - beatPosition / 0.09)) : 0;
                var noise = ambient ? (random.NextDouble() - 0.5) * 0.018 : 0;
                var left = Math.Clamp(melody * 0.32 + pad + bass + kick + noise, -0.92, 0.92);
                var right = Math.Clamp(melody * 0.28 + pad * 0.98 + bass * 0.94 + kick + noise, -0.92, 0.92);
                var fade = Math.Min(1.0, Math.Min(time / 0.25, (durationSeconds - time) / 0.5));
                WriteInt16(samples, i * 4, (short)(left * fade * short.MaxValue));
                WriteInt16(samples, i * 4 + 2, (short)(right * fade * short.MaxValue));
            }

            await stream.WriteAsync(samples.AsMemory(0, count * bytesPerFrame), cancellationToken);
        }

        await stream.FlushAsync(cancellationToken);
        return fullPath;
    }

    private static int ParseTempo(string prompt)
    {
        var words = prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            if (int.TryParse(word.Trim(',', '.', ':', ';'), out var value) && value is >= 50 and <= 160)
                return value;
        }
        if (ContainsAny(prompt, "fast", "upbeat", "energetic", "වේගවත්")) return 125;
        if (ContainsAny(prompt, "slow", "calm", "relax", "සන්සුන්", "මන්දගාමී")) return 72;
        return 96;
    }

    private static double MidiToHz(int midi) => 440.0 * Math.Pow(2, (midi - 69) / 12.0);

    private static void WriteInt16(byte[] buffer, int offset, short value)
    {
        buffer[offset] = (byte)(value & 0xff);
        buffer[offset + 1] = (byte)((value >> 8) & 0xff);
    }

    private static bool ContainsAny(string text, params string[] terms) =>
        terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
}
