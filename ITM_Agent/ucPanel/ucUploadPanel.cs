// ITM_Agent/ucPanel/ucUploadPanel.cs
using ITM_Agent.Core;
using ITM_Agent.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace ITM_Agent.ucPanel
{
    public partial class ucUploadPanel : UserControl, IPanelState
    {
        //--- 의존성 서비스 ---
        private readonly IAppServiceProvider _serviceProvider;
        private readonly ILogger _logger;
        private readonly SettingsManager _settingsManager;
        private readonly PluginManager _pluginManager;
        private readonly FileWatcherService _fileWatcher;

        // Settings.ini의 섹션 및 키 이름을 상수로 관리
        private const string UploadSection = "UploadSetting";

        private struct UploadTarget
        {
            public string Key { get; }
            public ComboBox PathCombo { get; }
            public ComboBox PluginCombo { get; }

            public UploadTarget(string key, ComboBox pathCombo, ComboBox pluginCombo)
            {
                Key = key;
                PathCombo = pathCombo;
                PluginCombo = pluginCombo;
            }
        }

        private readonly List<UploadTarget> _uploadTargets;

        // 생성자 파라미터를 IAppServiceProvider로 수정
        public ucUploadPanel(IAppServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _logger = _serviceProvider.GetService<ILogger>();
            _settingsManager = _serviceProvider.GetService<SettingsManager>();
            _pluginManager = _serviceProvider.GetService<PluginManager>();
            _fileWatcher = _serviceProvider.GetService<FileWatcherService>();

            if (_logger == null || _settingsManager == null || _pluginManager == null || _fileWatcher == null)
                throw new InvalidOperationException("필수 서비스가 주입되지 않았습니다.");

            InitializeComponent();

            _uploadTargets = new List<UploadTarget>
            {
                new UploadTarget("WaferFlat", cb_WaferFlat_Path, cb_FlatPlugin),
                new UploadTarget("PreAlign", cb_PreAlign_Path, cb_PreAlignPlugin),
                new UploadTarget("Image", cb_ImgPath, cb_ImagePlugin),
                new UploadTarget("Error", cb_ErrPath, cb_ErrPlugin),
                new UploadTarget("Event", cb_EvPath, cb_EvPlugin),
                new UploadTarget("Wave", cb_WavePath, cb_WavePlugin),
            };

            this.Load += OnPanelLoad;
        }

        private void OnPanelLoad(object sender, EventArgs e)
        {
            RegisterEventHandlers();
            LoadCommonData();
            LoadAllUploadSettings();
        }

        // ... 이하 나머지 코드는 이전 답변과 동일합니다 ...

        #region --- 설정 및 UI 이벤트 핸들러 ---
        private void RegisterEventHandlers()
        {
            btn_FlatSet.Click += (s, e) => SaveUploadSetting(_uploadTargets[0]);
            btn_PreAlignSet.Click += (s, e) => SaveUploadSetting(_uploadTargets[1]);
            btn_ImgSet.Click += (s, e) => SaveUploadSetting(_uploadTargets[2]);
            btn_ErrSet.Click += (s, e) => SaveUploadSetting(_uploadTargets[3]);
            btn_EvSet.Click += (s, e) => SaveUploadSetting(_uploadTargets[4]);
            btn_WaveSet.Click += (s, e) => SaveUploadSetting(_uploadTargets[5]);

            btn_FlatClear.Click += (s, e) => ClearUploadSetting(_uploadTargets[0]);
            btn_PreAlignClear.Click += (s, e) => ClearUploadSetting(_uploadTargets[1]);
            btn_ImgClear.Click += (s, e) => ClearUploadSetting(_uploadTargets[2]);
            btn_ErrClear.Click += (s, e) => ClearUploadSetting(_uploadTargets[3]);
            btn_EvClear.Click += (s, e) => ClearUploadSetting(_uploadTargets[4]);
            btn_WaveClear.Click += (s, e) => ClearUploadSetting(_uploadTargets[5]);
        }

        private void LoadCommonData()
        {
            var regexFolders = _settingsManager.GetSectionEntries("Regex")
                .Select(entry => entry.Split(new[] { "->" }, StringSplitOptions.None))
                .Where(parts => parts.Length == 2)
                .Select(parts => parts[1].Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var pluginNames = _pluginManager.GetLoadedPlugins()
                .Select(p => p.Name)
                .ToArray();

            foreach (var target in _uploadTargets)
            {
                target.PathCombo.Items.Clear();
                target.PathCombo.Items.AddRange(regexFolders);
                target.PluginCombo.Items.Clear();
                target.PluginCombo.Items.AddRange(pluginNames);
            }
        }

        private void LoadAllUploadSettings()
        {
            foreach (var target in _uploadTargets)
            {
                string settingValue = _settingsManager.GetValue(UploadSection, target.Key);
                if (!string.IsNullOrEmpty(settingValue))
                {
                    var parts = settingValue.Split(',');
                    if (parts.Length == 2)
                    {
                        string folder = parts[0].Split(new[] { ':' }, 2)[1].Trim();
                        string plugin = parts[1].Split(new[] { ':' }, 2)[1].Trim();
                        target.PathCombo.SelectedItem = folder;
                        target.PluginCombo.SelectedItem = plugin;
                    }
                }
            }
        }

        private void SaveUploadSetting(UploadTarget target)
        {
            string folder = target.PathCombo.Text.Trim();
            string plugin = target.PluginCombo.Text.Trim();

            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(plugin))
            {
                MessageBox.Show("폴더와 플러그인을 모두 선택해야 합니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string settingValue = $"Folder : {folder}, Plugin : {plugin}";
            _settingsManager.SetValue(UploadSection, target.Key, settingValue);
            _logger.LogEvent($"[ucUploadPanel] Upload setting saved for '{target.Key}': {settingValue}");
            MessageBox.Show($"{target.Key} 설정이 저장되었습니다.", "저장 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ClearUploadSetting(UploadTarget target)
        {
            target.PathCombo.SelectedIndex = -1;
            target.PathCombo.Text = "";
            target.PluginCombo.SelectedIndex = -1;
            target.PluginCombo.Text = "";
            _settingsManager.SetValue(UploadSection, target.Key, null);
            _logger.LogEvent($"[ucUploadPanel] Upload setting cleared for '{target.Key}'.");
        }
        #endregion

        #region --- 파일 감시 이벤트 처리 ---

        private void OnWatchedFileChanged(string filePath, WatcherChangeTypes changeType)
        {
            if (changeType != WatcherChangeTypes.Created && changeType != WatcherChangeTypes.Changed) return;

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => ProcessFile(filePath)));
            }
            else
            {
                ProcessFile(filePath);
            }
        }

        private void ProcessFile(string filePath)
        {
            string fileDirectory = Path.GetDirectoryName(filePath);

            foreach (var target in _uploadTargets)
            {
                if (target.PathCombo.SelectedItem?.ToString().Equals(fileDirectory, StringComparison.OrdinalIgnoreCase) ?? false)
                {
                    string pluginName = target.PluginCombo.SelectedItem?.ToString();
                    if (!string.IsNullOrEmpty(pluginName))
                    {
                        var plugin = _pluginManager.GetPlugin(pluginName);
                        if (plugin != null)
                        {
                            _logger.LogDebug($"[ucUploadPanel]'{filePath}' matches '{target.Key}'. Triggering plugin: '{pluginName}'");
                            ThreadPool.QueueUserWorkItem(_ => plugin.Process(filePath));
                        }
                    }
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
            }
        }

        #endregion
    }
}
