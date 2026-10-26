using System.Text;
using Pointik.Core.Input;

namespace Pointik.Core.Effects;

/// <summary>
/// Génère en mémoire un court son de clic au format WAV (PCM 16 bits mono).
/// Synthèse originale : brève impulsion sinusoïdale amortie, plus grave pour le clic droit.
/// </summary>
public static class ClickSoundGenerator
{
    private const int SampleRate = 44100;

    public static byte[] CreateWav(MouseButton button)
    {
        var frequency = button switch
        {
            MouseButton.Left => 1900.0,
            MouseButton.Right => 1250.0,
            _ => 1550.0,
        };
        const double durationSeconds = 0.035;
        var count = (int)(SampleRate * durationSeconds);
        var samples = new short[count];
        var rng = new Random(42);
        for (var i = 0; i < count; i++)
        {
            var t = (double)i / SampleRate;
            var envelope = Math.Exp(-t * 160);
            // Un peu de bruit au tout début donne l'attaque « mécanique » du clic.
            var noise = i < 60 ? (rng.NextDouble() * 2 - 1) * 0.5 : 0;
            var value = (Math.Sin(2 * Math.PI * frequency * t) * 0.6 + noise) * envelope;
            samples[i] = (short)(Math.Clamp(value, -1, 1) * short.MaxValue * 0.35);
        }
        return ToWav(samples);
    }

    private static byte[] ToWav(short[] samples)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms, Encoding.ASCII);
        var dataBytes = samples.Length * 2;
        w.Write("RIFF"u8);
        w.Write(36 + dataBytes);
        w.Write("WAVE"u8);
        w.Write("fmt "u8);
        w.Write(16);              // taille du bloc fmt
        w.Write((short)1);        // PCM
        w.Write((short)1);        // mono
        w.Write(SampleRate);
        w.Write(SampleRate * 2);  // octets par seconde
        w.Write((short)2);        // alignement
        w.Write((short)16);       // bits par échantillon
        w.Write("data"u8);
        w.Write(dataBytes);
        foreach (var s in samples) w.Write(s);
        w.Flush();
        return ms.ToArray();
    }
}
