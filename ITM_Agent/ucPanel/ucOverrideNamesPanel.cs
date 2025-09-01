// ITM_Agent/ucPanel/ucOverrideNamesPanel.cs
using ITM_Agent.Core;
using ITM_Agent.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace ITM_Agent.ucPanel
{
    public partial class ucOverrideNamesPanel : UserControl, IPanelState
    {
        //--- 의존성 서비스 ---
        private readonly IAppServiceProvider _serviceProvider;
        private readonly ILogger _logger;
        private readonly SettingsManager _settingsManager;

        //--- 내부 상태 관리 ---
        private string _baseDatePath;
        private List<string> _targetComparePaths = new List<string>();

        // ★★★★★ 누락되었던 FileSystemWatcher 추가 ★★★★★
        private FileSystemWatcher _baseDateFolderWatcher;
        private readonly Dictionary<string, DateTime> _pendingBaselineFiles = new Dictionary<string, DateTime>();
        private readonly object _lock = new object();
        private System.Threading.Timer _stabilityTimer;
        private const double StabilityCheckSeconds = 2.0;

        // Settings.ini의 섹션 및 키 이름
        private const string Section = "OverrideNames";
        private const string KeyBaseDatePath = "BaseDatePath";
        private const string SectionTargetCompare = "TargetComparePath";

        public ucOverrideNamesPanel(IAppServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _logger = _serviceProvider.GetService<ILogger>();
            _settingsManager = _serviceProvider.GetService<SettingsManager>();
            
            // FileWatcherService는 더 이상 직접 사용하지 않습니다.

            InitializeComponent();

            RegisterEventHandlers();
            LoadSettings();
        }

        private void RegisterEventHandlers()
        {
            cb_BaseDatePath.SelectedIndexChanged += OnBaseDatePathChanged;
            btn_BaseClear.Click += OnBaseClearClick;
            btn_SelectFolder.Click += OnSelectTargetFolderClick;
            btn_Remove.Click += OnRemoveTargetFolderClick;
        }

        private void LoadSettings()
        {
            var regexFolders = _settingsManager.GetSectionEntries("Regex")
                .Select(e => e.Split(new[] { "->" }, StringSplitOptions.None))
                .Where(parts => parts.Length == 2)
                .Select(parts => parts[1].Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            cb_BaseDatePath.Items.Clear();
            cb_BaseDatePath.Items.AddRange(regexFolders);

            _baseDatePath = _settingsManager.GetValue(Section, KeyBaseDatePath);
            cb_BaseDatePath.SelectedItem = _baseDatePath;

            // _baseDatePath가 설정되어 있으면 감시 시작
            if (!string.IsNullOrEmpty(_baseDatePath))
            {
                StartWatchingBaseDateFolder(_baseDatePath);
            }

            _targetComparePaths = _settingsManager.GetSectionEntries(SectionTargetCompare);
            lb_TargetComparePath.Items.Clear();
            lb_TargetComparePath.Items.AddRange(_targetComparePaths.ToArray());
        }

        #region --- UI 이벤트 핸들러 ---

        private void OnBaseDatePathChanged(object sender, EventArgs e)
        {
            _baseDatePath = cb_BaseDatePath.SelectedItem?.ToString();
            _settingsManager.SetValue(Section, KeyBaseDatePath, _baseDatePath);
            // 선택된 폴더로 감시자 재시작
            StartWatchingBaseDateFolder(_baseDatePath);
        }

        private void OnBaseClearClick(object sender, EventArgs e)
        {
            cb_BaseDatePath.SelectedIndex = -1;
            _baseDatePath = null;
            _settingsManager.SetValue(Section, KeyBaseDatePath, null);
            // 감시자 중지
            StopWatchingBaseDateFolder();
        }

        private void OnSelectTargetFolderClick(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    if (!_targetComparePaths.Contains(dialog.SelectedPath, StringComparer.OrdinalIgnoreCase))
                    {
                        _targetComparePaths.Add(dialog.SelectedPath);
                        _settingsManager.SetSectionEntries(SectionTargetCompare, _targetComparePaths);
                        LoadSettings();
                    }
                }
            }
        }

        private void OnRemoveTargetFolderClick(object sender, EventArgs e)
        {
            if (lb_TargetComparePath.SelectedItem == null) return;
            string selected = lb_TargetComparePath.SelectedItem.ToString();
            _targetComparePaths.Remove(selected);
            _settingsManager.SetSectionEntries(SectionTargetCompare, _targetComparePaths);
            LoadSettings();
        }
        #endregion

        #region --- 파일 처리 및 감시 로직 (기능 복원) ---

        private void StartWatchingBaseDateFolder(string path)
        {
            StopWatchingBaseDateFolder(); // 기존 감시자 정리

            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                _logger.LogDebug($"[OverrideNames] Base date folder path is invalid or does not exist: '{path}'");
                return;
            }

            _baseDateFolderWatcher = new FileSystemWatcher(path)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                IncludeSubdirectories = true // 원본 코드와 동일하게 하위 폴더 포함
            };

            _baseDateFolderWatcher.Created += OnBaseDateFileEvent;
            _baseDateFolderWatcher.Changed += OnBaseDateFileEvent;
            _baseDateFolderWatcher.EnableRaisingEvents = true;
            _logger.LogEvent($"[OverrideNames] Started watching base date folder: {path}");
        }

        private void StopWatchingBaseDateFolder()
        {
            if (_baseDateFolderWatcher != null)
            {
                _baseDateFolderWatcher.EnableRaisingEvents = false;
                _baseDateFolderWatcher.Dispose();
                _baseDateFolderWatcher = null;
                _logger.LogEvent("[OverrideNames] Stopped watching base date folder.");
            }
        }

        private void OnBaseDateFileEvent(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(e.FullPath)) return;

            lock (_lock)
            {
                _pendingBaselineFiles[e.FullPath] = DateTime.Now;
                if (_stabilityTimer == null)
                {
                    _stabilityTimer = new System.Threading.Timer(_ => CheckFileStability(), null, 1000, 1000);
                }
            }
        }
        
        // .info 파일 생성 및 이름 변경 로직은 원본과 거의 동일하게 복원
        private void CheckFileStability()
        {
            var stableFiles = new List<string>();
            lock (_lock)
            {
                var now = DateTime.Now;
                stableFiles = _pendingBaselineFiles
                    .Where(f => (now - f.Value).TotalSeconds >= StabilityCheckSeconds)
                    .Select(f => f.Key).ToList();

                stableFiles.ForEach(f => _pendingBaselineFiles.Remove(f));

                if (!_pendingBaselineFiles.Any())
                {
                    _stabilityTimer?.Dispose();
                    _stabilityTimer = null;
                }
            }

            foreach (var stableFile in stableFiles)
            {
                ThreadPool.QueueUserWorkItem(_ => CreateBaselineInfoFile(stableFile));
            }
        }

        private void CreateBaselineInfoFile(string filePath)
        {
            try
            {
                string content;
                using (var sr = new StreamReader(filePath, Encoding.GetEncoding(949), true))
                {
                    content = sr.ReadToEnd();
                }

                var match = Regex.Match(content, @"Date and Time:\s*(\d{2}/\d{2}/\d{4} \d{2}:\d{2}:\d{2} (AM|PM))");
                if (match.Success && DateTime.TryParse(match.Groups[1].Value, out DateTime result))
                {
                    string baseFolder = _settingsManager.GetSectionEntries("BaseFolder").FirstOrDefault();
                    if (string.IsNullOrEmpty(baseFolder)) return;

                    string baselineFolder = Path.Combine(baseFolder, "Baseline");
                    Directory.CreateDirectory(baselineFolder);

                    string originalName = Path.GetFileNameWithoutExtension(filePath);
                    string infoFileName = $"{result:yyyyMMdd_HHmmss}_{originalName}.info";
                    string infoFilePath = Path.Combine(baselineFolder, infoFileName);

                    File.Create(infoFilePath).Close();
                    _logger.LogEvent($"[OverrideNames] Baseline info file created: {infoFileName}");

                    // .info 파일 생성 후 즉시 이름 변경 로직 트리거
                    RenameTargetFilesBasedOnInfo(infoFilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[OverrideNames] Failed to create .info file for '{filePath}'. Error: {ex.Message}");
            }
        }

        private void RenameTargetFilesBasedOnInfo(string infoFilePath)
        {
            var infoMatch = Regex.Match(Path.GetFileName(infoFilePath), @"(\d{8}_\d{6})_([^_]+?)_(C\dW\d+)");
            if (!infoMatch.Success) return;
            
            string timeInfo = infoMatch.Groups[1].Value;
            string prefix = infoMatch.Groups[2].Value;
            string cInfo = infoMatch.Groups[3].Value;
            
            foreach(var targetFolder in _targetComparePaths)
            {
                if(!Directory.Exists(targetFolder)) continue;

                var filesToRename = Directory.GetFiles(targetFolder, $"*{prefix}*");

                foreach (var file in filesToRename)
                {
                    try
                    {
                        if(Path.GetFileName(file).Contains("_#1_"))
                        {
                            string newFileName = Path.GetFileName(file).Replace("_#1_", $"_{cInfo}_");
                            string newFilePath = Path.Combine(targetFolder, newFileName);
                            File.Move(file, newFilePath);
                            _logger.LogEvent($"[OverrideNames] File renamed: {Path.GetFileName(file)} -> {newFileName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[OverrideNames] Failed to rename file '{file}'. Error: {ex.Message}");
                    }
                }
            }
        }

        #endregion

        #region --- IPanelState 구현 ---
        public void UpdateState(bool isRunning)
        {
            this.Enabled = !isRunning;
            
            // 실행 상태에 따라 자체 Watcher 제어
            if (isRunning)
            {
                StartWatchingBaseDateFolder(_baseDatePath);
            }
            else
            {
                StopWatchingBaseDateFolder();
            }
        }
        #endregion
    }
}
