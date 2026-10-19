using System;
using System.IO;
using System.Windows.Media;

namespace DigimonSurvivors.Audio;

public static class BgmSystem
{
    private static MediaPlayer? _bgmPlayer;
    private static string? _currentTrack;
    private static string? _audioCacheDir;
    private static bool _isInitialized = false;

    public static double Volume { get; set; } = 0.45;
    public static bool IsMuted { get; set; } = false;

    public static void Initialize(string projectOrAppDir)
    {
        if (_isInitialized) return;

        try
        {
            _audioCacheDir = Path.Combine(projectOrAppDir, "Assets", "Audio");
            Directory.CreateDirectory(_audioCacheDir);

            // BGM WAV 파일 생성
            string combatPath = Path.Combine(_audioCacheDir, "bgm_combat.wav");
            if (!File.Exists(combatPath) || new FileInfo(combatPath).Length < 1000)
            {
                byte[] combatBytes = GenerateCombatTheme();
                File.WriteAllBytes(combatPath, combatBytes);
            }

            string bossPath = Path.Combine(_audioCacheDir, "bgm_boss.wav");
            if (!File.Exists(bossPath) || new FileInfo(bossPath).Length < 1000)
            {
                byte[] bossBytes = GenerateBossTheme();
                File.WriteAllBytes(bossPath, bossBytes);
            }

            _bgmPlayer = new MediaPlayer();
            _bgmPlayer.Volume = Volume;
            _bgmPlayer.MediaEnded += (s, e) =>
            {
                // 무한 루프
                if (_bgmPlayer != null && !string.IsNullOrEmpty(_currentTrack))
                {
                    _bgmPlayer.Position = TimeSpan.Zero;
                    _bgmPlayer.Play();
                }
            };

            _isInitialized = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BGM Init notice: {ex.Message}");
        }
    }

    public static void PlayCombat()
    {
        PlayTrack("bgm_combat.wav");
    }

    public static void PlayBoss()
    {
        PlayTrack("bgm_boss.wav");
    }

    public static void Stop()
    {
        try
        {
            _bgmPlayer?.Stop();
            _currentTrack = null;
        }
        catch { }
    }

    public static void SetMute(bool mute)
    {
        IsMuted = mute;
        if (_bgmPlayer != null)
        {
            _bgmPlayer.Volume = mute ? 0.0 : Volume;
        }
    }

    private static void PlayTrack(string filename)
    {
        if (IsMuted || _bgmPlayer == null || string.IsNullOrEmpty(_audioCacheDir)) return;
        if (_currentTrack == filename) return; // 이미 재생 중

        try
        {
            string path = Path.Combine(_audioCacheDir, filename);
            if (!File.Exists(path)) return;

            _currentTrack = filename;
            _bgmPlayer.Open(new Uri(path, UriKind.Absolute));
            _bgmPlayer.Volume = Volume;
            _bgmPlayer.Play();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BGM Play error: {ex.Message}");
        }
    }

