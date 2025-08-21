// ITM_Agent.Services/SettingsManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ITM_Agent.Services
{
    /// <summary>
    /// Settings.ini 파일의 모든 섹션과 키-값 데이터를 읽고 쓰는 중앙 관리 서비스입니다.
    /// 스레드로부터 안전한 파일 접근을 보장합니다.
    /// </summary>
    public class SettingsManager
    {
        private readonly string _settingsFilePath;
        private static readonly object _fileLock = new object();

        public SettingsManager(string settingsFilePath)
        {
            _settingsFilePath = settingsFilePath;
            EnsureSettingsFileExists();
        }

        private void EnsureSettingsFileExists()
        {
            lock (_fileLock)
            {
                if (!File.Exists(_settingsFilePath))
                {
                    File.WriteAllText(_settingsFilePath, "", Encoding.UTF8);
                }
            }
        }

        #region --- Generic Section/Key Management ---

        /// <summary>
        /// 특정 섹션에서 키에 해당하는 값을 읽어옵니다.
        /// </summary>
        public string GetValue(string section, string key)
        {
            lock (_fileLock)
            {
                var lines = File.ReadAllLines(_settingsFilePath);
                bool inSection = false;
                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();
                    if (trimmedLine.Equals($"[{section}]", StringComparison.OrdinalIgnoreCase))
                    {
                        inSection = true;
                        continue;
                    }
                    if (inSection && (trimmedLine.StartsWith("[") || string.IsNullOrWhiteSpace(trimmedLine)))
                    {
                        break; // 다른 섹션 시작 또는 섹션 끝
                    }
                    if (inSection)
                    {
                        var parts = trimmedLine.Split(new[] { '=' }, 2);
                        if (parts.Length == 2 && parts[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                        {
                            return parts[1].Trim();
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 특정 섹션에 키와 값을 설정합니다. 섹션이나 키가 없으면 새로 만듭니다.
        /// </summary>
        public void SetValue(string section, string key, string value)
        {
            lock (_fileLock)
            {
                var lines = File.Exists(_settingsFilePath) ? File.ReadAllLines(_settingsFilePath).ToList() : new List<string>();
                string sectionHeader = $"[{section}]";
                int sectionIndex = lines.FindIndex(l => l.Trim().Equals(sectionHeader, StringComparison.OrdinalIgnoreCase));

                if (sectionIndex == -1) // 섹션 없음
                {
                    if (lines.Any() && !string.IsNullOrWhiteSpace(lines.Last())) lines.Add("");
                    lines.Add(sectionHeader);
                    lines.Add($"{key} = {value}");
                }
                else // 섹션 있음
                {
                    int keyIndex = -1;
                    for (int i = sectionIndex + 1; i < lines.Count; i++)
                    {
                        if (lines[i].Trim().StartsWith("[")) break; // 다음 섹션
                        var parts = lines[i].Split(new[] { '=' }, 2);
                        if (parts.Length == 2 && parts[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                        {
                            keyIndex = i;
                            break;
                        }
                    }

                    if (keyIndex != -1) // 키 있음
                    {
                        lines[keyIndex] = $"{key} = {value}";
                    }
                    else // 키 없음
                    {
                        lines.Insert(sectionIndex + 1, $"{key} = {value}");
                    }
                }
                File.WriteAllLines(_settingsFilePath, lines, Encoding.UTF8);
            }
        }

        /// <summary>
        /// 특정 섹션의 모든 줄(Key=Value 형식 또는 그냥 값)을 리스트로 읽어옵니다.
        /// </summary>
        public List<string> GetSectionEntries(string section)
        {
            var entries = new List<string>();
            lock (_fileLock)
            {
                if (!File.Exists(_settingsFilePath)) return entries;

                var lines = File.ReadAllLines(_settingsFilePath);
                string sectionHeader = $"[{section}]";
                bool inSection = false;
                foreach (var line in lines)
                {
                    string trimmedLine = line.Trim();
                    if (trimmedLine.Equals(sectionHeader, StringComparison.OrdinalIgnoreCase))
                    {
                        inSection = true;
                        continue;
                    }
                    if (inSection)
                    {
                        if (trimmedLine.StartsWith("[") || string.IsNullOrWhiteSpace(trimmedLine)) break;
                        entries.Add(trimmedLine);
                    }
                }
            }
            return entries;
        }

        /// <summary>
        /// 특정 섹션의 모든 내용을 새로운 항목들로 덮어씁니다.
        /// </summary>
        public void SetSectionEntries(string section, IEnumerable<string> entries)
        {
            lock (_fileLock)
            {
                var lines = File.Exists(_settingsFilePath) ? File.ReadAllLines(_settingsFilePath).ToList() : new List<string>();
                string sectionHeader = $"[{section}]";
                int sectionIndex = lines.FindIndex(l => l.Trim().Equals(sectionHeader, StringComparison.OrdinalIgnoreCase));

                if (sectionIndex != -1) // 기존 섹션 삭제
                {
                    int endIndex = lines.FindIndex(sectionIndex + 1, l => l.Trim().StartsWith("["));
                    if (endIndex == -1) endIndex = lines.Count;
                    lines.RemoveRange(sectionIndex, endIndex - sectionIndex);
                }

                // 새 섹션 추가
                if (lines.Any() && !string.IsNullOrWhiteSpace(lines.Last())) lines.Add("");
                lines.Add(sectionHeader);
                lines.AddRange(entries);
                if (lines.Any() && !string.IsNullOrWhiteSpace(lines.Last())) lines.Add("");

                File.WriteAllLines(_settingsFilePath, lines, Encoding.UTF8);
            }
        }

        /// <summary>
        /// 지정된 섹션 전체를 파일에서 삭제합니다.
        /// </summary>
        public void RemoveSection(string section)
        {
            lock (_fileLock)
            {
                if (!File.Exists(_settingsFilePath)) return;
                var lines = File.ReadAllLines(_settingsFilePath).ToList();
                string sectionHeader = $"[{section}]";
                int sectionIndex = lines.FindIndex(l => l.Trim().Equals(sectionHeader, StringComparison.OrdinalIgnoreCase));

                if (sectionIndex != -1)
                {
                    int endIndex = lines.FindIndex(sectionIndex + 1, l => l.Trim().StartsWith("["));
                    if (endIndex == -1) endIndex = lines.Count;
                    lines.RemoveRange(sectionIndex, endIndex - sectionIndex);
                    File.WriteAllLines(_settingsFilePath, lines, Encoding.UTF8);
                }
            }
        }

        #endregion
    }
}
