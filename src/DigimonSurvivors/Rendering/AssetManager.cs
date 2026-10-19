using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DigimonSurvivors.Rendering;

public static class AssetManager
{
    private static readonly Dictionary<string, BitmapSource> _spriteCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, BitmapSource> _cardCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, BitmapSource> _skillCache = new(StringComparer.OrdinalIgnoreCase);

    public static string BaseDirectory { get; } = AppDomain.CurrentDomain.BaseDirectory;
    public static string SpritesDirectory => Path.Combine(BaseDirectory, "Assets", "Sprites");
    public static string CardsDirectory => Path.Combine(BaseDirectory, "Assets", "Cards");
    public static string SkillsDirectory => Path.Combine(BaseDirectory, "Assets", "Skills");
    public static string UiDirectory => Path.Combine(BaseDirectory, "Assets", "UI");

    public static void PrewarmAllSprites()
    {
        string dir = Directory.Exists(SpritesDirectory) ? SpritesDirectory : @"D:\Project\DigimonSurvivors\Assets\Sprites";
        if (!Directory.Exists(dir)) return;

        foreach (var file in Directory.GetFiles(dir, "*.png"))
        {
            string filename = Path.GetFileName(file);
            GetSprite(filename);
        }
    }

    public static void PrewarmAllSkills()
    {
        string dir = Directory.Exists(SkillsDirectory) ? SkillsDirectory : @"D:\Project\DigimonSurvivors\Assets\Skills";
        if (!Directory.Exists(dir)) return;

        foreach (var file in Directory.GetFiles(dir, "*.png"))
        {
            string skillId = Path.GetFileNameWithoutExtension(file);
            GetSkillSprite(skillId);
        }
    }

    public static BitmapSource? GetSprite(string filename)
    {
        if (string.IsNullOrEmpty(filename)) return null;

        if (_spriteCache.TryGetValue(filename, out var cached))
        {
            return cached;
        }

        string fullPath = Path.Combine(SpritesDirectory, filename);
        if (!File.Exists(fullPath))
        {
            string devPath = Path.Combine(@"D:\Project\DigimonSurvivors\Assets\Sprites", filename);
            if (File.Exists(devPath)) fullPath = devPath;
            else return null;
        }

        try
        {
            var transparentSource = LoadAndRemoveBackground(fullPath);
            if (transparentSource != null)
            {
                _spriteCache[filename] = transparentSource;
                return transparentSource;
            }
        }
        catch
        {
            // 폴백
        }

        return null;
    }

