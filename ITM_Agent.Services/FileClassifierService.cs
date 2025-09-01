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

        // --- 파일 안정화 로직을 위한 필드 추가 ---
        private readonly Dictionary<string, DateTime> _pendingFiles = new Dictionary<string, DateTime>();
        private readonly object _lock = new object();
        private Timer _stabilityTimer;
        private const int STABILITY_CHECK_INTERVAL_MS = 1500; // 1.5초마다 안정화 여부 체크
        private const int STABILITY_THRESHOLD_SECONDS = 3;    // 3초간 변경 없으면 안정화로 간주

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
            _stabilityTimer?.Dispose(); // 타이머 정지
            _stabilityTimer = null;
            _logger.LogEvent("[FileClassifier] Service stopped.");
        }

        private void LoadSettings()
        {
            _excludeFolders = _settingsManager.GetSectionEntries("ExcludeFolders")
                .Select(p => p.ToUpperInvariant())
                .ToList();

            _regexRules = new Dictionary<string, string>();
            var regexEntries = _settingsManager.GetSectionEntries("Regex");
            foreach (var entry in regexEntries)
            {
                var parts = entry.Split(new[] { "->" }, StringSplitOptions.None);
                if (parts.Length == 2)
                {
                    _regexRules[parts[0].Trim()] = parts[1].Trim();
                }
            }
        }

        /// <summary>
        /// 파일 변경 이벤트 핸들러 (수정됨)
        /// 파일을 바로 처리하지 않고 '대기 큐'에 추가하여 안정화를 기다립니다.
        /// </summary>
        private void OnWatchedFileChanged(string filePath, WatcherChangeTypes changeType)
        {
            // 파일이 아닌 폴더이거나, 삭제 이벤트인 경우 무시
            if (!File.Exists(filePath) || changeType == WatcherChangeTypes.Deleted)
            {
                return;
            }

            lock (_lock)
            {
                // 파일 경로를 키로 사용하여 마지막 변경 시간을 기록/갱신
                _pendingFiles[filePath] = DateTime.Now;

                // 안정화 체크 타이머가 없으면 새로 생성하여 시작
                if (_stabilityTimer == null)
                {
                    _stabilityTimer = new Timer(
                        _ => CheckFileStability(),
                        null,
                        STABILITY_CHECK_INTERVAL_MS,
                        STABILITY_CHECK_INTERVAL_MS);
                }
            }
        }

        /// <summary>
        /// 주기적으로 파일 변경이 멈췄는지(안정화되었는지) 검사하는 메서드 (신규 추가)
        /// </summary>
        private void CheckFileStability()
        {
            List<string> stableFiles;
            lock (_lock)
            {
                DateTime now = DateTime.Now;
                // 마지막 변경 시간으로부터 일정 시간(STABILITY_THRESHOLD_SECONDS)이 지난 파일들을 '안정화'된 것으로 간주
                stableFiles = _pendingFiles
                    .Where(kvp => (now - kvp.Value).TotalSeconds >= STABILITY_THRESHOLD_SECONDS)
                    .Select(kvp => kvp.Key)
                    .ToList();

                // 안정화된 파일들은 대기 큐에서 제거
                stableFiles.ForEach(file => _pendingFiles.Remove(file));

                // 대기 큐가 비었으면 타이머 중지 (리소스 절약)
                if (!_pendingFiles.Any())
                {
                    _stabilityTimer?.Dispose();
                    _stabilityTimer = null;
                }
            }

            // 안정화된 파일들을 백그라운드 스레드에서 순차적으로 처리
            foreach (var filePath in stableFiles)
            {
                ThreadPool.QueueUserWorkItem(_ => ProcessFile(filePath));
            }
        }


        /// <summary>
        /// 실제 파일 복사 및 분류 로직 (기존 메서드 재사용)
        /// </summary>
        private void ProcessFile(string filePath)
        {
            try
            {
                // 파일이 실제로 존재하는지 한 번 더 확인
                if (!File.Exists(filePath))
                {
                    _logger.LogDebug($"[FileClassifier] File no longer exists, skipping: {Path.GetFileName(filePath)}");
                    return;
                }
                
                // 안정화 대기 로직이 추가되었으므로, 파일 잠금 재시도(WaitForFileReady)는 여기서 불필요
                // if (!WaitForFileReady(filePath, 10, 500)) ... (제거)

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
                // 파일 복사 실패 시, 상세한 오류 기록
                _logger.LogError($"[FileClassifier] Error processing file '{filePath}'. Exception: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
