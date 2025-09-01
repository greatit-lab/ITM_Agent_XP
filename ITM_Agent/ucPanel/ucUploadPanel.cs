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

        // ★★★★★ FileWatcherService를 사용하지 않고 자체 Watcher 관리 ★★★★★
        private readonly Dictionary<string, FileSystemWatcher> _watchers = new Dictionary<string, FileSystemWatcher>();

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

        public ucUploadPanel(IAppServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _logger = _serviceProvider.GetService<ILogger>();
            _settingsManager = _serviceProvider.GetService<SettingsManager>();
            _pluginManager = _serviceProvider.GetService<PluginManager>();

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
        
        // ... (LoadCommonData, LoadAllUploadSettings 등 기존 코드는 변경 없음) ...
        // ... (ClearUploadSetting 등 기존 코드는 변경 없음) ...
        
        private void SaveUploadSetting(UploadTarget target)
        {
            string folder = target.PathCombo.Text.Trim();
            string pluginName = target.PluginCombo.Text.Trim();

            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(pluginName))
            {
                MessageBox.Show("폴더와 플러그인을 모두 선택해야 합니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string settingValue = $"Folder : {folder}, Plugin : {pluginName}";
            _settingsManager.SetValue(UploadSection, target.Key, settingValue);
            
            // ★★★★★ Watcher 시작/재시작 로직 추가 ★★★★★
            if(_isRunning)
            {
                StartWatcher(target.Key, folder);
            }

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
            
            // ★★★★★ Watcher 중지 로직 추가 ★★★★★
            StopWatcher(target.Key);
            
            _logger.LogEvent($"[ucUploadPanel] Upload setting cleared for '{target.Key}'.");
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

        #endregion

        #region --- 파일 감시 로직 (기능 복원) ---
        
        private void StartWatcher(string key, string path)
        {
            StopWatcher(key); // 기존 감시자 정리

            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                _logger.LogError($"[ucUploadPanel] Cannot watch invalid path for '{key}': {path}");
                return;
            }

            var watcher = new FileSystemWatcher(path)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
            };
            
            // 이벤트 핸들러에 키와 플러그인 정보를 함께 넘겨주기 위해 람다식 사용
            watcher.Created += (sender, e) => OnFileCreated(e, key);
            watcher.EnableRaisingEvents = true;

            _watchers[key] = watcher;
            _logger.LogEvent($"[ucUploadPanel] Started watching for '{key}' at '{path}'");
        }

        private void StopWatcher(string key)
        {
            if (_watchers.TryGetValue(key, out FileSystemWatcher watcher))
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                _watchers.Remove(key);
                _logger.LogEvent($"[ucUploadPanel] Stopped watching for '{key}'");
            }
        }

        private void OnFileCreated(FileSystemEventArgs e, string key)
        {
            // 해당 키에 맞는 플러그인 찾기
            var target = _uploadTargets.FirstOrDefault(t => t.Key == key);
            if (target.Equals(default(UploadTarget))) return;

            string pluginName = null;
            // UI 컨트롤 접근은 Invoke 필요
            this.Invoke((MethodInvoker)delegate
            {
                pluginName = target.PluginCombo.SelectedItem?.ToString();
            });

            if (string.IsNullOrEmpty(pluginName))
            {
                _logger.LogError($"[ucUploadPanel] No plugin selected for '{key}' to process file: {e.Name}");
                return;
            }

            var plugin = _pluginManager.GetPlugin(pluginName);
            if (plugin == null)
            {
                _logger.LogError($"[ucUploadPanel] Plugin '{pluginName}' not found for key '{key}'");
                return;
            }

            // 플러그인 처리는 백그라운드 스레드에서
            ThreadPool.QueueUserWorkItem(_ =>
            {
                // 원본 코드의 OverrideNamesPanel 연동 로직은 이제 각 플러그인 내부나
                // 별도 서비스에서 처리해야 하므로 여기서는 직접 호출하지 않음
                // 필요 시 IAppServiceProvider를 통해 OverrideNamesPanel의 public 메서드에 접근 가능
                plugin.Process(e.FullPath);
            });
        }
        
        #endregion

        #region --- IPanelState 구현 ---

        private bool _isRunning = false;
        public void UpdateState(bool isRunning)
        {
            _isRunning = isRunning;
            this.Enabled = !isRunning;

            if (isRunning)
            {
                // 저장된 모든 설정에 대해 감시 시작
                foreach(var target in _uploadTargets)
                {
                    string folder = target.PathCombo.SelectedItem?.ToString();
                    if(!string.IsNullOrEmpty(folder))
                    {
                        StartWatcher(target.Key, folder);
                    }
                }
            }
            else
            {
                // 모든 감시 중지
                var keys = _watchers.Keys.ToList();
                foreach(var key in keys)
                {
                    StopWatcher(key);
                }
            }
        }

        #endregion
    }
}
