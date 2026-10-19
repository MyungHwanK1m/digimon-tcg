using System;
using System.Collections.Generic;
using System.IO;
using System.Media;

namespace DigimonSurvivors.Audio;

public static class SoundSystem
{
    private static readonly Dictionary<string, List<SoundPlayer>> _playerPools = new();
    private static readonly Dictionary<string, int> _poolIndices = new();
    private const int PoolSize = 3;

    public static bool IsMuted { get; set; } = false;

    static SoundSystem()
    {
        try
        {
            RegisterSound("shoot", GenerateTone(880, 440, 0.08, 0.22));
            RegisterSound("hit", GenerateNoise(0.08, 0.3));
            RegisterSound("crit", GenerateTone(350, 1100, 0.14, 0.45));
            RegisterSound("gem", GenerateTone(1200, 1600, 0.06, 0.18));
            RegisterSound("dash", GenerateTone(300, 900, 0.12, 0.35));
            RegisterSound("meat", GenerateArpeggio(new[] { 587.33, 739.99, 880.0, 1174.66 }, 0.06, 0.35));
            RegisterSound("bomb", GenerateBombNoise(0.55, 0.5));
            RegisterSound("freeze", GenerateTone(2000, 800, 0.25, 0.3));
            RegisterSound("levelUp", GenerateArpeggio(new[] { 523.25, 659.25, 783.99, 1046.50 }, 0.08, 0.35));
            RegisterSound("evolution", GenerateArpeggio(new[] { 440.0, 554.37, 659.25, 880.0, 1108.73, 1318.51, 1760.0 }, 0.1, 0.45));
            RegisterSound("boss", GenerateTone(220, 110, 0.6, 0.4));
            RegisterSound("gameOver", GenerateArpeggio(new[] { 392.0, 370.0, 349.2, 329.6, 261.6 }, 0.15, 0.35));
        }
        catch
        {
            // 오디오 장치가 없는 환경에서도 안전하게 무시
        }
    }

    private static void RegisterSound(string key, byte[] wavBytes)
    {
        var list = new List<SoundPlayer>();
        for (int i = 0; i < PoolSize; i++)
        {
            var ms = new MemoryStream(wavBytes);
            var player = new SoundPlayer(ms);
            player.Load();
            list.Add(player);
        }
        _playerPools[key] = list;
        _poolIndices[key] = 0;
    }

    private static void PlayPool(string key)
    {
        if (IsMuted) return;
        if (!_playerPools.TryGetValue(key, out var list) || list.Count == 0) return;

        try
        {
            int idx = _poolIndices[key];
            var player = list[idx];
            _poolIndices[key] = (idx + 1) % list.Count;
            player.Play();
        }
        catch { }
    }

    public static void PlayShoot() => PlayPool("shoot");
    public static void PlayHit() => PlayPool("hit");
    public static void PlayCrit() => PlayPool("crit");
    public static void PlayGem() => PlayPool("gem");
    public static void PlayDash() => PlayPool("dash");
    public static void PlayMeat() => PlayPool("meat");
    public static void PlayBomb() => PlayPool("bomb");
    public static void PlayFreeze() => PlayPool("freeze");
    public static void PlayLevelUp() => PlayPool("levelUp");
    public static void PlayEvolution() => PlayPool("evolution");
    public static void PlayBoss() => PlayPool("boss");
    public static void PlayGameOver() => PlayPool("gameOver");

    #region 파형 합성기
    private static byte[] GenerateTone(double startFreq, double endFreq, double durationSeconds, double volume)
    {
        int sampleRate = 22050;
        int sampleCount = (int)(sampleRate * durationSeconds);
        short[] samples = new short[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            double t = (double)i / sampleCount;
            double freq = startFreq + (endFreq - startFreq) * t;
            double angle = 2.0 * Math.PI * freq * (i / (double)sampleRate);
            double envelope = Math.Sin(Math.PI * (1.0 - t));
            samples[i] = (short)(Math.Sin(angle) * envelope * volume * short.MaxValue);
        }

        return CreateWavHeaderAndData(samples, sampleRate);
    }

    private static byte[] GenerateNoise(double durationSeconds, double volume)
    {
        int sampleRate = 22050;
        int sampleCount = (int)(sampleRate * durationSeconds);
        short[] samples = new short[sampleCount];
        var rnd = new Random();

        for (int i = 0; i < sampleCount; i++)
        {
            double t = (double)i / sampleCount;
            double envelope = Math.Pow(1.0 - t, 2);
            double val = (rnd.NextDouble() * 2.0 - 1.0) * envelope * volume;
            samples[i] = (short)(val * short.MaxValue);
        }

        return CreateWavHeaderAndData(samples, sampleRate);
    }

    private static byte[] GenerateBombNoise(double durationSeconds, double volume)
    {
        int sampleRate = 22050;
        int sampleCount = (int)(sampleRate * durationSeconds);
        short[] samples = new short[sampleCount];
        var rnd = new Random();

        for (int i = 0; i < sampleCount; i++)
        {
            double t = (double)i / sampleCount;
            double envelope = Math.Pow(1.0 - t, 1.4);
            // 저음 럼블 + 노이즈
            double sub = Math.Sin(2.0 * Math.PI * 65.0 * (1.0 - t * 0.5) * (i / (double)sampleRate));
            double noise = (rnd.NextDouble() * 2.0 - 1.0);
            double val = (sub * 0.6 + noise * 0.4) * envelope * volume;
            samples[i] = (short)(Math.Clamp(val, -1.0, 1.0) * short.MaxValue);
        }

        return CreateWavHeaderAndData(samples, sampleRate);
    }

    private static byte[] GenerateArpeggio(double[] freqs, double noteDuration, double volume)
    {
        int sampleRate = 22050;
        int totalSamples = (int)(sampleRate * noteDuration * freqs.Length);
        short[] samples = new short[totalSamples];
        int noteSamples = (int)(sampleRate * noteDuration);

        for (int n = 0; n < freqs.Length; n++)
        {
            double f = freqs[n];
            for (int i = 0; i < noteSamples; i++)
            {
                int idx = n * noteSamples + i;
                if (idx >= totalSamples) break;
                double t = (double)i / noteSamples;
                double angle = 2.0 * Math.PI * f * (i / (double)sampleRate);
                double envelope = Math.Sin(Math.PI * (1.0 - t));
                samples[idx] = (short)(Math.Sin(angle) * envelope * volume * short.MaxValue);
            }
        }

        return CreateWavHeaderAndData(samples, sampleRate);
    }

    private static byte[] CreateWavHeaderAndData(short[] samples, int sampleRate)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        int subChunk2Size = samples.Length * 2;
        int chunkSize = 36 + subChunk2Size;

        bw.Write(new[] { 'R', 'I', 'F', 'F' });
        bw.Write(chunkSize);
        bw.Write(new[] { 'W', 'A', 'V', 'E' });
        bw.Write(new[] { 'f', 'm', 't', ' ' });
        bw.Write(16);
        bw.Write((short)1);
        bw.Write((short)1);
        bw.Write(sampleRate);
        bw.Write(sampleRate * 2);
        bw.Write((short)2);
        bw.Write((short)16);
        bw.Write(new[] { 'd', 'a', 't', 'a' });
        bw.Write(subChunk2Size);

        foreach (var sample in samples)
        {
            bw.Write(sample);
        }

        return ms.ToArray();
    }
    #endregion
}