    #region 절차적 BGM 신디사이저 엔진 (16비트 PCM 스테레오/모노)
    private static byte[] GenerateCombatTheme()
    {
        int sampleRate = 22050;
        double bpm = 126.0;
        double beatSec = 60.0 / bpm;
        int beats = 32; // 8마디 (약 15.2초)
        int totalSamples = (int)(sampleRate * beatSec * beats);
        short[] samples = new short[totalSamples];

        // 코드 진행: E minor -> C major -> G major -> D major (각 2마디 = 8비트)
        double[][] chordRoots = {
            new[] { 164.81, 196.00, 246.94 }, // Em (E3, G3, B3)
            new[] { 130.81, 164.81, 196.00 }, // C (C3, E3, G3)
            new[] { 196.00, 246.94, 293.66 }, // G (G3, B3, D4)
            new[] { 146.83, 185.00, 220.00 }  // D (D3, F#3, A3)
        };

        double[] bassNotes = { 82.41, 65.41, 98.00, 73.42 }; // E2, C2, G2, D2

        var rnd = new Random(101);

        for (int i = 0; i < totalSamples; i++)
        {
            double t = (double)i / sampleRate;
            double currentBeat = t / beatSec;
            int beatIndex = (int)currentBeat % beats;
            int chordIndex = (beatIndex / 8) % 4;

            double mix = 0.0;

            // 1. 드럼: 킥 (매 박자), 스네어 (2, 4박), 하이햇 (8분음표)
            double beatFraction = currentBeat - Math.Floor(currentBeat);
            // 킥
            if (beatFraction < 0.15)
            {
                double kickT = beatFraction / 0.15;
                double kickFreq = 120.0 * Math.Pow(0.1, kickT);
                mix += Math.Sin(2 * Math.PI * kickFreq * t) * (1.0 - kickT) * 0.45;
            }
            // 스네어 (2번째, 4번째 박자)
            if ((beatIndex % 2 == 1) && beatFraction < 0.18)
            {
                double snT = beatFraction / 0.18;
                mix += (rnd.NextDouble() * 2.0 - 1.0) * (1.0 - snT) * 0.3;
            }
            // 하이햇 (매 8분음표)
            double halfBeat = (currentBeat * 2.0) - Math.Floor(currentBeat * 2.0);
            if (halfBeat < 0.06)
            {
                double hhT = halfBeat / 0.06;
                mix += (rnd.NextDouble() * 2.0 - 1.0) * (1.0 - hhT) * 0.12;
            }

            // 2. 묵직한 톱니파 베이스라인 (16분음표 펄스)
            double subBeat = (currentBeat * 4.0) - Math.Floor(currentBeat * 4.0);
            double bassFreq = bassNotes[chordIndex];
            double bassSaw = ((t * bassFreq) % 1.0) * 2.0 - 1.0;
            mix += bassSaw * Math.Pow(1.0 - subBeat, 1.5) * 0.32;

            // 3. 신디 아르페지오 (16분음표 디지털 멜로디)
            int arpStep = (int)(currentBeat * 4.0) % 3;
            double arpFreq = chordRoots[chordIndex][arpStep] * 2.0;
            double arpWave = Math.Sin(2 * Math.PI * arpFreq * t);
            mix += arpWave * Math.Pow(1.0 - subBeat, 2.0) * 0.22;

            mix = Math.Clamp(mix, -1.0, 1.0);
            samples[i] = (short)(mix * short.MaxValue * 0.85);
        }

        return CreateWavHeaderAndData(samples, sampleRate);
    }

    private static byte[] GenerateBossTheme()
    {
        int sampleRate = 22050;
        double bpm = 146.0;
        double beatSec = 60.0 / bpm;
        int beats = 32;
        int totalSamples = (int)(sampleRate * beatSec * beats);
        short[] samples = new short[totalSamples];

        double[] bassRoots = { 73.42, 69.30, 82.41, 65.41 }; // D2, C#2, E2, C2 (긴장감 넘치는 반음계)
        var rnd = new Random(202);

        for (int i = 0; i < totalSamples; i++)
        {
            double t = (double)i / sampleRate;
            double currentBeat = t / beatSec;
            int beatIndex = (int)currentBeat % beats;
            int chordIndex = (beatIndex / 8) % 4;

            double mix = 0.0;

            // 더 빠른 더블 킥 비트
            double halfBeat = (currentBeat * 2.0) - Math.Floor(currentBeat * 2.0);
            if (halfBeat < 0.12)
            {
                double kickT = halfBeat / 0.12;
                mix += Math.Sin(2 * Math.PI * (140.0 * Math.Pow(0.1, kickT)) * t) * (1.0 - kickT) * 0.5;
            }
            // 강력한 인더스트리얼 스네어
            double beatFraction = currentBeat - Math.Floor(currentBeat);
            if ((beatIndex % 2 == 1) && beatFraction < 0.22)
            {
                double snT = beatFraction / 0.22;
                mix += (rnd.NextDouble() * 2.0 - 1.0) * (1.0 - snT) * 0.38;
            }

            // 고속 갤로핑 베이스
            double gallop = (currentBeat * 4.0) - Math.Floor(currentBeat * 4.0);
            double bFreq = bassRoots[chordIndex];
            double saw = ((t * bFreq) % 1.0) * 2.0 - 1.0;
            mix += saw * (1.0 - gallop) * 0.35;

            // 사이렌풍 고음 신스 리드
            double sirenFreq = 440.0 + Math.Sin(t * 8.0) * 120.0;
            mix += Math.Sin(2 * Math.PI * sirenFreq * t) * 0.15;

            mix = Math.Clamp(mix, -1.0, 1.0);
            samples[i] = (short)(mix * short.MaxValue * 0.88);
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
        bw.Write((short)1); // PCM
        bw.Write((short)1); // Mono
        bw.Write(sampleRate);
        bw.Write(sampleRate * 2);
        bw.Write((short)2);
        bw.Write((short)16);
        bw.Write(new[] { 'd', 'a', 't', 'a' });
        bw.Write(subChunk2Size);

        foreach (var s in samples)
        {
            bw.Write(s);
        }

        return ms.ToArray();
    }
    #endregion
}
