using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DigimonTcg.Core.Data;

namespace DigimonTcg.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        ApplyTcgCardBackImages();
    }

    /// <summary>
    /// 처리되지 않은 UI 예외로 게임이 그냥 꺼지지 않도록: 실행 파일 옆 crash_log.txt 에 기록하고 알린 뒤 계속 진행한다.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        string logPath = Path.Combine(AppContext.BaseDirectory, "crash_log.txt");
        try
        {
            File.AppendAllText(logPath, $"==== {DateTime.Now:yyyy-MM-dd HH:mm:ss} · {DigimonTcg.Core.AppVersion.Display} ====\n{e.Exception}\n\n");
        }
        catch
        {
            // 로그 기록 실패는 무시
        }

        MessageBox.Show(
            $"예상하지 못한 오류가 발생했지만 게임을 계속 진행합니다.\n\n{e.Exception.Message}\n\n자세한 내용: {logPath}",
            $"DIGIMON TCG {DigimonTcg.Core.AppVersion.Display} - 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }

    /// <summary>
    /// data/images/_card_back.jpg (메인 덱) · _digitama_back.jpg (디지타마 덱) 가 있으면
    /// 실제 TCG 카드 뒷면 이미지로 뒷면 브러시를 교체한다. 없으면 DuelField.xaml 의 벡터 뒷면을 그대로 쓴다.
    /// MainWindow(StartupUri)가 만들어지기 전에 호출해야 StaticResource 참조가 교체된 브러시를 받는다.
    /// </summary>
    private void ApplyTcgCardBackImages()
    {
        string imagesDir = CardCatalogService.ResolveImagesDirectory();

        var cardBack = LoadImage(Path.Combine(imagesDir, "_card_back.jpg"));
        if (cardBack != null)
        {
            Resources["Duel.Brush.CardBackArt"] = Freeze(new ImageBrush(cardBack) { Stretch = Stretch.Fill });
            // 시큐리티처럼 가로로 눕혀 놓는 뒷면: 정규화 좌표에서 90° 돌리면 가로 칸에 세로 카드가 비율 그대로 들어간다
            Resources["Duel.Brush.CardBackArt.Sideways"] = Freeze(new ImageBrush(cardBack)
            {
                Stretch = Stretch.Fill,
                RelativeTransform = new RotateTransform(90, 0.5, 0.5)
            });
        }

        var eggBack = LoadImage(Path.Combine(imagesDir, "_digitama_back.jpg"));
        if (eggBack != null)
        {
            // 슬리브 사진이라 아래쪽 저작권 표기 부분은 잘라 낸다
            Resources["Duel.Brush.EggBackArt"] = Freeze(new ImageBrush(eggBack)
            {
                Stretch = Stretch.Fill,
                Viewbox = new Rect(0.02, 0.02, 0.96, 0.88),
                ViewboxUnits = BrushMappingMode.RelativeToBoundingBox
            });
        }
    }

    private static BitmapImage? LoadImage(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
