using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DigimonSurvivors.Audio;
using DigimonSurvivors.Engine;
using DigimonSurvivors.Models;
using DigimonSurvivors.Rendering;
using DigimonSurvivors.Systems;

namespace DigimonSurvivors;

public class CardViewModel
{
    public UpgradeOption Option { get; set; } = null!;
    public string Title => Option.Title;
    public string Subtitle => Option.Subtitle;
    public string Description => Option.Description;
    public BitmapSource? CardImage => AssetManager.GetCardImage(Option.CardId) ?? AssetManager.GetSkillSprite(Option.TargetId);
}

public partial class MainWindow : Window
{
    private readonly GameEngine _engine = new();
    private readonly Stopwatch _stopwatch = new();
    private TimeSpan _lastTime;
    private string _selectedStarterId = "agumon";
    private Vector _currentMoveInput = new(0, 0);

    public MainWindow()
    {
        InitializeComponent();

        CanvasGame.Engine = _engine;

        _engine.OnLevelUpTriggered += HandleLevelUp;
        _engine.OnEvolutionTriggered += HandleEvolution;
        _engine.OnGameOverTriggered += HandleGameOver;
        _engine.OnVictoryTriggered += HandleVictory;

        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        string projectRoot = @"D:\Project\DigimonSurvivors";
        // 공식 밴다이 원작 일러스트 자동 변환 및 동기화
        Tools.NukkiProcessor.ProcessOfficialArtwork();
        Tools.DigimonTokenGenerator.GenerateAll(projectRoot);

        AssetManager.PrewarmAllSprites();
        AssetManager.PrewarmAllSkills();

        string baseDir = System.IO.Directory.Exists(System.IO.Path.Combine(projectRoot, "Assets"))
            ? projectRoot
            : AppDomain.CurrentDomain.BaseDirectory;
        BgmSystem.Initialize(baseDir);

        // 스타터 8종 이미지 로드
        ImgAgumon.Source = AssetManager.GetSprite("Agumon.png");
        ImgGabumon.Source = AssetManager.GetSprite("Gabumon.png");
        ImgPiyomon.Source = AssetManager.GetSprite("Piyomon.png");
        ImgPicoDevimon.Source = AssetManager.GetSprite("PicoDevimon.png");
        ImgPatamon.Source = AssetManager.GetSprite("Patamon.png");
        ImgTentomon.Source = AssetManager.GetSprite("Tentomon.png");
        ImgGomamon.Source = AssetManager.GetSprite("Gomamon.png");
        ImgGoburimon.Source = AssetManager.GetSprite("Goburimon.png");

        UpdateStarterSelectionUI();
        UpdateSettingsUI();

        string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app_icon.png");
        if (System.IO.File.Exists(iconPath))
        {
            try { Icon = new BitmapImage(new Uri(iconPath, UriKind.Absolute)); } catch { }
        }

        _stopwatch.Start();
        _lastTime = _stopwatch.Elapsed;
        CompositionTarget.Rendering += OnGameLoop;

        Focus();
    }

    private void OnGameLoop(object? sender, EventArgs e)
    {
        var currentTime = _stopwatch.Elapsed;
        double dt = (currentTime - _lastTime).TotalSeconds;
        _lastTime = currentTime;

        dt = Math.Min(dt, 0.05);

        // 1. 키보드 이동 입력 수집
        _currentMoveInput = new Vector(0, 0);
        if (_engine.State == GameState.Playing)
        {
            if (Keyboard.IsKeyDown(Key.W) || Keyboard.IsKeyDown(Key.Up)) _currentMoveInput.Y -= 1;
            if (Keyboard.IsKeyDown(Key.S) || Keyboard.IsKeyDown(Key.Down)) _currentMoveInput.Y += 1;
            if (Keyboard.IsKeyDown(Key.A) || Keyboard.IsKeyDown(Key.Left)) _currentMoveInput.X -= 1;
            if (Keyboard.IsKeyDown(Key.D) || Keyboard.IsKeyDown(Key.Right)) _currentMoveInput.X += 1;
        }

        // 2. 엔진 업데이트
        _engine.Update(dt, _currentMoveInput);

        // 3. 진화 컷신 종료 감지
        if (_engine.State != GameState.EvolutionCutscene && EvolutionOverlay.Visibility == Visibility.Visible)
        {
            EvolutionOverlay.Visibility = Visibility.Collapsed;
        }

        // 4. 화면 리드로우
        CanvasGame.InvalidateVisual();
    }

