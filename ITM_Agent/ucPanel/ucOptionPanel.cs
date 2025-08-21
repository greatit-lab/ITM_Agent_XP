// ITM_Agent/ucPanel/ucOptionPanel.cs
using ITM_Agent.Core;
using ITM_Agent.Services;
using System;
using System.Windows.Forms;

namespace ITM_Agent.ucPanel
{
    public partial class ucOptionPanel : UserControl, IPanelState
    {
        private readonly IAppServiceProvider _serviceProvider;
        private readonly SettingsManager _settingsManager;
        private readonly ILogger _logger;

        private const string OptionSection = "Option";
        private const string KeyPerfLog = "EnablePerfoLog";
        private const string KeyInfoAutoDel = "EnableInfoAutoDel";
        private const string KeyInfoRetention = "InfoRetentionDays";
        private const string KeyDebugMode = "DebugMode";

        public ucOptionPanel(IAppServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _settingsManager = serviceProvider.GetService<SettingsManager>();
            _logger = serviceProvider.GetService<ILogger>();

            InitializeComponent();
            InitializeControls();
            LoadSettings();
        }

        private void InitializeControls()
        {
            cb_info_Retention.Items.AddRange(new object[] { "1", "3", "5", "7", "14", "30" });
            cb_info_Retention.DropDownStyle = ComboBoxStyle.DropDownList;
        }

        private void LoadSettings()
        {
            // 이벤트 핸들러를 잠시 분리하여 Load 중 불필요한 이벤트 발생 방지
            chk_infoDel.CheckedChanged -= chk_infoDel_CheckedChanged;
            cb_info_Retention.SelectedIndexChanged -= cb_info_Retention_SelectedIndexChanged;

            chk_DebugMode.Checked = _settingsManager.GetValue(OptionSection, KeyDebugMode) == "1";
            chk_PerfoMode.Checked = _settingsManager.GetValue(OptionSection, KeyPerfLog) == "1";
            chk_infoDel.Checked = _settingsManager.GetValue(OptionSection, KeyInfoAutoDel) == "1";
            cb_info_Retention.SelectedItem = _settingsManager.GetValue(OptionSection, KeyInfoRetention);

            UpdateRetentionControlsState();

            // 이벤트 핸들러 다시 연결
            chk_infoDel.CheckedChanged += chk_infoDel_CheckedChanged;
            cb_info_Retention.SelectedIndexChanged += cb_info_Retention_SelectedIndexChanged;
        }

        #region --- 이벤트 핸들러 ---

        private void chk_DebugMode_CheckedChanged(object sender, EventArgs e)
        {
            bool isEnabled = chk_DebugMode.Checked;
            _settingsManager.SetValue(OptionSection, KeyDebugMode, isEnabled ? "1" : "0");

            if (_logger is LogManager logManager)
            {
                logManager.SetDebugMode(isEnabled);
            }
            _logger.LogEvent($"Debug mode {(isEnabled ? "enabled" : "disabled")}.");
        }

        private void chk_PerfoMode_CheckedChanged(object sender, EventArgs e)
        {
            bool isEnabled = chk_PerfoMode.Checked;
            _settingsManager.SetValue(OptionSection, KeyPerfLog, isEnabled ? "1" : "0");
            _logger.LogEvent($"Performance logging {(isEnabled ? "enabled" : "disabled")}.");
        }

        private void chk_infoDel_CheckedChanged(object sender, EventArgs e)
        {
            bool isEnabled = chk_infoDel.Checked;
            // 1. 체크박스 상태를 .ini 파일에 즉시 기록
            _settingsManager.SetValue(OptionSection, KeyInfoAutoDel, isEnabled ? "1" : "0");

            if (isEnabled)
            {
                // 2. 체크 시: 콤보박스에 선택된 값이 없으면 기본값("7")을 설정
                if (cb_info_Retention.SelectedItem == null)
                {
                    cb_info_Retention.SelectedItem = "7";
                }
                else
                {
                    // 이미 값이 있으면 그 값을 .ini에 다시 기록
                    _settingsManager.SetValue(OptionSection, KeyInfoRetention, cb_info_Retention.SelectedItem.ToString());
                }
            }
            else
            {
                // 3. 체크 해제 시: 콤보박스 선택을 초기화하고, .ini 파일의 설정값도 삭제
                cb_info_Retention.SelectedIndex = -1;
                _settingsManager.SetValue(OptionSection, KeyInfoRetention, null); // null을 전달하여 키-값 쌍을 제거
            }
            
            // 4. UI 상태 업데이트
            UpdateRetentionControlsState();
        }

        private void cb_info_Retention_SelectedIndexChanged(object sender, EventArgs e)
        {
            // 콤보박스 선택이 변경될 때만 .ini 파일에 값을 기록
            if (cb_info_Retention.SelectedItem != null)
            {
                _settingsManager.SetValue(OptionSection, KeyInfoRetention, cb_info_Retention.SelectedItem.ToString());
            }
        }

        #endregion

        private void UpdateRetentionControlsState()
        {
            // chk_infoDel 체크 여부에 따라 관련 컨트롤들의 활성화 상태를 제어
            bool isEnabled = chk_infoDel.Checked;
            label3.Enabled = isEnabled;
            label4.Enabled = isEnabled;
            cb_info_Retention.Enabled = isEnabled;
        }

        public void UpdateState(bool isRunning)
        {
            this.Enabled = !isRunning;
        }
    }
}
