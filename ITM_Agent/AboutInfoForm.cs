// ITM_Agent/AboutInfoForm.cs
using ITM_Agent.Properties;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace ITM_Agent
{
    /// <summary>
    /// 애플리케이션의 버전 및 개발자 정보를 표시하는 폼입니다.
    /// </summary>
    public partial class AboutInfoForm : Form
    {
        public AboutInfoForm(string versionInfo)
        {
            InitializeComponent();

            // MainForm으로부터 버전 정보를 전달받아 표시
            lb_Version.Text = versionInfo;

            // 리소스 파일에서 UI 텍스트 로드
            this.label1.Text = Resources.AboutInfo_Desc1;
            this.label2.Text = Resources.AboutInfo_Desc2;
            this.label3.Text = Resources.AboutInfo_Desc3;
            this.label4.Text = Resources.AboutInfo_Desc4;

            // 아이콘 이미지 로드
            LoadIconImage();
        }

        private void LoadIconImage()
        {
            try
            {
                // 실행 파일과 동일한 경로에 있는 아이콘 파일을 사용
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Icons", "icon.png");
                if (File.Exists(iconPath))
                {
                    using (Image originalImage = Image.FromFile(iconPath))
                    {
                        // 이미지에 투명도(Opacity) 적용
                        picIcon.Image = ApplyOpacity(originalImage, 0.5f);
                    }
                }
                else
                {
                    // 파일이 없을 경우 시스템 기본 아이콘 사용
                    picIcon.Image = SystemIcons.Application.ToBitmap();
                }
            }
            catch (Exception)
            {
                // 이미지 로딩 중 오류 발생 시 기본 아이콘으로 대체
                picIcon.Image = SystemIcons.Application.ToBitmap();
            }
        }

        /// <summary>
        /// 이미지에 투명도(알파값)를 적용하여 새로운 비트맵을 생성합니다.
        /// </summary>
        private static Bitmap ApplyOpacity(Image sourceImage, float opacity)
        {
            var bmp = new Bitmap(sourceImage.Width, sourceImage.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                var matrix = new ColorMatrix { Matrix33 = opacity }; // Alpha 값 조절
                var attributes = new ImageAttributes();
                attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

                g.DrawImage(sourceImage,
                    new Rectangle(0, 0, bmp.Width, bmp.Height),
                    0, 0, sourceImage.Width, sourceImage.Height,
                    GraphicsUnit.Pixel, attributes);
            }
            return bmp;
        }
    }
}
