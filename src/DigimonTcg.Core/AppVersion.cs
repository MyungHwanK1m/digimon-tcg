using System.Reflection;

namespace DigimonTcg.Core;

/// <summary>
/// 앱 버전 (src/DigimonTcg.Version.props의 Version + 빌드한 git 커밋 해시).
/// 예: Number = "0.8.0", Commit = "bf07dad", Display = "v0.8.0 (bf07dad)"
/// </summary>
public static class AppVersion
{
    private static readonly string Informational =
        typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(AppVersion).Assembly.GetName().Version?.ToString(3)
        ?? "0.0.0";

    /// <summary>버전 번호 (예: 0.8.0)</summary>
    public static string Number => Informational.Split('+')[0];

    /// <summary>빌드한 커밋 해시 (git이 없던 빌드면 빈 문자열)</summary>
    public static string Commit
    {
        get
        {
            int plus = Informational.IndexOf('+');
            if (plus < 0) return string.Empty;
            string hash = Informational[(plus + 1)..];
            return hash.Length > 7 ? hash[..7] : hash;
        }
    }

    /// <summary>화면 표시용 (예: v0.8.0 (bf07dad))</summary>
    public static string Display => Commit.Length > 0 ? $"v{Number} ({Commit})" : $"v{Number}";
}
