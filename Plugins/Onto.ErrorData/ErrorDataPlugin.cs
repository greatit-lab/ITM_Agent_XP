// Plugins/Onto.ErrorData/ErrorDataPlugin.cs
using ITM_Agent.Core;
using ITM_Agent.Services;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Onto.ErrorData
{
    /// <summary>
    /// ONTO 장비의 Error 로그 파일(*.log)을 파싱하여 DB에 업로드하는 플러그인입니다.
    /// DB에 등록된 허용 Error ID 목록을 기준으로 데이터를 필터링합니다.
    /// </summary>
    public class ErrorDataPlugin : PluginBase
    {
        //--- 의존성 서비스 ---
        private SettingsManager _settingsManager;
        private DatabaseManager _databaseManager;
        private TimeSyncProvider _timeSyncProvider;

        //--- 플러그인 내부 상태 ---
        private string _eqpid;
        private HashSet<string> _allowedErrorIds;

        //--- IPlugin 구현 ---
        public override string Name => "Onto_ErrorData";
        public override string Version => "2.0.0";

        /// <summary>
        /// 플러그인 초기화 시 필요한 서비스를 주입받습니다.
        /// </summary>
        public override void Initialize(IAppServiceProvider serviceProvider)
        {
            base.Initialize(serviceProvider); // 기본 초기화 (Logger 할당)

            _settingsManager = serviceProvider.GetService<SettingsManager>();
            _databaseManager = serviceProvider.GetService<DatabaseManager>();
            _timeSyncProvider = TimeSyncProvider.Instance; // 싱글턴 인스턴스 사용

            if (_settingsManager == null || _databaseManager == null || _timeSyncProvider == null)
            {
                throw new InvalidOperationException("필수 서비스가 주입되지 않았습니다.");
            }

            // EQPID 및 필터 목록 미리 로드
            _eqpid = _settingsManager.GetValue("Eqpid", "Eqpid");
            _allowedErrorIds = LoadErrorFilterSetFromDb();
        }

        /// <summary>
        /// 지정된 파일 경로의 데이터를 처리하고 업로드합니다.
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

                // .NET Framework 4는 CP949 인코딩을 위해 별도 처리가 필요할 수 있습니다.
                // Program.cs에서 Encoding.RegisterProvider(CodePagesEncodingProvider.Instance) 호출을 권장합니다.
                string content = File.ReadAllText(path, Encoding.GetEncoding(949));
                var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                var errorTable = BuildErrorDataTable(lines);
                if (errorTable.Rows.Count == 0)
                {
                    Logger.LogDebug($"[{Name}] No valid error data found in: {Path.GetFileName(path)}");
                    return;
                }

                var filteredTable = ApplyErrorFilter(errorTable, out int matched, out int skipped);
                Logger.LogEvent($"[{Name}] Filter Result for '{Path.GetFileName(path)}': Total={errorTable.Rows.Count}, Matched={matched}, Skipped={skipped}");

                if (filteredTable.Rows.Count > 0)
                {
                    // DatabaseManager 서비스를 통해 표준화된 방식으로 DB에 삽입
                    _databaseManager.BulkInsert(filteredTable, "plg_error");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[{Name}] Unhandled exception while processing '{path}'. Error: {ex.Message}");
            }
        }

        #region --- 데이터 처리 및 테이블 생성 로직 ---

        private DataTable BuildErrorDataTable(string[] lines)
        {
            var dt = new DataTable("plg_error");
            dt.Columns.AddRange(new[]
            {
                new DataColumn("eqpid", typeof(string)),
                new DataColumn("error_id", typeof(string)),
                new DataColumn("time_stamp", typeof(DateTime)),
                new DataColumn("error_label", typeof(string)),
                new DataColumn("error_desc", typeof(string)),
                new DataColumn("millisecond", typeof(int)),
                new DataColumn("extra_message_1", typeof(string)),
                new DataColumn("extra_message_2", typeof(string)),
                new DataColumn("serv_ts", typeof(DateTime))
            });

            var regex = new Regex(@"^(?<id>\w+),\s*(?<ts>[^,]+),\s*(?<lbl>[^,]+),\s*(?<desc>[^,]+),\s*(?<ms>\d+)(?:,\s*(?<extra>.*))?", RegexOptions.Compiled);

            foreach (var line in lines)
            {
                var match = regex.Match(line);
                if (!match.Success) continue;

                if (DateTime.TryParseExact(match.Groups["ts"].Value.Trim(), "dd-MMM-yy h:mm:ss tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedTs))
                {
                    var dr = dt.NewRow();
                    dr["eqpid"] = _eqpid;
                    dr["error_id"] = match.Groups["id"].Value.Trim();
                    dr["time_stamp"] = parsedTs;
                    dr["error_label"] = match.Groups["lbl"].Value.Trim();
                    dr["error_desc"] = match.Groups["desc"].Value.Trim();
                    dr["millisecond"] = int.TryParse(match.Groups["ms"].Value, out int ms) ? ms : 0;
                    dr["extra_message_1"] = match.Groups["extra"].Value.Trim();
                    dr["extra_message_2"] = DBNull.Value; // 필요 시 추가 로직
                    dr["serv_ts"] = _timeSyncProvider.ToSynchronizedKst(parsedTs); // TimeSyncProvider 사용
                    dt.Rows.Add(dr);
                }
            }
            return dt;
        }

        private DataTable ApplyErrorFilter(DataTable sourceTable, out int matchedCount, out int skippedCount)
        {
            matchedCount = 0;
            skippedCount = 0;

            var filteredTable = sourceTable.Clone();
            if (_allowedErrorIds == null || _allowedErrorIds.Count == 0)
            {
                Logger.LogDebug($"[{Name}] No error ID filter rules loaded. All data will be skipped.");
                skippedCount = sourceTable.Rows.Count;
                return filteredTable; // 필터 규칙 없으면 빈 테이블 반환
            }

            foreach (DataRow row in sourceTable.Rows)
            {
                string errorId = row["error_id"].ToString().Trim().ToUpperInvariant();
                if (_allowedErrorIds.Contains(errorId))
                {
                    filteredTable.ImportRow(row);
                    matchedCount++;
                }
                else
                {
                    skippedCount++;
                }
            }
            return filteredTable;
        }

        #endregion

        #region --- DB 연동 및 유틸리티 ---

        private HashSet<string> LoadErrorFilterSetFromDb()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                // DatabaseManager에 SELECT를 위한 범용 메서드가 있다면 그것을 사용하는 것이 이상적입니다.
                // 여기서는 임시로 DatabaseManager에 추가될 ExecuteQuery 메서드를 가정합니다.
                var resultTable = _databaseManager.ExecuteQuery("SELECT error_id FROM public.err_severity_map");
                foreach (DataRow row in resultTable.Rows)
                {
                    if (row[0] != DBNull.Value)
                    {
                        set.Add(row[0].ToString().Trim().ToUpperInvariant());
                    }
                }
                    Logger.LogEvent($"[{Name}] Loaded {set.Count} error ID filter(s) from DB.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[{Name}] Failed to load error filter set from DB. Error: {ex.Message}");
            }
            return set;
        }

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
