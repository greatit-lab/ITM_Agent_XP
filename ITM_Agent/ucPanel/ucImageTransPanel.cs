// ITM_Agent/ucPanel/ucImageTransPanel.cs
using ITM_Agent.Core;
using ITM_Agent.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace ITM_Agent.ucPanel
{
    public partial class ucImageTransPanel : UserControl, IPanelState
    {
        //--- 의존성 서비스 ---
        private readonly IAppServiceProvider _serviceProvider;
        private readonly ILogger _logger;
        private readonly SettingsManager _settingsManager;
        private readonly FileWatcherService _fileWatcher;
        private readonly PdfMergeManager _pdfMergeManager;

        //--- 내부 상태 관리 ---
        private readonly Dictionary<string, DateTime> _pendingFiles = new Dictionary<string, DateTime>();
        private readonly object _lock = new object();
        private System.Threading.Timer _checkTimer; // Timer 모호성 해결
        private readonly HashSet<string> _processedBaseNames = new HashSet<string>();

        // Settings.ini의 섹션 및 키 이름
        private const string Section = "ImageTrans";
        private const string KeyTargetFolder = "Target";
        private const string KeyWaitTime = "Wait";
        private const string KeySaveFolder = "SaveFolder";

        // 생성자 파라미터를 IAppServiceProvider로 수정
        public ucImageTransPanel(IAppServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _logger = _serviceProvider.GetService<ILogger>();
            _settingsManager = _serviceProvider.GetService<SettingsManager>();
            _fileWatcher = _serviceProvider.GetService<FileWatcherService>();

            // PdfMergeManager 생성자 호출 방식 수정
            _pdfMergeManager = new PdfMergeManager(_logger);

            InitializeComponent();
            this.Load += OnPanelLoad;
        }

        private void OnPanelLoad(object sender, EventArgs e)
        {
            RegisterEventHandlers();
            LoadSettings();
        }

        // ... 이하 나머지 코드는 이전 답변과 동일합니다 ...
        #region --- 설정 저장/삭제 및 UI 이벤트 핸들러 ---

        private void RegisterEventHandlers()
        {
            btn_SetFolder.Click += (s, e) => SaveSetting(KeyTargetFolder, cb_TargetImageFolder.Text);
            btn_FolderClear.Click += (s, e) => ClearSetting(KeyTargetFolder, cb_TargetImageFolder);
            btn_SetTime.Click += (s, e) => SaveSetting(KeyWaitTime, cb_WaitTime.Text);
            btn_TimeClear.Click += (s, e) => ClearSetting(KeyWaitTime, cb_WaitTime);
            btn_SelectOutputFolder.Click += SelectOutputFolder_Click;
        }

        private void LoadSettings()
        {
            var regexFolders = _settingsManager.GetSectionEntries("Regex")
                .Select(entry => entry.Split(new[] { "->" }, StringSplitOptions.None))
                .Where(parts => parts.Length == 2)
                .Select(parts => parts[1].Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            cb_TargetImageFolder.Items.Clear();
            cb_TargetImageFolder.Items.AddRange(regexFolders);
            cb_TargetImageFolder.SelectedItem = _settingsManager.GetValue(Section, KeyTargetFolder);

            cb_WaitTime.Items.Clear();
            cb_WaitTime.Items.AddRange(new object[] { "30", "60", "120", "180", "300" });
            cb_WaitTime.SelectedItem = _settingsManager.GetValue(Section, KeyWaitTime);

            lb_ImageSaveFolder.Text = _settingsManager.GetValue(Section, KeySaveFolder) ?? "(Not Set)";
        }

        private void SaveSetting(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                MessageBox.Show("값을 선택하거나 입력해야 합니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _settingsManager.SetValue(Section, key, value);
            MessageBox.Show("설정이 저장되었습니다.", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ClearSetting(string key, ComboBox comboBox)
        {
            comboBox.SelectedIndex = -1;
            comboBox.Text = "";
            _settingsManager.SetValue(Section, key, null);
        }

        private void SelectOutputFolder_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                string baseFolder = _settingsManager.GetSectionEntries("BaseFolder").FirstOrDefault();
                dialog.SelectedPath = !string.IsNullOrEmpty(baseFolder) ? baseFolder : AppDomain.CurrentDomain.BaseDirectory;
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    lb_ImageSaveFolder.Text = dialog.SelectedPath;
                    _settingsManager.SetValue(Section, KeySaveFolder, dialog.SelectedPath);
                }
            }
        }

        #endregion

        #region --- 파일 감시 및 PDF 병합 로직 ---

        private void OnWatchedFileChanged(string filePath, WatcherChangeTypes changeType)
        {
            string targetFolder = _settingsManager.GetValue(Section, KeyTargetFolder);
            if (string.IsNullOrEmpty(targetFolder) || !Path.GetDirectoryName(filePath).Equals(targetFolder, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string fileName = Path.GetFileNameWithoutExtension(filePath);
            if (fileName.Contains("_#1_") || !Regex.IsMatch(fileName, @"^.+_\d+$"))
            {
                return;
            }

            lock (_lock)
            {
                _pendingFiles[filePath] = DateTime.Now;
                if (_checkTimer == null)
                {
                    _checkTimer = new System.Threading.Timer(_ => CheckPendingFiles(), null, 1000, 1000);
                }
            }
        }

        private void CheckPendingFiles()
        {
            int waitSeconds = int.TryParse(_settingsManager.GetValue(Section, KeyWaitTime), out int sec) ? sec : 30;
            var now = DateTime.Now;
            List<string> filesToProcess = new List<string>();

            lock (_lock)
            {
                filesToProcess = _pendingFiles
                    .Where(kvp => (now - kvp.Value).TotalSeconds >= waitSeconds)
                    .Select(kvp => kvp.Key)
                    .ToList();

                filesToProcess.ForEach(path => _pendingFiles.Remove(path));

                if (!_pendingFiles.Any())
                {
                    _checkTimer?.Dispose();
                    _checkTimer = null;
                }
            }

            var baseNamesToProcess = filesToProcess
                .Select(GetBaseName)
                .Where(name => name != null)
                .Distinct();

            foreach (var baseName in baseNamesToProcess)
            {
                lock (_lock)
                {
                    if (_processedBaseNames.Contains(baseName)) continue;
                    _processedBaseNames.Add(baseName);
                }

                ThreadPool.QueueUserWorkItem(_ => MergeImageGroup(baseName, _settingsManager.GetValue(Section, KeyTargetFolder)));
            }
        }

        private void MergeImageGroup(string baseName, string folder)
        {
            try
            {
                string safeBaseName = baseName.Replace('.', '_');
                string saveFolder = _settingsManager.GetValue(Section, KeySaveFolder) ?? folder;
                string outputPdfPath = Path.Combine(saveFolder, $"{safeBaseName}.pdf");

                var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };
                var imageFiles = Directory.GetFiles(folder, $"{baseName}_*.*", SearchOption.TopDirectoryOnly)
                    .Where(file => imageExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    .Select(path => new { Path = path, Match = Regex.Match(Path.GetFileNameWithoutExtension(path), @"_(\d+)$") })
                    .Where(x => x.Match.Success)
                    .Select(x => new { x.Path, Page = int.Parse(x.Match.Groups[1].Value) })
                    .OrderBy(x => x.Page)
                    .Select(x => x.Path)
                    .ToList();

                if (imageFiles.Any())
                {
                    _pdfMergeManager.MergeImagesToPdf(imageFiles, outputPdfPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ucImageTransPanel] Failed to merge PDF for '{baseName}'. Error: {ex.Message}");
            }
        }

        private string GetBaseName(string filePath)
        {
            var match = Regex.Match(Path.GetFileNameWithoutExtension(filePath), @"^(?<basename>.+)_\d+$");
            return match.Success ? match.Groups["basename"].Value : null;
        }

        #endregion

        #region --- IPanelState 구현 ---

        public void UpdateState(bool isRunning)
        {
            this.Enabled = !isRunning;

            if (isRunning)
            {
                _processedBaseNames.Clear();
                _fileWatcher.FileChanged += OnWatchedFileChanged;
            }
            else
            {
                _fileWatcher.FileChanged -= OnWatchedFileChanged;
                _checkTimer?.Dispose();
                _checkTimer = null;
            }
        }

        #endregion
    }
}
