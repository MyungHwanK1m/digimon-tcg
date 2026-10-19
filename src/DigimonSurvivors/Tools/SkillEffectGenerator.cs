using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DigimonSurvivors.Tools;

public static class SkillEffectGenerator
{
    public static void GenerateAll(string baseDir)
    {
        string skillsDir = Path.Combine(baseDir, "Assets", "Skills");
        string uiDir = Path.Combine(baseDir, "Assets", "UI");
        string envDir = Path.Combine(baseDir, "Assets", "Environment");

        Directory.CreateDirectory(skillsDir);
        Directory.CreateDirectory(uiDir);
        Directory.CreateDirectory(envDir);

        // 1. 스킬 투사체 및 이펙트 스프라이트 생성
        GenerateBabyFlame(Path.Combine(skillsDir, "baby_flame.png"));
        GenerateMegaFlame(Path.Combine(skillsDir, "mega_flame.png"));
        GeneratePetitFire(Path.Combine(skillsDir, "petit_fire.png"));
        GenerateFoxFire(Path.Combine(skillsDir, "fox_fire.png"));
        GenerateShadowWing(Path.Combine(skillsDir, "shadow_wing.png"));
        GenerateGigaDestroyer(Path.Combine(skillsDir, "giga_destroyer.png"));
        GenerateHammerSpark(Path.Combine(skillsDir, "hammer_spark.png"));
        GenerateSorrowBlue(Path.Combine(skillsDir, "sorrow_blue.png"));
        GenerateKaiserNail(Path.Combine(skillsDir, "kaiser_nail.png"));
        GenerateGaiaForce(Path.Combine(skillsDir, "gaia_force.png"));
        GenerateCocytusBreath(Path.Combine(skillsDir, "cocytus_breath.png"));
        GenerateGreySword(Path.Combine(skillsDir, "grey_sword.png"));

        // 스타터 및 신규 시그니처 / 서브 스킬
        GeneratePicoDart(Path.Combine(skillsDir, "pico_dart.png"));
        GenerateAirShot(Path.Combine(skillsDir, "air_shot.png"));
        GeneratePetitThunder(Path.Combine(skillsDir, "petit_thunder.png"));
        GenerateSurfingFish(Path.Combine(skillsDir, "surfing_fish.png"));
        GenerateGoburiStrike(Path.Combine(skillsDir, "goburi_strike.png"));
        GenerateMeteorWing(Path.Combine(skillsDir, "meteor_wing.png"));
        GenerateDeathClaw(Path.Combine(skillsDir, "death_claw.png"));
        GenerateHeavensKnuckle(Path.Combine(skillsDir, "heavens_knuckle.png"));
        GenerateMegaBlaster(Path.Combine(skillsDir, "mega_blaster.png"));
        GenerateSpitFire(Path.Combine(skillsDir, "spit_fire.png"));
        GenerateTridentArm(Path.Combine(skillsDir, "trident_arm.png"));
        GenerateHolyRod(Path.Combine(skillsDir, "holy_rod.png"));
        GenerateDramonKiller(Path.Combine(skillsDir, "dramon_killer.png"));
        GenerateDarkMatter(Path.Combine(skillsDir, "dark_matter.png"));
        GenerateShadowWingEx(Path.Combine(skillsDir, "shadow_wing_ex.png"));
        GenerateStarlightExplosion(Path.Combine(skillsDir, "starlight_explosion.png"));
        GenerateNightRaid(Path.Combine(skillsDir, "night_raid.png"));
        GenerateTrumpSword(Path.Combine(skillsDir, "trump_sword.png"));
        GenerateHeavenGate(Path.Combine(skillsDir, "heaven_gate.png"));
        GenerateStrikeOfTheSevenStars(Path.Combine(skillsDir, "strike_of_the_seven_stars.png"));
        GenerateHornBuster(Path.Combine(skillsDir, "horn_buster.png"));
        GenerateGigaBlaster(Path.Combine(skillsDir, "giga_blaster.png"));
        GenerateGroundZero(Path.Combine(skillsDir, "ground_zero.png"));
        GenerateHolyArrow(Path.Combine(skillsDir, "holy_arrow.png"));
        GenerateDarknessWave(Path.Combine(skillsDir, "darkness_wave.png"));
        GenerateFlowerCannon(Path.Combine(skillsDir, "flower_cannon.png"));
        GenerateInfinityCannon(Path.Combine(skillsDir, "infinity_cannon.png"));
        GenerateHaouken(Path.Combine(skillsDir, "haouken.png"));
        GenerateIceArrow(Path.Combine(skillsDir, "ice_arrow.png"));
        GenerateHyperCannon(Path.Combine(skillsDir, "hyper_cannon.png"));
        GenerateHarpoonTorpedo(Path.Combine(skillsDir, "harpoon_torpedo.png"));
        GenerateForbiddenTemptation(Path.Combine(skillsDir, "forbidden_temptation.png"));

        // 2. UI 속성 뱃지 및 프레임 생성
        GenerateAttributeBadge(Path.Combine(uiDir, "attr_vaccine.png"), "VAC", Color.FromRgb(40, 180, 255));
        GenerateAttributeBadge(Path.Combine(uiDir, "attr_data.png"), "DATA", Color.FromRgb(40, 220, 80));
        GenerateAttributeBadge(Path.Combine(uiDir, "attr_virus.png"), "VIRUS", Color.FromRgb(255, 60, 60));

        // 3. 환경 바닥 타일 생성
        GenerateMatrixTile(Path.Combine(envDir, "matrix_tile.png"));
    }

    private static void SaveVisualToPng(Visual visual, int width, int height, string filePath)
    {
        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        encoder.Save(fs);
    }

    // ==================== 스킬 이펙트 드로잉 ====================

    private static void GenerateBabyFlame(string path)
    {
        int size = 128;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);

            // 외곽 불꽃 후광 (주황)
            var outerGlow = new RadialGradientBrush(Color.FromArgb(180, 255, 60, 0), Color.FromArgb(0, 255, 40, 0));
            dc.DrawEllipse(outerGlow, null, center, 56, 56);

            // 본체 화염구 (밝은 주황/노랑)
            var bodyBrush = new RadialGradientBrush(Color.FromRgb(255, 230, 80), Color.FromRgb(255, 80, 0));
            dc.DrawEllipse(bodyBrush, null, center, 36, 36);

            // 초고열 핵 (백색광)
            var coreBrush = new RadialGradientBrush(Colors.White, Color.FromArgb(0, 255, 255, 200));
            dc.DrawEllipse(coreBrush, null, center, 18, 18);

            // 불꽃 꼬리 잔상 파티클
            var tailBrush = new SolidColorBrush(Color.FromArgb(200, 255, 120, 0));
            dc.DrawEllipse(tailBrush, null, new Point(center.X - 22, center.Y + 4), 10, 8);
            dc.DrawEllipse(tailBrush, null, new Point(center.X - 36, center.Y - 2), 6, 6);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateMegaFlame(string path)
    {
        int size = 160;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);

            // 거대 폭발 코로나 링
            var ringGlow = new RadialGradientBrush(Color.FromArgb(140, 255, 100, 0), Color.FromArgb(0, 255, 20, 0));
            dc.DrawEllipse(ringGlow, null, center, 75, 75);

