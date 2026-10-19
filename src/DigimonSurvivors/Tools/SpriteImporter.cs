using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DigimonSurvivors.Tools;

public static class SpriteImporter
{
    public static void ImportAllGenerated(string brainDir, string targetSpritesDir)
    {
        Directory.CreateDirectory(targetSpritesDir);

        var mapping = new (string pattern, string outputName)[]
        {
            ("patamon_sprite_*.jpg", "Patamon.png"),
            ("tentomon_sprite_*.jpg", "Tentomon.png"),
            ("gomamon_sprite_*.jpg", "Gomamon.png"),
            ("angemon_sprite_*.jpg", "Angemon.png")
        };

        foreach (var (pattern, outputName) in mapping)
        {
            var files = Directory.GetFiles(brainDir, pattern);
            if (files.Length == 0) continue;
            Array.Sort(files);
            string latest = files[^1];
            string outPath = Path.Combine(targetSpritesDir, outputName);
            ProcessToTransparentPng(latest, outPath);
        }
    }

    public static void ProcessToTransparentPng(string inputPath, string outputPath)
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

        // 외곽 코너 픽셀들의 평균 색상으로 배경 판단 (밝은 배경 vs 어두운 배경)
        int[] corners = { 0, w - 1, (h - 1) * w, (h - 1) * w + (w - 1) };
        long totalLum = 0;
        foreach (int c in corners)
        {
            int r = (pixels[c] >> 16) & 0xFF;
            int g = (pixels[c] >> 8) & 0xFF;
            int b = pixels[c] & 0xFF;
            totalLum += (r + g + b) / 3;
        }
        bool isDarkBg = (totalLum / 4) < 128;

        bool IsBg(int col)
        {
            int r = (col >> 16) & 0xFF;
            int g = (col >> 8) & 0xFF;
            int b = col & 0xFF;

            if (isDarkBg)
            {
                return r <= 45 && g <= 45 && b <= 45;
            }
            else
            {
                return r >= 225 && g >= 225 && b >= 225 &&
                       Math.Abs(r - g) <= 20 && Math.Abs(g - b) <= 20;
            }
        }

        byte[] bgMask = new byte[w * h];
        var queue = new Queue<int>(w * 4);

        for (int x = 0; x < w; x++)
        {
            int top = x;
            if (IsBg(pixels[top])) { bgMask[top] = 1; queue.Enqueue(top); }
            int btm = (h - 1) * w + x;
            if (IsBg(pixels[btm])) { bgMask[btm] = 1; queue.Enqueue(btm); }
        }

        for (int y = 0; y < h; y++)
        {
            int left = y * w;
            if (bgMask[left] == 0 && IsBg(pixels[left])) { bgMask[left] = 1; queue.Enqueue(left); }
            int right = y * w + (w - 1);
            if (bgMask[right] == 0 && IsBg(pixels[right])) { bgMask[right] = 1; queue.Enqueue(right); }
        }

        while (queue.Count > 0)
        {
            int curr = queue.Dequeue();
            int cx = curr % w;
            int cy = curr / w;

            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };

            for (int d = 0; d < 4; d++)
            {
                int nx = cx + dx[d];
                int ny = cy + dy[d];
                if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;

                int ni = ny * w + nx;
                if (bgMask[ni] == 0 && IsBg(pixels[ni]))
                {
                    bgMask[ni] = 1;
                    queue.Enqueue(ni);
                }
            }
        }

        // 투명화 및 경계선 앤티에일리어싱 처리
        for (int i = 0; i < pixels.Length; i++)
        {
            if (bgMask[i] == 1)
            {
                pixels[i] = 0; // 완전 투명
            }
            else
            {
                int r = (pixels[i] >> 16) & 0xFF;
                int g = (pixels[i] >> 8) & 0xFF;
                int b = pixels[i] & 0xFF;

                // 만약 배경색과 인접한 테두리라면 미세 디프린징
                int cx = i % w;
                int cy = i / w;
                bool isBorder = false;
                if (cx > 0 && bgMask[i - 1] == 1) isBorder = true;
                if (cx < w - 1 && bgMask[i + 1] == 1) isBorder = true;
                if (cy > 0 && bgMask[i - w] == 1) isBorder = true;
                if (cy < h - 1 && bgMask[i + w] == 1) isBorder = true;

                if (isBorder && isDarkBg && r < 50 && g < 50 && b < 50)
                {
                    pixels[i] = 0;
                }
                else
                {
                    pixels[i] = (255 << 24) | (r << 16) | (g << 8) | b;
                }
            }
        }

        var writeable = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
        writeable.WritePixels(new System.Windows.Int32Rect(0, 0, w, h), pixels, stride, 0);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(writeable));
        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        encoder.Save(fs);
    }
}
