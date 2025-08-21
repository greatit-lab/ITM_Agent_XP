// ITM_Agent.Services/FileClassifierService.cs
using ITM_Agent.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace ITM_Agent.Services
{
    public class FileClassifierService : IDisposable
    {
        private readonly ILogger _logger;
        private readonly SettingsManager _settingsManager;
        private readonly FileWatcherService _fileWatcher;
        private List<string> _excludeFolders;
        private Dictionary<string, string> _regexRules;

        public FileClassifierService(IAppServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetService<ILogger>();
            _settingsManager = serviceProvider.GetService<SettingsManager>();
            _fileWatcher = serviceProvider.GetService<FileWatcherService>();
        }

        public void Start()
        {
            _logger.LogEvent("[FileClassifier] Service started.");
            LoadSettings();
            _fileWatcher.FileChanged += OnWatchedFileChanged;
        }

        public void Stop()
        {
            _fileWatcher.FileChanged -= OnWatchedFileChanged;
            _logger.LogEvent("[FileClassifier] Service stopped.");
        }

        private void LoadSettings()
        {
            _excludeFolders = _settingsManager.GetSectionEntries("ExcludeFolders")
                .Select(p => p.ToUpperInvariant())
                .ToList();
                
            _regexRules = new Dictionary<string, string>();
            var regexEntries = _settingsManager.GetSectionEntries("Regex");
            foreach(var entry in regexEntries)
            {
                var parts = entry.Split(new[] { "->" }, StringSplitOptions.None);
                if(parts.Length == 2)
                {
                    _regexRules[parts[0].Trim()] = parts[1].Trim();
                }
            }
        }

        private void OnWatchedFileChanged(string filePath, WatcherChangeTypes changeType)
        {
            if (changeType != WatcherChangeTypes.Created && changeType != WatcherChangeTypes.Changed) return;
            
            ThreadPool.QueueUserWorkItem(_ => ProcessFile(filePath));
        }

        private void ProcessFile(string filePath)
        {
            try
            {
                // ★★★★★★★★★★★★ 수정된 부분 ★★★★★★★★★★★★
                // 파일 처리를 시작하기 전에 파일이 안정화될 때까지 대기합니다.
                if (!WaitForFileReady(filePath, 10, 500))
                {
                     _logger.LogError($"[FileClassifier] File copy failed (file locked after retries): {Path.GetFileName(filePath)}");
                     return;
                }
                // ★★★★★★★★★★★★★★★★★★★★★★★★★★★★★

                string fileDirectory = Path.GetDirectoryName(filePath)?.ToUpperInvariant();
                string fileName = Path.GetFileName(filePath);

                if (fileDirectory != null && _excludeFolders.Contains(fileDirectory))
                {
                    _logger.LogDebug($"[FileClassifier] File skipped (in excluded folder): {fileName}");
                    return;
                }

                foreach (var rule in _regexRules)
                {
                    if (Regex.IsMatch(fileName, rule.Key))
                    {
                        string destinationFolder = rule.Value;
                        string destinationFile = Path.Combine(destinationFolder, fileName);
                        
                        Directory.CreateDirectory(destinationFolder);

                        File.Copy(filePath, destinationFile, true);
                        _logger.LogEvent($"[FileClassifier] File '{fileName}' classified and copied to '{destinationFolder}'");
                        
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[FileClassifier] Error processing file '{filePath}'. Exception: {ex.Message}");
            }
        }
        
        // ★★★★★★★★★★★★ 추가된 메서드 ★★★★★★★★★★★★
        /// <summary>
        /// 파일이 다른 프로세스에 의해 잠겨 있지 않은지 확인하고, 잠겨 있다면 잠시 대기 후 재시도합니다.
        /// </summary>
        private bool WaitForFileReady(string filePath, int maxRetries, int delayMilliseconds)
        {
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    // 파일을 읽기/쓰기 공유 모드로 열어봄으로써 잠금 상태를 확인
                    using (File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        return true; // 파일 접근 성공
                    }
                }
                catch (IOException)
                {
                    Thread.Sleep(delayMilliseconds); // 잠시 대기 후 재시도
                }
            }
            return false; // 최종적으로 파일 접근 실패
        }
        // ★★★★★★★★★★★★★★★★★★★★★★★★★★★★★

        public void Dispose()
        {
            Stop();
        }
    }
}