            // 충격파 링
            var shockPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 255, 220, 100)), 3.0);
            dc.DrawEllipse(null, shockPen, center, 58, 58);

            // 메인 폭염체
            var mainBrush = new RadialGradientBrush(Color.FromRgb(255, 250, 160), Color.FromRgb(240, 50, 0));
            dc.DrawEllipse(mainBrush, null, center, 44, 44);

            // 초고열 화염 핵
            dc.DrawEllipse(Brushes.White, null, center, 22, 22);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GeneratePetitFire(string path)
    {
        int size = 128;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);

            // 푸른 도깨비불 신령 오라
            var aura = new RadialGradientBrush(Color.FromArgb(160, 0, 190, 255), Color.FromArgb(0, 0, 80, 255));
            dc.DrawEllipse(aura, null, center, 54, 54);

            // 청백색 불꽃체
            var flame = new RadialGradientBrush(Color.FromRgb(160, 240, 255), Color.FromRgb(0, 120, 255));
            dc.DrawEllipse(flame, null, center, 32, 32);

            // 빛나는 핵
            dc.DrawEllipse(Brushes.White, null, center, 14, 14);

            // 회전 꼬리 궤적
            var wispPen = new Pen(new SolidColorBrush(Color.FromArgb(180, 80, 220, 255)), 3.0);
            DrawArc(dc, wispPen, center, 42, 42, -40, 160);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateFoxFire(string path)
    {
        int size = 128;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);

            // 냉기 서리 후광
            var glow = new RadialGradientBrush(Color.FromArgb(150, 60, 210, 255), Color.FromArgb(0, 0, 140, 255));
            dc.DrawEllipse(glow, null, center, 50, 50);

            // 쐐기형 청백색 탄환
            var pathGeo = new PathGeometry();
            var fig = new PathFigure { StartPoint = new Point(center.X + 32, center.Y), IsClosed = true };
            fig.Segments.Add(new LineSegment(new Point(center.X - 24, center.Y - 18), true));
            fig.Segments.Add(new LineSegment(new Point(center.X - 14, center.Y), true));
            fig.Segments.Add(new LineSegment(new Point(center.X - 24, center.Y + 18), true));
            pathGeo.Figures.Add(fig);

            var frostBrush = new LinearGradientBrush(Color.FromRgb(220, 250, 255), Color.FromRgb(0, 140, 255), 0.0);
            dc.DrawGeometry(frostBrush, new Pen(Brushes.White, 1.5), pathGeo);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateShadowWing(string path)
    {
        int w = 160;
        int h = 90;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(w / 2.0, h / 2.0);

            // 진홍색 화염 날개 참격
            var wingGeo = new PathGeometry();
            var fig = new PathFigure { StartPoint = new Point(center.X - 70, center.Y), IsClosed = true };
            fig.Segments.Add(new BezierSegment(new Point(center.X - 20, center.Y - 38), new Point(center.X + 30, center.Y - 38), new Point(center.X + 70, center.Y), true));
            fig.Segments.Add(new BezierSegment(new Point(center.X + 20, center.Y - 14), new Point(center.X - 30, center.Y - 14), new Point(center.X - 70, center.Y), true));
            wingGeo.Figures.Add(fig);

            var wingBrush = new LinearGradientBrush(Color.FromRgb(255, 60, 60), Color.FromRgb(255, 180, 40), 0.0);
            dc.DrawGeometry(wingBrush, new Pen(new SolidColorBrush(Colors.White), 2.0), wingGeo);

            // 후광
            var glow = new RadialGradientBrush(Color.FromArgb(140, 255, 40, 40), Color.FromArgb(0, 255, 40, 40));
            dc.DrawEllipse(glow, null, center, 75, 40);
        }
        SaveVisualToPng(dv, w, h, path);
    }

    private static void GenerateGigaDestroyer(string path)
    {
        int size = 128;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);

            // 로켓 추진 화염 꼬리
            var thrust = new LinearGradientBrush(Color.FromRgb(255, 230, 80), Color.FromArgb(0, 255, 40, 0), 0.0);
            dc.DrawEllipse(thrust, null, new Point(center.X - 32, center.Y), 22, 10);

            // 생체 순항 미사일 본체 (강철 실버)
            var missileGeo = new PathGeometry();
            var fig = new PathFigure { StartPoint = new Point(center.X + 36, center.Y), IsClosed = true };
            fig.Segments.Add(new LineSegment(new Point(center.X + 16, center.Y - 12), true));
            fig.Segments.Add(new LineSegment(new Point(center.X - 24, center.Y - 12), true));
            fig.Segments.Add(new LineSegment(new Point(center.X - 24, center.Y + 12), true));
            fig.Segments.Add(new LineSegment(new Point(center.X + 16, center.Y + 12), true));
            missileGeo.Figures.Add(fig);

            var steelBrush = new LinearGradientBrush(Color.FromRgb(210, 220, 230), Color.FromRgb(100, 110, 125), 90.0);
            dc.DrawGeometry(steelBrush, new Pen(new SolidColorBrush(Color.FromRgb(40, 50, 60)), 1.5), missileGeo);

            // 전면 붉은 탄두 레이더 돔
            var warhead = new PathGeometry();
            var warFig = new PathFigure { StartPoint = new Point(center.X + 36, center.Y), IsClosed = true };
            warFig.Segments.Add(new LineSegment(new Point(center.X + 22, center.Y - 10), true));
            warFig.Segments.Add(new LineSegment(new Point(center.X + 22, center.Y + 10), true));
            warhead.Figures.Add(warFig);
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(240, 40, 40)), null, warhead);

            // 날개 핀 (상하)
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(60, 70, 80)), null, new Rect(center.X - 24, center.Y - 20, 8, 8));
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(60, 70, 80)), null, new Rect(center.X - 24, center.Y + 12, 8, 8));
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateHammerSpark(string path)
    {
        int size = 128;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);

            // 푸른 벼락 오라
            var glow = new RadialGradientBrush(Color.FromArgb(160, 40, 200, 255), Color.FromArgb(0, 0, 100, 255));
            dc.DrawEllipse(glow, null, center, 56, 56);

            // 번개 지그재그
            var boltGeo = new PathGeometry();
            var fig = new PathFigure { StartPoint = new Point(center.X - 10, center.Y - 50), IsClosed = false };
            fig.Segments.Add(new LineSegment(new Point(center.X + 8, center.Y - 15), true));
            fig.Segments.Add(new LineSegment(new Point(center.X - 14, center.Y + 5), true));
            fig.Segments.Add(new LineSegment(new Point(center.X + 16, center.Y + 50), true));
            boltGeo.Figures.Add(fig);

            dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromRgb(80, 220, 255)), 5.0), boltGeo);
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(Colors.White), 2.5), boltGeo);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateSorrowBlue(string path)
    {
        int size = 160;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);

            // 빙결 마법진 링
            var ringPen = new Pen(new SolidColorBrush(Color.FromArgb(180, 80, 200, 255)), 2.0);
            dc.DrawEllipse(null, ringPen, center, 65, 65);
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(120, 160, 240, 255)), 1.0), center, 52, 52);

            // 눈꽃 얼음 결정 십자선
            var frostPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 200, 250, 255)), 2.5);
            dc.DrawLine(frostPen, new Point(center.X - 60, center.Y), new Point(center.X + 60, center.Y));
            dc.DrawLine(frostPen, new Point(center.X, center.Y - 60), new Point(center.X, center.Y + 60));

            // 중심 냉기 안개
            var mist = new RadialGradientBrush(Color.FromArgb(140, 100, 220, 255), Color.FromArgb(0, 20, 100, 255));
            dc.DrawEllipse(mist, null, center, 50, 50);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateKaiserNail(string path)
    {
        int size = 160;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);

            // 3연격 발톱 참격선
            for (int i = -1; i <= 1; i++)
            {
                double offset = i * 16.0;
                var clawGeo = new PathGeometry();
                var fig = new PathFigure { StartPoint = new Point(center.X - 50 + offset, center.Y + 45), IsClosed = false };
                fig.Segments.Add(new BezierSegment(
                    new Point(center.X - 10 + offset, center.Y - 10),
                    new Point(center.X + 25 + offset, center.Y - 35),
                    new Point(center.X + 60 + offset, center.Y - 45), true));
                clawGeo.Figures.Add(fig);

                dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(140, 0, 180, 255)), 6.0), clawGeo);
                dc.DrawGeometry(null, new Pen(new SolidColorBrush(Colors.White), 2.0), clawGeo);
            }
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateGaiaForce(string path)
    {
        int size = 220;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);

            // 거대 미니어처 태양 에너지 링
            var corona = new RadialGradientBrush(Color.FromArgb(160, 255, 60, 0), Color.FromArgb(0, 255, 20, 0));
            dc.DrawEllipse(corona, null, center, 100, 100);

            // 압축 대기 고리
            var ringPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 200, 80)), 4.0);
            dc.DrawEllipse(null, ringPen, center, 78, 78);

            // 고밀도 플라즈마 구체
            var sunBody = new RadialGradientBrush(Color.FromRgb(255, 255, 200), Color.FromRgb(240, 60, 0));
            dc.DrawEllipse(sunBody, null, center, 65, 65);

            // 초핵심부 백색 백열광
            dc.DrawEllipse(Brushes.White, null, center, 35, 35);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateCocytusBreath(string path)
    {
        int w = 240;
        int h = 90;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(w / 2.0, h / 2.0);

            // 극저온 레이저 빔 오라
            var beamGlow = new LinearGradientBrush(Color.FromArgb(180, 40, 220, 255), Color.FromArgb(0, 0, 120, 255), 90.0);
            dc.DrawRectangle(beamGlow, null, new Rect(0, center.Y - 32, w, 64));

            // 중심 빔 코어
            var beamCore = new LinearGradientBrush(Colors.White, Color.FromRgb(160, 240, 255), 90.0);
            dc.DrawRectangle(beamCore, null, new Rect(0, center.Y - 12, w, 24));
        }
        SaveVisualToPng(dv, w, h, path);
    }

    private static void GenerateGreySword(string path)
    {
        int size = 180;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);

            // 성검 검기 파동 (황금 & 청백)
            var slashGeo = new PathGeometry();
            var fig = new PathFigure { StartPoint = new Point(center.X - 75, center.Y + 50), IsClosed = true };
            fig.Segments.Add(new BezierSegment(new Point(center.X - 10, center.Y - 50), new Point(center.X + 50, center.Y - 60), new Point(center.X + 80, center.Y - 40), true));
            fig.Segments.Add(new BezierSegment(new Point(center.X + 30, center.Y - 20), new Point(center.X - 20, center.Y - 10), new Point(center.X - 75, center.Y + 50), true));
            slashGeo.Figures.Add(fig);

            var holyBrush = new LinearGradientBrush(Color.FromRgb(255, 230, 80), Color.FromRgb(0, 220, 255), 45.0);
            dc.DrawGeometry(holyBrush, new Pen(Brushes.White, 3.0), slashGeo);

            // 성스러운 후광
            var glow = new RadialGradientBrush(Color.FromArgb(140, 0, 200, 255), Color.FromArgb(0, 0, 100, 255));
            dc.DrawEllipse(glow, null, center, 80, 80);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GeneratePicoDart(string path)
    {
        int size = 128;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var glow = new RadialGradientBrush(Color.FromArgb(170, 150, 0, 255), Color.FromArgb(0, 50, 0, 120));
            dc.DrawEllipse(glow, null, center, 50, 50);

            var geo = new PathGeometry();
            var fig = new PathFigure { StartPoint = new Point(center.X + 35, center.Y), IsClosed = true };
            fig.Segments.Add(new LineSegment(new Point(center.X - 25, center.Y - 14), true));
            fig.Segments.Add(new LineSegment(new Point(center.X - 15, center.Y), true));
            fig.Segments.Add(new LineSegment(new Point(center.X - 25, center.Y + 14), true));
            geo.Figures.Add(fig);

            var brush = new LinearGradientBrush(Color.FromRgb(255, 60, 220), Color.FromRgb(80, 0, 160), 0.0);
            dc.DrawGeometry(brush, new Pen(new SolidColorBrush(Color.FromRgb(220, 180, 255)), 1.5), geo);

            dc.DrawEllipse(Brushes.White, null, new Point(center.X + 15, center.Y), 5, 5);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(200, 255, 0, 150)), null, new Point(center.X - 35, center.Y - 4), 4, 4);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(200, 160, 0, 255)), null, new Point(center.X - 42, center.Y + 5), 3, 3);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateAirShot(string path)
    {
        int size = 128;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var aura = new RadialGradientBrush(Color.FromArgb(140, 200, 240, 255), Color.FromArgb(0, 255, 255, 255));
            dc.DrawEllipse(aura, null, center, 52, 52);

            var ringPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), 2.5);
            dc.DrawEllipse(null, ringPen, center, 36, 36);

            var ringPen2 = new Pen(new SolidColorBrush(Color.FromArgb(160, 140, 220, 255)), 1.8);
            dc.DrawEllipse(null, ringPen2, center, 24, 24);

            var coreBrush = new RadialGradientBrush(Colors.White, Color.FromArgb(60, 180, 230, 255));
            dc.DrawEllipse(coreBrush, null, center, 14, 14);

            var swirlPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 255, 255, 200)), 2.0);
            DrawArc(dc, swirlPen, center, 44, 44, 10, 100);
            DrawArc(dc, swirlPen, center, 44, 44, 190, 100);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GeneratePetitThunder(string path)
    {
        int size = 128;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var aura = new RadialGradientBrush(Color.FromArgb(180, 80, 255, 60), Color.FromArgb(0, 10, 160, 30));
            dc.DrawEllipse(aura, null, center, 54, 54);

            var boltGeo = new PathGeometry();
            var fig = new PathFigure { StartPoint = new Point(center.X - 35, center.Y - 30), IsClosed = false };
            fig.Segments.Add(new LineSegment(new Point(center.X - 5, center.Y - 5), true));
            fig.Segments.Add(new LineSegment(new Point(center.X - 15, center.Y + 2), true));
            fig.Segments.Add(new LineSegment(new Point(center.X + 15, center.Y - 8), true));
            fig.Segments.Add(new LineSegment(new Point(center.X + 5, center.Y + 10), true));
            fig.Segments.Add(new LineSegment(new Point(center.X + 38, center.Y + 32), true));
            boltGeo.Figures.Add(fig);

            var boltPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 255, 120)), 3.5);
            dc.DrawGeometry(null, boltPen, boltGeo);
            var innerBoltPen = new Pen(Brushes.White, 1.5);
            dc.DrawGeometry(null, innerBoltPen, boltGeo);

            dc.DrawEllipse(Brushes.White, null, center, 14, 14);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(120, 150, 255, 100)), null, center, 24, 24);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateSurfingFish(string path)
    {
        int size = 128;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var aura = new RadialGradientBrush(Color.FromArgb(160, 0, 220, 255), Color.FromArgb(0, 0, 70, 180));
            dc.DrawEllipse(aura, null, center, 52, 52);

            var geo = new PathGeometry();
            var fig = new PathFigure { StartPoint = new Point(center.X + 36, center.Y), IsClosed = true };
            fig.Segments.Add(new BezierSegment(new Point(center.X + 15, center.Y - 20), new Point(center.X - 15, center.Y - 18), new Point(center.X - 25, center.Y), true));
            fig.Segments.Add(new LineSegment(new Point(center.X - 40, center.Y - 16), true));
            fig.Segments.Add(new LineSegment(new Point(center.X - 32, center.Y), true));
            fig.Segments.Add(new LineSegment(new Point(center.X - 40, center.Y + 16), true));
            fig.Segments.Add(new LineSegment(new Point(center.X - 25, center.Y), true));
            fig.Segments.Add(new BezierSegment(new Point(center.X - 15, center.Y + 18), new Point(center.X + 15, center.Y + 20), new Point(center.X + 36, center.Y), true));
            geo.Figures.Add(fig);

            var fishBrush = new LinearGradientBrush(Color.FromRgb(180, 245, 255), Color.FromRgb(0, 140, 255), 0.0);
            dc.DrawGeometry(fishBrush, new Pen(Brushes.White, 2.0), geo);

            dc.DrawEllipse(Brushes.White, null, new Point(center.X - 10, center.Y - 2), 4, 4);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(200, 120, 230, 255)), null, new Point(center.X - 46, center.Y - 6), 5, 5);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(200, 160, 240, 255)), null, new Point(center.X - 50, center.Y + 8), 4, 4);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateGoburiStrike(string path)
    {
        int size = 128;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var aura = new RadialGradientBrush(Color.FromArgb(180, 255, 140, 20), Color.FromArgb(0, 120, 50, 0));
            dc.DrawEllipse(aura, null, center, 55, 55);

            var geo = new PathGeometry();
            var fig = new PathFigure { StartPoint = new Point(center.X + 35, center.Y), IsClosed = true };
            fig.Segments.Add(new LineSegment(new Point(center.X + 15, center.Y - 24), true));
            fig.Segments.Add(new LineSegment(new Point(center.X - 10, center.Y - 28), true));
            fig.Segments.Add(new LineSegment(new Point(center.X - 35, center.Y - 10), true));
            fig.Segments.Add(new LineSegment(new Point(center.X - 30, center.Y + 14), true));
            fig.Segments.Add(new LineSegment(new Point(center.X, center.Y + 28), true));
            fig.Segments.Add(new LineSegment(new Point(center.X + 26, center.Y + 16), true));
            geo.Figures.Add(fig);

            var rockBrush = new LinearGradientBrush(Color.FromRgb(255, 170, 60), Color.FromRgb(90, 50, 20), 45.0);
            dc.DrawGeometry(rockBrush, new Pen(new SolidColorBrush(Color.FromRgb(255, 230, 140)), 2.0), geo);

            var crackPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 240, 100)), 2.0);
            dc.DrawLine(crackPen, new Point(center.X - 15, center.Y - 10), new Point(center.X + 15, center.Y + 5));
            dc.DrawLine(crackPen, new Point(center.X, center.Y), new Point(center.X + 5, center.Y - 18));
            dc.DrawEllipse(Brushes.White, null, center, 10, 10);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateMeteorWing(string path)
    {
        int size = 140;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var glow = new RadialGradientBrush(Color.FromArgb(180, 255, 80, 0), Color.FromArgb(0, 200, 30, 0));
            dc.DrawEllipse(glow, null, center, 60, 60);

            var geo = new PathGeometry();
            var fig = new PathFigure { StartPoint = new Point(center.X + 50, center.Y - 20), IsClosed = true };
            fig.Segments.Add(new BezierSegment(new Point(center.X + 10, center.Y - 45), new Point(center.X - 35, center.Y - 25), new Point(center.X - 50, center.Y + 25), true));
            fig.Segments.Add(new BezierSegment(new Point(center.X - 25, center.Y + 10), new Point(center.X + 15, center.Y - 5), new Point(center.X + 50, center.Y - 20), true));
            geo.Figures.Add(fig);

            var wingBrush = new LinearGradientBrush(Color.FromRgb(255, 240, 120), Color.FromRgb(220, 40, 0), 30.0);
            dc.DrawGeometry(wingBrush, new Pen(Brushes.White, 2.5), geo);

            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(220, 255, 180, 0)), null, new Point(center.X - 30, center.Y - 8), 12, 10);
            dc.DrawEllipse(Brushes.White, null, new Point(center.X + 15, center.Y - 14), 8, 8);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateDeathClaw(string path)
    {
        int size = 140;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var glow = new RadialGradientBrush(Color.FromArgb(180, 160, 0, 220), Color.FromArgb(0, 60, 0, 100));
            dc.DrawEllipse(glow, null, center, 58, 58);

            for (int k = -1; k <= 1; k++)
            {
                var clawGeo = new PathGeometry();
                double offsetY = k * 18.0;
                var fig = new PathFigure { StartPoint = new Point(center.X + 45, center.Y + offsetY), IsClosed = true };
                fig.Segments.Add(new BezierSegment(new Point(center.X + 10, center.Y - 12 + offsetY), new Point(center.X - 25, center.Y - 6 + offsetY), new Point(center.X - 45, center.Y + offsetY), true));
                fig.Segments.Add(new BezierSegment(new Point(center.X - 15, center.Y + 2 + offsetY), new Point(center.X + 15, center.Y + 4 + offsetY), new Point(center.X + 45, center.Y + offsetY), true));
                clawGeo.Figures.Add(fig);

                var clawBrush = new LinearGradientBrush(Color.FromRgb(255, 50, 120), Color.FromRgb(40, 0, 60), 0.0);
                dc.DrawGeometry(clawBrush, new Pen(new SolidColorBrush(Color.FromRgb(255, 180, 220)), 1.5), clawGeo);
            }

            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(200, 255, 80, 80)), null, new Point(center.X + 35, center.Y), 10, 10);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateHeavensKnuckle(string path)
    {
        int size = 160;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var glow = new RadialGradientBrush(Color.FromArgb(200, 255, 230, 80), Color.FromArgb(0, 255, 160, 0));
            dc.DrawEllipse(glow, null, center, 72, 72);

            var rayPen = new Pen(new SolidColorBrush(Color.FromArgb(220, 255, 255, 180)), 4.0);
            dc.DrawLine(rayPen, new Point(center.X - 60, center.Y), new Point(center.X + 60, center.Y));
            dc.DrawLine(rayPen, new Point(center.X, center.Y - 60), new Point(center.X, center.Y + 60));

            var fistBrush = new RadialGradientBrush(Colors.White, Color.FromRgb(255, 200, 20));
            dc.DrawEllipse(fistBrush, new Pen(Brushes.White, 3.0), center, 38, 38);

            var subGlow = new SolidColorBrush(Color.FromArgb(220, 255, 255, 120));
            dc.DrawEllipse(subGlow, null, new Point(center.X + 32, center.Y - 25), 8, 8);
            dc.DrawEllipse(subGlow, null, new Point(center.X - 30, center.Y + 28), 7, 7);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateMegaBlaster(string path)
    {
        int size = 160;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var glow = new RadialGradientBrush(Color.FromArgb(190, 40, 255, 140), Color.FromArgb(0, 0, 160, 80));
            dc.DrawEllipse(glow, null, center, 70, 70);

            var plasmaBrush = new RadialGradientBrush(Colors.White, Color.FromRgb(0, 230, 160));
            dc.DrawEllipse(plasmaBrush, new Pen(new SolidColorBrush(Color.FromRgb(180, 255, 220)), 3.0), center, 42, 42);

            var arcPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 255, 120)), 2.5);
            DrawArc(dc, arcPen, center, 56, 56, 30, 120);
            DrawArc(dc, arcPen, center, 56, 56, 210, 120);

            var boltPen = new Pen(Brushes.White, 2.0);
            dc.DrawLine(boltPen, new Point(center.X - 25, center.Y - 15), new Point(center.X + 25, center.Y + 15));
            dc.DrawLine(boltPen, new Point(center.X - 15, center.Y + 25), new Point(center.X + 15, center.Y - 25));
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateSpitFire(string path)
    {
        int size = 128;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var glow = new RadialGradientBrush(Color.FromArgb(170, 255, 100, 0), Color.FromArgb(0, 255, 20, 0));
            dc.DrawEllipse(glow, null, center, 48, 48);

            var geo = new PathGeometry();
            var fig = new PathFigure { StartPoint = new Point(center.X + 32, center.Y), IsClosed = true };
            fig.Segments.Add(new BezierSegment(new Point(center.X + 10, center.Y - 16), new Point(center.X - 15, center.Y - 14), new Point(center.X - 30, center.Y), true));
            fig.Segments.Add(new BezierSegment(new Point(center.X - 15, center.Y + 14), new Point(center.X + 10, center.Y + 16), new Point(center.X + 32, center.Y), true));
            geo.Figures.Add(fig);

            var flameBrush = new LinearGradientBrush(Color.FromRgb(255, 240, 140), Color.FromRgb(240, 50, 0), 0.0);
            dc.DrawGeometry(flameBrush, new Pen(Brushes.White, 2.0), geo);

            dc.DrawEllipse(Brushes.White, null, new Point(center.X + 12, center.Y), 8, 8);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateTridentArm(string path)
    {
        int size = 140;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var glow = new RadialGradientBrush(Color.FromArgb(150, 255, 50, 50), Color.FromArgb(0, 100, 20, 20));
            dc.DrawEllipse(glow, null, center, 56, 56);

            var steelBrush = new LinearGradientBrush(Color.FromRgb(240, 245, 255), Color.FromRgb(100, 120, 140), 45.0);
            var prongPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 60, 60)), 2.5);

            dc.DrawRectangle(steelBrush, prongPen, new Rect(center.X - 20, center.Y - 6, 60, 12));
            dc.DrawRectangle(steelBrush, prongPen, new Rect(center.X - 15, center.Y - 24, 45, 8));
            dc.DrawRectangle(steelBrush, prongPen, new Rect(center.X - 15, center.Y + 16, 45, 8));

            var wirePen = new Pen(new SolidColorBrush(Color.FromRgb(80, 200, 255)), 2.0);
            dc.DrawLine(wirePen, new Point(center.X - 45, center.Y), new Point(center.X - 20, center.Y));
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateHolyRod(string path)
    {
        int size = 140;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var glow = new RadialGradientBrush(Color.FromArgb(180, 255, 220, 50), Color.FromArgb(0, 200, 160, 0));
            dc.DrawEllipse(glow, null, center, 62, 62);

            var rodBrush = new LinearGradientBrush(Color.FromRgb(255, 245, 180), Color.FromRgb(220, 170, 30), 45.0);
            var pen = new Pen(Brushes.White, 2.0);

            dc.DrawRoundedRectangle(rodBrush, pen, new Rect(center.X - 8, center.Y - 48, 16, 96), 4, 4);
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromRgb(255, 230, 80)), 4.0), new Point(center.X, center.Y - 32), 18, 18);
            dc.DrawEllipse(Brushes.White, null, new Point(center.X, center.Y - 32), 6, 6);

            var haloPen = new Pen(new SolidColorBrush(Color.FromArgb(160, 255, 255, 180)), 2.0);
            dc.DrawEllipse(null, haloPen, center, 44, 44);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateDramonKiller(string path)
    {
        int size = 160;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var glow = new RadialGradientBrush(Color.FromArgb(190, 255, 60, 0), Color.FromArgb(0, 200, 0, 0));
            dc.DrawEllipse(glow, null, center, 72, 72);

            var clawBrush = new LinearGradientBrush(Color.FromRgb(255, 240, 100), Color.FromRgb(255, 80, 0), 45.0);
            var pen = new Pen(Brushes.White, 3.0);

            var slash1 = new PathGeometry();
            var f1 = new PathFigure { StartPoint = new Point(center.X - 55, center.Y - 45), IsClosed = true };
            f1.Segments.Add(new BezierSegment(new Point(center.X - 10, center.Y - 5), new Point(center.X + 10, center.Y + 5), new Point(center.X + 55, center.Y + 45), true));
            f1.Segments.Add(new BezierSegment(new Point(center.X + 25, center.Y + 15), new Point(center.X - 25, center.Y - 15), new Point(center.X - 55, center.Y - 45), true));
            slash1.Figures.Add(f1);
            dc.DrawGeometry(clawBrush, pen, slash1);

            var slash2 = new PathGeometry();
            var f2 = new PathFigure { StartPoint = new Point(center.X - 55, center.Y + 45), IsClosed = true };
            f2.Segments.Add(new BezierSegment(new Point(center.X - 10, center.Y + 5), new Point(center.X + 10, center.Y - 5), new Point(center.X + 55, center.Y - 45), true));
            f2.Segments.Add(new BezierSegment(new Point(center.X + 25, center.Y - 15), new Point(center.X - 25, center.Y + 15), new Point(center.X - 55, center.Y + 45), true));
            slash2.Figures.Add(f2);
            dc.DrawGeometry(clawBrush, pen, slash2);

            dc.DrawEllipse(Brushes.White, null, center, 14, 14);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateDarkMatter(string path)
    {
        int size = 160;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var aura = new RadialGradientBrush(Color.FromArgb(200, 140, 0, 255), Color.FromArgb(0, 40, 0, 80));
            dc.DrawEllipse(aura, null, center, 74, 74);

            var spiralPen = new Pen(new SolidColorBrush(Color.FromArgb(200, 220, 100, 255)), 2.5);
            DrawArc(dc, spiralPen, center, 56, 56, 0, 140);
            DrawArc(dc, spiralPen, center, 44, 44, 120, 140);
            DrawArc(dc, spiralPen, center, 32, 32, 240, 140);

            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(15, 5, 25)), new Pen(new SolidColorBrush(Color.FromRgb(255, 60, 220)), 3.0), center, 22, 22);
            dc.DrawEllipse(Brushes.Black, null, center, 16, 16);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateShadowWingEx(string path)
    {
        int size = 160;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var glow = new RadialGradientBrush(Color.FromArgb(200, 255, 120, 0), Color.FromArgb(0, 220, 20, 0));
            dc.DrawEllipse(glow, null, center, 75, 75);

            var birdGeo = new PathGeometry();
            var fig = new PathFigure { StartPoint = new Point(center.X + 55, center.Y), IsClosed = true };
            fig.Segments.Add(new BezierSegment(new Point(center.X + 25, center.Y - 50), new Point(center.X - 25, center.Y - 45), new Point(center.X - 50, center.Y - 15), true));
            fig.Segments.Add(new LineSegment(new Point(center.X - 30, center.Y), true));
            fig.Segments.Add(new BezierSegment(new Point(center.X - 50, center.Y + 15), new Point(center.X - 25, center.Y + 45), new Point(center.X + 25, center.Y + 50), true));
            birdGeo.Figures.Add(fig);

            var fireBrush = new LinearGradientBrush(Color.FromRgb(255, 250, 160), Color.FromRgb(240, 40, 0), 0.0);
            dc.DrawGeometry(fireBrush, new Pen(Brushes.White, 2.5), birdGeo);

            dc.DrawEllipse(Brushes.White, null, new Point(center.X + 20, center.Y), 16, 16);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateStarlightExplosion(string path)
    {
        int size = 180;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var glow = new RadialGradientBrush(Color.FromArgb(190, 255, 240, 180), Color.FromArgb(0, 255, 100, 150));
            dc.DrawEllipse(glow, null, center, 84, 84);

            var rayPen = new Pen(new SolidColorBrush(Color.FromArgb(230, 255, 255, 200)), 3.5);
            for (int a = 0; a < 8; a++)
            {
                double rad = a * Math.PI / 4.0;
                dc.DrawLine(rayPen, center, new Point(center.X + Math.Cos(rad) * 72, center.Y + Math.Sin(rad) * 72));
            }

            var novaBrush = new RadialGradientBrush(Colors.White, Color.FromRgb(255, 215, 60));
            dc.DrawEllipse(novaBrush, new Pen(Brushes.White, 3.0), center, 32, 32);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateNightRaid(string path)
    {
        int size = 140;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var glow = new RadialGradientBrush(Color.FromArgb(170, 180, 0, 50), Color.FromArgb(0, 50, 0, 20));
            dc.DrawEllipse(glow, null, center, 60, 60);

            Point[] batCenters = { new(center.X + 15, center.Y), new(center.X - 20, center.Y - 18), new(center.X - 20, center.Y + 18) };
            foreach (var bc in batCenters)
            {
                var batGeo = new PathGeometry();
                var fig = new PathFigure { StartPoint = new Point(bc.X, bc.Y - 4), IsClosed = true };
                fig.Segments.Add(new BezierSegment(new Point(bc.X - 10, bc.Y - 16), new Point(bc.X - 22, bc.Y - 8), new Point(bc.X - 24, bc.Y), true));
                fig.Segments.Add(new LineSegment(new Point(bc.X - 12, bc.Y + 4), true));
                fig.Segments.Add(new LineSegment(new Point(bc.X, bc.Y + 6), true));
                fig.Segments.Add(new LineSegment(new Point(bc.X + 12, bc.Y + 4), true));
                fig.Segments.Add(new LineSegment(new Point(bc.X + 24, bc.Y), true));
                fig.Segments.Add(new BezierSegment(new Point(bc.X + 22, bc.Y - 8), new Point(bc.X + 10, bc.Y - 16), new Point(bc.X, bc.Y - 4), true));
                batGeo.Figures.Add(fig);

                dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(30, 10, 40)), new Pen(new SolidColorBrush(Color.FromRgb(255, 30, 80)), 1.5), batGeo);
                dc.DrawEllipse(Brushes.Red, null, new Point(bc.X - 3, bc.Y), 2, 2);
                dc.DrawEllipse(Brushes.Red, null, new Point(bc.X + 3, bc.Y), 2, 2);
            }
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateTrumpSword(string path)
    {
        int size = 140;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var glow = new RadialGradientBrush(Color.FromArgb(170, 180, 50, 255), Color.FromArgb(0, 70, 0, 140));
            dc.DrawEllipse(glow, null, center, 62, 62);

            for (int i = 0; i < 4; i++)
            {
                double angle = i * 90.0 * Math.PI / 180.0;
                double cos = Math.Cos(angle);
                double sin = Math.Sin(angle);

                Point tip = new(center.X + cos * 52, center.Y + sin * 52);
                Point basePt = new(center.X + cos * 14, center.Y + sin * 14);

                var bladePen = new Pen(Brushes.White, 2.5);
                dc.DrawLine(bladePen, basePt, tip);

                Point guardL = new(basePt.X - sin * 8, basePt.Y + cos * 8);
                Point guardR = new(basePt.X + sin * 8, basePt.Y - cos * 8);
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(255, 215, 0)), 2.0), guardL, guardR);
            }

            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 50, 150)), null, center, 8, 8);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateHeavenGate(string path)
    {
        int size = 180;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var glow = new RadialGradientBrush(Color.FromArgb(200, 255, 240, 100), Color.FromArgb(0, 200, 140, 0));
            dc.DrawEllipse(glow, null, center, 82, 82);

            var gatePen = new Pen(new SolidColorBrush(Color.FromRgb(255, 220, 80)), 4.0);
            dc.DrawRoundedRectangle(null, gatePen, new Rect(center.X - 35, center.Y - 55, 70, 110), 35, 35);

            var portalBrush = new RadialGradientBrush(Colors.White, Color.FromRgb(40, 140, 255));
            dc.DrawEllipse(portalBrush, null, center, 28, 48);

            var crossPen = new Pen(Brushes.White, 3.0);
            dc.DrawLine(crossPen, new Point(center.X - 45, center.Y), new Point(center.X + 45, center.Y));
            dc.DrawLine(crossPen, new Point(center.X, center.Y - 45), new Point(center.X, center.Y + 45));
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateStrikeOfTheSevenStars(string path)
    {
        int size = 180;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var glow = new RadialGradientBrush(Color.FromArgb(210, 255, 240, 120), Color.FromArgb(0, 255, 120, 0));
            dc.DrawEllipse(glow, null, center, 85, 85);

            var sunBrush = new RadialGradientBrush(Colors.White, Color.FromRgb(255, 210, 30));
            dc.DrawEllipse(sunBrush, new Pen(Brushes.White, 3.0), center, 32, 32);

            for (int i = 0; i < 7; i++)
            {
                double angle = (i * 2.0 * Math.PI / 7.0) - Math.PI / 2.0;
                Point orbPt = new(center.X + Math.Cos(angle) * 58, center.Y + Math.Sin(angle) * 58);
                dc.DrawEllipse(Brushes.White, new Pen(new SolidColorBrush(Color.FromRgb(255, 220, 50)), 2.0), orbPt, 9, 9);
            }
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateHornBuster(string path)
    {
        int size = 160;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var glow = new RadialGradientBrush(Color.FromArgb(190, 80, 230, 255), Color.FromArgb(0, 20, 100, 200));
            dc.DrawEllipse(glow, null, center, 74, 74);

            var beamBrush = new LinearGradientBrush(Colors.White, Color.FromRgb(0, 180, 255), 0.0);
            dc.DrawRectangle(beamBrush, new Pen(Brushes.White, 2.5), new Rect(center.X - 60, center.Y - 18, 120, 36));

            var pen = new Pen(new SolidColorBrush(Color.FromRgb(140, 255, 220)), 2.5);
            DrawArc(dc, pen, center, 48, 48, 45, 90);
            DrawArc(dc, pen, center, 48, 48, 225, 90);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateGigaBlaster(string path)
    {
        int size = 180;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var glow = new RadialGradientBrush(Color.FromArgb(200, 255, 230, 60), Color.FromArgb(0, 0, 180, 100));
            dc.DrawEllipse(glow, null, center, 85, 85);

            dc.DrawEllipse(new RadialGradientBrush(Colors.White, Color.FromRgb(100, 255, 160)), new Pen(Brushes.White, 3.5), center, 48, 48);

            var thunderPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 255, 120)), 3.0);
            DrawArc(dc, thunderPen, center, 65, 65, -30, 100);
            DrawArc(dc, thunderPen, center, 65, 65, 150, 100);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateGroundZero(string path)
    {
        int size = 160;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var glow = new RadialGradientBrush(Color.FromArgb(190, 80, 255, 40), Color.FromArgb(0, 20, 80, 10));
            dc.DrawEllipse(glow, null, center, 72, 72);

            var missileBrush = new LinearGradientBrush(Color.FromRgb(240, 245, 230), Color.FromRgb(60, 80, 50), 0.0);
            dc.DrawRoundedRectangle(missileBrush, new Pen(new SolidColorBrush(Color.FromRgb(120, 255, 60)), 2.5), new Rect(center.X - 45, center.Y - 18, 90, 36), 18, 18);

            dc.DrawEllipse(Brushes.Black, null, new Point(center.X + 15, center.Y - 6), 5, 5);
            dc.DrawEllipse(Brushes.Black, null, new Point(center.X + 15, center.Y + 6), 5, 5);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateHolyArrow(string path)
    {
        int size = 140;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var glow = new RadialGradientBrush(Color.FromArgb(180, 255, 160, 220), Color.FromArgb(0, 200, 80, 150));
            dc.DrawEllipse(glow, null, center, 62, 62);

            var arrowGeo = new PathGeometry();
            var fig = new PathFigure { StartPoint = new Point(center.X + 48, center.Y), IsClosed = true };
            fig.Segments.Add(new LineSegment(new Point(center.X + 15, center.Y - 14), true));
            fig.Segments.Add(new LineSegment(new Point(center.X + 20, center.Y - 5), true));
            fig.Segments.Add(new LineSegment(new Point(center.X - 45, center.Y - 4), true));
            fig.Segments.Add(new LineSegment(new Point(center.X - 35, center.Y - 15), true));
            fig.Segments.Add(new LineSegment(new Point(center.X - 48, center.Y), true));
            fig.Segments.Add(new LineSegment(new Point(center.X - 35, center.Y + 15), true));
            fig.Segments.Add(new LineSegment(new Point(center.X - 45, center.Y + 4), true));
            fig.Segments.Add(new LineSegment(new Point(center.X + 20, center.Y + 5), true));
            fig.Segments.Add(new LineSegment(new Point(center.X + 15, center.Y + 14), true));
            arrowGeo.Figures.Add(fig);

            var arrowBrush = new LinearGradientBrush(Color.FromRgb(255, 250, 200), Color.FromRgb(255, 100, 180), 0.0);
            dc.DrawGeometry(arrowBrush, new Pen(Brushes.White, 2.0), arrowGeo);
            dc.DrawEllipse(Brushes.White, null, new Point(center.X + 25, center.Y), 6, 6);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateDarknessWave(string path)
    {
        int size = 160;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var glow = new RadialGradientBrush(Color.FromArgb(190, 160, 0, 180), Color.FromArgb(0, 40, 0, 50));
            dc.DrawEllipse(glow, null, center, 74, 74);

            var geo = new PathGeometry();
            var fig = new PathFigure { StartPoint = new Point(center.X + 50, center.Y), IsClosed = true };
            fig.Segments.Add(new BezierSegment(new Point(center.X + 35, center.Y - 45), new Point(center.X, center.Y - 55), new Point(center.X - 45, center.Y - 40), true));
            fig.Segments.Add(new BezierSegment(new Point(center.X - 15, center.Y - 25), new Point(center.X + 15, center.Y), new Point(center.X - 45, center.Y + 40), true));
            fig.Segments.Add(new BezierSegment(new Point(center.X, center.Y + 55), new Point(center.X + 35, center.Y + 45), new Point(center.X + 50, center.Y), true));
            geo.Figures.Add(fig);

            var darkBrush = new LinearGradientBrush(Color.FromRgb(255, 50, 150), Color.FromRgb(20, 5, 30), 0.0);
            dc.DrawGeometry(darkBrush, new Pen(new SolidColorBrush(Color.FromRgb(220, 120, 255)), 2.5), geo);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateFlowerCannon(string path)
    {
        int size = 140;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var glow = new RadialGradientBrush(Color.FromArgb(180, 255, 140, 200), Color.FromArgb(0, 60, 200, 100));
            dc.DrawEllipse(glow, null, center, 62, 62);

            for (int p = 0; p < 5; p++)
            {
                double angle = p * 72.0 * Math.PI / 180.0;
                Point pt = new(center.X + Math.Cos(angle) * 26, center.Y + Math.Sin(angle) * 26);
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(220, 255, 120, 180)), new Pen(Brushes.White, 1.5), pt, 16, 12);
            }

            dc.DrawEllipse(new RadialGradientBrush(Colors.White, Color.FromRgb(255, 230, 80)), null, center, 14, 14);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateInfinityCannon(string path)
    {
        int size = 180;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var glow = new RadialGradientBrush(Color.FromArgb(200, 255, 80, 0), Color.FromArgb(0, 120, 20, 0));
            dc.DrawEllipse(glow, null, center, 84, 84);

            var beamBrush = new LinearGradientBrush(Colors.White, Color.FromRgb(255, 60, 0), 0.0);
            dc.DrawRectangle(beamBrush, new Pen(Brushes.White, 2.5), new Rect(center.X - 70, center.Y - 26, 140, 20));
            dc.DrawRectangle(beamBrush, new Pen(Brushes.White, 2.5), new Rect(center.X - 70, center.Y + 6, 140, 20));

            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromRgb(255, 220, 80)), 3.0), new Point(center.X + 25, center.Y), 38, 38);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateHaouken(string path)
    {
        int size = 140;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var glow = new RadialGradientBrush(Color.FromArgb(190, 255, 30, 30), Color.FromArgb(0, 120, 0, 0));
            dc.DrawEllipse(glow, null, center, 62, 62);

            var fistBrush = new RadialGradientBrush(Colors.White, Color.FromRgb(220, 20, 20));
            dc.DrawEllipse(fistBrush, new Pen(new SolidColorBrush(Color.FromRgb(255, 160, 50)), 3.0), center, 36, 36);

            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(160, 255, 80, 0)), null, new Point(center.X - 25, center.Y), 16, 16);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(100, 255, 0, 0)), null, new Point(center.X - 42, center.Y), 10, 10);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateIceArrow(string path)
    {
        int size = 140;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var glow = new RadialGradientBrush(Color.FromArgb(180, 0, 200, 255), Color.FromArgb(0, 0, 60, 180));
            dc.DrawEllipse(glow, null, center, 60, 60);

            var geo = new PathGeometry();
            var fig = new PathFigure { StartPoint = new Point(center.X + 48, center.Y), IsClosed = true };
            fig.Segments.Add(new LineSegment(new Point(center.X - 35, center.Y - 14), true));
            fig.Segments.Add(new LineSegment(new Point(center.X - 20, center.Y), true));
            fig.Segments.Add(new LineSegment(new Point(center.X - 35, center.Y + 14), true));
            geo.Figures.Add(fig);

            var iceBrush = new LinearGradientBrush(Color.FromRgb(230, 250, 255), Color.FromRgb(40, 140, 240), 0.0);
            dc.DrawGeometry(iceBrush, new Pen(Brushes.White, 2.0), geo);
            dc.DrawEllipse(Brushes.White, null, new Point(center.X + 15, center.Y), 7, 7);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateHyperCannon(string path)
    {
        int size = 140;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var glow = new RadialGradientBrush(Color.FromArgb(190, 255, 140, 20), Color.FromArgb(0, 160, 40, 0));
            dc.DrawEllipse(glow, null, center, 60, 60);

            var shellBrush = new LinearGradientBrush(Color.FromRgb(240, 245, 255), Color.FromRgb(80, 90, 100), 0.0);
            dc.DrawRoundedRectangle(shellBrush, new Pen(new SolidColorBrush(Color.FromRgb(255, 180, 40)), 2.5), new Rect(center.X - 36, center.Y - 16, 72, 32), 12, 12);

            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(220, 255, 80, 0)), null, new Point(center.X - 35, center.Y), 14, 14);
            dc.DrawEllipse(Brushes.White, null, new Point(center.X + 15, center.Y), 8, 8);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateHarpoonTorpedo(string path)
    {
        int size = 140;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var glow = new RadialGradientBrush(Color.FromArgb(170, 0, 180, 255), Color.FromArgb(0, 0, 60, 160));
            dc.DrawEllipse(glow, null, center, 60, 60);

            var torpedoBrush = new LinearGradientBrush(Color.FromRgb(240, 60, 60), Color.FromRgb(180, 200, 220), 0.0);
            dc.DrawRoundedRectangle(torpedoBrush, new Pen(Brushes.White, 2.0), new Rect(center.X - 40, center.Y - 14, 80, 28), 10, 10);

            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(200, 180, 240, 255)), null, new Point(center.X - 48, center.Y - 4), 6, 6);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(200, 140, 220, 255)), null, new Point(center.X - 54, center.Y + 5), 5, 5);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateForbiddenTemptation(string path)
    {
        int size = 160;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            var glow = new RadialGradientBrush(Color.FromArgb(200, 220, 0, 120), Color.FromArgb(0, 60, 0, 60));
            dc.DrawEllipse(glow, null, center, 74, 74);

            var roseBrush = new RadialGradientBrush(Color.FromRgb(255, 30, 100), Color.FromRgb(30, 0, 30));
            dc.DrawEllipse(roseBrush, new Pen(new SolidColorBrush(Color.FromRgb(255, 120, 180)), 2.5), center, 36, 36);

            for (int i = 0; i < 6; i++)
            {
                double angle = i * 60.0 * Math.PI / 180.0;
                Point pt = new(center.X + Math.Cos(angle) * 38, center.Y + Math.Sin(angle) * 38);
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(220, 40, 180, 80)), null, pt, 8, 6);
            }

            dc.DrawEllipse(Brushes.White, null, center, 12, 12);
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateAttributeBadge(string path, string label, Color color)
    {
        int size = 64;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            Point center = new(size / 2.0, size / 2.0);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(220, 15, 20, 30)),
                new Pen(new SolidColorBrush(color), 2.5), new Rect(2, 2, size - 4, size - 4), 10, 10);

            var tf = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.ExtraBold, FontStretches.Normal);
            var text = new FormattedText(label, System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, tf, 13, new SolidColorBrush(color), 1.0);
            dc.DrawText(text, new Point(center.X - text.Width / 2.0, center.Y - text.Height / 2.0));
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void GenerateMatrixTile(string path)
    {
        int size = 128;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            // 배경
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(10, 14, 24)), null, new Rect(0, 0, size, size));

            // 격자 테두리
            var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 0, 180, 255)), 1.5);
            dc.DrawRectangle(null, gridPen, new Rect(0, 0, size, size));

            // 모서리 사이버 노드 점
            var nodeBrush = new SolidColorBrush(Color.FromArgb(100, 0, 220, 255));
            dc.DrawRectangle(nodeBrush, null, new Rect(0, 0, 6, 6));
            dc.DrawRectangle(nodeBrush, null, new Rect(size - 6, 0, 6, 6));
            dc.DrawRectangle(nodeBrush, null, new Rect(0, size - 6, 6, 6));
            dc.DrawRectangle(nodeBrush, null, new Rect(size - 6, size - 6, 6, 6));

            // 중심 십자 서킷 패턴
            var circuitPen = new Pen(new SolidColorBrush(Color.FromArgb(20, 0, 220, 255)), 1.0);
            dc.DrawLine(circuitPen, new Point(size / 2.0, 10), new Point(size / 2.0, size - 10));
            dc.DrawLine(circuitPen, new Point(10, size / 2.0), new Point(size - 10, size / 2.0));
        }
        SaveVisualToPng(dv, size, size, path);
    }

    private static void DrawArc(DrawingContext dc, Pen pen, Point center, double rx, double ry, double startAngle, double sweepAngle)
    {
        double startRad = startAngle * Math.PI / 180.0;
        double endRad = (startAngle + sweepAngle) * Math.PI / 180.0;

        Point p1 = new(center.X + Math.Cos(startRad) * rx, center.Y + Math.Sin(startRad) * ry);
        Point p2 = new(center.X + Math.Cos(endRad) * rx, center.Y + Math.Sin(endRad) * ry);

        var geo = new PathGeometry();
        var fig = new PathFigure { StartPoint = p1, IsClosed = false };
        fig.Segments.Add(new ArcSegment(p2, new Size(rx, ry), 0, Math.Abs(sweepAngle) > 180, sweepAngle > 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise, true));
        geo.Figures.Add(fig);

        dc.DrawGeometry(null, pen, geo);
    }
}
