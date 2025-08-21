// ITM_Agent.Services/InfoRetentionCleaner.cs
using ITM_Agent.Core;
using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace ITM_Agent.Services
{
    public class InfoRetentionCleaner : IDisposable
    {
        private readonly ILogger _logger;
        private readonly SettingsManager _settingsManager;
        private Timer _timer;

        private static readonly Regex[] DatePatterns = new[]
        {
            new Regex(@"(?<!\d)(?<ymd>\d{8})_(?<hms>\d{6})(?!\d)", RegexOptions.Compiled),
            new Regex(@"(?<!\d)(?<date>\d{4}-\d{2}-\d{2})(?!\d)", RegexOptions.Compiled),
            new Regex(@"(?<!\d)(?<ymd>\d{8})(?!\d)", RegexOptions.Compiled)
        };

        // 생성자의 파라미터를 IAppServiceProvider로 수정
        public InfoRetentionCleaner(IAppServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetService<ILogger>();
            _settingsManager = serviceProvider.GetService<SettingsManager>();

            if (_logger == null || _settingsManager == null)
            {
                throw new InvalidOperationException("InfoRetentionCleaner에 필수 서비스가 주입되지 않았습니다.");
            }
        }

        public void Start()
        {
            if (_timer != null) return;
            _timer = new Timer(_ => Execute(), null, TimeSpan.Zero, TimeSpan.FromHours(1));
            _logger.LogEvent("[InfoRetentionCleaner] Service started. Will run every 1 hour.");
        }

        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
            _logger.LogEvent("[InfoRetentionCleaner] Service stopped.");
        }

        // ... 이하 나머지 코드는 변경 없음 ...
        private void Execute()
        {
            if (_settingsManager.GetValue("Option", "EnableInfoAutoDel") != "1")
            {
                _logger.LogDebug("[InfoRetentionCleaner] Auto-deletion is disabled. Skipping execution.");
                return;
            }

            if (!int.TryParse(_settingsManager.GetValue("Option", "InfoRetentionDays"), out int retentionDays) || retentionDays <= 0)
            {
                _logger.LogDebug("[InfoRetentionCleaner] Invalid retention days setting. Skipping execution.");
                return;
            }

            string baseFolder = _settingsManager.GetValue("BaseFolder", "");
            if (string.IsNullOrEmpty(baseFolder) || !Directory.Exists(baseFolder))
            {
                _logger.LogDebug("[InfoRetentionCleaner] BaseFolder is not set or does not exist. Skipping execution.");
                return;
            }

            _logger.LogEvent($"[InfoRetentionCleaner] Starting cleanup task. Retention period: {retentionDays} days.");
            CleanFolderRecursively(baseFolder, retentionDays);
        }

        private void CleanFolderRecursively(string rootDirectory, int retentionDays)
        {
            try
            {
                DateTime cutoffDate = DateTime.Today.AddDays(-retentionDays);

                foreach (var file in Directory.EnumerateFiles(rootDirectory, "*.*", SearchOption.AllDirectories))
                {
                    DateTime? fileDate = TryExtractDateFromFileName(Path.GetFileName(file));
                    if (fileDate.HasValue && fileDate.Value.Date < cutoffDate)
                    {
                        TryDeleteFile(file);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[InfoRetentionCleaner] An error occurred during recursive clean. Error: {ex.Message}");
            }
        }

        private DateTime? TryExtractDateFromFileName(string fileName)
        {
            foreach (var pattern in DatePatterns)
            {
                var match = pattern.Match(fileName);
                if (match.Success)
                {
                    string dateStr = match.Groups["ymd"]?.Value ?? match.Groups["date"]?.Value;
                    if (DateTime.TryParseExact(dateStr, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result) ||
                        DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                    {
                        return result.Date;
                    }
                }
            }
            return null;
        }

        private void TryDeleteFile(string filePath)
        {
            try
            {
                File.SetAttributes(filePath, FileAttributes.Normal);
                File.Delete(filePath);
                _logger.LogEvent($"[InfoRetentionCleaner] Deleted old file: {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[InfoRetentionCleaner] Failed to delete file '{filePath}'. Error: {ex.Message}");
            }
        }
        
        public void Dispose()
        {
            Stop();
        }
    }
}
