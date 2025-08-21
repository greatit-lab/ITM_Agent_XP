// ITM_Agent/MainForm.Designer.cs
namespace ITM_Agent
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form 디자이너에서 생성한 코드

        private void InitializeComponent()
        {
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.파일ToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.newToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator7 = new System.Windows.Forms.ToolStripSeparator();
            this.saveAsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator8 = new System.Windows.Forms.ToolStripSeparator();
            this.quitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem8 = new System.Windows.Forms.ToolStripMenuItem();
            this.tsm_Categorize = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator9 = new System.Windows.Forms.ToolStripSeparator();
            this.tsm_Option = new System.Windows.Forms.ToolStripMenuItem();
            this.tsm_Onto = new System.Windows.Forms.ToolStripMenuItem();
            this.tsm_OverrideNames = new System.Windows.Forms.ToolStripMenuItem();
            this.tsm_ImageTrans = new System.Windows.Forms.ToolStripMenuItem();
            this.tsm_UploadData = new System.Windows.Forms.ToolStripMenuItem();
            this.tsm_Nova = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem4 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem5 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem6 = new System.Windows.Forms.ToolStripMenuItem();
            this.tsm_Plugin = new System.Windows.Forms.ToolStripMenuItem();
            this.tsm_PluginList = new System.Windows.Forms.ToolStripMenuItem();
            this.tsm_About = new System.Windows.Forms.ToolStripMenuItem();
            this.tsm_AboutInfo = new System.Windows.Forms.ToolStripMenuItem();
            this.splitContainer3 = new System.Windows.Forms.SplitContainer();
            this.lb_eqpid = new System.Windows.Forms.Label();
            this.pMain = new System.Windows.Forms.Panel();
            this.btn_Quit = new System.Windows.Forms.Button();
            this.btn_Stop = new System.Windows.Forms.Button();
            this.btn_Run = new System.Windows.Forms.Button();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.ts_Status = new System.Windows.Forms.ToolStripStatusLabel();
            this.menuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).BeginInit();
            this.splitContainer3.Panel1.SuspendLayout();
            this.splitContainer3.Panel2.SuspendLayout();
            this.splitContainer3.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.파일ToolStripMenuItem1,
            this.toolStripMenuItem8,
            this.tsm_Onto,
            this.tsm_Nova,
            this.tsm_Plugin,
            this.tsm_About});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(676, 24);
            this.menuStrip1.TabIndex = 1;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // 파일ToolStripMenuItem1
            // 
            this.파일ToolStripMenuItem1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.newToolStripMenuItem,
            this.openToolStripMenuItem,
            this.toolStripSeparator7,
            this.saveAsToolStripMenuItem,
            this.toolStripSeparator8,
            this.quitToolStripMenuItem});
            this.파일ToolStripMenuItem1.Name = "파일ToolStripMenuItem1";
            this.파일ToolStripMenuItem1.Size = new System.Drawing.Size(37, 20);
            this.파일ToolStripMenuItem1.Text = "File";
            // 
            // newToolStripMenuItem
            // 
            this.newToolStripMenuItem.Name = "newToolStripMenuItem";
            this.newToolStripMenuItem.Size = new System.Drawing.Size(114, 22);
            this.newToolStripMenuItem.Text = "New";
            this.newToolStripMenuItem.Click += new System.EventHandler(this.NewMenuItem_Click);
            // 
            // openToolStripMenuItem
            // 
            this.openToolStripMenuItem.Name = "openToolStripMenuItem";
            this.openToolStripMenuItem.Size = new System.Drawing.Size(114, 22);
            this.openToolStripMenuItem.Text = "Open";
            this.openToolStripMenuItem.Click += new System.EventHandler(this.OpenMenuItem_Click);
            // 
            // toolStripSeparator7
            // 
            this.toolStripSeparator7.Name = "toolStripSeparator7";
            this.toolStripSeparator7.Size = new System.Drawing.Size(111, 6);
            // 
            // saveAsToolStripMenuItem
            // 
            this.saveAsToolStripMenuItem.Name = "saveAsToolStripMenuItem";
            this.saveAsToolStripMenuItem.Size = new System.Drawing.Size(114, 22);
            this.saveAsToolStripMenuItem.Text = "Save as";
            this.saveAsToolStripMenuItem.Click += new System.EventHandler(this.SaveAsMenuItem_Click);
            // 
            // toolStripSeparator8
            // 
            this.toolStripSeparator8.Name = "toolStripSeparator8";
            this.toolStripSeparator8.Size = new System.Drawing.Size(111, 6);
            // 
            // quitToolStripMenuItem
            // 
            this.quitToolStripMenuItem.Name = "quitToolStripMenuItem";
            this.quitToolStripMenuItem.Size = new System.Drawing.Size(114, 22);
            this.quitToolStripMenuItem.Text = "Quit";
            this.quitToolStripMenuItem.Click += new System.EventHandler(this.QuitMenuItem_Click);
            // 
            // toolStripMenuItem8
            // 
            this.toolStripMenuItem8.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsm_Categorize,
            this.toolStripSeparator9,
            this.tsm_Option});
            this.toolStripMenuItem8.Name = "toolStripMenuItem8";
            this.toolStripMenuItem8.Size = new System.Drawing.Size(70, 20);
            this.toolStripMenuItem8.Text = "Common";
            // 
            // tsm_Categorize
            // 
            this.tsm_Categorize.Name = "tsm_Categorize";
            this.tsm_Categorize.Size = new System.Drawing.Size(131, 22);
            this.tsm_Categorize.Text = "Categorize";
            // 
            // toolStripSeparator9
            // 
            this.toolStripSeparator9.Name = "toolStripSeparator9";
            this.toolStripSeparator9.Size = new System.Drawing.Size(128, 6);
            // 
            // tsm_Option
            // 
            this.tsm_Option.Name = "tsm_Option";
            this.tsm_Option.Size = new System.Drawing.Size(131, 22);
            this.tsm_Option.Text = "Option";
            // 
            // tsm_Onto
            // 
            this.tsm_Onto.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsm_OverrideNames,
            this.tsm_ImageTrans,
            this.tsm_UploadData});
            this.tsm_Onto.Name = "tsm_Onto";
            this.tsm_Onto.Size = new System.Drawing.Size(52, 20);
            this.tsm_Onto.Text = "ONTO";
            // 
            // tsm_OverrideNames
            // 
            this.tsm_OverrideNames.Name = "tsm_OverrideNames";
            this.tsm_OverrideNames.Size = new System.Drawing.Size(160, 22);
            this.tsm_OverrideNames.Text = "Override Names";
            // 
            // tsm_ImageTrans
            // 
            this.tsm_ImageTrans.Name = "tsm_ImageTrans";
            this.tsm_ImageTrans.Size = new System.Drawing.Size(160, 22);
            this.tsm_ImageTrans.Text = "Image Trans";
            // 
            // tsm_UploadData
            // 
            this.tsm_UploadData.Name = "tsm_UploadData";
            this.tsm_UploadData.Size = new System.Drawing.Size(160, 22);
            this.tsm_UploadData.Text = "Upload Data";
            // 
            // tsm_Nova
            // 
            this.tsm_Nova.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItem4,
            this.toolStripMenuItem5,
            this.toolStripMenuItem6});
            this.tsm_Nova.Name = "tsm_Nova";
            this.tsm_Nova.Size = new System.Drawing.Size(53, 20);
            this.tsm_Nova.Text = "NOVA";
            // 
            // toolStripMenuItem4
            // 
            this.toolStripMenuItem4.Name = "toolStripMenuItem4";
            this.toolStripMenuItem4.Size = new System.Drawing.Size(160, 22);
            this.toolStripMenuItem4.Text = "Override Names";
            // 
            // toolStripMenuItem5
            // 
            this.toolStripMenuItem5.Name = "toolStripMenuItem5";
            this.toolStripMenuItem5.Size = new System.Drawing.Size(160, 22);
            this.toolStripMenuItem5.Text = "Image Trans";
            // 
            // toolStripMenuItem6
            // 
            this.toolStripMenuItem6.Name = "toolStripMenuItem6";
            this.toolStripMenuItem6.Size = new System.Drawing.Size(160, 22);
            this.toolStripMenuItem6.Text = "Upload Data";
            // 
            // tsm_Plugin
            // 
            this.tsm_Plugin.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsm_PluginList});
            this.tsm_Plugin.Name = "tsm_Plugin";
            this.tsm_Plugin.Size = new System.Drawing.Size(53, 20);
            this.tsm_Plugin.Text = "Plugin";
            // 
            // tsm_PluginList
            // 
            this.tsm_PluginList.Name = "tsm_PluginList";
            this.tsm_PluginList.Size = new System.Drawing.Size(130, 22);
            this.tsm_PluginList.Text = "Plugin List";
            // 
            // tsm_About
            // 
            this.tsm_About.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsm_AboutInfo});
            this.tsm_About.Name = "tsm_About";
            this.tsm_About.Size = new System.Drawing.Size(52, 20);
            this.tsm_About.Text = "About";
            // 
            // tsm_AboutInfo
            // 
            this.tsm_AboutInfo.Name = "tsm_AboutInfo";
            this.tsm_AboutInfo.Size = new System.Drawing.Size(180, 22);
            this.tsm_AboutInfo.Text = "Information...";
            // 
            // splitContainer3
            // 
            this.splitContainer3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer3.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitContainer3.IsSplitterFixed = true;
            this.splitContainer3.Location = new System.Drawing.Point(0, 24);
            this.splitContainer3.Name = "splitContainer3";
            this.splitContainer3.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer3.Panel1
            // 
            this.splitContainer3.Panel1.Controls.Add(this.lb_eqpid);
            this.splitContainer3.Panel1.Controls.Add(this.pMain);
            // 
            // splitContainer3.Panel2
            // 
            this.splitContainer3.Panel2.Controls.Add(this.btn_Quit);
            this.splitContainer3.Panel2.Controls.Add(this.btn_Stop);
            this.splitContainer3.Panel2.Controls.Add(this.btn_Run);
            this.splitContainer3.Size = new System.Drawing.Size(676, 385);
            this.splitContainer3.SplitterDistance = 331;
            this.splitContainer3.TabIndex = 10;
            // 
            // lb_eqpid
            // 
            this.lb_eqpid.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lb_eqpid.AutoSize = true;
            this.lb_eqpid.Location = new System.Drawing.Point(554, 7);
            this.lb_eqpid.Name = "lb_eqpid";
            this.lb_eqpid.Size = new System.Drawing.Size(37, 12);
            this.lb_eqpid.TabIndex = 17;
            this.lb_eqpid.Text = "EqpId";
            // 
            // pMain
            // 
            this.pMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pMain.Location = new System.Drawing.Point(0, 25);
            this.pMain.Name = "pMain";
            this.pMain.Size = new System.Drawing.Size(676, 303);
            this.pMain.TabIndex = 0;
            // 
            // btn_Quit
            // 
            this.btn_Quit.Location = new System.Drawing.Point(457, 6);
            this.btn_Quit.Name = "btn_Quit";
            this.btn_Quit.Size = new System.Drawing.Size(208, 39);
            this.btn_Quit.TabIndex = 11;
            this.btn_Quit.Text = "Quit";
            this.btn_Quit.UseVisualStyleBackColor = true;
            // 
            // btn_Stop
            // 
            this.btn_Stop.Location = new System.Drawing.Point(235, 6);
            this.btn_Stop.Name = "btn_Stop";
            this.btn_Stop.Size = new System.Drawing.Size(208, 39);
            this.btn_Stop.TabIndex = 10;
            this.btn_Stop.Text = "Stop";
            this.btn_Stop.UseVisualStyleBackColor = true;
            // 
            // btn_Run
            // 
            this.btn_Run.Location = new System.Drawing.Point(12, 6);
            this.btn_Run.Name = "btn_Run";
            this.btn_Run.Size = new System.Drawing.Size(208, 39);
            this.btn_Run.TabIndex = 9;
            this.btn_Run.Text = "Run";
            this.btn_Run.UseVisualStyleBackColor = true;
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ts_Status});
            this.statusStrip1.Location = new System.Drawing.Point(0, 409);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(676, 22);
            this.statusStrip1.TabIndex = 2;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // ts_Status
            // 
            this.ts_Status.Name = "ts_Status";
            this.ts_Status.Size = new System.Drawing.Size(121, 17);
            this.ts_Status.Text = "toolStripStatusLabel1";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(676, 431);
            this.Controls.Add(this.splitContainer3);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.menuStrip1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "ITM Agent";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.splitContainer3.Panel1.ResumeLayout(false);
            this.splitContainer3.Panel1.PerformLayout();
            this.splitContainer3.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).EndInit();
            this.splitContainer3.ResumeLayout(false);
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem 파일ToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem newToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator7;
        private System.Windows.Forms.ToolStripMenuItem saveAsToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator8;
        private System.Windows.Forms.ToolStripMenuItem quitToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem8;
        private System.Windows.Forms.ToolStripMenuItem tsm_Categorize;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator9;
        private System.Windows.Forms.ToolStripMenuItem tsm_Option;
        private System.Windows.Forms.ToolStripMenuItem tsm_Onto;
        private System.Windows.Forms.ToolStripMenuItem tsm_OverrideNames;
        private System.Windows.Forms.ToolStripMenuItem tsm_ImageTrans;
        private System.Windows.Forms.ToolStripMenuItem tsm_UploadData;
        private System.Windows.Forms.ToolStripMenuItem tsm_Nova;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem4;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem5;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem6;
        private System.Windows.Forms.ToolStripMenuItem tsm_Plugin;
        private System.Windows.Forms.ToolStripMenuItem tsm_PluginList;
        private System.Windows.Forms.ToolStripMenuItem tsm_About;
        private System.Windows.Forms.ToolStripMenuItem tsm_AboutInfo;
        private System.Windows.Forms.SplitContainer splitContainer3;
        private System.Windows.Forms.Button btn_Quit;
        private System.Windows.Forms.Button btn_Stop;
        private System.Windows.Forms.Button btn_Run;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel ts_Status;
        private System.Windows.Forms.Panel pMain;
        private System.Windows.Forms.Label lb_eqpid;
    }
}
