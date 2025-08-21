// ITM_Agent/ucPanel/ucPluginPanel.cs
using ITM_Agent.Core;
using ITM_Agent.Services;
using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ITM_Agent.ucPanel
{
    public partial class ucPluginPanel : UserControl, IPanelState
    {
        private readonly IAppServiceProvider _serviceProvider;
        private readonly ILogger _logger;
        private readonly PluginManager _pluginManager;
        private readonly string _pluginsPath;

        // 생성자 파라미터를 IAppServiceProvider로 수정
        public ucPluginPanel(IAppServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = _serviceProvider.GetService<ILogger>();
            _pluginManager = _serviceProvider.GetService<PluginManager>();

            if (_logger == null || _pluginManager == null)
                throw new InvalidOperationException("Required services could not be resolved.");

            _pluginsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Library");

            InitializeComponent();
            this.Load += OnPanelLoad;
        }

        private void OnPanelLoad(object sender, EventArgs e)
        {
            // 디자이너 파일에서 이미 이벤트가 연결되어 있으므로, 여기서는 Refresh만 호출
            RefreshPluginList();
        }

        private void RefreshPluginList()
        {
            lb_PluginList.Items.Clear();
            var plugins = _pluginManager.GetLoadedPlugins();

            if (plugins.Any())
            {
                int index = 1;
                foreach (var plugin in plugins)
                {
                    lb_PluginList.Items.Add($"{index++}. {plugin.Name} (v{plugin.Version})");
                }
            }
            else
            {
                lb_PluginList.Items.Add("No plugins loaded.");
            }
        }

        // 메서드 이름을 디자이너 파일과 일치시킴 (Btn_ -> btn_)
        private void btn_PlugAdd_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog
            {
                Filter = "DLL Files (*.dll)|*.dll|All Files (*.*)|*.*",
                InitialDirectory = AppDomain.CurrentDomain.BaseDirectory,
                Title = "Add a new plugin"
            })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;

                try
                {
                    string sourceDllPath = dialog.FileName;
                    string destDllPath = Path.Combine(_pluginsPath, Path.GetFileName(sourceDllPath));

                    if (File.Exists(destDllPath))
                    {
                        MessageBox.Show("동일한 이름의 플러그인 DLL이 이미 존재합니다.", "중복 파일", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    File.Copy(sourceDllPath, destDllPath);
                    _logger.LogEvent($"[ucPluginPanel] Plugin file copied: {Path.GetFileName(destDllPath)}");

                    _pluginManager.LoadPlugins();
                    RefreshPluginList();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[ucPluginPanel] Failed to add plugin. Error: {ex.Message}");
                    MessageBox.Show($"플러그인 추가에 실패했습니다: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // 메서드 이름을 디자이너 파일과 일치시킴 (Btn_ -> btn_)
        private void btn_PlugRemove_Click(object sender, EventArgs e)
        {
            if (lb_PluginList.SelectedItem == null)
            {
                MessageBox.Show("삭제할 플러그인을 선택하세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string selectedText = lb_PluginList.SelectedItem.ToString();
            string pluginName = selectedText.Split(new[] { ' ' }, 2)[1].Split('(')[0].Trim();

            var result = MessageBox.Show($"'{pluginName}' 플러그인을 삭제하시겠습니까?", "삭제 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result != DialogResult.Yes) return;

            try
            {
                IPlugin pluginToRemove = _pluginManager.GetPlugin(pluginName);
                if (pluginToRemove == null)
                {
                    throw new InvalidOperationException("Could not find the selected plugin instance.");
                }

                // AssemblyPath 속성이 리팩토링된 IPlugin/PluginBase에 없으므로, 파일 이름을 추정
                string dllFileName = $"{pluginToRemove.GetType().Assembly.GetName().Name}.dll";
                string dllPath = Path.Combine(_pluginsPath, dllFileName);
                
                if (File.Exists(dllPath))
                {
                    File.Delete(dllPath);
                    _logger.LogEvent($"[ucPluginPanel] Plugin file deleted: {Path.GetFileName(dllPath)}");
                }
                else
                {
                     _logger.LogError($"[ucPluginPanel] Could not find plugin file to delete: {dllPath}");
                }

                _pluginManager.LoadPlugins();
                RefreshPluginList();
            }
            catch (Exception ex)
            {
                _logger.LogError($"[ucPluginPanel] Failed to remove plugin. Error: {ex.Message}");
                MessageBox.Show($"플러그인 삭제에 실패했습니다: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void UpdateState(bool isRunning)
        {
            this.Enabled = !isRunning;
        }
    }
}
