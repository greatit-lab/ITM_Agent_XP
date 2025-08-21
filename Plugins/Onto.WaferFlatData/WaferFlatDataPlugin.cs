// Plugins/Onto.WaferFlatData/WaferFlatDataPlugin.cs
using ITM_Agent.Core;
using ITM_Agent.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Onto.WaferFlatData
{
    /// <summary>
    /// ONTO 장비의 Wafer Flat 데이터 파일을 파싱하여 DB에 업로드하는 플러그인입니다.
    /// 동적으로 변하는 CSV 헤더를 정규화하여 처리합니다.
    /// </summary>
    public class WaferFlatDataPlugin : PluginBase
    {
        //--- 의존성 서비스 ---
        private SettingsManager _settingsManager;
        private DatabaseManager _databaseManager;
        private TimeSyncProvider _timeSyncProvider;

        //--- 플러그인 내부 상태 ---
        private string _eqpid;

        //--- IPlugin 구현 ---
        public override string Name => "Onto_WaferFlatData";
        public override string Version => "2.0.0";

        public override void Initialize(IAppServiceProvider serviceProvider)
        {
            base.Initialize(serviceProvider); // 기본 초기화

            _settingsManager = serviceProvider.GetService<SettingsManager>();
            _databaseManager = serviceProvider.GetService<DatabaseManager>();
            _timeSyncProvider = TimeSyncProvider.Instance;

            if (_settingsManager == null || _databaseManager == null || _timeSyncProvider == null)
            {
                throw new InvalidOperationException("필수 서비스가 주입되지 않았습니다.");
            }

            _eqpid = _settingsManager.GetValue("Eqpid", "Eqpid");
        }

        /// <summary>
        /// 지정된 Wafer Flat 데이터 파일의 데이터를 처리하고 업로드합니다.
        /// </summary>
        public override void Process(string path, params object[] args)
        {
            Logger.LogEvent($"[{Name}] Processing file: {path}");
            try
            {
                if (!WaitForFileReady(path))
                {
                    Logger.LogEvent($"[{Name}] Skipped – file is not ready: {path}");
                    return;
                }

                string content = File.ReadAllText(path, Encoding.GetEncoding(949));
                var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                var waferFlatTable = BuildWaferFlatDataTable(lines);
                if (waferFlatTable.Rows.Count == 0)
                {
                    Logger.LogDebug($"[{Name}] No valid wafer flat data found in: {Path.GetFileName(path)}");
                    return;
                }

                _databaseManager.BulkInsert(waferFlatTable, "plg_wf_flat");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[{Name}] Unhandled exception while processing '{path}'. Error: {ex.Message}");
            }
        }

        #region --- 데이터 파싱 및 테이블 생성 ---

        private DataTable BuildWaferFlatDataTable(string[] lines)
        {
            // 1. Key-Value 메타데이터 파싱
            var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in lines)
            {
                var parts = line.Split(new[] { ':' }, 2);
                if (parts.Length == 2)
                {
                    meta[parts[0].Trim()] = parts[1].Trim();
                }
            }

            // 2. CSV 데이터 시작 지점(헤더) 찾기
            int headerIndex = Array.FindIndex(lines, l => l.TrimStart().StartsWith("Point#", StringComparison.OrdinalIgnoreCase));
            if (headerIndex == -1) return new DataTable(); // 헤더 없으면 처리 불가

            // 3. 헤더 정규화 및 인덱스 매핑
            var headers = lines[headerIndex].Split(',').Select(NormalizeHeader).ToList();
            var headerMap = headers.Select((h, i) => new { h, i })
                                   .ToDictionary(x => x.h, x => x.i);

            // 4. DataTable 스키마 정의
            var dt = new DataTable("plg_wf_flat");
            foreach (var header in headers)
            {
                dt.Columns.Add(header, typeof(object)); // 모든 데이터를 object로 받아 유연성 확보
            }
            // 공통 메타데이터 컬럼 추가
            dt.Columns.Add("eqpid", typeof(string));
            dt.Columns.Add("cassettercp", typeof(string));
            dt.Columns.Add("stagercp", typeof(string));
            dt.Columns.Add("stagegroup", typeof(string));
            dt.Columns.Add("lotid", typeof(string));
            dt.Columns.Add("waferid", typeof(int));
            dt.Columns.Add("datetime", typeof(DateTime));
            dt.Columns.Add("film", typeof(string));
            dt.Columns.Add("serv_ts", typeof(DateTime));

            // 5. 데이터 행 파싱
            for (int i = headerIndex + 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var values = lines[i].Split(',');
                if (values.Length < headers.Count) continue;

                var dr = dt.NewRow();
                dr["eqpid"] = _eqpid;

                // 메타데이터 채우기
                meta.TryGetValue("Cassette Recipe Name", out string cassetteRcp);
                dr["cassettercp"] = cassetteRcp;
                meta.TryGetValue("Stage Recipe Name", out string stageRcp);
                dr["stagercp"] = stageRcp;
                meta.TryGetValue("Stage Group Name", out string stageGroup);
                dr["stagegroup"] = stageGroup;
                meta.TryGetValue("Lot ID", out string lotId);
                dr["lotid"] = lotId;
                meta.TryGetValue("Film Name", out string filmName);
                dr["film"] = filmName;

                if (meta.TryGetValue("Wafer ID", out string waferIdStr) &&
                    int.TryParse(Regex.Match(waferIdStr, @"\d+").Value, out int waferNo))
                {
                    dr["waferid"] = waferNo;
                }

                if (meta.TryGetValue("Date and Time", out string dateTimeStr) &&
                    DateTime.TryParse(dateTimeStr, out DateTime dateTime))
                {
                    dr["datetime"] = dateTime;
                    dr["serv_ts"] = _timeSyncProvider.ToSynchronizedKst(dateTime);
                }

                // CSV 데이터 채우기
                foreach (var header in headerMap)
                {
                    dr[header.Key] = ParseValue(values[header.Value]);
                }
                dt.Rows.Add(dr);
            }
            return dt;
        }

        private string NormalizeHeader(string header)
        {
            header = header.ToLowerInvariant().Trim();
            header = Regex.Replace(header, @"\s+", "_"); // 공백 -> 언더스코어
            header = Regex.Replace(header, @"[#/\.\-\(\)]", ""); // 특수문자 제거
            return header;
        }

        private object ParseValue(string value)
        {
            string trimmedValue = value.Trim();
            if (string.IsNullOrEmpty(trimmedValue)) return DBNull.Value;
            if (int.TryParse(trimmedValue, out int intVal)) return intVal;
            if (double.TryParse(trimmedValue, out double dblVal)) return dblVal;
            return trimmedValue;
        }

        #endregion

        #region --- 유틸리티 ---

        private bool WaitForFileReady(string path, int maxRetries = 10, int delayMs = 300)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        return true;
                    }
                }
                catch (IOException) { Thread.Sleep(delayMs); }
            }
            return false;
        }

        #endregion
    }
}
