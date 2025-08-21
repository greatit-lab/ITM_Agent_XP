// ITM_Agent/MainForm.cs
using ITM_Agent.Core;
using ITM_Agent.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading; // ThreadPool 사용을 위해 추가
using System.Windows.Forms;

namespace ITM_Agent
{
    public partial class MainForm : Form
    {
        private const string AppVersion = "v2.0.0";

        //--- DI Services ---
        private readonly IAppServiceProvider _serviceProvider;
        private readonly ILogger _logger;
        private readonly SettingsManager _settingsManager;
        private readonly PluginManager _pluginManager;
        private readonly FileWatcherService _fileWatcher;
        private readonly PerformanceMonitor _performanceMonitor;
        private readonly EqpidManager _eqpidManager;
        private readonly InfoRetentionCleaner _infoCleaner;
        private readonly PerformanceDbWriter _performanceDbWriter;

        //--- UI Panels ---
        private readonly Dictionary<string, UserControl> _panels = new Dictionary<string, UserControl>();
        private bool _isRunning = false;

        //--- Tray Icon Components ---
        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;
        private ToolStripMenuItem _runMenuItem;
        private ToolStripMenuItem _stopMenuItem;
        private ToolStripMenuItem _quitMenuItem;

        public MainForm(IAppServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            _logger = _serviceProvider.GetService<ILogger>();
            _settingsManager = _serviceProvider.GetService<SettingsManager>();
            _pluginManager = _serviceProvider.GetService<PluginManager>();
            _fileWatcher = _serviceProvider.GetService<FileWatcherService>();
            _performanceMonitor = _serviceProvider.GetService<PerformanceMonitor>();
            _eqpidManager = new EqpidManager(_serviceProvider, AppVersion);
            _infoCleaner = new InfoRetentionCleaner(_serviceProvider);
            _performanceDbWriter = new PerformanceDbWriter(_serviceProvider);

            InitializeComponent();
            InitializeCustomComponents();
            
            // 폼이 표시된 직후에 비동기 초기화를 시작하도록 Shown 이벤트 핸들러를 연결합니다.
            this.Shown += MainForm_Shown;
        }
        
        // ... (InitializeCustomComponents 메서드는 이전과 동일) ...

        private void MainForm_Load(object sender, EventArgs e)
        {
            // Load 이벤트에서는 UI를 '초기화 중' 상태로만 설정하여 화면을 빠르게 표시합니다.
            ts_Status.Text = "Initializing...";
            ts_Status.ForeColor = Color.Orange;
            btn_Run.Enabled = false;
            btn_Stop.Enabled = false;
            btn_Quit.Enabled = false;
        }

        /// <summary>
        /// 폼이 사용자에게 처음으로 표시된 직후에 호출됩니다.
        /// 무거운 초기화 작업을 백그라운드 스레드에서 수행합니다.
        /// </summary>
        private void MainForm_Shown(object sender, EventArgs e)
        {
            // ThreadPool을 사용하여 백그라운드 스레드에서 초기화 작업을 실행합니다.
            ThreadPool.QueueUserWorkItem(_ =>
            {
                // 1. 시간이 오래 걸리는 작업 (DB 연결, 플러그인 로딩)
                TimeSyncProvider.Instance.Initialize(_serviceProvider);
                _pluginManager.LoadPlugins();

                // 2. 백그라운드 작업이 끝난 후, UI 스레드에서 나머지 작업을 실행하도록 요청합니다.
                //    BeginInvoke는 UI 스레드에서 코드를 안전하게 실행하도록 보장합니다.
                this.BeginInvoke(new Action(() =>
                {
                    // EQPID 확인 (사용자 입력이 필요할 수 있으므로 UI 스레드에서 처리)
                    InitializeEqpid();
                    if (this.IsDisposed) return; // 사용자가 EQPID 입력을 취소하면 폼이 닫히므로 중단

                    // 나머지 UI 초기화
                    UpdateMenusBasedOnType();
                    ShowPanel("Categorize");
                    UpdateUIState(false); // 최종 UI 상태 업데이트
                }));
            });
        }
        