    public static BitmapSource? GetCardImage(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return null;

        if (_cardCache.TryGetValue(cardId, out var cached))
        {
            return cached;
        }

        string fullPath = Path.Combine(CardsDirectory, $"{cardId}.jpg");
        if (!File.Exists(fullPath))
        {
            string devPath = Path.Combine(@"D:\Project\DigimonSurvivors\Assets\Cards", $"{cardId}.jpg");
            if (File.Exists(devPath)) fullPath = devPath;
            else
            {
                string tcgPath = Path.Combine(@"D:\Project\DigimonTcg\data\images", $"{cardId}.jpg");
                if (File.Exists(tcgPath)) fullPath = tcgPath;
                else return null;
            }
        }

        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(fullPath, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            _cardCache[cardId] = bmp;
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    public static BitmapSource? GetSkillSprite(string skillId)
    {
        if (string.IsNullOrEmpty(skillId)) return null;

        if (_skillCache.TryGetValue(skillId, out var cached))
        {
            return cached;
        }

        string filename = $"{skillId}.png";
        string fullPath = Path.Combine(SkillsDirectory, filename);
        if (!File.Exists(fullPath))
        {
            string devPath = Path.Combine(@"D:\Project\DigimonSurvivors\Assets\Skills", filename);
            if (File.Exists(devPath)) fullPath = devPath;
            else return null;
        }

        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(fullPath, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            _skillCache[skillId] = bmp;
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 이미지 외곽 테두리로부터 Flood-fill 탐색하여 흰색 배경을 완벽하게 누끼(투명화) 처리합니다.
    /// 디지몬의 눈동자, 이빨, 발톱 등 내부의 흰색 영역은 보존되며, 경계선 앤티에일리어싱 헤일로를 제거합니다.
    /// </summary>
    public static BitmapSource? LoadAndRemoveBackground(string filePath)
    {
        var rawBmp = new BitmapImage();
        rawBmp.BeginInit();
        rawBmp.CacheOption = BitmapCacheOption.OnLoad;
        rawBmp.UriSource = new Uri(filePath, UriKind.Absolute);
        rawBmp.EndInit();

        // Bgra32로 변환
        var formatted = new FormatConvertedBitmap(rawBmp, PixelFormats.Bgra32, null, 0);
        int w = formatted.PixelWidth;
        int h = formatted.PixelHeight;
        int stride = w * 4;
        int[] pixels = new int[w * h];
        formatted.CopyPixels(pixels, stride, 0);

        // 이미 투명한 배경인지 확인 (네 모퉁이 확인)
        int corner1 = (pixels[0] >> 24) & 0xFF;
        int corner2 = (pixels[w - 1] >> 24) & 0xFF;
        int corner3 = (pixels[(h - 1) * w] >> 24) & 0xFF;
        int corner4 = (pixels[w * h - 1] >> 24) & 0xFF;

        if (corner1 < 30 && corner2 < 30 && corner3 < 30 && corner4 < 30)
        {
            // 이미 투명화되어 있는 이미지
            formatted.Freeze();
            return formatted;
        }

        // 흰색 계열 판정 함수 (R, G, B >= 225)
        static bool IsNearWhite(int col)
        {
            int r = (col >> 16) & 0xFF;
            int g = (col >> 8) & 0xFF;
            int b = col & 0xFF;
            return r >= 225 && g >= 225 && b >= 225;
        }

        byte[] bgMask = new byte[w * h]; // 1 = 외곽 배경
        var queue = new Queue<int>(w * 4);

        // 상하 테두리 탐색 시작점
        for (int x = 0; x < w; x++)
        {
            int top = x;
            if (IsNearWhite(pixels[top])) { bgMask[top] = 1; queue.Enqueue(top); }
            int btm = (h - 1) * w + x;
            if (IsNearWhite(pixels[btm])) { bgMask[btm] = 1; queue.Enqueue(btm); }
        }

        // 좌우 테두리 탐색 시작점
        for (int y = 0; y < h; y++)
        {
            int left = y * w;
            if (bgMask[left] == 0 && IsNearWhite(pixels[left])) { bgMask[left] = 1; queue.Enqueue(left); }
            int right = y * w + (w - 1);
            if (bgMask[right] == 0 && IsNearWhite(pixels[right])) { bgMask[right] = 1; queue.Enqueue(right); }
        }

        // BFS Flood-fill
        while (queue.Count > 0)
        {
            int curr = queue.Dequeue();
            int cx = curr % w;
            int cy = curr / w;

            // 상
            if (cy > 0)
            {
                int up = (cy - 1) * w + cx;
                if (bgMask[up] == 0 && IsNearWhite(pixels[up])) { bgMask[up] = 1; queue.Enqueue(up); }
            }
            // 하
            if (cy < h - 1)
            {
                int down = (cy + 1) * w + cx;
                if (bgMask[down] == 0 && IsNearWhite(pixels[down])) { bgMask[down] = 1; queue.Enqueue(down); }
            }
            // 좌
            if (cx > 0)
            {
                int left = cy * w + (cx - 1);
                if (bgMask[left] == 0 && IsNearWhite(pixels[left])) { bgMask[left] = 1; queue.Enqueue(left); }
            }
            // 우
            if (cx < w - 1)
            {
                int right = cy * w + (cx + 1);
                if (bgMask[right] == 0 && IsNearWhite(pixels[right])) { bgMask[right] = 1; queue.Enqueue(right); }
            }
        }

        // 픽셀 투명화 및 경계선 앤티에일리어싱(Defringe)
        for (int i = 0; i < pixels.Length; i++)
        {
            if (bgMask[i] == 1)
            {
                pixels[i] = 0; // 완전 투명
            }
            else
            {
                int cx = i % w;
                int cy = i / w;
                bool isBorder = (cx > 0 && bgMask[i - 1] == 1) ||
                                (cx < w - 1 && bgMask[i + 1] == 1) ||
                                (cy > 0 && bgMask[i - w] == 1) ||
                                (cy < h - 1 && bgMask[i + w] == 1);

                if (isBorder)
                {
                    int col = pixels[i];
                    int r = (col >> 16) & 0xFF;
                    int g = (col >> 8) & 0xFF;
                    int b = col & 0xFF;
                    int brightness = (r + g + b) / 3;

                    // 경계 흰색 테두리 부드럽게 페더링
                    if (brightness > 200)
                    {
                        double alphaRatio = Math.Clamp((255 - brightness) / 55.0, 0.05, 1.0);
                        int alpha = (int)(alphaRatio * 255);
                        pixels[i] = (alpha << 24) | (r << 16) | (g << 8) | b;
                    }
                }
            }
        }

        var wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
        wb.WritePixels(new Int32Rect(0, 0, w, h), pixels, stride, 0);
        wb.Freeze();

        // 투명화된 깨끗한 PNG 파일로 디스크에 자동 재저장 (영구 누끼 적용)
        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(wb));
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            encoder.Save(fs);
        }
        catch
        {
            // 디스크 쓰기 오류 시에도 인메모리 반환
        }

        return wb;
    }
}
