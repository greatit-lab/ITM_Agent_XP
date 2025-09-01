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
            StartWatchingBaseDateFolder(_baseDatePath);
        }

        private void OnBaseClearClick(object sender, EventArgs e)
        {
            cb_BaseDatePath.SelectedIndex = -1;
            _baseDatePath = null;
            _settingsManager.SetValue(Section, KeyBaseDatePath, null);
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

        #region --- 파일 처리 및 감시 로직 (수정됨) ---

        private void StartWatchingBaseDateFolder(string path)
        {
            StopWatchingBaseDateFolder();

            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                _logger.LogDebug($"[OverrideNames] Base date folder path is invalid or does not exist: '{path}'");
                return;
            }

            _baseDateFolderWatcher = new FileSystemWatcher(path)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                IncludeSubdirectories = true
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

                    RenameTargetFilesBasedOnInfo(infoFilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[OverrideNames] Failed to create .info file for '{filePath}'. Error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// .info 파일을 기반으로 대상 파일 이름을 변경하는 핵심 로직 (수정됨)
        /// </summary>
        private void RenameTargetFilesBasedOnInfo(string infoFilePath)
        {
            // 1. .info 파일 이름에서 핵심 정보(시간, prefix, C-info) 추출
            var infoMatch = Regex.Match(Path.GetFileName(infoFilePath), @"(\d{8}_\d{6})_([^_]+?)_(C\dW\d+)");
            if (!infoMatch.Success) return;

            string timeInfo = infoMatch.Groups[1].Value;   // 예: 20250812_143210
            string prefix = infoMatch.Groups[2].Value;     // 예: BB000000_17PT_ABC123.1
            string cInfo = infoMatch.Groups[3].Value;      // 예: C3W21

            // 2. 설정된 모든 '비교 대상 폴더'를 순회
            foreach (var targetFolder in _targetComparePaths)
            {
                if (!Directory.Exists(targetFolder)) continue;

                // ★★★★★ 핵심 수정 ★★★★★
                // 3. 시간과 prefix를 모두 포함하는 파일만 정확히 검색
                // 예: "*20250812_143210*BB000000_17PT_ABC123.1*_#1_*.dat"
                string searchPattern = $"*{timeInfo}*{prefix}*_#1_*.*";
                var filesToRename = Directory.GetFiles(targetFolder, searchPattern);

                // 4. 찾은 파일들의 이름을 변경
                foreach (var file in filesToRename)
                {
                    try
                    {
                        // File.Exists로 이중 확인하여 경쟁 상태로 인한 오류 방지
                        if (File.Exists(file))
                        {
                            string newFileName = Path.GetFileName(file).Replace("_#1_", $"_{cInfo}_");
                            string newFilePath = Path.Combine(targetFolder, newFileName);
                            File.Move(file, newFilePath);
                            _logger.LogEvent($"[OverrideNames] File renamed: {Path.GetFileName(file)} -> {newFileName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        // File.Move에서 발생하는 예외 기록
                        _logger.LogError($"[OverrideNames] Failed to rename file '{file}'. Error: {ex.Message}");
                    }
                }
            }
        }

        #endregion

        #region --- IPanelState 구현 ---
        private bool _isRunning = false;
        public void UpdateState(bool isRunning)
        {
            _isRunning = isRunning;
            // UI 컨트롤 활성화/비활성화
            SetChildControlsEnabled(this.groupBox1, !isRunning);
            SetChildControlsEnabled(this.groupBox2, !isRunning);

            if (isRunning)
            {
                StartWatchingBaseDateFolder(_baseDatePath);
            }
            else
            {
                StopWatchingBaseDateFolder();
            }
        }

        private void SetChildControlsEnabled(Control parent, bool enabled)
        {
            foreach (Control ctrl in parent.Controls)
            {
                ctrl.Enabled = enabled;
            }
        }

        #endregion
    }
}
