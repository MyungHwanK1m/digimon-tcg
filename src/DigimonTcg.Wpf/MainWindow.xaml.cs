using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DigimonTcg.Core.Data;
using DigimonTcg.Core.DeckBuilder;
using DigimonTcg.Core.Engine;
using DigimonTcg.Core.Models;

namespace DigimonTcg.Wpf;

public partial class MainWindow : Window
{
    private readonly CardCatalogService _catalogService;
    private readonly GameState _gameState;
    private readonly GoldfishEngine _engine;
    private readonly Dictionary<string, BitmapImage> _bitmapCache = new();

    private List<CardData> _builderMainDeck = new();
    private List<CardData> _builderDigitamaDeck = new();
    private CardData? _pendingHandCard;
    private bool _pendingIsEvolution;
    private bool _pendingTargetsOpponent;
    private CardInstance? _pendingAttackerInstance;
    private bool _isSecurityPeekEnabled = false;

    // 드래그 앤 드롭 상태 필드
    private Point _dragStartPoint;
    private bool _isDragging;
    private CardData? _potentialDragHandCard;
    private CardInstance? _potentialDragFieldAttacker;
    private CardInstance? _potentialDragBreedingDigimon;
    private Border? _dragGhostBorder;
    private Border? _dragGhostBadgeBorder;
    private TextBlock? _dragGhostBadgeText;

    public MainWindow()
    {
        InitializeComponent();

        _catalogService = new CardCatalogService();
        _gameState = new GameState();
        _engine = new GoldfishEngine(_gameState);

        // 기본 ST-1 가이아 레드 프리셋 로드 및 메인 화면 [실전 AI 듀얼 아레나 (Stage 3)] 시작
        var (st1Main, st1Digitama) = StarterDecks.CreateSt1GaiaRedPreset();
        _builderMainDeck = st1Main.ToList();
        _builderDigitamaDeck = st1Digitama.ToList();

        _isSecurityPeekEnabled = false;
        _engine.StartNewGame(_builderMainDeck, _builderDigitamaDeck, stage: PracticeStage.Stage3_AiBattle);

        // 초기 프리뷰 카드 세팅 (워그레이몬 또는 첫 패 카드)
        var initialPreview = _gameState.Player.Hand.FirstOrDefault() ?? _catalogService.Catalog.First();
        UpdateCardInspector(initialPreview);

        UpdateTopTabVisuals(activeTab: 0);
        RefreshAllSimulatorUi();
        RefreshCatalogUi();
        RefreshDeckBuilderUi();
        ShowToast(true, "⚔️ [실전 AI 듀얼 아레나] 공식 플레이매트가 준비되었습니다! 패의 카드를 마우스로 드래그하여 진화/등장시키고 AI와 대결하세요.");
    }

    // ==================== 카드 이미지 로더 & 인스펙터 ====================
    private BitmapImage? GetCardBitmap(CardData card)
    {
        if (_bitmapCache.TryGetValue(card.CardId, out var cached))
            return cached;

        string localPath = _catalogService.GetLocalImageFilePath(card);
        if (File.Exists(localPath) && new FileInfo(localPath).Length > 500)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(localPath, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                _bitmapCache[card.CardId] = bmp;
                return bmp;
            }
            catch
            {
                // 이미지 디코딩 실패 시 무시
            }
        }
        else
        {
            // 백그라운드로 이미지 다운로드 시도 후 캐시
            _ = DownloadAndRefreshCardImageAsync(card);
        }

