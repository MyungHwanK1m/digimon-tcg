using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DigimonSurvivors.Data;
using DigimonSurvivors.Models;

namespace DigimonSurvivors.Tools;

/// <summary>
/// 공식 TCG 카드의 고해상도 일러스트를 추출하여 속성별 사이버 네온 링이 적용된 정밀 토큰 스프라이트를 자동 빌드합니다.
/// </summary>
public static class DigimonTokenGenerator
{
    private static readonly HashSet<string> _targetSpriteNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Kabuterimon.png", "Ikkakumon.png", "DarkTyrannomon.png", "Coredramon.png",
        "Unimon.png", "Gatomon.png", "Wizardmon.png", "BlackGatomon.png",
        "Togemon.png", "Woodmon.png", "Grizzlymon.png", "Tankmon.png",
        "Seadramon.png", "HolyAngemon.png", "MegaKabuterimon.png", "Zudomon.png",
        "Angewomon.png", "LadyDevimon.png", "Lilimon.png", "Seraphimon.png",
        "HerculesKabuterimon.png", "Plesiomon.png", "Rosemon.png", "Mugendramon.png"
    };

    public static void GenerateAll(string projectRoot)
    {
        string spritesDir = Path.Combine(projectRoot, "Assets", "Sprites");
        string charDir = Path.Combine(spritesDir, "Characters");
        Directory.CreateDirectory(spritesDir);
        Directory.CreateDirectory(charDir);

        string tcgCardsDir = @"D:\Project\DigimonTcg\data\images";
        string extDevDir = @"D:\Project\DigimonSurvivors\Assets\Sprites";
        string pubDir = @"D:\Project\DigimonSurvivors\publish\Assets\Sprites";

        foreach (var profile in GameDatabase.Digimons.Values)
        {
            if (string.IsNullOrEmpty(profile.SpriteFileName) || string.IsNullOrEmpty(profile.CardId))
                continue;

            string outPath = Path.Combine(spritesDir, profile.SpriteFileName);
            string charOutPath = Path.Combine(charDir, profile.SpriteFileName);

            // 타겟 생성 목록에 있거나 파일이 아예 없는 경우 생성
            bool needsGeneration = _targetSpriteNames.Contains(profile.SpriteFileName) || !File.Exists(outPath);
            if (!needsGeneration) continue;

            string cardPath = Path.Combine(tcgCardsDir, $"{profile.CardId}.jpg");
            if (!File.Exists(cardPath))
            {
                cardPath = Path.Combine(projectRoot, "Assets", "Cards", $"{profile.CardId}.jpg");
                if (!File.Exists(cardPath)) continue;
            }

            try
            {
                var token = CreateTokenBitmap(cardPath, profile.Attribute);
                if (token != null)
                {
                    SavePng(token, outPath);
                    SavePng(token, charOutPath);

                    string tcgDir = @"D:\Project\DigimonTcg\src\DigimonSurvivors\Assets\Sprites";
                    if (Directory.Exists(tcgDir))
                    {
                        SavePng(token, Path.Combine(tcgDir, profile.SpriteFileName));
                        SavePng(token, Path.Combine(tcgDir, "Characters", profile.SpriteFileName));
                    }

                    if (Directory.Exists(extDevDir))
                    {
                        SavePng(token, Path.Combine(extDevDir, profile.SpriteFileName));
                        SavePng(token, Path.Combine(extDevDir, "Characters", profile.SpriteFileName));
                    }

                    if (Directory.Exists(pubDir))
                    {
                        SavePng(token, Path.Combine(pubDir, profile.SpriteFileName));
                        SavePng(token, Path.Combine(pubDir, "Characters", profile.SpriteFileName));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to generate token for {profile.Name}: {ex.Message}");
            }
        }
    }

    public static BitmapSource? CreateTokenBitmap(string cardPath, DigimonAttribute attribute)
    {
        if (!File.Exists(cardPath)) return null;

        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.UriSource = new Uri(cardPath, UriKind.Absolute);
        bmp.EndInit();
        bmp.Freeze();

        int width = 256;
        int height = 256;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            // 카드 상단 일러스트 영역 정밀 크롭 (300x419 기준 X:40~250, Y:33~243)
            int cropX = Math.Max(0, Math.Min(bmp.PixelWidth - 210, 45));
            int cropY = Math.Max(0, Math.Min(bmp.PixelHeight - 210, 33));
            int cropW = Math.Min(210, bmp.PixelWidth - cropX);
            int cropH = Math.Min(210, bmp.PixelHeight - cropY);

            var rectCrop = new Int32Rect(cropX, cropY, cropW, cropH);
            var cardCrop = new CroppedBitmap(bmp, rectCrop);

            // 속성별 고유 사이버 오라
            Color auraColor = attribute switch
            {
                DigimonAttribute.Vaccine => Color.FromRgb(0, 229, 255), // Bright Cyan
                DigimonAttribute.Data => Color.FromRgb(0, 230, 118),    // Emerald Green
                DigimonAttribute.Virus => Color.FromRgb(255, 60, 90),   // Crimson
                _ => Color.FromRgb(255, 215, 0)                         // Gold
            };

            // 1. 발밑 자연스러운 접지 그림자
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)), null, new Point(128, 234), 68, 16);

            // 2. 캐릭터 후방 은은한 네온 에너지 오라 (원형 링 대신 방사형 소프트 빛)
            var auraBrush = new RadialGradientBrush(Color.FromArgb(55, auraColor.R, auraColor.G, auraColor.B), Color.FromArgb(0, auraColor.R, auraColor.G, auraColor.B))
            {
                Center = new Point(0.5, 0.48),
                GradientOrigin = new Point(0.5, 0.48),
                RadiusX = 0.52,
                RadiusY = 0.52
            };
            dc.DrawRectangle(auraBrush, null, new Rect(0, 0, width, height));

            // 3. 딱딱한 원형 테두리(코인/토큰)를 완전 배제하고, 자연스러운 캐릭터 실루엣을 위한 소프트 경계 페이드 마스크 적용
            var opacityMask = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.48),
                GradientOrigin = new Point(0.5, 0.48),
                RadiusX = 0.5,
                RadiusY = 0.5
            };
            opacityMask.GradientStops.Add(new GradientStop(Color.FromArgb(255, 0, 0, 0), 0.0));
            opacityMask.GradientStops.Add(new GradientStop(Color.FromArgb(255, 0, 0, 0), 0.72));
            opacityMask.GradientStops.Add(new GradientStop(Color.FromArgb(120, 0, 0, 0), 0.88));
            opacityMask.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 1.0));
            opacityMask.Freeze();

            dc.PushOpacityMask(opacityMask);
            var drawRect = new Rect(10, 10, 236, 236);
            dc.DrawImage(cardCrop, drawRect);
            dc.Pop();
        }

        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }

    public static void SavePng(BitmapSource source, string outputPath)
    {
        string? dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(source));
        using var fs = File.Create(outputPath);
        enc.Save(fs);
    }
}