    // 5. 스페이스바 대시 항상 최우선 처리 (PreviewKeyDown 터널링 이벤트로 버튼 포커스 간섭 방지)
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            if (_engine.State == GameState.Playing)
            {
                _engine.TryPlayerDash(_currentMoveInput);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Escape)
        {
            if (SettingsOverlay.Visibility == Visibility.Visible)
            {
                SettingsOverlay.Visibility = Visibility.Collapsed;
                e.Handled = true;
                return;
            }

            if (_engine.State == GameState.Playing)
            {
                _engine.State = GameState.Paused;
                PauseOverlay.Visibility = Visibility.Visible;
                e.Handled = true;
            }
            else if (_engine.State == GameState.Paused)
            {
                _engine.State = GameState.Playing;
                PauseOverlay.Visibility = Visibility.Collapsed;
                e.Handled = true;
            }
        }
        else if (e.Key == Key.M)
        {
            bool newMute = !BgmSystem.IsMuted;
            BgmSystem.SetMute(newMute);
            SoundSystem.IsMuted = newMute;
            GameSettings.BgmEnabled = !newMute;
            GameSettings.SfxEnabled = !newMute;
            UpdateSettingsUI();
            e.Handled = true;
        }
    }

    #region 스타터 8종 선택 핸들러
    private void SelectStarter_Agumon(object sender, MouseButtonEventArgs e) { _selectedStarterId = "agumon"; UpdateStarterSelectionUI(); }
    private void SelectStarter_Gabumon(object sender, MouseButtonEventArgs e) { _selectedStarterId = "gabumon"; UpdateStarterSelectionUI(); }
    private void SelectStarter_Piyomon(object sender, MouseButtonEventArgs e) { _selectedStarterId = "piyomon"; UpdateStarterSelectionUI(); }
    private void SelectStarter_PicoDevimon(object sender, MouseButtonEventArgs e) { _selectedStarterId = "pico_devimon"; UpdateStarterSelectionUI(); }
    private void SelectStarter_Patamon(object sender, MouseButtonEventArgs e) { _selectedStarterId = "patamon"; UpdateStarterSelectionUI(); }
    private void SelectStarter_Tentomon(object sender, MouseButtonEventArgs e) { _selectedStarterId = "tentomon"; UpdateStarterSelectionUI(); }
    private void SelectStarter_Gomamon(object sender, MouseButtonEventArgs e) { _selectedStarterId = "gomamon"; UpdateStarterSelectionUI(); }
    private void SelectStarter_Goburimon(object sender, MouseButtonEventArgs e) { _selectedStarterId = "goburimon"; UpdateStarterSelectionUI(); }

    private void UpdateStarterSelectionUI()
    {
        var gold = new SolidColorBrush(Color.FromRgb(255, 215, 0));
        var borderNormal = new Thickness(2);
        var borderActive = new Thickness(4);

        SetCardBorder(CardAgumon, _selectedStarterId == "agumon", gold, borderActive, borderNormal, Color.FromRgb(255, 82, 82));
        SetCardBorder(CardGabumon, _selectedStarterId == "gabumon", gold, borderActive, borderNormal, Color.FromRgb(64, 196, 255));
        SetCardBorder(CardPiyomon, _selectedStarterId == "piyomon", gold, borderActive, borderNormal, Color.FromRgb(255, 128, 171));
        SetCardBorder(CardPicoDevimon, _selectedStarterId == "pico_devimon", gold, borderActive, borderNormal, Color.FromRgb(186, 104, 200));
        SetCardBorder(CardPatamon, _selectedStarterId == "patamon", gold, borderActive, borderNormal, Color.FromRgb(255, 213, 79));
        SetCardBorder(CardTentomon, _selectedStarterId == "tentomon", gold, borderActive, borderNormal, Color.FromRgb(129, 199, 132));
        SetCardBorder(CardGomamon, _selectedStarterId == "gomamon", gold, borderActive, borderNormal, Color.FromRgb(77, 208, 225));
        SetCardBorder(CardGoburimon, _selectedStarterId == "goburimon", gold, borderActive, borderNormal, Color.FromRgb(176, 190, 197));
    }

    private static void SetCardBorder(System.Windows.Controls.Border card, bool isSelected, Brush activeBrush, Thickness activeThick, Thickness normalThick, Color defaultColor)
    {
        if (card == null) return;
        card.BorderBrush = isSelected ? activeBrush : new SolidColorBrush(defaultColor);
        card.BorderThickness = isSelected ? activeThick : normalThick;
        card.Background = isSelected ? new SolidColorBrush(Color.FromRgb(28, 40, 60)) : new SolidColorBrush(Color.FromRgb(22, 32, 50));
    }

    private void BtnStartGame_Click(object sender, RoutedEventArgs e)
    {
        TitleOverlay.Visibility = Visibility.Collapsed;
        GameOverOverlay.Visibility = Visibility.Collapsed;
        VictoryOverlay.Visibility = Visibility.Collapsed;
        LevelUpOverlay.Visibility = Visibility.Collapsed;
        EvolutionOverlay.Visibility = Visibility.Collapsed;
        PauseOverlay.Visibility = Visibility.Collapsed;
        SettingsOverlay.Visibility = Visibility.Collapsed;

        _engine.StartGame(_selectedStarterId);
        Focus();
    }
    #endregion

    #region 레벨업 & 카드 선택
    private void HandleLevelUp()
    {
        var viewModels = _engine.CurrentLevelUpOptions.Select(opt => new CardViewModel { Option = opt }).ToList();
        ListCards.ItemsSource = viewModels;

        if (_engine.PendingLevelUps > 1)
        {
            TxtLevelUpSub.Text = $"⭐ 연속 레벨업 선택 진행 중 (남은 선택: {_engine.PendingLevelUps}회)";
        }
        else
        {
            TxtLevelUpSub.Text = "데이터를 흡수하여 새로운 무기, 패시브 또는 진화 카드를 선택하세요";
        }

        LevelUpOverlay.Visibility = Visibility.Visible;
    }

    private void CardItem_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is CardViewModel vm)
        {
            LevelUpOverlay.Visibility = Visibility.Collapsed;
            _engine.SelectUpgrade(vm.Option);
            Focus();
        }
    }
    #endregion

    #region 진화 컷신
    private void HandleEvolution()
    {
        if (_engine.EvolvingFromProfile != null && _engine.EvolvingToProfile != null)
        {
            ImgEvoFrom.Source = AssetManager.GetDigimonSprite(_engine.EvolvingFromProfile);
            ImgEvoTo.Source = AssetManager.GetDigimonSprite(_engine.EvolvingToProfile);
            TxtEvoNames.Text = $"{_engine.EvolvingFromProfile.Name} 진화! ──────> {_engine.EvolvingToProfile.Name}!";
            EvolutionOverlay.Visibility = Visibility.Visible;
        }
    }
    #endregion

    #region 일시정지 & 설정창
    private void BtnResume_Click(object sender, RoutedEventArgs e)
    {
        PauseOverlay.Visibility = Visibility.Collapsed;
        _engine.State = GameState.Playing;
        Focus();
    }

    private void BtnOpenSettings_Click(object sender, RoutedEventArgs e)
    {
        UpdateSettingsUI();
        SettingsOverlay.Visibility = Visibility.Visible;
    }

    private void BtnCloseSettings_Click(object sender, RoutedEventArgs e)
    {
        SettingsOverlay.Visibility = Visibility.Collapsed;
        Focus();
    }

    private void BtnToggleBgm_Click(object sender, RoutedEventArgs e)
    {
        GameSettings.BgmEnabled = !GameSettings.BgmEnabled;
        BgmSystem.SetMute(!GameSettings.BgmEnabled);
        UpdateSettingsUI();
    }

    private void BtnToggleSfx_Click(object sender, RoutedEventArgs e)
    {
        GameSettings.SfxEnabled = !GameSettings.SfxEnabled;
        SoundSystem.IsMuted = !GameSettings.SfxEnabled;
        UpdateSettingsUI();
    }

    private void BtnToggleShake_Click(object sender, RoutedEventArgs e)
    {
        GameSettings.ScreenShakeEnabled = !GameSettings.ScreenShakeEnabled;
        UpdateSettingsUI();
    }

    private void BtnToggleDamageNum_Click(object sender, RoutedEventArgs e)
    {
        GameSettings.DamageNumbersEnabled = !GameSettings.DamageNumbersEnabled;
        UpdateSettingsUI();
    }

    private void BtnToggleMinimap_Click(object sender, RoutedEventArgs e)
    {
        GameSettings.MinimapEnabled = !GameSettings.MinimapEnabled;
        UpdateSettingsUI();
    }

    private void BtnToggleParticles_Click(object sender, RoutedEventArgs e)
    {
        GameSettings.ParticlesEnabled = !GameSettings.ParticlesEnabled;
        UpdateSettingsUI();
    }

    private void UpdateSettingsUI()
    {
        SetToggleButton(BtnToggleBgm, GameSettings.BgmEnabled);
        SetToggleButton(BtnToggleSfx, GameSettings.SfxEnabled);
        SetToggleButton(BtnToggleShake, GameSettings.ScreenShakeEnabled);
        SetToggleButton(BtnToggleDamageNum, GameSettings.DamageNumbersEnabled);
        SetToggleButton(BtnToggleMinimap, GameSettings.MinimapEnabled);
        SetToggleButton(BtnToggleParticles, GameSettings.ParticlesEnabled);
    }

    private static void SetToggleButton(System.Windows.Controls.Button btn, bool enabled)
    {
        if (btn == null) return;
        btn.Content = enabled ? "ON" : "OFF";
        btn.Background = enabled ? new SolidColorBrush(Color.FromRgb(0, 229, 255)) : new SolidColorBrush(Color.FromRgb(70, 80, 90));
        btn.Foreground = enabled ? new SolidColorBrush(Color.FromRgb(8, 12, 20)) : Brushes.White;
    }
    #endregion

    #region 게임오버 & 승리 결과 보고서
    private void HandleGameOver()
    {
        int min = (int)(_engine.GameTime / 60.0);
        int sec = (int)(_engine.GameTime % 60.0);

        TxtGameOverTime.Text = $"생존 시간: {min:00}분 {sec:00}초";
        TxtGameOverKills.Text = $"격파한 적: {_engine.Player?.KillCount ?? 0}체";
        TxtGameOverLevel.Text = $"도달 레벨: Lv.{_engine.Player?.Level ?? 1} ({_engine.Player?.Profile.Name})";

        int score = (int)(_engine.GameTime * 10 + (_engine.Player?.KillCount ?? 0) * 15 + (_engine.Player?.Level ?? 1) * 100);
        string rank = score >= 5000 ? "RANK S" : (score >= 3000 ? "RANK A" : (score >= 1500 ? "RANK B" : "RANK C"));
        TxtGameOverRank.Text = rank;
        TxtGameOverRank.Foreground = rank == "RANK S" ? Brushes.Gold : (rank == "RANK A" ? Brushes.Cyan : Brushes.White);
        TxtGameOverDamageReport.Text = BuildDamageReport();

        GameOverOverlay.Visibility = Visibility.Visible;
    }

    private void HandleVictory()
    {
        int min = (int)(_engine.GameTime / 60.0);
        int sec = (int)(_engine.GameTime % 60.0);

        TxtVictoryTime.Text = $"클리어 시간: {min:00}분 {sec:00}초";
        TxtVictoryKills.Text = $"격파한 적: {_engine.Player?.KillCount ?? 0}체";
        TxtVictoryLevel.Text = $"최종 레벨: Lv.{_engine.Player?.Level ?? 1} ({_engine.Player?.Profile.Name})";
        TxtVictoryDamageReport.Text = BuildDamageReport();

        VictoryOverlay.Visibility = Visibility.Visible;
    }

    private string BuildDamageReport()
    {
        if (_engine.Player == null || _engine.Player.TotalDamageDealt <= 0)
        {
            return "전투 기록이 없습니다.";
        }

        double total = _engine.Player.TotalDamageDealt;
        var list = _engine.Player.SkillDamageStats
            .OrderByDescending(kvp => kvp.Value)
            .Take(4)
            .Select(kvp =>
            {
                double pct = (kvp.Value / total) * 100.0;
                string skillName = kvp.Key switch
                {
                    "baby_flame" => "베이비 플레임",
                    "mega_flame" => "메가 플레임",
                    "gaia_force" => "테라 광선 (가이아 포스)",
                    "shadow_wing" => "섀도우 윙",
                    "petit_fire" => "쁘띠 파이어",
                    "hammer_spark" => "해머 스파크",
                    "giga_destroyer" => "기가 디스트로이어",
                    "grey_sword" => "그레이 소드",
                    "fox_fire" => "폭스 파이어",
                    "sorrow_blue" => "소로우 블루",
                    "kaiser_nail" => "카이저 네일",
                    "cocytus_breath" => "코퀴토스 브레스",
                    "pico_dart" => "피코 다트",
                    "air_shot" => "공기팡",
                    "petit_thunder" => "꼬마 벼락",
                    "surfing_fish" => "물고기 행진",
                    "goburi_strike" => "고블 스트라이크",
                    "Bomb" => "크래시 폭탄",
                    _ => kvp.Key
                };
                return $"• {skillName}: {(int)kvp.Value:N0} ({pct:0.0}%)";
            });

        return $"총 가한 피해량: {(int)total:N0}\n" + string.Join("\n", list);
    }

    private void BtnRestart_Click(object sender, RoutedEventArgs e)
    {
        GameOverOverlay.Visibility = Visibility.Collapsed;
        _engine.StartGame(_selectedStarterId);
        Focus();
    }

    private void BtnToTitle_Click(object sender, RoutedEventArgs e)
    {
        BgmSystem.Stop();
        PauseOverlay.Visibility = Visibility.Collapsed;
        SettingsOverlay.Visibility = Visibility.Collapsed;
        VictoryOverlay.Visibility = Visibility.Collapsed;
        GameOverOverlay.Visibility = Visibility.Collapsed;
        TitleOverlay.Visibility = Visibility.Visible;
    }
    #endregion
}