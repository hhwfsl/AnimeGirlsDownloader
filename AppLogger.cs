using AnimeGirlsDownloader.Enums;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading;

namespace AnimeGirlsDownloader
{
    public static class AppLogger
    {
        private static readonly SemaphoreSlim _logLock = new SemaphoreSlim(1, 1);
        private static Action<InfoBarSeverity, string, InfoBarInfoType>? _addToMessageQueue = null;
        //private static Action<string>? _InfoInfomation = null;
        private static async void WriteLog(InfoBarSeverity status, string message)
        {
            await _logLock.WaitAsync();
            string log = $"[{status}][{DateTime.Now:G}] {message}";
            string path = Path.Combine(AppConsts.AppLogPath, $"{DateTime.Now:D}.txt");
            Console.WriteLine(log);
            if (!Directory.Exists(AppConsts.AppLogPath))
            {
                Directory.CreateDirectory(AppConsts.AppLogPath);
            }
            if (!File.Exists(path))
            {
                File.Create(path).Close();
            }
            await File.AppendAllTextAsync(path, log + Environment.NewLine);
            _logLock.Release();
        }
        private static void AddToInfoBarQueue(InfoBarSeverity status, string message, InfoBarInfoType type)
        {
            _addToMessageQueue?.Invoke(status, message, type);
        }
        public static void Initialize(Action<InfoBarSeverity, string, InfoBarInfoType> addToMessageQueue)
        {
            _addToMessageQueue = addToMessageQueue;
        }
        public static void LogInfo(string message)
        {
            WriteLog(InfoBarSeverity.Informational, message);
        }
        public static void LogWarning(string message)
        {
            WriteLog(InfoBarSeverity.Warning, message);
        }
        public static void LogError(string message)
        {
            WriteLog(InfoBarSeverity.Error, message);
        }
        public static void LogSuccess(string message)
        {
            WriteLog(InfoBarSeverity.Success, message);
        }
        public static void LogInfoWithInfoBar(string message, InfoBarInfoType type = InfoBarInfoType.Auto)
        {
            WriteLog(InfoBarSeverity.Informational, message);
            AddToInfoBarQueue(InfoBarSeverity.Informational, message, type);
        }
        public static void LogWarningWithInfoBar(string message, InfoBarInfoType type = InfoBarInfoType.Manually)
        {
            WriteLog(InfoBarSeverity.Warning, message);
            AddToInfoBarQueue(InfoBarSeverity.Warning, message, type);
        }
        public static void LogErrorWithInfoBar(string message, InfoBarInfoType type = InfoBarInfoType.Manually)
        {
            WriteLog(InfoBarSeverity.Error, message);
            AddToInfoBarQueue(InfoBarSeverity.Error, message, type);
        }
        public static void LogSuccessWithInfoBar(string message, InfoBarInfoType type = InfoBarInfoType.Auto)
        {
            WriteLog(InfoBarSeverity.Success, message);
            AddToInfoBarQueue(InfoBarSeverity.Success, message, type);
        }
    }
}