        return null;
    }

    private async System.Threading.Tasks.Task DownloadAndRefreshCardImageAsync(CardData card)
    {
        var path = await _catalogService.EnsureCardImageCachedAsync(card);
        if (path != null)
        {
            Dispatcher.Invoke(() =>
            {
                if (!_bitmapCache.ContainsKey(card.CardId))
                {
                    RefreshAllSimulatorUi();
                    if (DeckBuilderView.Visibility == Visibility.Visible)
                        RefreshCatalogUi();
                }
            });
        }
    }

    private void UpdateCardInspector(CardData card)
    {
        TxtInspectorId.Text = card.CardId;
        InspectorCardFrame.BorderBrush = GetCardColorBrush(card.Color);
        InspectorColorBadge.Background = GetCardColorBrush(card.Color);

        TxtInspectorBadge.Text = card.Type == CardType.Digimon || card.Type == CardType.Digitama
            ? $"Lv.{card.Level} {card.Color} {card.Type}"
            : $"{card.Color} {card.Type}";

        TxtInspectorName.Text = card.Name;
        TxtInspectorStats.Text = card.Type == CardType.Digimon
            ? $"등장 코스트: {card.PlayCost} | 진화: {card.EvoCost} (Lv.{card.EvoRequirementLevel})\n기본 DP: {card.DP:N0}"
            : card.Type == CardType.Digitama
                ? "육성 에어리어 전용 Lv.2 디지타마"
                : $"사용 코스트: {card.PlayCost}";

        TxtInspectorEffect.Text = string.IsNullOrWhiteSpace(card.EffectText) ? "없음 (바닐라)" : card.EffectText;
        TxtInspectorInherited.Text = string.IsNullOrWhiteSpace(card.InheritedEffectText) ? "없음" : card.InheritedEffectText;

        var bmp = GetCardBitmap(card);
        if (bmp != null)
        {
            ImgInspectorCard.Source = bmp;
            ImgInspectorCard.Visibility = Visibility.Visible;
            InspectorFallbackPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            ImgInspectorCard.Visibility = Visibility.Collapsed;
            InspectorFallbackPanel.Visibility = Visibility.Visible;
            TxtInspectorFallback.Text = $"{card.CardId}\n{card.Name}\n(Lv.{card.Level})";
        }
    }

    /// <summary>
    /// 실물 TCG 카드 디자인 아트워크 박스 생성 (코스트 오브, DP 배지, 한글 이름 오버레이, 레스트 기울임 포함)
    /// </summary>
    private FrameworkElement CreateCardVisualFrame(
        CardData card,
        double width,
        double height,
        string? customDpOrBadge = null,
        bool isRested = false)
    {
        var rootGrid = new Grid
        {
            Width = width,
            Height = height,
            ClipToBounds = true
        };

        var bmp = GetCardBitmap(card);
        if (bmp != null)
        {
            var img = new Image
            {
                Source = bmp,
                Stretch = Stretch.UniformToFill
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            rootGrid.Children.Add(img);
        }
        else
        {
            // 오프라인 폴백 프로시저럴 TCG 일러스트 배경
            rootGrid.Children.Add(new Border
            {
                Background = new LinearGradientBrush(
                    (Color)ColorConverter.ConvertFromString("#1E1E2E"),
                    GetCardColorBrush(card.Color).Color,
                    45.0),
                Child = new TextBlock
                {
                    Text = $"{card.CardId}\n{card.Name}",
                    Foreground = BrushFromHex("#11111B"),
                    FontWeight = FontWeights.ExtraBold,
                    FontSize = 13,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            });
        }

        // 좌상단 등장 코스트 & 진화 코스트 배지
        if (card.Type != CardType.Digitama)
        {
            var topLeftStack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(4)
            };

            topLeftStack.Children.Add(new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = BrushFromHex("#E611111B"),
                BorderBrush = GetCardColorBrush(card.Color),
                BorderThickness = new Thickness(1.5),
                ToolTip = $"등장/사용 코스트: {card.PlayCost}",
                Child = new TextBlock
                {
                    Text = card.PlayCost.ToString(),
                    Foreground = BrushFromHex("#F8FAFC"),
                    FontWeight = FontWeights.ExtraBold,
                    FontSize = 11.5,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            });

            if (card.Type == CardType.Digimon)
            {
                topLeftStack.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Background = GetCardColorBrush(card.Color),
                    Padding = new Thickness(4, 1, 4, 1),
                    Margin = new Thickness(0, 2, 0, 0),
                    ToolTip = $"진화 코스트: {card.EvoCost} (Lv.{card.EvoRequirementLevel} 필요)",
                    Child = new TextBlock
                    {
                        Text = $"진화{card.EvoCost}",
                        Foreground = BrushFromHex("#11111B"),
                        FontWeight = FontWeights.ExtraBold,
                        FontSize = 9.5
                    }
                });
            }

            rootGrid.Children.Add(topLeftStack);
        }

        // 우상단 DP 또는 커스텀 배지
        string rightBadgeText = customDpOrBadge ?? (card.Type == CardType.Digimon ? $"{card.DP} DP" : card.Type.ToString());
        rootGrid.Children.Add(new Border
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(4),
            CornerRadius = new CornerRadius(4),
            Background = BrushFromHex("#D911111B"),
            BorderBrush = BrushFromHex("#F9E2AF"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(5, 2, 5, 2),
            Child = new TextBlock
            {
                Text = rightBadgeText,
                Foreground = BrushFromHex("#F9E2AF"),
                FontWeight = FontWeights.ExtraBold,
                FontSize = 10
            }
        });

        // 하단 반투명 한글 카드명 & 레벨 리본
        var bottomRibbon = new Border
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            Background = BrushFromHex("#D911111B"),
            BorderBrush = GetCardColorBrush(card.Color),
            BorderThickness = new Thickness(0, 1.5, 0, 0),
            Padding = new Thickness(5, 3, 5, 3),
            Child = new DockPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = card.Level > 0 ? $"Lv.{card.Level} {card.Name}" : card.Name,
                        Foreground = BrushFromHex("#F8FAFC"),
                        FontWeight = FontWeights.Bold,
                        FontSize = 11,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    }
                }
            }
        };
        rootGrid.Children.Add(bottomRibbon);

        // 레스트(REST) 상태 오버레이
        if (isRested)
        {
            rootGrid.Children.Add(new Border
            {
                Background = BrushFromHex("#8811111B"),
                Child = new Border
                {
                    Background = BrushFromHex("#FAB387"),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 3, 8, 3),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = "↻ REST (레스트)",
                        Foreground = BrushFromHex("#11111B"),
                        FontWeight = FontWeights.ExtraBold,
                        FontSize = 11
                    }
                }
            });
        }

        var outerBorder = new Border
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(7),
            BorderBrush = GetCardColorBrush(card.Color),
            BorderThickness = new Thickness(2),
            Background = BrushFromHex("#11111B"),
            ClipToBounds = true,
            Child = rootGrid,
            RenderTransformOrigin = new Point(0.5, 0.5)
        };

        if (isRested)
        {
            outerBorder.RenderTransform = new RotateTransform(-6.0);
        }

        outerBorder.MouseEnter += (_, _) => UpdateCardInspector(card);
        return outerBorder;
    }

    // ==================== 3대 메인 모드 탭 전환 (실전 AI 듀얼 아레나 / 1인 솔리테어 훈련소 / 디지털 카드 도감 & 덱 공방) ====================
    private void UpdateTopTabVisuals(int activeTab)
    {
        // 0: 실전 AI 듀얼 아레나, 1: 1인 솔리테어 훈련소, 2: 디지털 카드 도감 & 덱 공방
        BtnTabOfficialMatch.Background = BrushFromHex(activeTab == 0 ? "#F38BA8" : "#25293E");
        BtnTabOfficialMatch.Foreground = BrushFromHex(activeTab == 0 ? "#11111B" : "#CDD6F4");

        BtnTabSimulator.Background = BrushFromHex(activeTab == 1 ? "#89B4FA" : "#25293E");
        BtnTabSimulator.Foreground = BrushFromHex(activeTab == 1 ? "#11111B" : "#CDD6F4");

        BtnTabDeckBuilder.Background = BrushFromHex(activeTab == 2 ? "#F9E2AF" : "#25293E");
        BtnTabDeckBuilder.Foreground = BrushFromHex(activeTab == 2 ? "#11111B" : "#CDD6F4");

        TxtCurrentModeSubtitle.Text = activeTab switch
        {
            0 => "⚔️ 실전 AI 듀얼 아레나 — 공식 룰 플레이매트 대전",
            1 => "🎯 1인 솔리테어 훈련소 — 5턴 타임어택 & 블로커 돌파 연습",
            _ => "📚 디지털 카드 도감 & 덱 공방 — 50장 메인 덱 & 디지타마 황금 비율 설계"
        };
    }

    private void BtnTabOfficialMatch_Click(object sender, RoutedEventArgs e)
    {
        SimulatorView.Visibility = Visibility.Visible;
        DeckBuilderView.Visibility = Visibility.Collapsed;
        UpdateTopTabVisuals(0);

        if (_gameState.CurrentStage != PracticeStage.Stage3_AiBattle)
        {
            _isSecurityPeekEnabled = false;
            _engine.StartNewGame(_builderMainDeck, _builderDigitamaDeck, stage: PracticeStage.Stage3_AiBattle);
            ShowToast(true, "⚔️ [실전 AI 듀얼 아레나] 공식 룰 AI 대전을 시작합니다! (시큐리티 비공개 & 메모리 줄다리기 적용)");
        }
        RefreshAllSimulatorUi();
    }

    private void BtnTabSimulator_Click(object sender, RoutedEventArgs e)
    {
        SimulatorView.Visibility = Visibility.Visible;
        DeckBuilderView.Visibility = Visibility.Collapsed;
        UpdateTopTabVisuals(1);

        if (_gameState.CurrentStage == PracticeStage.Stage3_AiBattle)
        {
            _isSecurityPeekEnabled = true;
            _engine.StartNewGame(_builderMainDeck, _builderDigitamaDeck, stage: PracticeStage.Stage1_Goldfish);
            ShowToast(true, "🎯 [1인 솔리테어 훈련소] 5턴 타임어택 모드로 전환했습니다! 상단에서 [Stage 1: 솔리테어] 또는 [Stage 2: 블로커 돌파]를 선택할 수 있습니다.");
        }
        RefreshAllSimulatorUi();
    }

    private void BtnTabDeckBuilder_Click(object sender, RoutedEventArgs e)
    {
        SimulatorView.Visibility = Visibility.Collapsed;
        DeckBuilderView.Visibility = Visibility.Visible;
        UpdateTopTabVisuals(2);
        RefreshCatalogUi();
        RefreshDeckBuilderUi();
    }

    private void AutoPassBreedingIfNeeded()
    {
        if (_gameState.CurrentPhase == Phase.Breeding && !_gameState.IsGameOver)
        {
            _engine.PassBreedingPhase();
        }
    }

    // ==================== 토스트 가이드 배너 ====================
    private void ShowToast(bool isSuccessOrGuide, string message)
    {
        if (isSuccessOrGuide)
        {
            ToastBanner.Background = BrushFromHex("#1E293B");
            ToastBanner.BorderBrush = BrushFromHex("#38BDF8");
            ToastBadge.Text = "💡 가이드 / 승인";
            ToastBadge.Foreground = BrushFromHex("#38BDF8");
        }
        else
        {
            ToastBanner.Background = BrushFromHex("#3B1D2E");
            ToastBanner.BorderBrush = BrushFromHex("#F38BA8");
            ToastBadge.Text = "⚠️ 규칙 위반 차단";
            ToastBadge.Foreground = BrushFromHex("#F38BA8");
        }
        ToastMessageText.Text = message;
    }

    private void ApplyActionResult(ActionResult result)
    {
        if (result.CanExecute)
        {
            ShowToast(true, result.GuideMessage);
        }
        else
        {
            ShowToast(false, result.Reason);
        }
        RefreshAllSimulatorUi();
    }

    private void BtnCancelPendingAction_Click(object sender, RoutedEventArgs e)
    {
        _pendingHandCard = null;
        _pendingAttackerInstance = null;
        ShowToast(true, "카드 사용/진화/공격 대상 선택을 취소했습니다.");
        RefreshAllSimulatorUi();
    }

    // ==================== 인게임 시뮬레이터 UI 렌더링 ====================
    private void RefreshAllSimulatorUi()
    {
        if (_pendingHandCard != null &&
            (!_gameState.Player.Hand.Contains(_pendingHandCard) ||
             _gameState.CurrentPhase != Phase.Main ||
             _gameState.IsGameOver))
        {
            _pendingHandCard = null;
        }

        if (_pendingAttackerInstance != null &&
            (!_gameState.Player.BattleArea.Contains(_pendingAttackerInstance) ||
             _pendingAttackerInstance.IsSuspended ||
             _gameState.CurrentPhase != Phase.Main ||
             _gameState.IsGameOver))
        {
            _pendingAttackerInstance = null;
        }

        if (SimulatorView.Visibility == Visibility.Visible)
        {
            UpdateTopTabVisuals(_gameState.CurrentStage == PracticeStage.Stage3_AiBattle ? 0 : 1);
        }

        RenderMemoryGauge();
        RenderTurnAndPhases();
        RenderOpponentSecurity();
        RenderOpponentBattleArea();
        RenderBreedingArea();
        RenderBattleArea();
        RenderHand();
        RenderTimeAttackHudAndLogs();
    }

    private void RenderMemoryGauge()
    {
        MemoryGaugeGrid.Children.Clear();
        for (int m = 10; m >= -10; m--)
        {
            bool isCurrent = _gameState.MemoryGauge == m;
            string bgHex = isCurrent
                ? "#F9E2AF"
                : m > 0 ? "#1E3A5F" : m == 0 ? "#313244" : "#4A1E2E";

            string fgHex = isCurrent ? "#11111B" : "#F8FAFC";
            string label = m > 0 ? $"+{m}" : m.ToString();

            var cell = new Border
            {
                Background = BrushFromHex(bgHex),
                BorderBrush = BrushFromHex(isCurrent ? "#F38BA8" : "#11111B"),
                BorderThickness = new Thickness(isCurrent ? 2 : 1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(1, 0, 1, 0),
                Child = new TextBlock
                {
                    Text = label,
                    Foreground = BrushFromHex(fgHex),
                    FontWeight = isCurrent ? FontWeights.ExtraBold : FontWeights.SemiBold,
                    FontSize = isCurrent ? 12.5 : 10.5,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            MemoryGaugeGrid.Children.Add(cell);
        }

        TxtMemoryStatus.Text = _gameState.MemoryGauge >= 0
            ? $"현재 메모리: +{_gameState.MemoryGauge} (플레이어 턴 유지)"
            : $"현재 메모리: {_gameState.MemoryGauge} (상대 턴 전환)";
    }

    private void RenderTurnAndPhases()
    {
        TxtTurnBadge.Text = $"TURN {_gameState.CurrentTurn}";
        PhaseStepPanel.Children.Clear();

        var phases = new (Phase Phase, string Name)[]
        {
            (Phase.Active, "1. 액티브"),
            (Phase.Draw, "2. 드로우"),
            (Phase.Breeding, "3. 육성"),
            (Phase.Main, "4. 메인"),
            (Phase.TurnEnd, "5. 턴종료")
        };

        foreach (var p in phases)
        {
            bool active = _gameState.CurrentPhase == p.Phase;
            var badge = new Border
            {
                Background = BrushFromHex(active ? "#89B4FA" : "#313244"),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(9, 3, 9, 3),
                Margin = new Thickness(0, 0, 5, 0),
                Child = new TextBlock
                {
                    Text = p.Name,
                    Foreground = BrushFromHex(active ? "#11111B" : "#A6ADC8"),
                    FontWeight = active ? FontWeights.Bold : FontWeights.Normal,
                    FontSize = 11.5
                }
            };
            PhaseStepPanel.Children.Add(badge);
        }

        BtnPassBreeding.IsEnabled = _gameState.CurrentPhase == Phase.Breeding && !_gameState.IsGameOver;
        BtnMulligan.IsEnabled = _gameState.CanMulligan && !_gameState.IsGameOver;
        BtnMulligan.Opacity = BtnMulligan.IsEnabled ? 1.0 : 0.45;

        // 훈련 단계 버튼 하이라이트
        BtnStage1.Background = BrushFromHex(_gameState.CurrentStage == PracticeStage.Stage1_Goldfish ? "#89B4FA" : "#313244");
        BtnStage1.Foreground = BrushFromHex(_gameState.CurrentStage == PracticeStage.Stage1_Goldfish ? "#11111B" : "#CDD6F4");
        BtnStage2.Background = BrushFromHex(_gameState.CurrentStage == PracticeStage.Stage2_BlockerDefense ? "#F9E2AF" : "#313244");
        BtnStage2.Foreground = BrushFromHex(_gameState.CurrentStage == PracticeStage.Stage2_BlockerDefense ? "#11111B" : "#CDD6F4");
        BtnStage3.Background = BrushFromHex(_gameState.CurrentStage == PracticeStage.Stage3_AiBattle ? "#F38BA8" : "#313244");
        BtnStage3.Foreground = BrushFromHex(_gameState.CurrentStage == PracticeStage.Stage3_AiBattle ? "#11111B" : "#CDD6F4");
    }

    private void BtnToggleSecurityPeek_Click(object sender, RoutedEventArgs e)
    {
        _isSecurityPeekEnabled = !_isSecurityPeekEnabled;
        RenderOpponentSecurity();
    }

    private void RenderOpponentSecurity()
    {
        BtnToggleSecurityPeek.Content = _isSecurityPeekEnabled ? "👁️ 앞면보기: ON" : "🙈 앞면보기: OFF (실전 비공개)";
        BtnToggleSecurityPeek.Background = BrushFromHex(_isSecurityPeekEnabled ? "#A6E3A1" : "#45475A");
        BtnToggleSecurityPeek.Foreground = BrushFromHex(_isSecurityPeekEnabled ? "#11111B" : "#F8FAFC");

        OpponentSecurityPanel.Children.Clear();

        var clearedHistory = _gameState.SecurityCheckHistory;
        var remainingCards = _gameState.DummyOpponent.SecurityStack;
        int clearedCount = clearedHistory.Count;
        int totalCount = clearedCount + remainingCards.Count;
        if (totalCount == 0) totalCount = 5;

        int pct = (int)Math.Round((double)clearedCount / totalCount * 100.0);
        TxtSecurityProgressLabel.Text = $"{clearedCount}/{totalCount} 돌파 ({pct}%)";
        BarSecurityProgress.Maximum = totalCount;
        BarSecurityProgress.Value = clearedCount;

        if (remainingCards.Count > 0)
        {
            var nextCard = remainingCards[0];
            TxtNextSecurityTarget.Text = _isSecurityPeekEnabled
                ? $"🎯 다음 타격 (#{clearedCount + 1}): [{nextCard.Name}] {(nextCard.Type == CardType.Digimon ? $"DP {nextCard.DP:N0}" : nextCard.Type.ToString())}"
                : $"🎯 다음 타격: 시큐리티 #{clearedCount + 1} (뒷면 비공개)";
            TxtNextSecurityTarget.Foreground = BrushFromHex("#89DCEB");
        }
        else
        {
            TxtNextSecurityTarget.Text = "🏆 상대 시큐리티 0장! 지금 어택하면 다이렉트 어택 승리!";
            TxtNextSecurityTarget.Foreground = BrushFromHex("#A6E3A1");
        }

        if (!string.IsNullOrWhiteSpace(_gameState.LastSecurityBattleResult))
        {
            TxtLastSecurityBattle.Text = $"⚔️ {_gameState.LastSecurityBattleResult}";
            TxtLastSecurityBattle.Foreground = BrushFromHex(_gameState.LastSecurityBattleWon ? "#A6E3A1" : "#F38BA8");
        }
        else
        {
            TxtLastSecurityBattle.Text = "내 디지몬을 드래그해 이곳에 놓으면 시큐리티를 공격합니다!";
            TxtLastSecurityBattle.Foreground = BrushFromHex("#A6ADC8");
        }

        // 1) 이미 오픈/소모된 시큐리티 슬롯 (#1 ~ #clearedCount)
        FrameworkElement? lastClearedSlot = null;
        for (int i = 0; i < clearedHistory.Count; i++)
        {
            var item = clearedHistory[i];
            int slotNum = i + 1;
            var widget = (FrameworkElement)CreateSecuritySlotWidget(item.SecurityCard, slotNum, isCleared: true, isBattleWon: item.IsBattleWon, isNextTarget: false, showFace: true);
            OpponentSecurityPanel.Children.Add(widget);
            lastClearedSlot = widget;
        }

        // 2) 남아 있는 시큐리티 슬롯 (#clearedCount+1 ~ #5)
        FrameworkElement? nextTargetSlot = null;
        for (int i = 0; i < remainingCards.Count; i++)
        {
            var card = remainingCards[i];
            int slotNum = clearedCount + i + 1;
            bool isNext = i == 0;
            var widget = (FrameworkElement)CreateSecuritySlotWidget(card, slotNum, isCleared: false, isBattleWon: true, isNextTarget: isNext, showFace: _isSecurityPeekEnabled);
            OpponentSecurityPanel.Children.Add(widget);
            if (isNext)
                nextTargetSlot = widget;
        }

        // 시큐리티가 타격/파괴될 때마다 방금 오픈된 시큐리티 카드(좌측)와 다음 타격 대상 시큐리티(우측)가 한눈에 보이도록 자동 스크롤 이동!
        double targetOffset = clearedCount <= 1 ? 0 : (clearedCount - 1) * 220.0;
        OpponentSecurityScrollViewer.UpdateLayout();
        OpponentSecurityScrollViewer.ScrollToHorizontalOffset(targetOffset);
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
        {
            if (clearedCount == 0)
            {
                OpponentSecurityScrollViewer.ScrollToHorizontalOffset(0);
            }
            else if (nextTargetSlot != null && lastClearedSlot != null)
            {
                OpponentSecurityScrollViewer.ScrollToHorizontalOffset(targetOffset);
                nextTargetSlot.BringIntoView();
            }
            else if (lastClearedSlot != null)
            {
                OpponentSecurityScrollViewer.ScrollToRightEnd();
                lastClearedSlot.BringIntoView();
            }
        }));
    }

    private UIElement CreateSecuritySlotWidget(CardData card, int slotNumber, bool isCleared, bool isBattleWon, bool isNextTarget, bool showFace)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // 좌측 미니 카드 이미지 또는 뒷면
        Border thumbBorder;
        if (showFace)
        {
            var bmp = GetCardBitmap(card);
            var thumbGrid = new Grid();
            if (bmp != null)
            {
                var img = new Image { Source = bmp, Stretch = Stretch.UniformToFill, Opacity = isCleared ? 0.45 : 1.0 };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                thumbGrid.Children.Add(img);
            }
            if (isCleared)
            {
                thumbGrid.Children.Add(new Border
                {
                    Background = BrushFromHex("#AA11111B"),
                    Child = new TextBlock
                    {
                        Text = isBattleWon ? "💥" : "💀",
                        FontSize = 18,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                });
            }

            thumbBorder = new Border
            {
                Width = 44,
                Height = 60,
                CornerRadius = new CornerRadius(4),
                BorderBrush = GetCardColorBrush(card.Color),
                BorderThickness = new Thickness(1.5),
                ClipToBounds = true,
                Child = thumbGrid
            };
        }
        else
        {
            thumbBorder = new Border
            {
                Width = 44,
                Height = 60,
                CornerRadius = new CornerRadius(4),
                Background = new LinearGradientBrush(
                    (Color)ColorConverter.ConvertFromString("#1E3A8A"),
                    (Color)ColorConverter.ConvertFromString("#0F172A"),
                    35.0),
                BorderBrush = BrushFromHex("#38BDF8"),
                BorderThickness = new Thickness(1.5),
                Child = new TextBlock
                {
                    Text = "SEC\n?",
                    Foreground = BrushFromHex("#38BDF8"),
                    FontWeight = FontWeights.ExtraBold,
                    FontSize = 11,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
        }
        Grid.SetColumn(thumbBorder, 0);
        row.Children.Add(thumbBorder);

        // 우측 시큐리티 슬롯 상태, 이름, DP 표시
        var infoStack = new StackPanel
        {
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        string badgeText = isCleared
            ? card.Type == CardType.Tamer
                ? $"#{slotNumber} 🎖️ [시큐리티] 테이머 등장!"
                : card.Type == CardType.Option
                    ? $"#{slotNumber} ✨ [시큐리티] 옵션 발동!"
                    : (isBattleWon ? $"#{slotNumber} ✅ 배틀 승리 (격파)" : $"#{slotNumber} 💀 내 디지몬 소멸 (자폭)")
            : isNextTarget
                ? $"#{slotNumber} 🎯 다음 타격 대상"
                : $"#{slotNumber} 🛡️ 대기 시큐리티";

        string badgeColor = isCleared
            ? (isBattleWon ? "#A6E3A1" : "#F38BA8")
            : isNextTarget
                ? "#F9E2AF"
                : "#A6ADC8";

        infoStack.Children.Add(new TextBlock
        {
            Text = badgeText,
            Foreground = BrushFromHex(badgeColor),
            FontWeight = FontWeights.ExtraBold,
            FontSize = 11
        });

        if (showFace)
        {
            string titlePrefix = card.Type switch
            {
                CardType.Digimon => $"Lv.{card.Level} ",
                CardType.Tamer => "[테이머] ",
                CardType.Option => "[옵션] ",
                _ => ""
            };

            infoStack.Children.Add(new TextBlock
            {
                Text = $"{titlePrefix}{card.Name}",
                Foreground = BrushFromHex(isCleared ? "#9399B2" : "#F8FAFC"),
                FontWeight = FontWeights.Bold,
                FontSize = 12.5,
                TextDecorations = isCleared && card.Type == CardType.Digimon ? TextDecorations.Strikethrough : null,
                Margin = new Thickness(0, 1, 0, 1)
            });

            string bottomBadgeText = card.Type switch
            {
                CardType.Digimon => $"⚔️ DP {card.DP:N0}",
                CardType.Tamer => "🎖️ [시큐리티] 코스트 0 등장",
                CardType.Option => "✨ [시큐리티] 즉시 효과 발동",
                _ => $"DP {card.DP:N0}"
            };

            infoStack.Children.Add(new Border
            {
                Background = BrushFromHex(isCleared ? "#232438" : isNextTarget ? "#3B1D2E" : "#1E293B"),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 1, 5, 1),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock
                {
                    Text = bottomBadgeText,
                    Foreground = BrushFromHex(isCleared ? "#6C7086" : card.Type == CardType.Digimon ? "#F38BA8" : "#F9E2AF"),
                    FontWeight = FontWeights.ExtraBold,
                    FontSize = 11
                }
            });
        }
        else
        {
            infoStack.Children.Add(new TextBlock
            {
                Text = "비공개 시큐리티",
                Foreground = BrushFromHex("#9399B2"),
                FontWeight = FontWeights.Bold,
                FontSize = 12
            });
            infoStack.Children.Add(new TextBlock
            {
                Text = "공격 시 오픈 & 효과 발동",
                Foreground = BrushFromHex("#6C7086"),
                FontSize = 10.5
            });
        }

        Grid.SetColumn(infoStack, 1);
        row.Children.Add(infoStack);

        var slotBorder = new Border
        {
            Width = 212,
            Height = 72,
            Background = BrushFromHex(isCleared ? "#11111B" : isNextTarget ? "#231E32" : "#141522"),
            BorderBrush = BrushFromHex(isCleared ? "#313244" : isNextTarget ? "#F9E2AF" : "#45475A"),
            BorderThickness = new Thickness(isNextTarget ? 2.5 : 1),
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(7, 5, 7, 5),
            Margin = new Thickness(0, 0, 8, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = row
        };

        if (showFace)
        {
            slotBorder.MouseEnter += (_, _) => UpdateCardInspector(card);
        }

        return slotBorder;
    }

    private void RenderOpponentBattleArea()
    {
        OpponentBattleAreaBorder.Visibility = Visibility.Visible;
        OpponentBattleAreaPanel.Children.Clear();

        if (_pendingHandCard != null && !_pendingIsEvolution && _pendingTargetsOpponent)
        {
            TxtOpponentFieldHeader.Text = $"🎯 [{_pendingHandCard.Name}] 발동 대기 중 👉 제거/무력화할 상대 디지몬을 클릭하세요!";
            TxtOpponentFieldHeader.Foreground = BrushFromHex("#A6E3A1");
        }
        else if (_pendingAttackerInstance != null)
        {
            TxtOpponentFieldHeader.Text = $"🎯 [{_pendingAttackerInstance.TopCard.Name}(DP {_pendingAttackerInstance.GetCurrentDP()})] 공격 대기 중 👉 타격할 상대 레스트(REST) 디지몬을 클릭하세요!";
            TxtOpponentFieldHeader.Foreground = BrushFromHex("#A6E3A1");
        }
        else
        {
            TxtOpponentFieldHeader.Text = _gameState.CurrentStage switch
            {
                PracticeStage.Stage1_Goldfish => "🎯 상대 배틀 에어리어 [1인 솔리테어 타임어택 — 방해 디지몬 없음]",
                PracticeStage.Stage2_BlockerDefense => "🛡️ 상대 배틀 에어리어 [블로커 수비진 — 옵션 또는 고DP로 돌파!]",
                _ => "👾 상대 AI 배틀 에어리어 [공식 룰 실전 대전 — 레스트 디지몬 직접 타격 가능!]"
            };
            TxtOpponentFieldHeader.Foreground = BrushFromHex("#FAB387");
        }

        TxtOpponentZonesSummary.Text = _gameState.CurrentStage == PracticeStage.Stage3_AiBattle
            ? $"AI 필드: {_gameState.DummyOpponent.BattleArea.Count}체 | AI 테이머: {_gameState.DummyOpponent.Tamers.Count}장 | AI 손패: {_gameState.DummyOpponent.Hand.Count}장 | AI 덱: {_gameState.DummyOpponent.MainDeck.Count}장"
            : $"상대 필드: {_gameState.DummyOpponent.BattleArea.Count}체 | 상대 트래시: {_gameState.DummyOpponent.Trash.Count}장";

        if (_gameState.DummyOpponent.BattleArea.Count == 0)
        {
            string emptyMsg = _gameState.CurrentStage == PracticeStage.Stage1_Goldfish
                ? "🎯 [1인 솔리테어 훈련 모드] 상대 필드가 비어 있습니다! 진화 체인을 완성해 좌측의 상대 시큐리티 5장을 빠르게 돌파하세요!"
                : "✅ 상대 배틀 에어리어가 비어 있습니다! 지금 바로 좌측 상대 시큐리티를 총공격하세요!";

            OpponentBattleAreaPanel.Children.Add(new Border
            {
                Background = BrushFromHex("#11111B"),
                BorderBrush = BrushFromHex("#313244"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(16, 12, 16, 12),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = emptyMsg,
                    Foreground = BrushFromHex("#A6E3A1"),
                    FontWeight = FontWeights.Bold,
                    FontSize = 12
                }
            });
            return;
        }

        foreach (var opp in _gameState.DummyOpponent.BattleArea.ToList())
        {
            bool isTargetForOption = _pendingHandCard != null && !_pendingIsEvolution && _pendingTargetsOpponent;
            bool isTargetForAttack = _pendingAttackerInstance != null && opp.IsSuspended;
            bool isHighlighted = isTargetForOption || isTargetForAttack;

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(66) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var thumb = CreateCardVisualFrame(
                opp.TopCard,
                width: 62,
                height: 84,
                customDpOrBadge: $"{opp.GetCurrentDP()}",
                isRested: opp.IsSuspended);
            Grid.SetColumn(thumb, 0);
            row.Children.Add(thumb);

            var infoStack = new StackPanel
            {
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            string statusText = opp.TopCard.IsBlocker && !opp.IsSuspended
                ? "🛡️ ≪블로커≫ 방어 대기!"
                : opp.IsSuspended
                    ? "↻ REST (직접 타격 가능!)"
                    : "⚡ ACTIVE";
            string statusColor = opp.TopCard.IsBlocker && !opp.IsSuspended
                ? "#F38BA8"
                : opp.IsSuspended
                    ? "#FAB387"
                    : "#89DCEB";

            infoStack.Children.Add(new TextBlock
            {
                Text = statusText,
                Foreground = BrushFromHex(statusColor),
                FontWeight = FontWeights.ExtraBold,
                FontSize = 11
            });

            infoStack.Children.Add(new TextBlock
            {
                Text = $"Lv.{opp.TopCard.Level} {opp.TopCard.Name}",
                Foreground = BrushFromHex("#F8FAFC"),
                FontWeight = FontWeights.Bold,
                FontSize = 12.5,
                Margin = new Thickness(0, 2, 0, 2)
            });

            infoStack.Children.Add(new TextBlock
            {
                Text = $"⚔️ DP {opp.GetCurrentDP():N0}",
                Foreground = BrushFromHex("#F9E2AF"),
                FontWeight = FontWeights.ExtraBold,
                FontSize = 11.5
            });

            if (isTargetForOption && _pendingHandCard != null)
            {
                var btnOptTarget = new Button
                {
                    Content = $"💥 [{_pendingHandCard.Name}] 적중!",
                    Background = BrushFromHex("#A6E3A1"),
                    Foreground = BrushFromHex("#11111B"),
                    FontWeight = FontWeights.ExtraBold,
                    FontSize = 10.5,
                    Padding = new Thickness(6, 3, 6, 3),
                    Margin = new Thickness(0, 4, 0, 0),
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                btnOptTarget.Click += (_, e) =>
                {
                    e.Handled = true;
                    ExecutePendingHandActionOnTarget(opp);
                };
                infoStack.Children.Add(btnOptTarget);
            }
            else if (_pendingAttackerInstance != null)
            {
                if (opp.IsSuspended)
                {
                    var btnHitTarget = new Button
                    {
                        Content = $"⚔️ [{_pendingAttackerInstance.TopCard.Name}]으로 격파!",
                        Background = BrushFromHex("#A6E3A1"),
                        Foreground = BrushFromHex("#11111B"),
                        FontWeight = FontWeights.ExtraBold,
                        FontSize = 10.5,
                        Padding = new Thickness(6, 3, 6, 3),
                        Margin = new Thickness(0, 4, 0, 0),
                        BorderThickness = new Thickness(0),
                        Cursor = System.Windows.Input.Cursors.Hand
                    };
                    btnHitTarget.Click += (_, e) =>
                    {
                        e.Handled = true;
                        var attacker = _pendingAttackerInstance;
                        _pendingAttackerInstance = null;
                        ApplyActionResult(_engine.AttackOpponentDigimon(attacker, opp));
                    };
                    infoStack.Children.Add(btnHitTarget);
                }
                else
                {
                    infoStack.Children.Add(new TextBlock
                    {
                        Text = "액티브 상태 (직접 공격 불가)",
                        Foreground = BrushFromHex("#6C7086"),
                        FontSize = 10,
                        Margin = new Thickness(0, 3, 0, 0)
                    });
                }
            }

            Grid.SetColumn(infoStack, 1);
            row.Children.Add(infoStack);

            var oppCardBorder = new Border
            {
                Width = 235,
                Tag = opp,
                Background = BrushFromHex(isHighlighted ? "#1B2E28" : "#141522"),
                BorderBrush = BrushFromHex(isHighlighted ? "#A6E3A1" : opp.TopCard.IsBlocker && !opp.IsSuspended ? "#F38BA8" : "#45475A"),
                BorderThickness = new Thickness(isHighlighted ? 2.5 : 1.5),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(7, 5, 7, 5),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = isHighlighted ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow,
                Child = row
            };

            oppCardBorder.MouseEnter += (_, _) =>
            {
                UpdateCardInspector(opp.TopCard);
                if (_pendingAttackerInstance != null)
                {
                    var check = ActionValidator.ValidateAttack(_gameState, _pendingAttackerInstance, opp);
                    ShowToast(check.CanExecute, check.CanExecute ? check.GuideMessage : check.Reason);
                }
            };

            oppCardBorder.MouseLeftButtonUp += (_, _) =>
            {
                if (isTargetForOption)
                {
                    ExecutePendingHandActionOnTarget(opp);
                }
                else if (_pendingAttackerInstance != null)
                {
                    var attacker = _pendingAttackerInstance;
                    _pendingAttackerInstance = null;
                    ApplyActionResult(_engine.AttackOpponentDigimon(attacker, opp));
                }
            };

            OpponentBattleAreaPanel.Children.Add(oppCardBorder);
        }
    }

    private void RenderBreedingArea()
    {
        TxtDigitamaCount.Text = $"디지타마 덱: {_gameState.Player.DigitamaDeck.Count}장";
        if (_gameState.Player.BreedingArea == null)
        {
            BreedingSlotControl.Content = new Border
            {
                Width = 175,
                Height = 230,
                Background = BrushFromHex("#11111B"),
                BorderBrush = BrushFromHex("#313244"),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 8, 0, 0),
                Child = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "🥚 DIGITAMA ZONE",
                            Foreground = BrushFromHex("#A6E3A1"),
                            FontWeight = FontWeights.ExtraBold,
                            FontSize = 13,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 0, 0, 8)
                        },
                        new TextBlock
                        {
                            Text = "육성 에어리어 비어 있음\n\n상단 [🥚 부화 (Lv.2)] 버튼을 눌러 디지타마 카드를 부화시키세요!",
                            Foreground = BrushFromHex("#A6ADC8"),
                            FontSize = 11.5,
                            TextAlignment = TextAlignment.Center,
                            TextWrapping = TextWrapping.Wrap
                        }
                    }
                }
            };
        }
        else
        {
            BreedingSlotControl.Content = CreateFieldDigimonWidget(_gameState.Player.BreedingArea, isBreedingArea: true);
        }
    }

    private void RenderBattleArea()
    {
        BattleAreaPanel.Children.Clear();
        if (_gameState.Player.BattleArea.Count == 0)
        {
            BattleAreaPanel.Children.Add(new Border
            {
                Background = BrushFromHex("#11111B"),
                BorderBrush = BrushFromHex("#313244"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(24, 18, 24, 18),
                Margin = new Thickness(8),
                Child = new TextBlock
                {
                    Text = "배틀 에어리어에 디지몬이 없습니다.\n육성 에어리어에서 성장한 디지몬을 이동시키거나, 아래 패(Hand)에서 디지몬을 등장시키세요.",
                    Foreground = BrushFromHex("#6C7086"),
                    FontSize = 12.5,
                    TextAlignment = TextAlignment.Center
                }
            });
        }
        else
        {
            foreach (var instance in _gameState.Player.BattleArea)
            {
                BattleAreaPanel.Children.Add(CreateFieldDigimonWidget(instance, isBreedingArea: false));
            }
        }

        if (_pendingHandCard != null)
        {
            TxtSelectedTarget.Text = _pendingIsEvolution
                ? $"🎯 [{_pendingHandCard.Name}(Lv.{_pendingHandCard.Level})] 진화 대기 중 👉 내 필드에서 Lv.{_pendingHandCard.EvoRequirementLevel} 디지몬을 클릭하세요!"
                : _pendingTargetsOpponent
                    ? $"🎯 [{_pendingHandCard.Name}] 사용 대기 중 👉 상단 상대 배틀 에어리어에서 상대 디지몬을 클릭하세요!"
                    : $"🎯 [{_pendingHandCard.Name}] 사용 대기 중 👉 내 배틀 에어리어에서 아군 디지몬을 클릭하세요!";
            TxtSelectedTarget.Foreground = BrushFromHex("#A6E3A1");
            BtnCancelPendingAction.Visibility = Visibility.Visible;
        }
        else if (_pendingAttackerInstance != null)
        {
            TxtSelectedTarget.Text = $"🎯 [{_pendingAttackerInstance.TopCard.Name}] 공격 대기 중 👉 상단 상대 배틀 에어리어에서 레스트(REST) 디지몬을 클릭하세요!";
            TxtSelectedTarget.Foreground = BrushFromHex("#A6E3A1");
            BtnCancelPendingAction.Visibility = Visibility.Visible;
        }
        else
        {
            TxtSelectedTarget.Text = "패에서 [🧬진화] 또는 [✨사용]을 누른 뒤 필드의 디지몬을 선택하세요";
            TxtSelectedTarget.Foreground = BrushFromHex("#F9E2AF");
            BtnCancelPendingAction.Visibility = Visibility.Collapsed;
        }

        // 필드 테이머 미니 카드 칩 렌더링 (지속 DP 버프 & 시큐리티 추가공격 & 메모리 효과 명시)
        TamerZonePanel.Children.Clear();
        TamerZonePanel.Children.Add(new TextBlock
        {
            Text = "🎖️ 필드 테이머 지속 효과: ",
            Foreground = BrushFromHex("#CBA6F7"),
            FontWeight = FontWeights.ExtraBold,
            FontSize = 11.5,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        });

        if (_gameState.Player.Tamers.Count == 0)
        {
            TamerZonePanel.Children.Add(new TextBlock
            {
                Text = "없음 (패에서 신태일/매튜를 등장시키면 아군 디지몬 DP 증가 & 턴 시작 메모리가 보정됩니다)",
                Foreground = BrushFromHex("#6C7086"),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        else
        {
            foreach (var group in _gameState.Player.Tamers.GroupBy(t => t.CardId))
            {
                var tamer = group.First();
                int count = group.Count();
                string auraDesc = tamer.CardId == "ST1-12" || tamer.Color == CardColor.Red
                    ? $"아군 전체 DP +{count * 1000:N0} | 진화원 4장↑ 시큐어택 +{count} | 턴 시작 최소 3메모리"
                    : $"진화 디지몬 DP +{count * 1000:N0} | 턴 시작 메모리 +{count} 추가";

                var tamerChip = new Border
                {
                    Background = GetCardColorBrush(tamer.Color),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 2, 8, 2),
                    Margin = new Thickness(0, 0, 6, 0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Child = new TextBlock
                    {
                        Text = $"★ {tamer.Name} x{count} [{auraDesc}]",
                        Foreground = BrushFromHex("#11111B"),
                        FontWeight = FontWeights.ExtraBold,
                        FontSize = 11
                    }
                };
                tamerChip.MouseEnter += (_, _) => UpdateCardInspector(tamer);
                TamerZonePanel.Children.Add(tamerChip);
            }
        }

        string myLastSecInfo = _gameState.PlayerSecurityCheckHistory.Count > 0
            ? $"최근 오픈: [{_gameState.PlayerSecurityCheckHistory[^1].SecurityCard.Name}] → {_gameState.PlayerSecurityCheckHistory[^1].OutcomeSummary}"
            : "50장 메인 덱 맨 위 5장 무작위 배치됨";

        TxtPlayerZonesSummary.Text =
            $"메인 덱: {_gameState.Player.MainDeck.Count}장 | 🛡️ 내 시큐리티: {_gameState.Player.SecurityStack.Count}/5장 | 트래시: {_gameState.Player.Trash.Count}장";

        // 1) 플레이매트 좌측 상단: 내 시큐리티 5장 미니 카드 스택 렌더링
        TxtMySecurityCountBadge.Text = $"{_gameState.Player.SecurityStack.Count} / 5장";
        TxtMySecurityLastEvent.Text = myLastSecInfo;
        TxtMySecurityLastEvent.Foreground = BrushFromHex(_gameState.PlayerSecurityCheckHistory.Count > 0 ? "#F9E2AF" : "#A6ADC8");

        MySecurityVisualStack.Children.Clear();
        int myClearedCount = _gameState.PlayerSecurityCheckHistory.Count;
        int myRemainingCount = _gameState.Player.SecurityStack.Count;
        int myTotalSlots = Math.Max(5, myClearedCount + myRemainingCount);

        for (int i = 0; i < 5; i++)
        {
            if (i < myRemainingCount)
            {
                var activeSecBox = new Border
                {
                    Background = new LinearGradientBrush(
                        (Color)ColorConverter.ConvertFromString("#1E3A8A"),
                        (Color)ColorConverter.ConvertFromString("#0F172A"),
                        35.0),
                    BorderBrush = BrushFromHex("#38BDF8"),
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(1, 0, 1, 0),
                    Child = new TextBlock
                    {
                        Text = $"🛡️#{i + 1}",
                        Foreground = BrushFromHex("#38BDF8"),
                        FontWeight = FontWeights.ExtraBold,
                        FontSize = 9.5,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                MySecurityVisualStack.Children.Add(activeSecBox);
            }
            else
            {
                int histIdx = i - myRemainingCount;
                var histItem = histIdx >= 0 && histIdx < _gameState.PlayerSecurityCheckHistory.Count
                    ? _gameState.PlayerSecurityCheckHistory[histIdx]
                    : null;

                var clearedSecBox = new Border
                {
                    Background = BrushFromHex("#181825"),
                    BorderBrush = BrushFromHex(histItem != null && histItem.IsBattleWon ? "#A6E3A1" : "#45475A"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(1, 0, 1, 0),
                    Cursor = histItem != null ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow,
                    Child = new TextBlock
                    {
                        Text = histItem != null ? (histItem.IsBattleWon ? "💥승" : "💔파") : "빈칸",
                        Foreground = BrushFromHex(histItem != null && histItem.IsBattleWon ? "#A6E3A1" : "#6C7086"),
                        FontWeight = FontWeights.Bold,
                        FontSize = 9.5,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                if (histItem != null)
                {
                    var capturedCard = histItem.SecurityCard;
                    clearedSecBox.MouseEnter += (_, _) => UpdateCardInspector(capturedCard);
                }
                MySecurityVisualStack.Children.Add(clearedSecBox);
            }
        }

        // 2) 플레이매트 우측: 메인 덱 장수 및 트래시(묘지) 맨 위 카드 렌더링
        TxtMainDeckVisualCount.Text = $"{_gameState.Player.MainDeck.Count}장";
        TxtTrashVisualCount.Text = $"{_gameState.Player.Trash.Count}장";

        if (_gameState.Player.Trash.Count > 0)
        {
            var topTrash = _gameState.Player.Trash[^1];
            var trashThumb = CreateCardVisualFrame(topTrash, width: 72, height: 96, customDpOrBadge: "TRASH");
            TrashTopCardControl.Content = trashThumb;
        }
        else
        {
            TrashTopCardControl.Content = new Border
            {
                Width = 72,
                Height = 96,
                CornerRadius = new CornerRadius(6),
                Background = BrushFromHex("#0F101A"),
                BorderBrush = BrushFromHex("#313244"),
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = "비어 있음",
                    Foreground = BrushFromHex("#6C7086"),
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
        }
    }

    private UIElement CreateFieldDigimonWidget(CardInstance instance, bool isBreedingArea)
    {
        bool isSummoningSick = !isBreedingArea && instance.TurnPlayed == _gameState.CurrentTurn;

        // 현재 패에서 선택된 카드(_pendingHandCard)의 유효한 아군 필드 대상인지 판별
        bool isPendingAllyTargetMode = _pendingHandCard != null && !_pendingTargetsOpponent;
        bool isValidPendingTarget = false;
        string invalidTargetReason = string.Empty;

        if (isPendingAllyTargetMode && _pendingHandCard != null)
        {
            if (_pendingIsEvolution)
            {
                var check = ActionValidator.ValidateEvolution(_gameState, instance, _pendingHandCard);
                isValidPendingTarget = check.CanExecute;
                invalidTargetReason = check.Reason;
            }
            else
            {
                if (isBreedingArea)
                {
                    isValidPendingTarget = false;
                    invalidTargetReason = "육성 에어리어의 디지몬은 옵션 효과의 대상으로 지정할 수 없습니다.";
                }
                else if (_pendingHandCard.CardId == "ST2-15" && instance.EvolutionSources.Count == 0)
                {
                    isValidPendingTarget = false;
                    invalidTargetReason = "카이저 네일은 진화원이 1장 이상 있는 디지몬에게만 사용할 수 있습니다.";
                }
                else
                {
                    isValidPendingTarget = true;
                }
            }
        }

        var stack = new StackPanel();

        // 상단 상태 표시줄
        var statusDock = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };
        string stateText = isBreedingArea
            ? "🛡️ 안전지대"
            : instance.IsSuspended ? "↻ REST" : isSummoningSick ? "⏳ 소환 멀미" : "⚡ ACTIVE";
        if (!isBreedingArea && instance.TopCard.IsBlocker)
        {
            stateText += " 🛡️블로커";
        }
        string stateColor = isBreedingArea
            ? "#A6E3A1"
            : instance.IsSuspended ? "#FAB387" : isSummoningSick ? "#F9E2AF" : "#A6E3A1";

        statusDock.Children.Add(new TextBlock
        {
            Text = stateText,
            Foreground = BrushFromHex(stateColor),
            FontWeight = FontWeights.ExtraBold,
            FontSize = 11
        });

        if (instance.TopCard.Type == CardType.Digimon)
        {
            int secChecks = instance.GetSecurityAttackCount();
            statusDock.Children.Add(new Border
            {
                Background = BrushFromHex(secChecks >= 2 ? "#F38BA8" : "#313244"),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(5, 1, 5, 1),
                HorizontalAlignment = HorizontalAlignment.Right,
                Child = new TextBlock
                {
                    Text = secChecks >= 2 ? $"💥 시큐 {secChecks}장 타격!" : $"시큐 1장",
                    Foreground = BrushFromHex(secChecks >= 2 ? "#11111B" : "#CDD6F4"),
                    FontWeight = FontWeights.ExtraBold,
                    FontSize = 10.5
                }
            });
        }
        stack.Children.Add(statusDock);

        // 실물 최상단 카드 아트워크 렌더링
        int totalBonusDp = instance.GetTotalBonusDP();
        string dpBadge = instance.TopCard.Type == CardType.Digimon
            ? totalBonusDp > 0
                ? $"{instance.GetCurrentDP():N0} DP (▲+{totalBonusDp:N0})"
                : $"{instance.GetCurrentDP():N0} DP"
            : $"Lv.{instance.TopCard.Level}";

        var cardArt = CreateCardVisualFrame(
            instance.TopCard,
            width: 162,
            height: 218,
            customDpOrBadge: dpBadge,
            isRested: instance.IsSuspended);
        cardArt.HorizontalAlignment = HorizontalAlignment.Center;
        stack.Children.Add(cardArt);

        // ==================== 📊 전투력(DP) & 시큐리티 추가공격권 상세 전광판 ====================
        if (instance.TopCard.Type == CardType.Digimon)
        {
            int baseDp = instance.TopCard.DP;
            int inhDp = instance.GetInheritedDpBonus();
            int tamerDp = instance.TamerDpBonus;
            int skillDp = instance.TemporaryDpBonus;
            int currentDp = instance.GetCurrentDP();

            int secCount = instance.GetSecurityAttackCount();
            int bonusSec = instance.GetTotalBonusSecurityAttack();

            var statHudStack = new StackPanel { Margin = new Thickness(0, 6, 0, 2) };

            // 1) DP 증가 상세 박스
            var dpBreakdownParts = new List<string> { $"기본 {baseDp:N0}" };
            if (inhDp > 0) dpBreakdownParts.Add($"🧬진화원 +{inhDp:N0}");
            if (tamerDp > 0) dpBreakdownParts.Add($"🎖️테이머 +{tamerDp:N0}");
            if (skillDp > 0) dpBreakdownParts.Add($"✨스킬 +{skillDp:N0}");

            var dpBox = new Border
            {
                Background = BrushFromHex(totalBonusDp > 0 ? "#162C26" : "#181926"),
                BorderBrush = BrushFromHex(totalBonusDp > 0 ? "#A6E3A1" : "#313244"),
                BorderThickness = new Thickness(totalBonusDp > 0 ? 1.5 : 1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 0, 0, 4),
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = totalBonusDp > 0
                                ? $"⚔️ 총 DP: {currentDp:N0} (▲ +{totalBonusDp:N0} 강화!)"
                                : $"⚔️ 총 DP: {currentDp:N0} (기본)",
                            Foreground = BrushFromHex(totalBonusDp > 0 ? "#A6E3A1" : "#F8FAFC"),
                            FontWeight = FontWeights.ExtraBold,
                            FontSize = 11.5
                        },
                        new TextBlock
                        {
                            Text = string.Join(" | ", dpBreakdownParts),
                            Foreground = BrushFromHex(totalBonusDp > 0 ? "#F9E2AF" : "#9399B2"),
                            FontWeight = FontWeights.SemiBold,
                            FontSize = 10,
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 1, 0, 0)
                        }
                    }
                }
            };
            statHudStack.Children.Add(dpBox);

            // 2) 시큐리티 추가공격권 상세 박스
            var secBreakdownParts = new List<string> { "기본 1장" };
            if (instance.TopCard.SecurityAttackBonus > 0) secBreakdownParts.Add($"🔥고유 +{instance.TopCard.SecurityAttackBonus}");
            if (instance.GetInheritedSecurityAttackBonus() > 0) secBreakdownParts.Add($"🧬진화원 +{instance.GetInheritedSecurityAttackBonus()}");
            if (instance.TamerSecurityAttackBonus > 0) secBreakdownParts.Add($"🎖️테이머 +{instance.TamerSecurityAttackBonus}");
            if (instance.TemporarySecurityAttackBonus > 0) secBreakdownParts.Add($"✨스킬 +{instance.TemporarySecurityAttackBonus}");

            string shieldIcons = string.Concat(Enumerable.Repeat("💥", Math.Min(secCount, 5)));
            var secBox = new Border
            {
                Background = BrushFromHex(bonusSec > 0 ? "#3B1D2E" : "#181926"),
                BorderBrush = BrushFromHex(bonusSec > 0 ? "#F9E2AF" : "#313244"),
                BorderThickness = new Thickness(bonusSec > 0 ? 1.5 : 1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 0, 0, 3),
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = bonusSec > 0
                                ? $"{shieldIcons} 시큐리티 타격: {secCount}장! (추가공격 +{bonusSec})"
                                : "🛡️ 시큐리티 타격: 1장 (기본 1회 체크)",
                            Foreground = BrushFromHex(bonusSec > 0 ? "#F9E2AF" : "#CDD6F4"),
                            FontWeight = FontWeights.ExtraBold,
                            FontSize = 11
                        },
                        new TextBlock
                        {
                            Text = string.Join(" | ", secBreakdownParts),
                            Foreground = BrushFromHex(bonusSec > 0 ? "#F38BA8" : "#9399B2"),
                            FontWeight = FontWeights.SemiBold,
                            FontSize = 10,
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 1, 0, 0)
                        }
                    }
                }
            };
            statHudStack.Children.Add(secBox);

            // 3) 현재 적용된 스킬/테이머/고유 어빌리티 칩 표시
            var activeBuffLines = new List<string>();
            activeBuffLines.AddRange(instance.ActiveSkillBuffLabels);
            if (instance.TamerDpBonus > 0 || instance.TamerSecurityAttackBonus > 0)
            {
                activeBuffLines.Add($"🎖️ 테이머 버프: DP +{instance.TamerDpBonus:N0}" +
                                    (instance.TamerSecurityAttackBonus > 0 ? $" / 시큐 +{instance.TamerSecurityAttackBonus}" : ""));
            }
            if (instance.TopCard.CardId == "ST2-11" && !instance.HasUsedOncePerTurnEffect)
            {
                activeBuffLines.Add("⚡ [어택 시] 스스로 액티브 재기상 (2회 연속공격!)");
            }

            foreach (var buffLabel in activeBuffLines)
            {
                statHudStack.Children.Add(new Border
                {
                    Background = BrushFromHex("#2A213E"),
                    BorderBrush = BrushFromHex("#CBA6F7"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(5, 2, 5, 2),
                    Margin = new Thickness(0, 1, 0, 1),
                    Child = new TextBlock
                    {
                        Text = buffLabel,
                        Foreground = BrushFromHex("#F8FAFC"),
                        FontWeight = FontWeights.Bold,
                        FontSize = 10,
                        TextWrapping = TextWrapping.Wrap
                    }
                });
            }

            stack.Children.Add(statHudStack);
        }

        // 아래에 겹쳐진 진화원 미니 카드 스트립 스택 시각화
        var evoSources = instance.GetEvolutionStackBottomUp();
        if (evoSources.Count > 0)
        {
            var evoContainer = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
            evoContainer.Children.Add(new TextBlock
            {
                Text = $"🧬 진화원 스택 ({evoSources.Count}장):",
                Foreground = BrushFromHex("#89DCEB"),
                FontWeight = FontWeights.Bold,
                FontSize = 10.5,
                Margin = new Thickness(0, 0, 0, 2)
            });

            foreach (var src in evoSources)
            {
                var srcRow = new DockPanel();
                var srcBmp = GetCardBitmap(src);
                if (srcBmp != null)
                {
                    srcRow.Children.Add(new Border
                    {
                        Width = 22,
                        Height = 28,
                        CornerRadius = new CornerRadius(2),
                        Margin = new Thickness(0, 0, 5, 0),
                        ClipToBounds = true,
                        Child = new Image { Source = srcBmp, Stretch = Stretch.UniformToFill }
                    });
                }

                string srcBonusBadge = src.InheritedSecurityAttackBonus > 0
                    ? $" [💥시큐어택 +{src.InheritedSecurityAttackBonus}]"
                    : src.InheritedDpBonus > 0
                        ? $" [▲DP +{src.InheritedDpBonus:N0}]"
                        : " (진화원)";

                srcRow.Children.Add(new TextBlock
                {
                    Text = $"Lv.{src.Level} {src.Name}{srcBonusBadge}",
                    Foreground = BrushFromHex(src.InheritedSecurityAttackBonus > 0 ? "#F9E2AF" : src.InheritedDpBonus > 0 ? "#A6E3A1" : "#F8FAFC"),
                    FontWeight = FontWeights.Bold,
                    FontSize = 10.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });

                var strip = new Border
                {
                    Background = BrushFromHex("#232438"),
                    BorderBrush = GetCardColorBrush(src.Color),
                    BorderThickness = new Thickness(3, 0, 0, 0),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(4, 2, 4, 2),
                    Margin = new Thickness(0, 1, 0, 1),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Child = srcRow
                };
                strip.MouseEnter += (_, _) => UpdateCardInspector(src);
                evoContainer.Children.Add(strip);
            }
            stack.Children.Add(evoContainer);
        }

        // 패에서 카드를 먼저 선택한 상태(_pendingHandCard != null)일 때 필드 대상 선택 버튼 표시
        if (isPendingAllyTargetMode && _pendingHandCard != null)
        {
            if (isValidPendingTarget)
            {
                var btnConfirmTarget = new Button
                {
                    Content = _pendingIsEvolution
                        ? $"🧬 여기에 [{_pendingHandCard.Name}] 진화!"
                        : $"✨ 여기에 [{_pendingHandCard.Name}] 적용!",
                    Background = BrushFromHex("#A6E3A1"),
                    Foreground = BrushFromHex("#11111B"),
                    FontWeight = FontWeights.ExtraBold,
                    FontSize = 11.5,
                    Padding = new Thickness(6, 6, 6, 6),
                    Margin = new Thickness(0, 5, 0, 3),
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                btnConfirmTarget.Click += (_, e) =>
                {
                    e.Handled = true;
                    ExecutePendingHandActionOnTarget(instance);
                };
                stack.Children.Add(btnConfirmTarget);
            }
            else
            {
                stack.Children.Add(new Border
                {
                    Background = BrushFromHex("#232438"),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(4, 3, 4, 3),
                    Margin = new Thickness(0, 5, 0, 3),
                    Child = new TextBlock
                    {
                        Text = _pendingIsEvolution
                            ? $"대상 불가 (Lv.{_pendingHandCard.EvoRequirementLevel} 필요)"
                            : "대상 불가",
                        Foreground = BrushFromHex("#6C7086"),
                        FontSize = 10.5,
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                });
            }
        }

        // 일반 상태에서 배틀 에어리어 디지몬의 공격 버튼 (시큐리티 어택 + 상대 레스트 디지몬 타격)
        if (!isBreedingArea && _pendingHandCard == null)
        {
            int checks = instance.GetSecurityAttackCount();
            var btnAttack = new Button
            {
                Content = _gameState.DummyOpponent.SecurityStack.Count > 0
                    ? $"⚔️ 시큐리티 어택 ({checks}장 타격!)"
                    : "🏆 다이렉트 어택 (게임 승리!)",
                Background = BrushFromHex("#F38BA8"),
                Foreground = BrushFromHex("#11111B"),
                FontWeight = FontWeights.ExtraBold,
                FontSize = 11,
                Padding = new Thickness(5, 5, 5, 5),
                Margin = new Thickness(0, 5, 0, 0),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnAttack.MouseEnter += (_, _) =>
            {
                UpdateCardInspector(instance.TopCard);
                var check = ActionValidator.ValidateAttack(_gameState, instance, null);
                ShowToast(check.CanExecute, check.CanExecute ? check.GuideMessage : check.Reason);
            };
            btnAttack.Click += (_, _) =>
            {
                AutoPassBreedingIfNeeded();
                _pendingAttackerInstance = null;
                var res = _engine.AttackSecurityOrDirect(instance);
                if (!res.CanExecute)
                {
                    ApplyActionResult(res);
                }
                else
                {
                    if (_gameState.SecurityCheckHistory.Count > 0)
                    {
                        UpdateCardInspector(_gameState.SecurityCheckHistory[^1].SecurityCard);
                    }
                    ShowToast(_gameState.LastSecurityBattleWon || _gameState.IsVictory, res.GuideMessage);
                    if (!_gameState.LastSecurityBattleWon)
                    {
                        ToastBadge.Text = "💀 배틀 패배 (소멸)";
                    }
                    RefreshAllSimulatorUi();
                }
            };
            stack.Children.Add(btnAttack);

            // 상대 필드에 레스트 상태인 디지몬이 있으면 [🎯 상대 디지몬 타격] 버튼 표시 (내 카드 먼저 클릭 -> 상대 디지몬 클릭)
            if (_gameState.DummyOpponent.BattleArea.Any(d => d.IsSuspended))
            {
                bool isThisAttackerSelected = ReferenceEquals(_pendingAttackerInstance, instance);
                var btnAttackDigimon = new Button
                {
                    Content = isThisAttackerSelected ? "❌ 타격 선택 취소" : "🎯 상대 디지몬 타격",
                    Background = BrushFromHex(isThisAttackerSelected ? "#FAB387" : "#89DCEB"),
                    Foreground = BrushFromHex("#11111B"),
                    FontWeight = FontWeights.ExtraBold,
                    FontSize = 10.5,
                    Padding = new Thickness(5, 4, 5, 4),
                    Margin = new Thickness(0, 4, 0, 0),
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                btnAttackDigimon.Click += (_, _) =>
                {
                    if (isThisAttackerSelected)
                    {
                        _pendingAttackerInstance = null;
                        ShowToast(true, "상대 디지몬 타격 대상 선택을 취소했습니다.");
                        RefreshAllSimulatorUi();
                        return;
                    }

                    AutoPassBreedingIfNeeded();
                    var firstRested = _gameState.DummyOpponent.BattleArea.FirstOrDefault(d => d.IsSuspended);
                    var check = ActionValidator.ValidateAttack(_gameState, instance, firstRested);
                    if (!check.CanExecute)
                    {
                        ShowToast(false, check.Reason);
                        return;
                    }

                    _pendingAttackerInstance = instance;
                    ShowToast(true, $"🎯 [{instance.TopCard.Name}(DP {instance.GetCurrentDP()})] 공격 준비 완료! 상단 상대 배틀 에어리어에서 타격할 레스트 디지몬을 클릭하세요.");
                    RefreshAllSimulatorUi();
                };
                stack.Children.Add(btnAttackDigimon);
            }
        }

        var fieldCardBorder = new Border
        {
            Width = isBreedingArea ? 206 : 210,
            Tag = instance,
            Background = BrushFromHex(isValidPendingTarget ? "#1B2E28" : "#11111B"),
            BorderBrush = BrushFromHex(isValidPendingTarget ? "#A6E3A1" : "#45475A"),
            BorderThickness = new Thickness(isValidPendingTarget ? 3 : 1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 10, 8),
            Opacity = isPendingAllyTargetMode && !isValidPendingTarget ? 0.55 : 1.0,
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = stack
        };

        // 배틀 에어리어 디지몬을 마우스로 드래그해서 상대 시큐리티나 상대 레스트 디지몬에게 드롭하면 즉시 공격!
        // 육성 에어리어 디지몬을 배틀 에어리어로 드래그하면 즉시 배틀 이동!
        fieldCardBorder.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (!_gameState.IsGameOver && !isPendingAllyTargetMode)
            {
                _dragStartPoint = e.GetPosition(this);
                _potentialDragHandCard = null;
                if (isBreedingArea)
                {
                    _potentialDragBreedingDigimon = instance;
                    _potentialDragFieldAttacker = null;
                }
                else
                {
                    _potentialDragFieldAttacker = instance;
                    _potentialDragBreedingDigimon = null;
                }
            }
        };

        // 패에서 카드 사용을 누른 상태에서는 필드 디지몬 카드 자체를 클릭해도 즉시 진화/적용 실행!
        fieldCardBorder.MouseLeftButtonUp += (_, _) =>
        {
            if (isPendingAllyTargetMode && _pendingHandCard != null)
            {
                if (isValidPendingTarget)
                {
                    ExecutePendingHandActionOnTarget(instance);
                }
                else
                {
                    ShowToast(false, invalidTargetReason);
                }
            }
        };

        return fieldCardBorder;
    }

    private void ExecutePendingHandActionOnTarget(CardInstance target)
    {
        if (_pendingHandCard == null)
            return;

        AutoPassBreedingIfNeeded();
        var cardToUse = _pendingHandCard;
        bool isEvo = _pendingIsEvolution;
        _pendingHandCard = null;
        _pendingTargetsOpponent = false;

        var res = isEvo
            ? _engine.EvolveDigimon(target, cardToUse)
            : _engine.PlayCard(cardToUse, target);

        ApplyActionResult(res);
    }

    private static (string Text, string ColorHex) GetHandCardQuickEffectSummary(CardData card)
    {
        return card.CardId switch
        {
            "ST1-12" => ("🎖️ 전체 DP+1000 & 진화4장↑ 시큐+1", "#F9E2AF"),
            "ST2-12" => ("🎖️ 진화아군 DP+1000 & 메모리+1", "#89DCEB"),
            "ST1-13" => ("✨ [옵션] 아군 1체 DP +3,000", "#A6E3A1"),
            "ST1-14" => ("💥 [옵션] DP +2,000 & 시큐어택 +1", "#F9E2AF"),
            "ST1-15" => ("🔥 [옵션] 상대 DP 4000↓ 2체 소멸", "#F38BA8"),
            "ST1-16" => ("🔥 [옵션] 상대 디지몬 1체 즉시 소멸", "#F38BA8"),
            "ST2-13" => ("⚡ [옵션] 공짜 메모리 +2 획득!", "#89DCEB"),
            "ST2-14" => ("❄️ [옵션] 상대 블로커 강제 레스트", "#89DCEB"),
            "ST2-15" => ("❄️ [옵션] 내 진화원 1장 공짜 등장", "#A6E3A1"),
            "ST2-16" => ("🌊 [옵션] 상대 디지몬 패로 바운스", "#89B4FA"),
            "ST1-07" or "ST2-08" => ("🧬 [진화원 효과] 💥시큐리티 어택 +1", "#F9E2AF"),
            "ST1-08" => ("🔥 [진화시] DP+3000 / 🧬진화원 DP+1000", "#A6E3A1"),
            "ST1-09" => ("🧬 [진화원 효과] ▲DP +2,000", "#A6E3A1"),
            "ST1-11" => ("💥 시큐어택+1 & [진화시] 추가 시큐+1", "#F9E2AF"),
            "ST2-11" => ("⚡ 시큐어택+1 & [어택시] 재기상 2연타", "#89DCEB"),
            "BT1-084" => ("👑 [Lv.7] DP 15,000 & 시큐어택 +1", "#F9E2AF"),
            _ when card.IsBlocker => ("🛡️ ≪블로커≫ 상대 공격 방어", "#FAB387"),
            _ when card.InheritedDpBonus > 0 => ($"🧬 [진화원 효과] ▲DP +{card.InheritedDpBonus:N0}", "#A6E3A1"),
            _ => ("⚔️ 기본 전투 디지몬", "#9399B2")
        };
    }

    private void RenderHand()
    {
        HandPanel.Children.Clear();
        TxtHandHeader.Text = $"🃏 내 손패 (Hand: {_gameState.Player.Hand.Count}장)";

        foreach (var card in _gameState.Player.Hand.ToList())
        {
            bool isThisCardPending = ReferenceEquals(_pendingHandCard, card);
            var cardStack = new StackPanel();

            // 실물 카드 디자인 프레임 (132 x 162)
            var visualCard = CreateCardVisualFrame(card, width: 132, height: 162);
            cardStack.Children.Add(visualCard);

            // 패 카드 하단 핵심 효과(DP증가/시큐리티 추가공격/테이머/옵션) 한줄 요약 배지
            var (effText, effColor) = GetHandCardQuickEffectSummary(card);
            cardStack.Children.Add(new Border
            {
                Background = BrushFromHex("#1E1E2E"),
                BorderBrush = BrushFromHex("#313244"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4, 2, 4, 2),
                Margin = new Thickness(0, 4, 0, 0),
                Child = new TextBlock
                {
                    Text = effText,
                    Foreground = BrushFromHex(effColor),
                    FontWeight = FontWeights.ExtraBold,
                    FontSize = 9.5,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                }
            });

            // 하단 [등장/사용] / [진화] 버튼
            var btnGrid = new UniformGrid
            {
                Rows = 1,
                Columns = card.Type == CardType.Digimon ? 2 : 1,
                Margin = new Thickness(0, 4, 0, 0)
            };

            bool isAllyTargetedOption = card.Type == CardType.Option && card.CardId is "ST1-13" or "ST1-14" or "ST2-15";
            bool isEnemyTargetedOption = card.Type == CardType.Option && card.CardId is "ST1-15" or "ST1-16" or "ST2-14" or "ST2-16";

            var btnPlay = new Button
            {
                Content = isThisCardPending && !_pendingIsEvolution
                    ? "❌취소"
                    : card.Type == CardType.Option ? $"✨사용({card.PlayCost})" : $"✨등장({card.PlayCost})",
                Height = 26,
                Background = BrushFromHex(isThisCardPending && !_pendingIsEvolution ? "#F38BA8" : "#89B4FA"),
                Foreground = BrushFromHex("#11111B"),
                FontWeight = FontWeights.ExtraBold,
                FontSize = 10.5,
                Padding = new Thickness(3, 3, 3, 3),
                Margin = new Thickness(0, 0, 2, 0),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnPlay.MouseEnter += (_, _) =>
            {
                UpdateCardInspector(card);
                var check = ActionValidator.ValidatePlay(_gameState, card);
                ShowToast(check.CanExecute, check.CanExecute ? check.GuideMessage : check.Reason);
            };
            btnPlay.Click += (_, _) =>
            {
                if (isThisCardPending && !_pendingIsEvolution)
                {
                    _pendingHandCard = null;
                    _pendingTargetsOpponent = false;
                    ShowToast(true, $"[{card.Name}] 사용 대상 선택을 취소했습니다.");
                    RefreshAllSimulatorUi();
                    return;
                }

                AutoPassBreedingIfNeeded();

                if (isAllyTargetedOption)
                {
                    var playCheck = ActionValidator.ValidatePlay(_gameState, card);
                    if (!playCheck.CanExecute)
                    {
                        ShowToast(false, playCheck.Reason);
                        return;
                    }
                    if (_gameState.Player.BattleArea.Count == 0)
                    {
                        ShowToast(false, $"배틀 에어리어에 [{card.Name}] 효과를 적용할 아군 디지몬이 없습니다.");
                        return;
                    }

                    _pendingAttackerInstance = null;
                    _pendingHandCard = card;
                    _pendingIsEvolution = false;
                    _pendingTargetsOpponent = false;
                    ShowToast(true, $"🎯 [{card.Name}] 사용을 선택했습니다! 내 배틀 에어리어에서 효과를 받을 아군 디지몬을 클릭하세요.");
                    RefreshAllSimulatorUi();
                    return;
                }

                if (isEnemyTargetedOption)
                {
                    var playCheck = ActionValidator.ValidatePlay(_gameState, card);
                    if (!playCheck.CanExecute)
                    {
                        ShowToast(false, playCheck.Reason);
                        return;
                    }
                    if (_gameState.DummyOpponent.BattleArea.Count == 0)
                    {
                        ShowToast(false, $"상대 배틀 에어리어에 [{card.Name}] 효과를 적용할 상대 디지몬이 없습니다.");
                        return;
                    }

                    _pendingAttackerInstance = null;
                    _pendingHandCard = card;
                    _pendingIsEvolution = false;
                    _pendingTargetsOpponent = true;
                    ShowToast(true, $"🎯 [{card.Name}] 사용을 선택했습니다! 상단 상대 배틀 에어리어에서 제거/무력화할 상대 디지몬을 클릭하세요.");
                    RefreshAllSimulatorUi();
                    return;
                }

                _pendingAttackerInstance = null;
                _pendingHandCard = null;
                _pendingTargetsOpponent = false;
                var res = _engine.PlayCard(card);
                ApplyActionResult(res);
            };
            btnGrid.Children.Add(btnPlay);

            if (card.Type == CardType.Digimon)
            {
                var btnEvo = new Button
                {
                    Content = isThisCardPending && _pendingIsEvolution ? "❌취소" : $"🧬진화({card.EvoCost})",
                    Height = 26,
                    Background = BrushFromHex(isThisCardPending && _pendingIsEvolution ? "#F38BA8" : "#A6E3A1"),
                    Foreground = BrushFromHex("#11111B"),
                    FontWeight = FontWeights.ExtraBold,
                    FontSize = 10.5,
                    Padding = new Thickness(3, 3, 3, 3),
                    Margin = new Thickness(2, 0, 0, 0),
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                btnEvo.MouseEnter += (_, _) =>
                {
                    UpdateCardInspector(card);
                    var allFieldTargets = new List<CardInstance>();
                    if (_gameState.Player.BreedingArea != null) allFieldTargets.Add(_gameState.Player.BreedingArea);
                    allFieldTargets.AddRange(_gameState.Player.BattleArea);

                    var firstValid = allFieldTargets.FirstOrDefault(t => ActionValidator.ValidateEvolution(_gameState, t, card).CanExecute);
                    if (firstValid != null)
                    {
                        var check = ActionValidator.ValidateEvolution(_gameState, firstValid, card);
                        ShowToast(true, $"[{card.Name}] {check.GuideMessage} (클릭 후 필드의 디지몬을 선택하세요)");
                    }
                    else
                    {
                        var check = ActionValidator.ValidateEvolution(_gameState, allFieldTargets.FirstOrDefault(), card);
                        ShowToast(false, check.Reason);
                    }
                };
                btnEvo.Click += (_, _) =>
                {
                    if (isThisCardPending && _pendingIsEvolution)
                    {
                        _pendingHandCard = null;
                        ShowToast(true, $"[{card.Name}] 진화 대상 선택을 취소했습니다.");
                        RefreshAllSimulatorUi();
                        return;
                    }

                    AutoPassBreedingIfNeeded();

                    var allFieldTargets = new List<CardInstance>();
                    if (_gameState.Player.BreedingArea != null) allFieldTargets.Add(_gameState.Player.BreedingArea);
                    allFieldTargets.AddRange(_gameState.Player.BattleArea);

                    var validTargets = allFieldTargets
                        .Where(t => ActionValidator.ValidateEvolution(_gameState, t, card).CanExecute)
                        .ToList();

                    if (validTargets.Count == 0)
                    {
                        var reasonCheck = ActionValidator.ValidateEvolution(_gameState, allFieldTargets.FirstOrDefault(), card);
                        ShowToast(false, reasonCheck.Reason);
                        return;
                    }

                    _pendingAttackerInstance = null;
                    _pendingHandCard = card;
                    _pendingIsEvolution = true;
                    _pendingTargetsOpponent = false;
                    ShowToast(true, $"🎯 패에서 [{card.Name}(Lv.{card.Level})] 진화를 선택했습니다! 이제 내 필드(육성/배틀 에어리어)에서 초록색으로 빛나는 디지몬을 클릭하세요.");
                    RefreshAllSimulatorUi();
                };
                btnGrid.Children.Add(btnEvo);
            }

            cardStack.Children.Add(btnGrid);

            var wrapper = new Border
            {
                Width = 148,
                Background = BrushFromHex(isThisCardPending ? "#26283E" : "#141522"),
                BorderBrush = BrushFromHex(isThisCardPending ? "#F9E2AF" : "#313244"),
                BorderThickness = new Thickness(isThisCardPending ? 2.5 : 1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6),
                Margin = new Thickness(0, 0, 8, 4),
                Cursor = System.Windows.Input.Cursors.Hand,
                Child = cardStack
            };
            wrapper.MouseEnter += (_, _) => UpdateCardInspector(card);

            // 패 카드를 마우스로 잡고 위로 드래그하면:
            // - 필드 디지몬 위에 드롭 -> 즉시 [진화] 또는 [옵션 버프/제거]
            // - 배틀 에어리어 빈 공간에 드롭 -> 즉시 [일반 등장/사용]
            wrapper.PreviewMouseLeftButtonDown += (_, e) =>
            {
                if (!_gameState.IsGameOver)
                {
                    _dragStartPoint = e.GetPosition(this);
                    _potentialDragHandCard = card;
                    _potentialDragFieldAttacker = null;
                    _potentialDragBreedingDigimon = null;
                }
            };

            HandPanel.Children.Add(wrapper);
        }
    }

    // ==================== 🖱️ 드래그 앤 드롭 (패 -> 진화/등장/옵션 & 필드 디지몬 -> 배틀이동/공격) ====================
    private void Window_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (SimulatorView.Visibility != Visibility.Visible)
            return;

        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
        {
            if (_isDragging)
            {
                CancelDragVisuals();
                RefreshAllSimulatorUi();
            }
            return;
        }

        if (_potentialDragHandCard == null && _potentialDragFieldAttacker == null && _potentialDragBreedingDigimon == null)
            return;

        Point pos = e.GetPosition(this);
        if (!_isDragging)
        {
            Vector diff = pos - _dragStartPoint;
            if (Math.Abs(diff.X) > 6 || Math.Abs(diff.Y) > 6)
            {
                StartCardDrag(pos);
            }
        }

        if (_isDragging && _dragGhostBorder != null)
        {
            Canvas.SetLeft(_dragGhostBorder, pos.X - 68);
            Canvas.SetTop(_dragGhostBorder, pos.Y - 96);
            UpdateDragHoverFeedback(pos);
        }
    }

    private void Window_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_isDragging)
        {
            _potentialDragHandCard = null;
            _potentialDragFieldAttacker = null;
            _potentialDragBreedingDigimon = null;
            return;
        }

        e.Handled = true;
        Point dropPos = e.GetPosition(this);
        CompleteCardDrop(dropPos);
    }

    private void StartCardDrag(Point pos)
    {
        _isDragging = true;
        if (_potentialDragBreedingDigimon == null)
        {
            AutoPassBreedingIfNeeded();
        }

        CardData? cardToPreview = _potentialDragHandCard
            ?? _potentialDragFieldAttacker?.TopCard
            ?? _potentialDragBreedingDigimon?.TopCard;
        if (cardToPreview == null)
            return;

        if (_potentialDragHandCard != null)
        {
            var hc = _potentialDragHandCard;
            _pendingAttackerInstance = null;
            _pendingHandCard = hc;
            _pendingIsEvolution = hc.Type == CardType.Digimon;
            _pendingTargetsOpponent = hc.Type == CardType.Option && hc.CardId is "ST1-15" or "ST1-16" or "ST2-14" or "ST2-16";
            RefreshAllSimulatorUi();
        }
        else if (_potentialDragFieldAttacker != null)
        {
            _pendingHandCard = null;
            _pendingAttackerInstance = _potentialDragFieldAttacker;
            RefreshAllSimulatorUi();
        }
        else if (_potentialDragBreedingDigimon != null)
        {
            _pendingHandCard = null;
            _pendingAttackerInstance = null;
            RefreshAllSimulatorUi();
        }

        // UI 갱신 직후 즉시 레이아웃을 확정하여 모든 카드/존의 ActualWidth, ActualHeight, 좌표를 최신화
        UpdateLayout();
        System.Windows.Input.Mouse.Capture(this, System.Windows.Input.CaptureMode.SubTree);

        // 반투명 플로팅 카드 고스트 생성
        DragOverlayCanvas.Children.Clear();
        DragOverlayCanvas.Visibility = Visibility.Visible;

        var ghostStack = new StackPanel { IsHitTestVisible = false };
        _dragGhostBadgeText = new TextBlock
        {
            Text = "🖱️ 드래그하여 대상 위에 놓으세요",
            Foreground = BrushFromHex("#11111B"),
            FontWeight = FontWeights.ExtraBold,
            FontSize = 11.5,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            IsHitTestVisible = false
        };

        _dragGhostBadgeBorder = new Border
        {
            Background = BrushFromHex("#F9E2AF"),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 0, 4),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = _dragGhostBadgeText,
            IsHitTestVisible = false
        };
        ghostStack.Children.Add(_dragGhostBadgeBorder);

        var ghostCardFrame = CreateCardVisualFrame(cardToPreview, width: 126, height: 168);
        ghostCardFrame.Opacity = 0.88;
        ghostCardFrame.HorizontalAlignment = HorizontalAlignment.Center;
        ghostCardFrame.IsHitTestVisible = false;
        ghostStack.Children.Add(ghostCardFrame);

        _dragGhostBorder = new Border
        {
            Child = ghostStack,
            IsHitTestVisible = false
        };

        DragOverlayCanvas.Children.Add(_dragGhostBorder);
        Canvas.SetLeft(_dragGhostBorder, pos.X - 68);
        Canvas.SetTop(_dragGhostBorder, pos.Y - 96);
    }

    private bool IsPointOverElement(FrameworkElement? element, Point windowPos, double expandMargin = 0)
    {
        if (element == null || !element.IsVisible || element.ActualWidth <= 0 || element.ActualHeight <= 0)
            return false;

        try
        {
            Point topLeft = element.TranslatePoint(new Point(0, 0), this);
            Rect rect = new Rect(
                topLeft.X - expandMargin,
                topLeft.Y - expandMargin,
                element.ActualWidth + expandMargin * 2,
                element.ActualHeight + expandMargin * 2);
            return rect.Contains(windowPos);
        }
        catch
        {
            return false;
        }
    }

    private CardInstance? FindClosestCardInPanel(Panel? panel, Point windowPos, double expandMargin = 10)
    {
        if (panel == null || !panel.IsVisible)
            return null;

        CardInstance? bestCard = null;
        double bestDistSq = double.MaxValue;

        foreach (var child in panel.Children.OfType<FrameworkElement>())
        {
            if (child.Tag is not CardInstance ci || !child.IsVisible || child.ActualWidth <= 0 || child.ActualHeight <= 0)
                continue;

            try
            {
                Point topLeft = child.TranslatePoint(new Point(0, 0), this);
                Rect rect = new Rect(
                    topLeft.X - expandMargin,
                    topLeft.Y - expandMargin,
                    child.ActualWidth + expandMargin * 2,
                    child.ActualHeight + expandMargin * 2);

                if (rect.Contains(windowPos))
                {
                    Point center = new Point(topLeft.X + child.ActualWidth * 0.5, topLeft.Y + child.ActualHeight * 0.5);
                    double dx = windowPos.X - center.X;
                    double dy = windowPos.Y - center.Y;
                    double distSq = dx * dx + dy * dy;
                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        bestCard = ci;
                    }
                }
            }
            catch
            {
                // ignore coordinate transform errors
            }
        }

        return bestCard;
    }

    private (CardInstance? AllyDigimon, CardInstance? OpponentDigimon, bool OverBattleZone, bool OverBreedingZone, bool OverOpponentSecurityZone, bool OverOpponentBattleZone)
        FindDropTargetInfo(Point windowPos)
    {
        bool overBattleZone = IsPointOverElement(BattleAreaZoneBorder, windowPos, expandMargin: 4);
        bool overBreedingZone = IsPointOverElement(BreedingAreaZoneBorder, windowPos, expandMargin: 4);
        bool overOpponentSecurity = IsPointOverElement(OpponentSecurityZoneBorder, windowPos, expandMargin: 4);
        bool overOpponentBattleZone = IsPointOverElement(OpponentBattleAreaBorder, windowPos, expandMargin: 4);

        CardInstance? opponentDigimon = overOpponentBattleZone
            ? FindClosestCardInPanel(OpponentBattleAreaPanel, windowPos, expandMargin: 12)
            : null;

        CardInstance? allyDigimon = null;
        if (overBreedingZone && _gameState.Player.BreedingArea != null)
        {
            allyDigimon = _gameState.Player.BreedingArea;
        }
        else if (overBattleZone)
        {
            allyDigimon = FindClosestCardInPanel(BattleAreaPanel, windowPos, expandMargin: 10);
        }

        return (allyDigimon, opponentDigimon, overBattleZone, overBreedingZone, overOpponentSecurity, overOpponentBattleZone);
    }

    private (bool IsEvolve, CardInstance? EvoTarget, bool IsPlay, bool IsInvalidBreeding)
        ResolveHandDigimonDropIntent(CardData card, (CardInstance? AllyDigimon, CardInstance? OpponentDigimon, bool OverBattleZone, bool OverBreedingZone, bool OverOpponentSecurityZone, bool OverOpponentBattleZone) info)
    {
        // 1) 육성 에어리어(Breeding Area) 위에 놓았을 때
        if (info.OverBreedingZone)
        {
            if (_gameState.Player.BreedingArea != null)
                return (true, _gameState.Player.BreedingArea, false, false);
            return (false, null, false, true);
        }

        // 2) 배틀 에어리어(Battle Area)의 특정 아군 디지몬 카드 위에 놓았을 때
        if (info.AllyDigimon != null)
        {
            // Lv.3 성장기 디지몬은 진화 조건이 Lv.2(유년기)이므로 배틀 에어리어 디지몬 위에는 절대 진화할 수 없음 -> 무조건 [배틀 에어리어 일반 등장]으로 처리!
            if (card.Level == 3)
            {
                return (false, null, true, false);
            }

            // 진화 가능 조건이 일치하는 디지몬 위에 올렸을 때 -> [진화]!
            if (ActionValidator.ValidateEvolution(_gameState, info.AllyDigimon, card).CanExecute ||
                info.AllyDigimon.TopCard.Level == card.EvoRequirementLevel)
            {
                return (true, info.AllyDigimon, false, false);
            }

            // 만약 배틀 에어리어에 진화 가능한 레벨의 디지몬이 아예 없는데 다른 레벨 디지몬 카드 위로 드래그했다면 -> [일반 등장]으로 스마트 처리!
            bool anyValidEvoInBattle = _gameState.Player.BattleArea.Any(d => d.TopCard.Level == card.EvoRequirementLevel);
            if (!anyValidEvoInBattle)
            {
                return (false, null, true, false);
            }

            return (true, info.AllyDigimon, false, false);
        }

        // 3) 배틀 에어리어 빈 공간에 놓았을 때 -> [일반 등장]!
        if (info.OverBattleZone)
        {
            return (false, null, true, false);
        }

        return (false, null, false, false);
    }

    private void ResetDropZoneHighlights()
    {
        BreedingAreaZoneBorder.BorderBrush = BrushFromHex("#A6E3A1");
        BreedingAreaZoneBorder.BorderThickness = new Thickness(1.5);

        BattleAreaZoneBorder.BorderBrush = BrushFromHex("#89B4FA");
        BattleAreaZoneBorder.BorderThickness = new Thickness(1.5);

        OpponentSecurityZoneBorder.BorderBrush = BrushFromHex("#F38BA8");
        OpponentSecurityZoneBorder.BorderThickness = new Thickness(1.5);

        OpponentBattleAreaBorder.BorderBrush = BrushFromHex("#FAB387");
        OpponentBattleAreaBorder.BorderThickness = new Thickness(1.5);
    }

    private void HighlightDropZone(Border? targetZone, string hexColor)
    {
        ResetDropZoneHighlights();
        if (targetZone != null)
        {
            targetZone.BorderBrush = BrushFromHex(hexColor);
            targetZone.BorderThickness = new Thickness(3.0);
        }
    }

    private void UpdateDragHoverFeedback(Point pos)
    {
        if (_dragGhostBadgeText == null || _dragGhostBadgeBorder == null)
            return;

        var info = FindDropTargetInfo(pos);

        if (_potentialDragHandCard != null)
        {
            var card = _potentialDragHandCard;
            if (card.Type == CardType.Digimon)
            {
                var intent = ResolveHandDigimonDropIntent(card, info);

                if (intent.IsInvalidBreeding)
                {
                    HighlightDropZone(BreedingAreaZoneBorder, "#F38BA8");
                    _dragGhostBadgeText.Text = "⚠️ 육성존 비어있음 ([🥚부화] 먼저 실행 또는 배틀존에 등장!)";
                    _dragGhostBadgeBorder.Background = BrushFromHex("#F38BA8");
                    return;
                }

                if (intent.IsEvolve && intent.EvoTarget != null)
                {
                    var check = ActionValidator.ValidateEvolution(_gameState, intent.EvoTarget, card);
                    HighlightDropZone(info.OverBreedingZone ? BreedingAreaZoneBorder : BattleAreaZoneBorder, check.CanExecute ? "#A6E3A1" : "#F38BA8");
                    if (check.CanExecute)
                    {
                        _dragGhostBadgeText.Text = $"🧬 [{intent.EvoTarget.TopCard.Name}] 위에 진화! (코스트 {card.EvoCost}, 1드로우)";
                        _dragGhostBadgeBorder.Background = BrushFromHex("#A6E3A1");
                        ShowToast(true, check.GuideMessage);
                    }
                    else
                    {
                        _dragGhostBadgeText.Text = $"⚠️ 진화 불가 (Lv.{card.EvoRequirementLevel} 필요) — 빈 공간에 놓으면 등장";
                        _dragGhostBadgeBorder.Background = BrushFromHex("#F38BA8");
                        ShowToast(false, check.Reason);
                    }
                    return;
                }

                if (intent.IsPlay)
                {
                    var playCheck = ActionValidator.ValidatePlay(_gameState, card);
                    HighlightDropZone(BattleAreaZoneBorder, playCheck.CanExecute ? "#89B4FA" : "#F38BA8");
                    if (playCheck.CanExecute)
                    {
                        _dragGhostBadgeText.Text = $"✨ 배틀 에어리어에 바로 등장! (코스트 {card.PlayCost})";
                        _dragGhostBadgeBorder.Background = BrushFromHex("#89B4FA");
                        ShowToast(true, playCheck.GuideMessage);
                    }
                    else
                    {
                        _dragGhostBadgeText.Text = "⚠️ 등장 불가";
                        _dragGhostBadgeBorder.Background = BrushFromHex("#F38BA8");
                        ShowToast(false, playCheck.Reason);
                    }
                    return;
                }

                ResetDropZoneHighlights();
                _dragGhostBadgeText.Text = $"🖱️ 디지몬 위에 놓으면 [진화({card.EvoCost})] / 배틀존에 놓으면 [등장({card.PlayCost})]";
                _dragGhostBadgeBorder.Background = BrushFromHex("#F9E2AF");
            }
            else if (card.Type == CardType.Option)
            {
                if (_pendingTargetsOpponent)
                {
                    var oppTarget = info.OpponentDigimon
                        ?? (info.OverOpponentBattleZone ? _gameState.DummyOpponent.BattleArea.OrderByDescending(d => d.GetCurrentDP()).FirstOrDefault() : null);

                    if (oppTarget != null)
                    {
                        HighlightDropZone(OpponentBattleAreaBorder, "#A6E3A1");
                        _dragGhostBadgeText.Text = $"🔥 상대 [{oppTarget.TopCard.Name}]에게 [{card.Name}] 발동!";
                        _dragGhostBadgeBorder.Background = BrushFromHex("#A6E3A1");
                    }
                    else
                    {
                        ResetDropZoneHighlights();
                        _dragGhostBadgeText.Text = "🖱️ 상단 [상대 배틀 에어리어]의 디지몬 위에 드롭하세요!";
                        _dragGhostBadgeBorder.Background = BrushFromHex("#F9E2AF");
                    }
                }
                else if (card.CardId is "ST1-13" or "ST1-14" or "ST2-15")
                {
                    var allyTarget = info.AllyDigimon
                        ?? (info.OverBattleZone ? _gameState.Player.BattleArea.OrderByDescending(d => d.GetCurrentDP()).FirstOrDefault() : null);

                    if (allyTarget != null && !_gameState.Player.BattleArea.Contains(allyTarget))
                        allyTarget = null;

                    if (allyTarget != null)
                    {
                        HighlightDropZone(BattleAreaZoneBorder, "#A6E3A1");
                        _dragGhostBadgeText.Text = $"✨ 아군 [{allyTarget.TopCard.Name}]에게 [{card.Name}] 적용!";
                        _dragGhostBadgeBorder.Background = BrushFromHex("#A6E3A1");
                    }
                    else
                    {
                        ResetDropZoneHighlights();
                        _dragGhostBadgeText.Text = "🖱️ [내 배틀 에어리어]의 아군 디지몬 위에 드롭하세요!";
                        _dragGhostBadgeBorder.Background = BrushFromHex("#F9E2AF");
                    }
                }
                else
                {
                    bool overPlaymat = info.OverBattleZone || info.OverBreedingZone;
                    HighlightDropZone(overPlaymat ? BattleAreaZoneBorder : null, "#89B4FA");
                    _dragGhostBadgeText.Text = overPlaymat
                        ? $"⚡ [{card.Name}] 즉시 발동! (코스트 {card.PlayCost})"
                        : $"🖱️ 배틀 에어리어에 드롭하여 [{card.Name}] 발동!";
                    _dragGhostBadgeBorder.Background = BrushFromHex(overPlaymat ? "#A6E3A1" : "#F9E2AF");
                }
            }
            else if (card.Type == CardType.Tamer)
            {
                bool overPlaymat = info.OverBattleZone || info.OverBreedingZone || info.AllyDigimon != null;
                HighlightDropZone(overPlaymat ? BattleAreaZoneBorder : null, "#A6E3A1");
                _dragGhostBadgeText.Text = overPlaymat
                    ? $"🎖️ 테이머 [{card.Name}] 필드에 등장! (코스트 {card.PlayCost})"
                    : "🖱️ 배틀 에어리어에 드롭하여 테이머를 등장시키세요!";
                _dragGhostBadgeBorder.Background = BrushFromHex(overPlaymat ? "#A6E3A1" : "#F9E2AF");
            }
        }
        else if (_potentialDragBreedingDigimon != null)
        {
            if (info.OverBattleZone)
            {
                var moveCheck = ActionValidator.ValidateMoveFromBreeding(_gameState);
                HighlightDropZone(BattleAreaZoneBorder, moveCheck.CanExecute ? "#89DCEB" : "#F38BA8");
                _dragGhostBadgeText.Text = moveCheck.CanExecute
                    ? $"🚀 [{_potentialDragBreedingDigimon.TopCard.Name}] 배틀 에어리어로 이동!"
                    : $"⚠️ {moveCheck.Reason}";
                _dragGhostBadgeBorder.Background = BrushFromHex(moveCheck.CanExecute ? "#A6E3A1" : "#F38BA8");
            }
            else
            {
                ResetDropZoneHighlights();
                _dragGhostBadgeText.Text = "🖱️ 우측 [내 배틀 에어리어]로 드롭하여 배틀 필드로 이동!";
                _dragGhostBadgeBorder.Background = BrushFromHex("#89DCEB");
            }
        }
        else if (_potentialDragFieldAttacker != null)
        {
            var attacker = _potentialDragFieldAttacker;
            if (info.OpponentDigimon != null)
            {
                var check = ActionValidator.ValidateAttack(_gameState, attacker, info.OpponentDigimon);
                HighlightDropZone(OpponentBattleAreaBorder, check.CanExecute ? "#A6E3A1" : "#F38BA8");
                _dragGhostBadgeText.Text = check.CanExecute
                    ? $"🎯 상대 [{info.OpponentDigimon.TopCard.Name}] 직접 타격!"
                    : "⚠️ 액티브 디지몬은 직접 공격 불가 (시큐리티 존에 드롭하세요)";
                _dragGhostBadgeBorder.Background = BrushFromHex(check.CanExecute ? "#A6E3A1" : "#F38BA8");
            }
            else if (info.OverOpponentSecurityZone || info.OverOpponentBattleZone)
            {
                var check = ActionValidator.ValidateAttack(_gameState, attacker, null);
                var targetBorder = info.OverOpponentSecurityZone ? OpponentSecurityZoneBorder : OpponentBattleAreaBorder;
                HighlightDropZone(targetBorder, check.CanExecute ? "#F38BA8" : "#FAB387");
                _dragGhostBadgeText.Text = check.CanExecute
                    ? (_gameState.DummyOpponent.SecurityStack.Count > 0
                        ? $"⚔️ 시큐리티 어택! ({attacker.GetSecurityAttackCount()}장 타격)"
                        : "🏆 다이렉트 어택 (게임 승리!)")
                    : $"⚠️ {check.Reason}";
                _dragGhostBadgeBorder.Background = BrushFromHex(check.CanExecute ? "#F38BA8" : "#FAB387");
            }
            else
            {
                ResetDropZoneHighlights();
                _dragGhostBadgeText.Text = "🖱️ 상단 [상대 시큐리티 존] 또는 [상대 레스트 디지몬] 위에 드롭하여 공격!";
                _dragGhostBadgeBorder.Background = BrushFromHex("#F9E2AF");
            }
        }
    }

    private void CompleteCardDrop(Point dropPos)
    {
        var draggedHand = _potentialDragHandCard;
        var draggedAttacker = _potentialDragFieldAttacker;
        var draggedBreeding = _potentialDragBreedingDigimon;

        var info = FindDropTargetInfo(dropPos);
        CancelDragVisuals();

        if (draggedHand != null)
        {
            if (draggedHand.Type == CardType.Digimon)
            {
                var intent = ResolveHandDigimonDropIntent(draggedHand, info);
                if (intent.IsInvalidBreeding)
                {
                    ShowToast(false, "육성 에어리어에는 디지몬을 바로 등장시킬 수 없습니다. 먼저 [🥚 부화 (Lv.2)]를 누르거나 우측 [배틀 에어리어]에 드롭하세요!");
                    RefreshAllSimulatorUi();
                    return;
                }

                if (intent.IsEvolve && intent.EvoTarget != null)
                {
                    ApplyActionResult(_engine.EvolveDigimon(intent.EvoTarget, draggedHand));
                    return;
                }

                if (intent.IsPlay)
                {
                    ApplyActionResult(_engine.PlayCard(draggedHand));
                    return;
                }
            }
            else if (draggedHand.Type == CardType.Option)
            {
                bool isEnemyTargeted = draggedHand.CardId is "ST1-15" or "ST1-16" or "ST2-14" or "ST2-16";
                bool isAllyTargeted = draggedHand.CardId is "ST1-13" or "ST1-14" or "ST2-15";

                if (isEnemyTargeted)
                {
                    var oppTarget = info.OpponentDigimon
                        ?? ((info.OverOpponentBattleZone || info.OverOpponentSecurityZone || info.OverBattleZone)
                            ? _gameState.DummyOpponent.BattleArea.OrderByDescending(d => d.GetCurrentDP()).FirstOrDefault()
                            : null);
                    if (oppTarget != null)
                    {
                        ApplyActionResult(_engine.PlayCard(draggedHand, oppTarget));
                        return;
                    }
                }
                else if (isAllyTargeted)
                {
                    var allyTarget = (info.AllyDigimon != null && _gameState.Player.BattleArea.Contains(info.AllyDigimon))
                        ? info.AllyDigimon
                        : (info.OverBattleZone
                            ? _gameState.Player.BattleArea.OrderByDescending(d => d.GetCurrentDP()).FirstOrDefault()
                            : null);
                    if (allyTarget != null)
                    {
                        ApplyActionResult(_engine.PlayCard(draggedHand, allyTarget));
                        return;
                    }
                }
                else if (info.OverBattleZone || info.OverBreedingZone || info.AllyDigimon != null)
                {
                    ApplyActionResult(_engine.PlayCard(draggedHand));
                    return;
                }
            }
            else if (draggedHand.Type == CardType.Tamer)
            {
                if (info.OverBattleZone || info.OverBreedingZone || info.AllyDigimon != null)
                {
                    ApplyActionResult(_engine.PlayCard(draggedHand));
                    return;
                }
            }
        }
        else if (draggedBreeding != null)
        {
            if (info.OverBattleZone)
            {
                ApplyActionResult(_engine.MoveFromBreedingArea());
                return;
            }
        }
        else if (draggedAttacker != null)
        {
            if (info.OpponentDigimon != null)
            {
                ApplyActionResult(_engine.AttackOpponentDigimon(draggedAttacker, info.OpponentDigimon));
                return;
            }
            if (info.OverOpponentSecurityZone || info.OverOpponentBattleZone)
            {
                var res = _engine.AttackSecurityOrDirect(draggedAttacker);
                if (!res.CanExecute)
                {
                    ApplyActionResult(res);
                }
                else
                {
                    if (_gameState.SecurityCheckHistory.Count > 0)
                    {
                        UpdateCardInspector(_gameState.SecurityCheckHistory[^1].SecurityCard);
                    }
                    ShowToast(_gameState.LastSecurityBattleWon || _gameState.IsVictory, res.GuideMessage);
                    RefreshAllSimulatorUi();
                }
                return;
            }
        }

        RefreshAllSimulatorUi();
    }

    private void CancelDragVisuals()
    {
        if (System.Windows.Input.Mouse.Captured != null)
        {
            System.Windows.Input.Mouse.Capture(null);
        }

        ResetDropZoneHighlights();
        _isDragging = false;
        _potentialDragHandCard = null;
        _potentialDragFieldAttacker = null;
        _potentialDragBreedingDigimon = null;
        _pendingHandCard = null;
        _pendingAttackerInstance = null;
        _dragGhostBorder = null;
        _dragGhostBadgeBorder = null;
        _dragGhostBadgeText = null;
        DragOverlayCanvas.Children.Clear();
        DragOverlayCanvas.Visibility = Visibility.Collapsed;
    }

    private void RenderTimeAttackHudAndLogs()
    {
        string stageLabel = _gameState.CurrentStage switch
        {
            PracticeStage.Stage1_Goldfish => "Stage 1 (기본 5턴 타임어택)",
            PracticeStage.Stage2_BlockerDefense => "Stage 2 (블로커 & 필드 돌파 챌린지)",
            PracticeStage.Stage3_AiBattle => "Stage 3 (AI 듀얼리스트 실전 배틀)",
            _ => "타임어택"
        };

        TxtHudTurn.Text = _gameState.CurrentStage == PracticeStage.Stage3_AiBattle
            ? $"• 모드: {stageLabel} | 현재 {_gameState.CurrentTurn}턴 (턴 제한 없음)"
            : $"• 모드: {stageLabel} | 현재 {_gameState.CurrentTurn} / {_gameState.TargetTurns}턴";
        TxtHudSecurity.Text = $"• 상대 시큐리티: {_gameState.DummyOpponent.SecurityStack.Count}/5장 | 🛡️ 내 시큐리티: {_gameState.Player.SecurityStack.Count}/5장";
        TxtHudEvoRate.Text = $"• 진화 체인 성공률 (Lv.3→Lv.6): {_gameState.EvolutionChainSuccessRate:F1}% ({_gameState.Lv6CompletedChains}/{_gameState.Lv3StartedChains})";
        TxtHudMemoryEff.Text = $"• 턴당 메모리 사용 효율: {_gameState.MemoryEfficiencyPerTurn:F1} 메모리/턴";

        if (_gameState.IsGameOver)
        {
            TxtGameResultBanner.Text = _gameState.GameOverReason;
            TxtGameResultBanner.Foreground = BrushFromHex(_gameState.IsVictory ? "#A6E3A1" : "#F38BA8");
        }
        else
        {
            TxtGameResultBanner.Text = _gameState.CurrentStage switch
            {
                PracticeStage.Stage1_Goldfish => "Stage 1 진행 중: 5턴 이내에 진화 체인을 완성해 시큐리티 5장을 돌파하세요!",
                PracticeStage.Stage2_BlockerDefense => "Stage 2 진행 중: 상대 필드의 ≪블로커≫를 옵션/고DP로 돌파하고 6턴 내에 승리하세요!",
                PracticeStage.Stage3_AiBattle => "Stage 3 진행 중: 메모리를 아껴 AI에게 역공 각을 주지 말고, AI보다 먼저 다이렉트 어택을 성공시키세요!",
                _ => "진행 중"
            };
            TxtGameResultBanner.Foreground = BrushFromHex("#CDD6F4");
        }

        LstActionLogs.ItemsSource = null;
        LstActionLogs.ItemsSource = _gameState.ActionLogs;
        if (_gameState.ActionLogs.Count > 0)
        {
            LstActionLogs.ScrollIntoView(_gameState.ActionLogs[^1]);
        }
    }

    // ==================== 인게임 버튼 이벤트 ====================
    private void BtnStage1_Click(object sender, RoutedEventArgs e)
    {
        _isSecurityPeekEnabled = true;
        _engine.StartNewGame(_builderMainDeck, _builderDigitamaDeck, stage: PracticeStage.Stage1_Goldfish);
        RefreshAllSimulatorUi();
        ShowToast(true, "🥇 [STAGE 1: 5턴 타임어택] 시작! 상대 방해 없이 5턴 안에 시큐리티 5장을 깨고 다이렉트 어택을 성공시키세요.");
    }

    private void BtnStage2_Click(object sender, RoutedEventArgs e)
    {
        _isSecurityPeekEnabled = true;
        _engine.StartNewGame(_builderMainDeck, _builderDigitamaDeck, stage: PracticeStage.Stage2_BlockerDefense);
        RefreshAllSimulatorUi();
        ShowToast(true, "🥈 [STAGE 2: 블로커 돌파] 시작! 상대 필드의 ≪블로커≫ 디지몬을 옵션 카드(가이아 포스/코큐토스 브레스)나 고DP 진화로 돌파하세요!");
    }

    private void BtnStage3_Click(object sender, RoutedEventArgs e)
    {
        _isSecurityPeekEnabled = false; // 공식 대인전 룰: 상대 시큐리티는 뒷면 비공개로 시작하며 공격 시 오픈됩니다!
        _engine.StartNewGame(_builderMainDeck, _builderDigitamaDeck, stage: PracticeStage.Stage3_AiBattle);
        RefreshAllSimulatorUi();
        ShowToast(true, "🥉 [STAGE 3: 공식 룰 AI 실전 배틀] 시작! 양쪽 모두 50장 덱 맨 위 5장이 시큐리티(비공개)로 세팅되며, 테이머/옵션의 [시큐리티] 효과가 100% 발동됩니다!");
    }

    private void BtnMulligan_Click(object sender, RoutedEventArgs e)
    {
        ApplyActionResult(_engine.ExecuteMulligan());
    }

    private void BtnHatch_Click(object sender, RoutedEventArgs e)
    {
        ApplyActionResult(_engine.HatchDigitama());
    }

    private void BtnMoveBreeding_Click(object sender, RoutedEventArgs e)
    {
        ApplyActionResult(_engine.MoveFromBreedingArea());
    }

    private void BtnPassBreeding_Click(object sender, RoutedEventArgs e)
    {
        ApplyActionResult(_engine.PassBreedingPhase());
    }

    private void BtnPassTurn_Click(object sender, RoutedEventArgs e)
    {
        ApplyActionResult(_engine.PassTurn());
    }

    private void BtnStartSt1_Click(object sender, RoutedEventArgs e)
    {
        var (main, digitama) = StarterDecks.CreateSt1GaiaRedPreset();
        _builderMainDeck = main;
        _builderDigitamaDeck = digitama;
        _engine.StartNewGame(_builderMainDeck, _builderDigitamaDeck, stage: _gameState.CurrentStage);
        RefreshAllSimulatorUi();
        ShowToast(true, "🔥 ST-1 가이아 레드 프리셋으로 새 연습 게임을 시작했습니다!");
    }

    private void BtnStartSt2_Click(object sender, RoutedEventArgs e)
    {
        var (main, digitama) = StarterDecks.CreateSt2CocytusBluePreset();
        _builderMainDeck = main;
        _builderDigitamaDeck = digitama;
        _engine.StartNewGame(_builderMainDeck, _builderDigitamaDeck, stage: _gameState.CurrentStage);
        RefreshAllSimulatorUi();
        ShowToast(true, "❄️ ST-2 코큐토스 블루 프리셋으로 새 연습 게임을 시작했습니다!");
    }

    // ==================== 덱 빌더 & 카드 도감 UI (실물 카드 디자인 그리드) ====================
    private void FilterChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;
        RefreshCatalogUi();
    }

    private void RefreshCatalogUi()
    {
        if (CatalogWrapPanel == null)
            return;

        string? keyword = TxtSearchKeyword.Text;
        CardColor? color = (CmbFilterColor.SelectedItem as ComboBoxItem)?.Content?.ToString() switch
        {
            "Red" => CardColor.Red,
            "Blue" => CardColor.Blue,
            "Yellow" => CardColor.Yellow,
            "Green" => CardColor.Green,
            "White" => CardColor.White,
            _ => null
        };

        CardType? type = (CmbFilterType.SelectedItem as ComboBoxItem)?.Content?.ToString() switch
        {
            "Digitama" => CardType.Digitama,
            "Digimon" => CardType.Digimon,
            "Tamer" => CardType.Tamer,
            "Option" => CardType.Option,
            _ => null
        };

        int? level = (CmbFilterLevel.SelectedItem as ComboBoxItem)?.Content?.ToString() switch
        {
            "Lv.2" => 2,
            "Lv.3" => 3,
            "Lv.4" => 4,
            "Lv.5" => 5,
            "Lv.6" => 6,
            "Lv.7" => 7,
            _ => null
        };

        var results = _catalogService.SearchCards(keyword, color, type, level);
        TxtCatalogCount.Text = $"검색된 카드: {results.Count}종 (전체 도감: {_catalogService.Catalog.Count}종 | 이미지 캐시: data/images/)";

        CatalogWrapPanel.Children.Clear();
        foreach (var card in results)
        {
            int countInDeck = _builderMainDeck.Count(c => c.CardId == card.CardId) +
                              _builderDigitamaDeck.Count(c => c.CardId == card.CardId);

            var stack = new StackPanel();

            var topInfo = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            topInfo.Children.Add(new TextBlock
            {
                Text = card.CardId,
                Foreground = BrushFromHex("#A6ADC8"),
                FontWeight = FontWeights.Bold,
                FontSize = 11
            });
            topInfo.Children.Add(new TextBlock
            {
                Text = $"덱 투입: {countInDeck}/4장",
                Foreground = BrushFromHex(countInDeck > 0 ? "#F9E2AF" : "#6C7086"),
                FontWeight = FontWeights.ExtraBold,
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Right
            });
            stack.Children.Add(topInfo);

            // 실물 카드 아트워크 프레임
            var visualCard = CreateCardVisualFrame(card, width: 152, height: 210);
            stack.Children.Add(visualCard);

            var btnRow = new UniformGrid { Rows = 1, Columns = 2, Margin = new Thickness(0, 6, 0, 0) };
            var btnAdd = new Button
            {
                Content = "+ 덱 추가",
                Background = BrushFromHex("#A6E3A1"),
                Foreground = BrushFromHex("#11111B"),
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(4, 4, 4, 4),
                Margin = new Thickness(0, 0, 2, 0),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnAdd.Click += (_, _) => AddCardToBuilderDeck(card);

            var btnRem = new Button
            {
                Content = "- 제거",
                Background = BrushFromHex("#45475A"),
                Foreground = BrushFromHex("#F8FAFC"),
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(4, 4, 4, 4),
                Margin = new Thickness(2, 0, 0, 0),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnRem.Click += (_, _) => RemoveCardFromBuilderDeck(card);

            btnRow.Children.Add(btnAdd);
            btnRow.Children.Add(btnRem);
            stack.Children.Add(btnRow);

            var cardContainer = new Border
            {
                Width = 170,
                Background = BrushFromHex("#11111B"),
                BorderBrush = BrushFromHex(countInDeck > 0 ? "#F9E2AF" : "#313244"),
                BorderThickness = new Thickness(countInDeck > 0 ? 1.5 : 1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 8, 8),
                Child = stack
            };
            cardContainer.MouseEnter += (_, _) => UpdateCardInspector(card);
            CatalogWrapPanel.Children.Add(cardContainer);
        }
    }

    private void AddCardToBuilderDeck(CardData card)
    {
        int currentCopies = _builderMainDeck.Count(c => c.CardId == card.CardId) +
                            _builderDigitamaDeck.Count(c => c.CardId == card.CardId);

        if (currentCopies >= DeckValidator.MaxCopiesPerCard)
        {
            ShowToast(false, $"동일 카드 번호({card.CardId})는 최대 4장까지만 넣을 수 있습니다.");
            return;
        }

        if (card.Type == CardType.Digitama)
        {
            if (_builderDigitamaDeck.Count >= DeckValidator.MaxDigitamaDeckSize)
            {
                ShowToast(false, "디지타마 덱은 최대 5장까지만 구성할 수 있습니다.");
                return;
            }
            _builderDigitamaDeck.Add(card);
        }
        else
        {
            if (_builderMainDeck.Count >= DeckValidator.RequiredMainDeckSize)
            {
                ShowToast(false, "메인 덱은 최대 50장까지만 구성할 수 있습니다.");
                return;
            }
            _builderMainDeck.Add(card);
        }

        RefreshCatalogUi();
        RefreshDeckBuilderUi();
    }

    private void RemoveCardFromBuilderDeck(CardData card)
    {
        if (card.Type == CardType.Digitama)
        {
            var found = _builderDigitamaDeck.FirstOrDefault(c => c.CardId == card.CardId);
            if (found != null) _builderDigitamaDeck.Remove(found);
        }
        else
        {
            var found = _builderMainDeck.FirstOrDefault(c => c.CardId == card.CardId);
            if (found != null) _builderMainDeck.Remove(found);
        }

        RefreshCatalogUi();
        RefreshDeckBuilderUi();
    }

    private void RefreshDeckBuilderUi()
    {
        if (GoldenRatioHudPanel == null)
            return;

        var ratio = DeckValidator.CalculateRatios(_builderMainDeck, _builderDigitamaDeck);
        GoldenRatioHudPanel.Children.Clear();

        foreach (var item in ratio.Items)
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(105) });

            var lbl = new TextBlock
            {
                Text = item.Label,
                Foreground = BrushFromHex("#CDD6F4"),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(lbl, 0);

            var bar = new ProgressBar
            {
                Minimum = 0,
                Maximum = Math.Max(item.RecommendedMax + 4, 16),
                Value = item.CurrentCount,
                Height = 10,
                Margin = new Thickness(5, 0, 5, 0),
                Background = BrushFromHex("#313244"),
                Foreground = BrushFromHex(item.StatusText == "적정" ? "#A6E3A1" : item.StatusText == "부족" ? "#F9E2AF" : "#F38BA8"),
                BorderThickness = new Thickness(0)
            };
            Grid.SetColumn(bar, 1);

            var statusTxt = new TextBlock
            {
                Text = $"{item.CurrentCount,2}/{item.RecommendedMin}~{item.RecommendedMax} ({item.StatusText})",
                Foreground = BrushFromHex(item.StatusText == "적정" ? "#A6E3A1" : item.StatusText == "부족" ? "#F9E2AF" : "#F38BA8"),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(statusTxt, 2);

            row.Children.Add(lbl);
            row.Children.Add(bar);
            row.Children.Add(statusTxt);
            GoldenRatioHudPanel.Children.Add(row);
        }

        var (isValid, errors) = DeckValidator.ValidateDeck(_builderMainDeck, _builderDigitamaDeck);
        if (isValid)
        {
            TxtDeckValidationStatus.Text = $"✅ 덱 검증 통과 (메인 {_builderMainDeck.Count}장 / 디지타마 {_builderDigitamaDeck.Count}장)";
            TxtDeckValidationStatus.Foreground = BrushFromHex("#A6E3A1");
        }
        else
        {
            TxtDeckValidationStatus.Text = "⚠️ " + string.Join(" / ", errors);
            TxtDeckValidationStatus.Foreground = BrushFromHex("#F38BA8");
        }

        TxtDeckListHeader.Text = $"📋 현재 덱 구성 (메인 {_builderMainDeck.Count}/50장, 디지타마 {_builderDigitamaDeck.Count}/5장)";
        CurrentDeckListPanel.Children.Clear();

        var grouped = _builderDigitamaDeck.Concat(_builderMainDeck)
            .GroupBy(c => c.CardId)
            .OrderBy(g => g.First().Type == CardType.Digitama ? 0 : 1)
            .ThenBy(g => g.First().Level)
            .ThenBy(g => g.Key);

        foreach (var g in grouped)
        {
            var card = g.First();
            int count = g.Count();

            var itemDock = new DockPanel();
            var bmp = GetCardBitmap(card);
            if (bmp != null)
            {
                itemDock.Children.Add(new Border
                {
                    Width = 22,
                    Height = 30,
                    CornerRadius = new CornerRadius(3),
                    Margin = new Thickness(0, 0, 6, 0),
                    ClipToBounds = true,
                    Child = new Image { Source = bmp, Stretch = Stretch.UniformToFill }
                });
            }

            itemDock.Children.Add(new TextBlock
            {
                Text = $"[{card.CardId}] {card.Name} {(card.Level > 0 ? $"(Lv.{card.Level})" : $"({card.Type})")}",
                Foreground = BrushFromHex("#F8FAFC"),
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center
            });

            var rightStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            rightStack.Children.Add(new TextBlock
            {
                Text = $"x{count}",
                Foreground = BrushFromHex("#F9E2AF"),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });

            var btnMinus = new Button
            {
                Content = "-",
                Width = 22,
                Height = 20,
                Background = BrushFromHex("#45475A"),
                Foreground = BrushFromHex("#F8FAFC"),
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 3, 0)
            };
            btnMinus.Click += (_, _) => RemoveCardFromBuilderDeck(card);

            var btnPlus = new Button
            {
                Content = "+",
                Width = 22,
                Height = 20,
                Background = BrushFromHex("#A6E3A1"),
                Foreground = BrushFromHex("#11111B"),
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0)
            };
            btnPlus.Click += (_, _) => AddCardToBuilderDeck(card);

            rightStack.Children.Add(btnMinus);
            rightStack.Children.Add(btnPlus);
            itemDock.Children.Add(rightStack);

            var rowBorder = new Border
            {
                Background = BrushFromHex("#11111B"),
                BorderBrush = GetCardColorBrush(card.Color),
                BorderThickness = new Thickness(3, 0, 0, 0),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 3, 6, 3),
                Margin = new Thickness(0, 0, 0, 3),
                Child = itemDock
            };
            rowBorder.MouseEnter += (_, _) => UpdateCardInspector(card);
            CurrentDeckListPanel.Children.Add(rowBorder);
        }
    }

    private async void BtnSyncApi_Click(object sender, RoutedEventArgs e)
    {
        BtnSyncApi.IsEnabled = false;
        ShowToast(true, "DigimonCard.io 공개 REST API에서 카드 메타데이터를 동기화하는 중입니다...");
        var (success, msg) = await _catalogService.SyncFromDigimonCardApiAsync("ST1-03");
        ShowToast(success, msg);
        RefreshCatalogUi();
        BtnSyncApi.IsEnabled = true;
    }

    private void BtnLoadPresetSt1_Click(object sender, RoutedEventArgs e)
    {
        var (main, digitama) = StarterDecks.CreateSt1GaiaRedPreset();
        _builderMainDeck = main;
        _builderDigitamaDeck = digitama;
        RefreshCatalogUi();
        RefreshDeckBuilderUi();
        ShowToast(true, "ST-1 가이아 레드 공식 프리셋(메인 50장, 디지타마 4장)을 불러왔습니다.");
    }

    private void BtnLoadPresetSt2_Click(object sender, RoutedEventArgs e)
    {
        var (main, digitama) = StarterDecks.CreateSt2CocytusBluePreset();
        _builderMainDeck = main;
        _builderDigitamaDeck = digitama;
        RefreshCatalogUi();
        RefreshDeckBuilderUi();
        ShowToast(true, "ST-2 코큐토스 블루 공식 프리셋(메인 50장, 디지타마 4장)을 불러왔습니다.");
    }

    private void BtnSaveCustomDeck_Click(object sender, RoutedEventArgs e)
    {
        _catalogService.SaveCustomDeck("사용자 커스텀 덱", _builderMainDeck, _builderDigitamaDeck);
        ShowToast(true, "현재 덱 구성을 로컬 파일(data/saved_decks.json)에 저장했습니다.");
    }

    private void BtnLoadCustomDeck_Click(object sender, RoutedEventArgs e)
    {
        var loaded = _catalogService.LoadCustomDeck();
        if (loaded == null)
        {
            ShowToast(false, "저장된 커스텀 덱 파일(data/saved_decks.json)이 없습니다.");
            return;
        }
        _builderMainDeck = loaded.Value.MainDeck;
        _builderDigitamaDeck = loaded.Value.DigitamaDeck;
        RefreshCatalogUi();
        RefreshDeckBuilderUi();
        ShowToast(true, $"[{loaded.Value.DeckName}] 덱을 불러왔습니다.");
    }

    private void BtnClearDeck_Click(object sender, RoutedEventArgs e)
    {
        _builderMainDeck.Clear();
        _builderDigitamaDeck.Clear();
        RefreshCatalogUi();
        RefreshDeckBuilderUi();
        ShowToast(true, "덱 구성을 초기화했습니다.");
    }

    private void BtnPlayWithCurrentDeck_Click(object sender, RoutedEventArgs e)
    {
        var (isValid, errors) = DeckValidator.ValidateDeck(_builderMainDeck, _builderDigitamaDeck);
        if (!isValid)
        {
            ShowToast(false, "덱 규칙을 만족하지 않아 게임을 시작할 수 없습니다: " + string.Join(" ", errors));
            return;
        }

        _isSecurityPeekEnabled = false;
        _engine.StartNewGame(_builderMainDeck, _builderDigitamaDeck, stage: PracticeStage.Stage3_AiBattle);
        BtnTabOfficialMatch_Click(sender, e);
        ShowToast(true, "⚔️ 현재 빌딩된 50장 커스텀 덱으로 [실전 AI 듀얼 아레나] 새 게임을 시작했습니다!");
    }

    private static SolidColorBrush BrushFromHex(string hex) =>
        (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;

    private static SolidColorBrush GetCardColorBrush(CardColor color) => color switch
    {
        CardColor.Red => BrushFromHex("#F38BA8"),
        CardColor.Blue => BrushFromHex("#89B4FA"),
        CardColor.Yellow => BrushFromHex("#F9E2AF"),
        CardColor.Green => BrushFromHex("#A6E3A1"),
        CardColor.Purple => BrushFromHex("#CBA6F7"),
        CardColor.White => BrushFromHex("#F8FAFC"),
        _ => BrushFromHex("#9399B2")
    };
}
