using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DigimonSurvivors.Tools;

public static class IconBuilder
{
    public static void BuildAppIcon(string projectRoot)
    {
        string assetsDir = Path.Combine(projectRoot, "Assets");
        string pngPath = Path.Combine(assetsDir, "app_icon.png");
        string icoPath = Path.Combine(assetsDir, "app_icon.ico");
        string agumonPath = Path.Combine(assetsDir, "Sprites", "Agumon.png");

        int size = 256;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);

            // 1. 외곽 원형 그림자
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)), null, new Point(center.X, center.Y + 6), 120, 120);

            // 2. 고대 디지바이스 / 사이버 팔각형 쉴드 배경
            var bgBrush = new RadialGradientBrush(Color.FromRgb(24, 38, 64), Color.FromRgb(8, 12, 22));
            var borderPen = new Pen(new LinearGradientBrush(Color.FromRgb(255, 215, 0), Color.FromRgb(0, 229, 255), 45.0), 5.0);
            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(14, 14, 228, 228), 36, 36);

            // 3. 내부 사이버 그리드 서킷 라인
            var circuitPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 0, 229, 255)), 1.5);
            dc.DrawLine(circuitPen, new Point(30, size / 2.0), new Point(226, size / 2.0));
            dc.DrawLine(circuitPen, new Point(size / 2.0, 30), new Point(size / 2.0, 226));
            dc.DrawEllipse(null, circuitPen, center, 85, 85);

            // 4. 후광 글로우 링
            var glowBrush = new RadialGradientBrush(Color.FromArgb(100, 255, 140, 0), Color.FromArgb(0, 255, 60, 0));
            dc.DrawEllipse(glowBrush, null, new Point(center.X, center.Y - 2), 75, 75);

            // 5. 아구몬 스프라이트 합성
            if (File.Exists(agumonPath))
            {
                var agumonBmp = new BitmapImage();
                agumonBmp.BeginInit();
                agumonBmp.CacheOption = BitmapCacheOption.OnLoad;
                agumonBmp.UriSource = new Uri(agumonPath, UriKind.Absolute);
                agumonBmp.EndInit();

                // 아구몬을 중앙에 배치
                double charSize = 175;
                dc.DrawImage(agumonBmp, new Rect(center.X - charSize / 2.0, center.Y - charSize / 2.0 - 6, charSize, charSize));
            }

            // 6. 하단 'DIGI SURVIVORS' 골드 리본 뱃지
            var ribbonBrush = new LinearGradientBrush(Color.FromRgb(220, 20, 20), Color.FromRgb(140, 0, 0), 90.0);
            var ribbonPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 215, 0)), 2.0);
            Rect ribbonRect = new(24, 195, 208, 38);
            dc.DrawRoundedRectangle(ribbonBrush, ribbonPen, ribbonRect, 8, 8);

            var tf = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.ExtraBold, FontStretches.Normal);
            var text = new FormattedText("DIGIMON SURVIVORS", System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, tf, 13.5, Brushes.White, 1.0);
            dc.DrawText(text, new Point(center.X - text.Width / 2.0, 204));
        }

        // 256x256 PNG 저장
        var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using (var fs = new FileStream(pngPath, FileMode.Create, FileAccess.Write))
        {
            encoder.Save(fs);
        }

        // 멀티 해상도 ICO 생성 (256, 128, 64, 48, 32, 16)
        int[] resolutions = { 256, 128, 64, 48, 32, 16 };
        byte[][] pngImages = new byte[resolutions.Length][];

        for (int i = 0; i < resolutions.Length; i++)
        {
            int r = resolutions[i];
            if (r == 256)
            {
                pngImages[i] = File.ReadAllBytes(pngPath);
            }
            else
            {
                var scaledDv = new DrawingVisual();
                using (var sdc = scaledDv.RenderOpen())
                {
                    sdc.DrawImage(rtb, new Rect(0, 0, r, r));
                }
                var scaledRtb = new RenderTargetBitmap(r, r, 96, 96, PixelFormats.Pbgra32);
                scaledRtb.Render(scaledDv);

                var sEnc = new PngBitmapEncoder();
                sEnc.Frames.Add(BitmapFrame.Create(scaledRtb));
                using var ms = new MemoryStream();
                sEnc.Save(ms);
                pngImages[i] = ms.ToArray();
            }
        }

        // ICO 바이너리 포맷 조합
        using (var fsIco = new FileStream(icoPath, FileMode.Create, FileAccess.Write))
        using (var bw = new BinaryWriter(fsIco))
        {
            // ICONDIR
            bw.Write((short)0); // reserved
            bw.Write((short)1); // icon type
            bw.Write((short)resolutions.Length); // count

            int offset = 6 + (16 * resolutions.Length);
            for (int i = 0; i < resolutions.Length; i++)
            {
                int r = resolutions[i];
                bw.Write((byte)(r == 256 ? 0 : r)); // width
                bw.Write((byte)(r == 256 ? 0 : r)); // height
                bw.Write((byte)0); // color count
                bw.Write((byte)0); // reserved
                bw.Write((short)1); // planes
                bw.Write((short)32); // bit count
                bw.Write(pngImages[i].Length); // bytes in res
                bw.Write(offset); // image offset
                offset += pngImages[i].Length;
            }

            // Image Data (PNG streams)
            for (int i = 0; i < resolutions.Length; i++)
            {
                bw.Write(pngImages[i]);
            }
        }
    }
}
