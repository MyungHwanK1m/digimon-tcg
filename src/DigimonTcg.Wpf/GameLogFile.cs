using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DigimonTcg.Wpf;

/// <summary>
/// 경기 로그를 파일로 남긴다 — 엔진의 ActionLogs는 메모리에만 있어 앱을 끄면 사라지므로,
/// 신고("20:10 경기에서 ~가 안 됐다")를 나중에 확인할 수 있게 한다.
/// 위치: %LOCALAPPDATA%\DigimonTcg\logs\game_yyyyMMdd_HHmmss.txt (경기 시작 시각), 최근 KeepCount개만 남긴다.
/// 파일을 못 쓰는 상황(권한 · 디스크)에서도 게임은 그대로 진행한다.
/// </summary>
internal sealed class GameLogFile
{
    private const int KeepCount = 30;

    public static string Directory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DigimonTcg", "logs");

    private string? _path;
    private string _header = string.Empty;
    private int _writtenCount = -1;

    /// <summary>새 경기 — 파일은 첫 Save 때(경기가 실제로 시작된 뒤) 만든다</summary>
    public void NewGame(string header)
    {
        _path = null;
        _header = header;
        _writtenCount = -1;
    }

    /// <summary>지금까지의 로그 전체로 이 경기 파일을 덮어쓴다 (로그가 늘었을 때만)</summary>
    public void Save(IReadOnlyList<string> logs)
    {
        if (logs.Count == _writtenCount)
            return;
        try
        {
            if (_path == null)
            {
                System.IO.Directory.CreateDirectory(Directory);
                _path = Path.Combine(Directory, $"game_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                Prune();
            }
            var text = new StringBuilder();
            text.AppendLine(_header);
            text.AppendLine($"기록 시각: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            text.AppendLine(new string('-', 60));
            foreach (var line in logs)
                text.AppendLine(line);
            File.WriteAllText(_path, text.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            _writtenCount = logs.Count;
        }
        catch (Exception)
        {
            // 로그 파일은 보조 기능 — 실패해도 게임을 멈추지 않는다
        }
    }

    private static void Prune()
    {
        foreach (var old in new DirectoryInfo(Directory).GetFiles("game_*.txt")
                     .OrderByDescending(f => f.Name, StringComparer.Ordinal)
                     .Skip(KeepCount - 1))   // 곧 만들 새 파일 자리를 남긴다
        {
            try { old.Delete(); } catch { }
        }
    }
}
