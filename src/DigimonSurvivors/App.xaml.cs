using System;
using System.IO;
using System.Windows;
using DigimonSurvivors.Tools;

namespace DigimonSurvivors;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // 1. 앱 아이콘(app_icon.png, app_icon.ico) 빌드
            string projectRoot = @"D:\Project\DigimonSurvivors";
            IconBuilder.BuildAppIcon(projectRoot);

            // 2. 디자인 에셋 (스킬 투사체, UI, 환경 타일) 자동 생성 및 세팅
            SkillEffectGenerator.GenerateAll(projectRoot);

            // 2-1. 모든 진화 디지몬 고유 토큰 스프라이트 빌드
            DigimonTokenGenerator.GenerateAll(projectRoot);
            string srcRoot = @"D:\Project\DigimonTcg\src\DigimonSurvivors";
            if (Directory.Exists(srcRoot) && !srcRoot.Equals(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                DigimonTokenGenerator.GenerateAll(srcRoot);
            }

            // 3. 공식 밴다이 원작 일러스트 정밀 처리
            NukkiProcessor.ProcessOfficialArtwork();

            // 디지몬 스프라이트 정밀 누끼(외곽 + 내부 고립 포켓 제거) 처리
            string freshDir = Path.Combine(projectRoot, "Assets", "Fresh");
            string spritesDir = Path.Combine(projectRoot, "Assets", "Sprites");

            if (Directory.Exists(freshDir))
            {
                NukkiProcessor.ProcessAll(freshDir, spritesDir);

                // 고품질 다크 애니메이션 VFX 임포트
                string brainDir = @"C:\Users\MyungHwan\.gemini\antigravity\brain\2274f5fa-7175-4dca-911e-091b500f2fc1";
                string skillsDir = Path.Combine(projectRoot, "Assets", "Skills");
                ImportBrainVfx(brainDir, skillsDir);

                // 분류 디렉토리(Characters, Enemies)로 정돈 복사
                CategorizeSprites(spritesDir);

                // 배포 publish 디렉토리에도 에셋 동기화
                string pubAssets = Path.Combine(projectRoot, "publish", "Assets");
                if (Directory.Exists(pubAssets))
                {
                    CopyDirectory(Path.Combine(projectRoot, "Assets"), pubAssets);
                }

                // 4. 바탕화면에 게임 바로가기(.lnk) 및 아이콘 생성
                string targetExe = Path.Combine(projectRoot, "publish", "DigimonSurvivors.exe");
                string iconPath = Path.Combine(projectRoot, "Assets", "app_icon.ico");
                ShortcutCreator.CreateAllShortcuts(targetExe, iconPath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Asset setup notice: {ex.Message}");
        }

        if (e.Args.Length > 0 && e.Args[0] == "--setup-only")
        {
            Shutdown();
        }
    }

    private static void CategorizeSprites(string spritesDir)
    {
        string charsDir = Path.Combine(spritesDir, "Characters");
        string enemiesDir = Path.Combine(spritesDir, "Enemies");
        Directory.CreateDirectory(charsDir);
        Directory.CreateDirectory(enemiesDir);

        var playerLine = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Agumon.png", "Greymon.png", "MetalGreymon.png", "WarGreymon.png", "Omegamon.png", "SkullGreymon.png",
            "Gabumon.png", "Garurumon.png", "WereGarurumon.png", "MetalGarurumon.png",
            "Piyomon.png", "Birdramon.png", "Garudamon.png", "Hououmon.png",
            "Patamon.png", "Angemon.png", "Tentomon.png", "Gomamon.png",
            "PicoDevimon.png", "Devimon.png", "Vamdemon.png", "Piemon.png",
            "Goburimon.png", "Orgemon.png"
        };

        foreach (var file in Directory.GetFiles(spritesDir, "*.png"))
        {
            string name = Path.GetFileName(file);
            string dest = playerLine.Contains(name) ? Path.Combine(charsDir, name) : Path.Combine(enemiesDir, name);
            File.Copy(file, dest, overwrite: true);
        }
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(sourceDir, file);
            string dest = Path.Combine(targetDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }
    }

    private static void ImportBrainVfx(string brainDir, string skillsDir)
    {
        if (!Directory.Exists(brainDir)) return;

        string[] skillNames = {
            "baby_flame", "mega_flame", "gaia_force", "shadow_wing",
            "petit_fire", "hammer_spark", "giga_destroyer", "grey_sword",
            "fox_fire", "sorrow_blue", "kaiser_nail", "cocytus_breath"
        };

        foreach (var name in skillNames)
        {
            var files = Directory.GetFiles(brainDir, $"{name}_*.jpg");
            if (files.Length > 0)
            {
                Array.Sort(files);
                string latest = files[^1];
                string destPng = Path.Combine(skillsDir, $"{name}.png");
                try
                {
                    VfxBlackToAlpha.ConvertBlackBackgroundToAlpha(latest, destPng);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"VFX convert error ({name}): {ex.Message}");
                }
            }
        }
    }
}
