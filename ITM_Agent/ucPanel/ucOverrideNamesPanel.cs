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
        private readonly FileWatcherService _fileWatcher;

        //--- 내부 상태 관리 ---
        private string _baseDatePath;
        // ★★★★★★★★★★★★ 수정된 부분 ★★★★★★★★★★★★
        // 생성자에서 즉시 초기화하여 null 상태를 방지합니다.
        private List<string> _targetComparePaths = new List<string>();
        // ★★★★★★★★★★★★★★★★★★★★★★★★★★★★★
        private const double StabilityCheckSeconds = 2.0;
        private readonly Dictionary<string, DateTime> _pendingBaselineFiles = new Dictionary<string, DateTime>();
        private readonly object _lock = new object();
        private System.Threading.Timer _stabilityTimer;

        // Settings.ini의 섹션 및 키 이름
        private const string Section = "OverrideNames";
        private const string KeyBaseDatePath = "BaseDatePath";
        private const string SectionTargetCompare = "TargetComparePath";

        public ucOverrideNamesPanel(IAppServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _logger = _serviceProvider.GetService<ILogger>();
            _settingsManager = _serviceProvider.GetService<SettingsManager>();
            _fileWatcher = _serviceProvider.GetService<FileWatcherService>();

            InitializeComponent();

            // ★★★★★★★★★★★★ 수정된 부분 ★★★★★★★★★★★★
            // Load 이벤트 대신 생성자에서 직접 초기화 로직을 호출합니다.
            RegisterEventHandlers();
            LoadSettings();
            // ★★★★★★★★★★★★★★★★★★★★★★★★★★★★★
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

            _targetComparePaths = _settingsManager.GetSectionEntries(SectionTargetCompare);
            lb_TargetComparePath.Items.Clear();
            lb_TargetComparePath.Items.AddRange(_targetComparePaths.ToArray());
        }

        #region --- UI 이벤트 핸들러 ---

        private void OnBaseDatePathChanged(object sender, EventArgs e)
        {
            _baseDatePath = cb_BaseDatePath.SelectedItem?.ToString();
            _settingsManager.SetValue(Section, KeyBaseDatePath, _baseDatePath);
        }

        private void OnBaseClearClick(object sender, EventArgs e)
        {
            cb_BaseDatePath.SelectedIndex = -1;
            _baseDatePath = null;
            _settingsManager.SetValue(Section, KeyBaseDatePath, null);
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

        #region --- 파일 처리 로직 ---

        private void OnWatchedFileChanged(string filePath, WatcherChangeTypes changeType)
        {
            // GetDirectoryName이 null을 반환할 수 있으므로 방어 코드 추가
            string directory = Path.GetDirectoryName(filePath);
            if (directory == null) return;

            if (directory.Equals(_baseDatePath, StringComparison.OrdinalIgnoreCase))
            {
                lock (_lock)
                {
                    _pendingBaselineFiles[filePath] = DateTime.Now;
                    if (_stabilityTimer == null)
                    {
                        _stabilityTimer = new System.Threading.Timer(_ => CheckFileStability(), null, 1000, 1000);
                    }
                }
            }
            else if (_targetComparePaths.Any(p => directory.Equals(p, StringComparison.OrdinalIgnoreCase))
                     && filePath.EndsWith(".info", StringComparison.OrdinalIgnoreCase))
            {
                ThreadPool.QueueUserWorkItem(_ => RenameTargetFilesBasedOnInfo(filePath));
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
                // CP949 인코딩으로 파일 읽기
                string content;
                using (var sr = new StreamReader(filePath, Encoding.GetEncoding(949)))
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
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[OverrideNames] Failed to create .info file for '{filePath}'. Error: {ex.Message}");
            }
        }

        private void RenameTargetFilesBasedOnInfo(string infoFilePath)
        {
            var infoMatch = Regex.Match(Path.GetFileName(infoFilePath), @"_([^_]+?)_(C\dW\d+)\.info$");
            if (!infoMatch.Success) return;

            string prefix = infoMatch.Groups[1].Value;
            string cInfo = infoMatch.Groups[2].Value;
            string targetFolder = Path.GetDirectoryName(infoFilePath);

            var filesToRename = Directory.GetFiles(targetFolder, $"*_{prefix}_#1_*.dat");

            foreach (var file in filesToRename)
            {
                try
                {
                    string newFileName = Path.GetFileName(file).Replace("_#1_", $"_{cInfo}_");
                    string newFilePath = Path.Combine(targetFolder, newFileName);
                    File.Move(file, newFilePath);
                    _logger.LogEvent($"[OverrideNames] File renamed: {Path.GetFileName(file)} -> {newFileName}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[OverrideNames] Failed to rename file '{file}'. Error: {ex.Message}");
                }
            }
        }
        
        #endregion

        #region --- IPanelState 구현 ---
        public void UpdateState(bool isRunning)
        {
            this.Enabled = !isRunning;
            if (isRunning)
            {
                _fileWatcher.FileChanged += OnWatchedFileChanged;
            }
            else
            {
                _fileWatcher.FileChanged -= OnWatchedFileChanged;
                _stabilityTimer?.Dispose();
                _stabilityTimer = null;
            }
        }
        #endregion
    }
}
