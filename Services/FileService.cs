using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using TransparentCaptureApp.Utilities;

namespace TransparentCaptureApp.Services;

public sealed class FileService
{
    public void EnsureDirectory(string directory)
    {
        Directory.CreateDirectory(directory);
    }

    public string CreateUniquePath(string directory, string prefix, string extension, DateTime timestamp)
    {
        Directory.CreateDirectory(directory);
        var baseName = $"{prefix}_{timestamp:yyyyMMdd_HHmmss}";
        var path = Path.Combine(directory, $"{baseName}{extension}");
        if (!File.Exists(path))
        {
            return path;
        }

        for (var i = 1; i < 1000; i++)
        {
            path = Path.Combine(directory, $"{baseName}_{i:000}{extension}");
            if (!File.Exists(path))
            {
                return path;
            }
        }

        throw new IOException("一意なファイル名を作成できませんでした。");
    }

    public void SaveText(string path, string text)
    {
        PathUtility.EnsureParentDirectory(path);
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }

    public void OpenFile(string path)
    {
        Process.Start(new ProcessStartInfo(path)
        {
            UseShellExecute = true
        });
    }

    public void OpenDirectory(string directory)
    {
        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{directory}\"")
        {
            UseShellExecute = true
        });
    }
}
