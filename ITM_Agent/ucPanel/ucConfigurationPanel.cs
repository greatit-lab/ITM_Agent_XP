// ITM_Agent/ucPanel/ucConfigurationPanel.cs
using ITM_Agent.Core;
using ITM_Agent.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace ITM_Agent.ucPanel
{
    public partial class ucConfigurationPanel : UserControl, IPanelState
    {
        private readonly SettingsManager _settingsManager;

        private const string TargetFoldersSection = "TargetFolders";
        private const string ExcludeFoldersSection = "ExcludeFolders";
        private const string BaseFolderSection = "BaseFolder";
        private const string RegexSection = "Regex";

        public ucConfigurationPanel(IAppServiceProvider serviceProvider)
        {
            _settingsManager = serviceProvider.GetService<SettingsManager>() ?? throw new ArgumentNullException(nameof(SettingsManager));
            InitializeComponent();
            RegisterEventHandlers();
            LoadAllSettings();
        }

        private void RegisterEventHandlers()
        {
            btn_TargetFolder.Click += (s, e) => AddFolder(TargetFoldersSection, lb_TargetList);
            btn_TargetRemove.Click += (s, e) => RemoveSelectedItems(TargetFoldersSection, lb_TargetList);
            btn_ExcludeFolder.Click += (s, e) => AddFolder(ExcludeFoldersSection, lb_ExcludeList);
            btn_ExcludeRemove.Click += (s, e) => RemoveSelectedItems(ExcludeFoldersSection, lb_ExcludeList);
            btn_BaseFolder.Click += SelectBaseFolder_Click;
            btn_RegAdd.Click += AddRegex_Click;
            btn_RegEdit.Click += EditRegex_Click;
            btn_RegRemove.Click += RemoveRegex_Click;
        }

        #region --- 데이터 로드 및 UI 갱신 ---
        private void LoadAllSettings()
        {
            LoadFolderList(TargetFoldersSection, lb_TargetList);
            LoadFolderList(ExcludeFoldersSection, lb_ExcludeList);
            LoadBaseFolder();
            LoadRegexList();
        }

        private void LoadFolderList(string section, ListBox listBox)
        {
            listBox.Items.Clear();
            var folders = _settingsManager.GetSectionEntries(section);
            listBox.Items.AddRange(folders.ToArray());
        }

        private void LoadBaseFolder()
        {
            string baseFolder = _settingsManager.GetSectionEntries(BaseFolderSection).FirstOrDefault();
            lb_BaseFolder.Text = string.IsNullOrEmpty(baseFolder) ? "(Not Set)" : baseFolder;
        }

        private void LoadRegexList()
        {
            lb_RegexList.Items.Clear();
            var regexEntries = _settingsManager.GetSectionEntries(RegexSection);
            lb_RegexList.Items.AddRange(regexEntries.ToArray());
        }
        #endregion

        #region --- 이벤트 핸들러 구현 ---
        private void AddFolder(string section, ListBox listBox)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    var currentFolders = listBox.Items.Cast<string>().ToList();
                    if (currentFolders.Contains(dialog.SelectedPath, StringComparer.OrdinalIgnoreCase))
                    {
                        MessageBox.Show("이미 추가된 폴더입니다.", "중복", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    currentFolders.Add(dialog.SelectedPath);
                    _settingsManager.SetSectionEntries(section, currentFolders);
                    LoadFolderList(section, listBox);
                }
            }
        }

        private void RemoveSelectedItems(string section, ListBox listBox)
        {
            if (listBox.SelectedItems.Count == 0) return;
            var currentItems = listBox.Items.Cast<string>().ToList();
            foreach (string selected in listBox.SelectedItems.Cast<string>().ToList())
            {
                currentItems.Remove(selected);
            }
            _settingsManager.SetSectionEntries(section, currentItems);
            LoadFolderList(section, listBox);
        }

        private void SelectBaseFolder_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    _settingsManager.SetSectionEntries(BaseFolderSection, new[] { dialog.SelectedPath });
                    LoadBaseFolder();
                }
            }
        }

        private void AddRegex_Click(object sender, EventArgs e)
        {
            using (var form = new RegexConfigForm(lb_BaseFolder.Text))
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    var currentRegex = _settingsManager.GetSectionEntries(RegexSection);
                    string newEntry = $"{form.RegexPattern} -> {form.TargetFolder}";
                    currentRegex.Add(newEntry);
                    _settingsManager.SetSectionEntries(RegexSection, currentRegex);
                    LoadRegexList();
                }
            }
        }

        private void EditRegex_Click(object sender, EventArgs e)
        {
            if (lb_RegexList.SelectedItem == null) return;
            string selectedEntry = lb_RegexList.SelectedItem.ToString();
            var parts = selectedEntry.Split(new[] { "->" }, StringSplitOptions.None);
            
            using (var form = new RegexConfigForm(lb_BaseFolder.Text))
            {
                form.RegexPattern = parts[0].Trim();
                form.TargetFolder = parts.Length > 1 ? parts[1].Trim() : "";

                if (form.ShowDialog() == DialogResult.OK)
                {
                    var currentRegex = _settingsManager.GetSectionEntries(RegexSection);
                    int index = currentRegex.IndexOf(selectedEntry);
                    if (index != -1)
                    {
                        currentRegex[index] = $"{form.RegexPattern} -> {form.TargetFolder}";
                        _settingsManager.SetSectionEntries(RegexSection, currentRegex);
                        LoadRegexList();
                    }
                }
            }
        }

        private void RemoveRegex_Click(object sender, EventArgs e)
        {
            RemoveSelectedItems(RegexSection, lb_RegexList);
        }
        #endregion

        #region --- IPanelState 구현 ---
        // UpdateState 메서드 수정
        public void UpdateState(bool isRunning)
        {
            // UserControl 전체를 비활성화하는 대신, 내부의 컨테이너(GroupBox)들만 비활성화합니다.
            // 이렇게 하면 TabControl 자체는 활성화 상태를 유지하여 탭 전환이 가능합니다.
            bool controlsEnabled = !isRunning;
            groupBox1.Enabled = controlsEnabled;
            groupBox2.Enabled = controlsEnabled;
            groupBox3.Enabled = controlsEnabled;
        }
        #endregion
    }
}
