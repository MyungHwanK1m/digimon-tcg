using System;
using System.IO;

namespace DigimonSurvivors.Tools;

public static class ShortcutCreator
{
    public static void CreateAllShortcuts(string targetExe, string iconPath)
    {
        string workDir = Path.GetDirectoryName(targetExe)!;

        string[] desktopFolders = {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "Desktop"),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
        };

        Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null) return;
        dynamic shell = Activator.CreateInstance(shellType)!;

        foreach (var desktop in desktopFolders)
        {
            if (!Directory.Exists(desktop)) continue;

            string[] shortcutNames = { "디지몬 서바이버즈.lnk", "Digimon Survivors.lnk" };
            foreach (var name in shortcutNames)
            {
                try
                {
                    string lnkPath = Path.Combine(desktop, name);
                    dynamic shortcut = shell.CreateShortcut(lnkPath);
                    shortcut.TargetPath = targetExe;
                    shortcut.WorkingDirectory = workDir;
                    shortcut.IconLocation = $"{iconPath},0";
                    shortcut.Description = "⚔️ DIGIMON SURVIVORS : DIGITAL RUSH";
                    shortcut.Save();
                }
                catch
                {
                    // 무시
                }
            }
        }
    }
}
