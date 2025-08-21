// ITM_Agent.Services/FileWatcherService.cs
using ITM_Agent.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace ITM_Agent.Services
{
    public class FileWatcherService : IDisposable
    {
        private readonly ILogger _logger;
        private readonly List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();
        private readonly object _lock = new object();
        private bool _isRunning = false;

        public event Action<string, WatcherChangeTypes> FileChanged;

        public FileWatcherService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Start(IEnumerable<string> foldersToWatch)
        {
            lock (_lock)
            {
                if (_isRunning)
                {
                    Stop();
                }

                if (foldersToWatch == null || !foldersToWatch.Any())
                {
                    _logger.LogEvent("[FileWatcherService] No folders to watch.");
                    return;
                }

                foreach (var folder in foldersToWatch.Distinct())
                {
                    if (!Directory.Exists(folder))
                    {
                        _logger.LogError($"[FileWatcherService] Watch folder does not exist: {folder}");
                        continue;
                    }

                    var watcher = new FileSystemWatcher(folder)
                    {
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                        // ★★★★★★★★★★★★ 수정된 부분 ★★★★★★★★★★★★
                        // 내부 버퍼 크기를 기본값(8KB)에서 최대값(64KB)으로 늘려 안정성 확보
                        InternalBufferSize = 65536
                        // ★★★★★★★★★★★★★★★★★★★★★★★★★★★★★
                    };

                    watcher.Created += OnFileEvent;
                    watcher.Changed += OnFileEvent;
                    watcher.Deleted += OnFileEvent;
                    watcher.Renamed += OnFileRenamedEvent;
                    watcher.Error += OnWatcherError; // 오류 이벤트 핸들러 연결

                    watcher.EnableRaisingEvents = true;
                    _watchers.Add(watcher);
                    _logger.LogEvent($"[FileWatcherService] Started watching folder: {folder}");
                }
                _isRunning = true;
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                if (!_isRunning) return;

                foreach (var watcher in _watchers)
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
                _watchers.Clear();
                _isRunning = false;
                _logger.LogEvent("[FileWatcherService] All watchers stopped.");
            }
        }

        private void OnFileEvent(object sender, FileSystemEventArgs e)
        {
            FileChanged?.Invoke(e.FullPath, e.ChangeType);
        }

        private void OnFileRenamedEvent(object sender, RenamedEventArgs e)
        {
            FileChanged?.Invoke(e.FullPath, WatcherChangeTypes.Created);
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            // 오류 발생 시 로그만 기록하고, 감시 자체는 계속 유지되도록 함
            _logger.LogError($"[FileWatcherService] A watcher error occurred: {e.GetException()?.Message}");
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