        // ... (이하 나머지 모든 코드는 이전 답변과 동일합니다) ...

        #region --- 기존 코드 (변경 없음) ---
        private void InitializeCustomComponents()
        {
            this.Text = $"ITM Agent - {AppVersion}";
            this.Icon = new Icon(@"Resources\Icons\icon.ico");

            _panels["Categorize"] = new ucConfigurationPanel(_serviceProvider);
            _panels["OverrideNames"] = new ucOverrideNamesPanel(_serviceProvider);
            _panels["ImageTrans"] = new ucImageTransPanel(_serviceProvider);
            _panels["UploadData"] = new ucUploadPanel(_serviceProvider);
            _panels["PluginList"] = new ucPluginPanel(_serviceProvider);
            _panels["Option"] = new ucOptionPanel(_serviceProvider);

            RegisterMenuEvents();
            btn_Run.Click += Btn_Run_Click;
            btn_Stop.Click += Btn_Stop_Click;
            btn_Quit.Click += Btn_Quit_Click;
            InitializeTrayIcon();
            this.FormClosing += MainForm_FormClosing;
        }

        private void InitializeEqpid()
        {
            if (!_eqpidManager.CheckEqpidExists())
            {
                using (var form = new EqpidInputForm())
                {
                    if (form.ShowDialog() == DialogResult.OK)
                    {
                        _eqpidManager.RegisterNewEqpid(form.Eqpid, form.Type);
                    }
                    else
                    {
                        ShutdownApplication();
                        return;
                    }
                }
            }
            lb_eqpid.Text = $"Eqpid: {_settingsManager.GetValue("Eqpid", "Eqpid")}";
        }

        private void UpdateMenusBasedOnType()
        {
            string type = _settingsManager.GetValue("Eqpid", "Type");
            if ("ONTO".Equals(type, StringComparison.OrdinalIgnoreCase))
            {
                tsm_Onto.Visible = true;
                tsm_Nova.Visible = false;
            }
            else if ("NOVA".Equals(type, StringComparison.OrdinalIgnoreCase))
            {
                tsm_Onto.Visible = false;
                tsm_Nova.Visible = true;
            }
            else
            {
                tsm_Onto.Visible = false;
                tsm_Nova.Visible = false;
            }
        }
        
        private void NewMenuItem_Click(object sender, EventArgs e) { /* New 로직 구현 */ }
        private void OpenMenuItem_Click(object sender, EventArgs e) { /* Open 로직 구현 */ }
        private void SaveAsMenuItem_Click(object sender, EventArgs e) { /* Save As 로직 구현 */ }
        private void QuitMenuItem_Click(object sender, EventArgs e) => Btn_Quit_Click(sender, e);
        
        private void Btn_Run_Click(object sender, EventArgs e)
        {
            _logger.LogEvent("Run button clicked.");
            var targetFolders = _settingsManager.GetSectionEntries("TargetFolders");
            if (!targetFolders.Any())
            {
                MessageBox.Show("감시할 대상 폴더가 설정되지 않았습니다.", "설정 필요", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _fileWatcher.Start(targetFolders);
            _performanceMonitor.Start();
            _performanceDbWriter.Start();
            _infoCleaner.Start();
            _pluginManager.GetLoadedPlugins().ToList().ForEach(p => p.Start());
            UpdateUIState(true);
        }

        private void Btn_Stop_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("모든 작업을 중지하시겠습니까?", "작업 중지 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            _logger.LogEvent("Stop button clicked.");
            _fileWatcher.Stop();
            _performanceMonitor.Stop();
            _performanceDbWriter.Stop();
            _infoCleaner.Stop();
            _pluginManager.GetLoadedPlugins().ToList().ForEach(p => p.Stop());
            UpdateUIState(false);
        }

        private void InitializeTrayIcon()
        {
            _trayMenu = new ContextMenuStrip();
            _runMenuItem = new ToolStripMenuItem("Run", null, (s, e) => { if (btn_Run.Enabled) btn_Run.PerformClick(); });
            _stopMenuItem = new ToolStripMenuItem("Stop", null, (s, e) => { if (btn_Stop.Enabled) btn_Stop.PerformClick(); });
            _quitMenuItem = new ToolStripMenuItem("Quit", null, (s, e) => Btn_Quit_Click(s, e));
            _trayMenu.Items.AddRange(new ToolStripItem[] {
                new ToolStripMenuItem(this.Text) { Enabled = false }, new ToolStripSeparator(),
                _runMenuItem, _stopMenuItem, _quitMenuItem
            });
            _trayIcon = new NotifyIcon
            {
                Icon = this.Icon, ContextMenuStrip = _trayMenu,
                Visible = true, Text = this.Text
            };
            _trayIcon.DoubleClick += (sender, e) => RestoreMainForm();
        }

        private void RestoreMainForm()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.Activate();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                _trayIcon.BalloonTipTitle = "ITM Agent";
                _trayIcon.BalloonTipText = "ITM Agent가 백그라운드에서 실행 중입니다.";
                _trayIcon.ShowBalloonTip(3000);
            }
        }

