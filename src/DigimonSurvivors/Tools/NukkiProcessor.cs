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
        bool IsBackgroundCandidate(int col)
        {
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

        // 3. 다리 사이, 날개 사이, 꼬리 아래 등 "고립된 내부 배경 포켓" 탐색!
        // 외곽과 연결되지 않았더라도, 면적이 넓은 순백색 덩어리(50픽셀 이상)이면서
        // 평균 밝기가 240 이상이고 색상 차이가 거의 없는 덩어리는 배경으로 판정
        bool[] visited2 = new bool[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int pos = y * w + x;
                if (bgMask[pos] == 0 && !visited2[pos] && IsBackgroundCandidate(pixels[pos]))
                {
                    // 연결된 흰색 컴포넌트 수집
                    var comp = new List<int>();
                    var q = new Queue<int>();
                    q.Enqueue(pos);
                    visited2[pos] = true;

                    while (q.Count > 0)
                    {
                        int c = q.Dequeue();
                        comp.Add(c);
                        int cx = c % w;
                        int cy = c / w;

                        int[] dx = { 1, -1, 0, 0 };
                        int[] dy = { 0, 0, 1, -1 };
                        for (int i = 0; i < 4; i++)
                        {
                            int nx = cx + dx[i];
                            int ny = cy + dy[i];
                            if (nx >= 0 && nx < w && ny >= 0 && ny < h)
                            {
                                int nPos = ny * w + nx;
                                if (!visited2[nPos] && bgMask[nPos] == 0 && IsBackgroundCandidate(pixels[nPos]))
                                {
                                    visited2[nPos] = true;
                                    q.Enqueue(nPos);
                                }
                            }
                        }
                    }

                    // 눈동자나 작은 치아(보통 15~35픽셀 이하)는 보존하고,
                    // 다리 사이/겨드랑이/날개 사이 등의 큰 고립 배경(40픽셀 이상) 또는
                    // 매우 순수한 흰색(평균 R,G,B >= 245) 덩어리는 배경으로 처리!
                    if (comp.Count >= 30)
                    {
                        double avgR = 0, avgG = 0, avgB = 0;
                        foreach (var p in comp)
                        {
                            avgR += (pixels[p] >> 16) & 0xFF;
                            avgG += (pixels[p] >> 8) & 0xFF;
                            avgB += pixels[p] & 0xFF;
                        }
                        avgR /= comp.Count;
                        avgG /= comp.Count;
                        avgB /= comp.Count;

                        if (avgR >= 238 && avgG >= 238 && avgB >= 238)
                        {
                            foreach (var p in comp)
                            {
                                bgMask[p] = 1;
                            }
                        }
                    }
                }
            }
        }

        // 4. 픽셀 알파 투명화 및 경계선 앤티에일리어싱 페더링 (디프린징)
        for (int i = 0; i < pixels.Length; i++)
        {
            if (bgMask[i] == 1)
            {
                pixels[i] = 0x00000000; // 완전 투명
            }
            else
            {
                int cx = i % w;
                int cy = i / w;
                bool isNearBg = (cx > 0 && bgMask[i - 1] == 1) ||
                                (cx < w - 1 && bgMask[i + 1] == 1) ||
                                (cy > 0 && bgMask[i - w] == 1) ||
                                (cy < h - 1 && bgMask[i + w] == 1);

                if (isNearBg)
                {
                    int col = pixels[i];
                    int r = (col >> 16) & 0xFF;
                    int g = (col >> 8) & 0xFF;
                    int b = col & 0xFF;
                    int bright = (r + g + b) / 3;

                    // 경계면 흰색 잔상 페더링
                    if (bright > 200)
                    {
                        double alphaRatio = Math.Clamp((255 - bright) / 55.0, 0.05, 1.0);
                        int alpha = (int)(alphaRatio * 255);
                        pixels[i] = (alpha << 24) | (r << 16) | (g << 8) | b;
                    }
                }
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
}
