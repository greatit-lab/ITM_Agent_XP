// ITM_Agent/EqpidInputForm.cs
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace ITM_Agent
{
    /// <summary>
    /// 프로그램 최초 실행 시 EQPID와 장비 타입을 입력받기 위한 폼입니다.
    /// </summary>
    public class EqpidInputForm : Form
    {
        /// <summary>
        /// 사용자가 입력한 EQPID 값입니다.
        /// </summary>
        public string Eqpid { get; private set; }

        /// <summary>
        /// 사용자가 선택한 장비 타입("ONTO" 또는 "NOVA")입니다.
        /// </summary>
        public string Type { get; private set; }

        // UI 컨트롤 선언
        private TextBox _eqpidTextBox;
        private Button _submitButton;
        private Button _cancelButton;
        private Label _warningLabel;
        private RadioButton _rdoOnto;
        private RadioButton _rdoNova;

        public EqpidInputForm()
        {
            InitializeForm();
            InitializeControls();
            RegisterEventHandlers();
        }

        private void InitializeForm()
        {
            this.Text = "New EQPID Registration";
            this.Size = new Size(320, 220);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.ControlBox = false; // 닫기 버튼 비활성화
        }

        private void InitializeControls()
        {
            // PictureBox for background icon
            var pictureBox = new PictureBox
            {
                Image = CreateTransparentImage("Resources\\Icons\\icon.png", 0.5f),
                Location = new Point(22, 40),
                Size = new Size(75, 75),
                SizeMode = PictureBoxSizeMode.StretchImage
            };

            var instructionLabel = new Label
            {
                Text = "신규 장비명과 타입을 입력하세요.",
                Top = 20,
                Left = 25,
                Width = 260
            };

            _rdoOnto = new RadioButton
            {
                Text = "ONTO",
                Top = 55,
                Left = 120,
                AutoSize = true,
                Checked = true // 기본 선택
            };

            _rdoNova = new RadioButton
            {
                Text = "NOVA",
                Top = 55,
                Left = 200,
                AutoSize = true
            };

            _eqpidTextBox = new TextBox
            {
                Top = 90,
                Left = 120,
                Width = 150
            };

            _warningLabel = new Label
            {
                Text = "장비명을 입력해주세요.",
                Top = 115,
                Left = 120,
                ForeColor = Color.Red,
                AutoSize = true,
                Visible = false
            };

            _submitButton = new Button
            {
                Text = "Submit",
                Top = 140,
                Left = 60,
                Width = 90
            };

            _cancelButton = new Button
            {
                Text = "Cancel",
                Top = 140,
                Left = 160,
                Width = 90
            };

            // 컨트롤들을 폼에 추가
            this.Controls.AddRange(new Control[]
            {
                pictureBox, instructionLabel, _rdoOnto, _rdoNova,
                _eqpidTextBox, _warningLabel, _submitButton, _cancelButton
            });

            pictureBox.SendToBack(); // 이미지를 맨 뒤로 보냄
        }

        private void RegisterEventHandlers()
        {
            _submitButton.Click += SubmitButton_Click;
            _cancelButton.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; };
            _eqpidTextBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true; // '띵' 소리 제거
                    _submitButton.PerformClick();
                }
            };
        }

        private void SubmitButton_Click(object sender, EventArgs e)
        {
            string eqpidInput = _eqpidTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(eqpidInput))
            {
                _warningLabel.Visible = true;
                return;
            }

            this.Eqpid = eqpidInput;
            this.Type = _rdoOnto.Checked ? "ONTO" : "NOVA";
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private Image CreateTransparentImage(string filePath, float opacity)
        {
            try
            {
                string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filePath);
                if (!File.Exists(fullPath)) return null;

                using (var original = new Bitmap(fullPath))
                {
                    var transparentImage = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(transparentImage))
                    {
                        var matrix = new ColorMatrix { Matrix33 = opacity };
                        var attributes = new ImageAttributes();
                        attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                        g.DrawImage(original, new Rectangle(0, 0, original.Width, original.Height),
                                    0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attributes);
                    }
                    return transparentImage;
                }
            }
            catch
            {
                return null; // 이미지 로딩 실패 시
            }
        }
    }
}