        private void Btn_Quit_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("프로그램을 완전히 종료하시겠습니까?", "종료 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                ShutdownApplication();
            }
        }

        private void ShutdownApplication()
        {
            _isRunning = false;
            if (_trayIcon != null) { _trayIcon.Visible = false; _trayIcon.Dispose(); }
            _infoCleaner.Dispose();
            _fileWatcher.Dispose();
            _performanceMonitor.Dispose();
            _performanceDbWriter.Dispose();
            _pluginManager?.GetLoadedPlugins().ToList().ForEach(p => p.Dispose());
            Application.Exit();
        }

        private bool IsReadyToRun()
        {
            bool hasTargetFolders = _settingsManager.GetSectionEntries("TargetFolders").Any();
            bool hasBaseFolder = _settingsManager.GetSectionEntries("BaseFolder").Any();
            bool hasRegex = _settingsManager.GetSectionEntries("Regex").Any();
            return hasTargetFolders && hasBaseFolder && hasRegex;
        }

        private void UpdateUIState(bool isRunning)
        {
            _isRunning = isRunning;
            btn_Run.Enabled = !isRunning && IsReadyToRun();
            btn_Stop.Enabled = isRunning;
            btn_Quit.Enabled = !isRunning;
            if (isRunning)
            {
                ts_Status.Text = "Running...";
                ts_Status.ForeColor = Color.Green;
            }
            else
            {
                ts_Status.Text = IsReadyToRun() ? "Ready to Run" : "Stopped";
                ts_Status.ForeColor = IsReadyToRun() ? Color.Blue : Color.Red;
            }
            newToolStripMenuItem.Enabled = !isRunning;
            openToolStripMenuItem.Enabled = !isRunning;
            saveAsToolStripMenuItem.Enabled = !isRunning;
            if (_trayMenu != null)
            {
                _runMenuItem.Enabled = btn_Run.Enabled;
                _stopMenuItem.Enabled = btn_Stop.Enabled;
                _quitMenuItem.Enabled = btn_Quit.Enabled;
            }
            foreach (var panel in _panels.Values)
            {
                if (panel is IPanelState updatablePanel)
                {
                    updatablePanel.UpdateState(isRunning);
                }
            }
        }

        private void RegisterMenuEvents()
        {
            tsm_Categorize.Click += (s, e) => ShowPanel("Categorize");
            tsm_OverrideNames.Click += (s, e) => ShowPanel("OverrideNames");
            tsm_ImageTrans.Click += (s, e) => ShowPanel("ImageTrans");
            tsm_UploadData.Click += (s, e) => ShowPanel("UploadData");
            tsm_PluginList.Click += (s, e) => ShowPanel("PluginList");
            tsm_Option.Click += (s, e) => ShowPanel("Option");
            tsm_AboutInfo.Click += (s, e) => new AboutInfoForm(AppVersion).ShowDialog();
        }

        private void ShowPanel(string name)
        {
            if (_panels.TryGetValue(name, out UserControl panel))
            {
                pMain.Controls.Clear();
                panel.Dock = DockStyle.Fill;
                pMain.Controls.Add(panel);
            }
        }
        #endregion
    }
}
