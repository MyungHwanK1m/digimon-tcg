using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DigimonSurvivors.Tools;

public static class NukkiProcessor
{
    public static void ProcessAll(string sourceDir, string targetDir)
    {
        if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir, "*.png"))
        {
            string filename = Path.GetFileName(file);
            string outPath = Path.Combine(targetDir, filename);
            ProcessImage(file, outPath);
        }
    }

    public static void ProcessImage(string inputPath, string outputPath)
    {
        var rawBmp = new BitmapImage();
        rawBmp.BeginInit();
        rawBmp.CacheOption = BitmapCacheOption.OnLoad;
        rawBmp.UriSource = new Uri(inputPath, UriKind.Absolute);
        rawBmp.EndInit();

        var formatted = new FormatConvertedBitmap(rawBmp, PixelFormats.Bgra32, null, 0);
        int w = formatted.PixelWidth;
        int h = formatted.PixelHeight;
        int stride = w * 4;
        int[] pixels = new int[w * h];
        formatted.CopyPixels(pixels, stride, 0);

        // 1. 배경 판정 기준
        // 배경은 순백색 또는 압축 노이즈로 인한 매우 밝은 무채색 (R,G,B >= 230, |R-G|<=12, |G-B|<=12)
        // 또는 이미 알파 투명화가 되어 있는 픽셀
        bool IsBackgroundCandidate(int col)
        {
            int a = (col >> 24) & 0xFF;
            if (a < 64) return true;

            int r = (col >> 16) & 0xFF;
            int g = (col >> 8) & 0xFF;
            int b = col & 0xFF;

            if (r < 225 || g < 225 || b < 225) return false;
            int diffRG = Math.Abs(r - g);
            int diffGB = Math.Abs(g - b);
            int diffRB = Math.Abs(r - b);
            return diffRG <= 15 && diffGB <= 15 && diffRB <= 15;
        }

        // 2. 외곽 테두리로부터 Flood-Fill (1차 배경 영역 탐색)
        byte[] bgMask = new byte[w * h]; // 1 = 확실한 외부 배경
        var queue = new Queue<int>(w * 4);

        for (int x = 0; x < w; x++)
        {
            int top = x;
            if (IsBackgroundCandidate(pixels[top])) { bgMask[top] = 1; queue.Enqueue(top); }
            int btm = (h - 1) * w + x;
            if (IsBackgroundCandidate(pixels[btm])) { bgMask[btm] = 1; queue.Enqueue(btm); }
        }

        for (int y = 0; y < h; y++)
        {
            int left = y * w;
            if (bgMask[left] == 0 && IsBackgroundCandidate(pixels[left])) { bgMask[left] = 1; queue.Enqueue(left); }
            int right = y * w + (w - 1);
            if (bgMask[right] == 0 && IsBackgroundCandidate(pixels[right])) { bgMask[right] = 1; queue.Enqueue(right); }
        }

        while (queue.Count > 0)
        {
            int curr = queue.Dequeue();
            int cx = curr % w;
            int cy = curr / w;

            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };

            for (int i = 0; i < 4; i++)
            {
                int nx = cx + dx[i];
                int ny = cy + dy[i];
                if (nx >= 0 && nx < w && ny >= 0 && ny < h)
                {
                    int nPos = ny * w + nx;
                    if (bgMask[nPos] == 0 && IsBackgroundCandidate(pixels[nPos]))
                    {
                        bgMask[nPos] = 1;
                        queue.Enqueue(nPos);
                    }
                }
            }
        }

        // 3. 고립된 내부 흰색 영역(몸체, 배, 털, 얼굴 등)은 캐릭터 본체이므로 절대 삭제하지 않고 100% 보존합니다.
        // 외곽 테두리로부터 도달 가능한 외부 배경만 투명화 처리합니다.

        // 4. 픽셀 알파 투명화 및 경계선 앤티에일리어싱 처리
        for (int i = 0; i < pixels.Length; i++)
        {
            int origA = (pixels[i] >> 24) & 0xFF;
            if (bgMask[i] == 1 || origA < 64)
            {
                pixels[i] = 0x00000000; // 완전 투명
            }
            else
            {
                // 원작 캐릭터의 흰색 털, 흰색 피부, 크림색 배 등은 완전히 불투명하게 보존
                int col = pixels[i];
                int r = (col >> 16) & 0xFF;
                int g = (col >> 8) & 0xFF;
                int b = col & 0xFF;
                pixels[i] = (255 << 24) | (r << 16) | (g << 8) | b;
            }
        }

        var wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
        wb.WritePixels(new Int32Rect(0, 0, w, h), pixels, stride, 0);
        wb.Freeze();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(wb));
        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        encoder.Save(fs);
    }

    public static void ProcessOfficialArtwork()
    {
        string tcgDir = @"D:\Project\DigimonTcg";
        string rawDir = Path.Combine(tcgDir, "src", "DigimonSurvivors", "Assets", "RawArt");
        string freshDir = Path.Combine(tcgDir, "src", "DigimonSurvivors", "Assets", "Fresh");
        Directory.CreateDirectory(freshDir);

        var targets = new (string rawFile, string spriteName)[]
        {
            (Path.Combine(rawDir, "Patamon.jpg"), "Patamon.png"),
            (Path.Combine(rawDir, "Gomamon.gif"), "Gomamon.png"),
            (Path.Combine(rawDir, "Tentomon.jpg"), "Tentomon.png"),
            (Path.Combine(rawDir, "Angemon.jpg"), "Angemon.png")
        };

        string extFreshDir = @"D:\Project\DigimonSurvivors\Assets\Fresh";
        if (!Directory.Exists(extFreshDir)) Directory.CreateDirectory(extFreshDir);

        string[] destDirs = new[]
        {
            freshDir,
            extFreshDir,
            Path.Combine(tcgDir, "src", "DigimonSurvivors", "Assets", "Sprites"),
            Path.Combine(tcgDir, "src", "DigimonSurvivors", "Assets", "Sprites", "Characters"),
            @"D:\Project\DigimonSurvivors\Assets\Sprites",
            @"D:\Project\DigimonSurvivors\Assets\Sprites\Characters",
            @"D:\Project\DigimonSurvivors\publish\Assets\Sprites",
            @"D:\Project\DigimonSurvivors\publish\Assets\Sprites\Characters"
        };

        foreach (var dir in destDirs)
        {
            try
            {
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }
            catch { }
        }

        foreach (var (srcFile, spriteName) in targets)
        {
            if (!File.Exists(srcFile)) continue;
            string freshPath = Path.Combine(freshDir, spriteName);
            ProcessImage(srcFile, freshPath);

            foreach (var d in destDirs)
            {
                try
                {
                    if (Directory.Exists(d))
                    {
                        File.Copy(freshPath, Path.Combine(d, spriteName), true);
                    }
                }
                catch { }
            }
        }

        // Fresh 내의 모든 원작 일러스트(가루몬 등 흰색 털/몸체 디지몬 포함)도 신규 알고리즘으로 동기화
        foreach (var freshFile in Directory.GetFiles(freshDir, "*.png"))
        {
            string spriteName = Path.GetFileName(freshFile);
            string tempOut = Path.Combine(Path.GetTempPath(), $"clean_{spriteName}");
            try
            {
                ProcessImage(freshFile, tempOut);
                foreach (var d in destDirs)
                {
                    if (Directory.Exists(d))
                    {
                        File.Copy(tempOut, Path.Combine(d, spriteName), true);
                    }
                }
                File.Delete(tempOut);
            }
            catch { }
        }
    }
}
