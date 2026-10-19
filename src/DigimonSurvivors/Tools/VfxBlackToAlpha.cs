using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DigimonSurvivors.Tools;

public static class VfxBlackToAlpha
{
    /// <summary>
    /// 검은색 배경(#000000)의 스킬 이펙트 이미지를 읽어
    /// 어두운 배경은 투명화하고, 밝은 빛과 화염은 자연스러운 반투명 발광(Glow)으로 변환합니다.
    /// </summary>
    public static void ConvertBlackBackgroundToAlpha(string inputPath, string outputPath)
    {
        if (!File.Exists(inputPath)) return;

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

        for (int i = 0; i < pixels.Length; i++)
        {
            int col = pixels[i];
            int r = (col >> 16) & 0xFF;
            int g = (col >> 8) & 0xFF;
            int b = col & 0xFF;

            // 최대 채널 값을 기준으로 알파값 결정
            int maxChannel = Math.Max(r, Math.Max(g, b));

            // 어두운 블랙 컷오프 임계값
            if (maxChannel < 18)
            {
                pixels[i] = 0x00000000;
            }
            else
            {
                // 부드러운 감마 커브로 알파 계산 (발광 효과 극대화)
                double t = (maxChannel - 18) / (255.0 - 18.0);
                double alpha = Math.Clamp(Math.Pow(t, 0.75) * 255.0, 0, 255);
                int a = (int)alpha;

                // 색상 정규화 (검은색 배경 영향 제거)
                double colorBoost = 255.0 / Math.Max(1, maxChannel);
                int newR = Math.Min(255, (int)(r * Math.Pow(colorBoost, 0.3)));
                int newG = Math.Min(255, (int)(g * Math.Pow(colorBoost, 0.3)));
                int newB = Math.Min(255, (int)(b * Math.Pow(colorBoost, 0.3)));

                pixels[i] = (a << 24) | (newR << 16) | (newG << 8) | newB;
            }
        }

        var wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
        wb.WritePixels(new Int32Rect(0, 0, w, h), pixels, stride, 0);
        wb.Freeze();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(wb));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        encoder.Save(fs);
    }
}
