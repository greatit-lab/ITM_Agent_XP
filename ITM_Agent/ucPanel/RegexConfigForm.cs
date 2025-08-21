// ITM_Agent/ucPanel/RegexConfigForm.cs
using ITM_Agent.Properties; // 리소스 파일 접근을 위함
using System;
using System.IO;
using System.Windows.Forms;

namespace ITM_Agent.ucPanel
{
    /// <summary>
    /// 정규식 패턴과 대상 폴더를 설정하기 위한 UI 폼입니다.
    /// </summary>
    public partial class RegexConfigForm : Form
    {
        private readonly string _initialFolderPath;

        /// <summary>
        /// 사용자가 입력한 정규식 패턴입니다.
        /// </summary>
        public string RegexPattern
        {
            get => tb_RegInput.Text;
            set => tb_RegInput.Text = value;
        }

        /// <summary>
        /// 사용자가 선택한 대상 폴더 경로입니다.
        /// </summary>
        public string TargetFolder
        {
            get => tb_RegFolder.Text;
            set => tb_RegFolder.Text = value;
        }

        public RegexConfigForm(string baseFolderPath)
        {
            InitializeComponent();
            _initialFolderPath = Directory.Exists(baseFolderPath) ? baseFolderPath : AppDomain.CurrentDomain.BaseDirectory;

            // 폼 기본 설정
            this.Text = "Regex Configuration";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // 이벤트 핸들러 연결
            btn_RegSelectFolder.Click += Btn_RegSelectFolder_Click;
            btn_RegApply.Click += Btn_RegApply_Click;
            btn_RegCancel.Click += (sender, e) => this.DialogResult = DialogResult.Cancel;
        }

        private void Btn_RegSelectFolder_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.SelectedPath = _initialFolderPath;
                dialog.Description = "정규식과 일치하는 파일을 복사할 대상 폴더를 선택하세요.";

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    TargetFolder = dialog.SelectedPath;
                }
            }
        }

        private void Btn_RegApply_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(RegexPattern))
            {
                // 리소스 파일에 정의된 메시지를 사용
                MessageBox.Show(Resources.MSG_REGEX_REQUIRED, Resources.CAPTION_WARNING, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                tb_RegInput.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(TargetFolder))
            {
                MessageBox.Show(Resources.MSG_FOLDER_REQUIRED, Resources.CAPTION_WARNING, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                tb_RegFolder.Focus();
                return;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
