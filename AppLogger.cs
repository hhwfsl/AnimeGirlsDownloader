using AnimeGirlsDownloader.Enums;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace AnimeGirlsDownloader;

public static class AppLogger
{
    private static readonly object SyncRoot = new();
    private static Action<InfoBarSeverity, string, InfoBarInfoType>? _messageSink;

    public static void Initialize(Action<InfoBarSeverity, string, InfoBarInfoType> messageSink)
    {
        _messageSink = messageSink ?? throw new ArgumentNullException(nameof(messageSink));
    }

    public static void LogInfo(string message) => WriteLog(InfoBarSeverity.Informational, message);
    public static void LogWarning(string message) => WriteLog(InfoBarSeverity.Warning, message);
    public static void LogError(string message) => WriteLog(InfoBarSeverity.Error, message);
    public static void LogSuccess(string message) => WriteLog(InfoBarSeverity.Success, message);

    public static void LogInfoWithInfoBar(string message, InfoBarInfoType type = InfoBarInfoType.Auto) =>
        WriteLogWithInfoBar(InfoBarSeverity.Informational, message, type);

    public static void LogWarningWithInfoBar(string message, InfoBarInfoType type = InfoBarInfoType.Manually) =>
        WriteLogWithInfoBar(InfoBarSeverity.Warning, message, type);

    public static void LogErrorWithInfoBar(string message, InfoBarInfoType type = InfoBarInfoType.Manually) =>
        WriteLogWithInfoBar(InfoBarSeverity.Error, message, type);

    public static void LogSuccessWithInfoBar(string message, InfoBarInfoType type = InfoBarInfoType.Auto) =>
        WriteLogWithInfoBar(InfoBarSeverity.Success, message, type);

    private static void WriteLogWithInfoBar(InfoBarSeverity severity, string message, InfoBarInfoType type)
    {
        WriteLog(severity, message);
        _messageSink?.Invoke(severity, message, type);
    }

    private static void WriteLog(InfoBarSeverity severity, string message)
    {
        string line = $"[{DateTimeOffset.Now:O}] [{severity}] {message}";
        Debug.WriteLine(line);

        try
        {
            lock (SyncRoot)
            {
                AppPaths.EnsureDataDirectories();
                string fileName = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".log";
                File.AppendAllText(Path.Combine(AppPaths.LogDirectory, fileName), line + Environment.NewLine);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Unable to write application log: {exception.Message}");
        }
    }
}
