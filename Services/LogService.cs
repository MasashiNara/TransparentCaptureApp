using System;
using System.IO;
using System.Text;
using TransparentCaptureApp.Utilities;

namespace TransparentCaptureApp.Services;

public sealed class LogService
{
    private readonly object _sync = new();
    private string _logFilePath;

    public LogService(string logFilePath)
    {
        _logFilePath = logFilePath;
    }

    public string LogFilePath => _logFilePath;

    public void SetLogFilePath(string logFilePath)
    {
        _logFilePath = logFilePath;
    }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message) => Write("ERROR", message);

    public void Error(Exception ex, string message)
    {
        Write("ERROR", $"{message}: {ex}");
    }

    private void Write(string level, string message)
    {
        lock (_sync)
        {
            PathUtility.EnsureParentDirectory(_logFilePath);
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
            File.AppendAllText(_logFilePath, line, new UTF8Encoding(false));
        }
    }
}
