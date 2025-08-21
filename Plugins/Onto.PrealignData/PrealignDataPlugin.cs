// Plugins/Onto.PrealignData/PrealignDataPlugin.cs
using ITM_Agent.Core;
using ITM_Agent.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Onto.PrealignData
{
    /// <summary>
    /// ONTO 장비의 PreAlign 로그 파일(PreAlignLog.dat)을 파싱하여 DB에 업로드하는 플러그인입니다.
    /// </summary>
    public class PrealignDataPlugin : PluginBase
    {
        //--- 의존성 서비스 ---
        private SettingsManager _settingsManager;
        private DatabaseManager _databaseManager;
        private TimeSyncProvider _timeSyncProvider;

        //--- 플러그인 내부 상태 ---
        private string _eqpid;

        //--- IPlugin 구현 ---
        public override string Name => "Onto_PrealignData";
        public override string Version => "2.0.0";

        /// <summary>
        /// 플러그인 초기화 시 필요한 서비스를 주입받습니다.
        /// </summary>
        public override void Initialize(IServiceProvider serviceProvider)
        {
            base.Initialize(serviceProvider); // 기본 초기화 (Logger 할당)

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
        /// 지정된 PreAlignLog.dat 파일의 데이터를 처리하고 업로드합니다.
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

                var prealignTable = BuildPrealignDataTable(lines);
                if (prealignTable.Rows.Count == 0)
                {
                    Logger.LogDebug($"[{Name}] No valid prealign data found in: {Path.GetFileName(path)}");
                    return;
                }

                _databaseManager.BulkInsert(prealignTable, "plg_prealign");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[{Name}] Unhandled exception while processing '{path}'. Error: {ex.Message}");
            }
        }

        #region --- 데이터 파싱 및 테이블 생성 ---

        private DataTable BuildPrealignDataTable(string[] lines)
        {
            var dt = new DataTable("plg_prealign");
            dt.Columns.AddRange(new[]
            {
                new DataColumn("eqpid", typeof(string)),
                new DataColumn("datetime", typeof(DateTime)),
                new DataColumn("xmm", typeof(decimal)),
                new DataColumn("ymm", typeof(decimal)),
                new DataColumn("notch", typeof(decimal)),
                new DataColumn("serv_ts", typeof(DateTime))
            });

            var regex = new Regex(@"Xmm\s*([-\d.]+)\s*Ymm\s*([-\d.]+)\s*Notch\s*([-\d.]+)\s*Time\s*([\d\-:\s]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            foreach (string line in lines)
            {
                Match match = regex.Match(line);
                if (!match.Success) continue;

                if (TryParseDate(match.Groups[4].Value.Trim(), out DateTime parsedTs) &&
                    decimal.TryParse(match.Groups[1].Value, out decimal x) &&
                    decimal.TryParse(match.Groups[2].Value, out decimal y) &&
                    decimal.TryParse(match.Groups[3].Value, out decimal n))
                {
                    var dr = dt.NewRow();
                    dr["eqpid"] = _eqpid;
                    dr["datetime"] = parsedTs;
                    dr["xmm"] = x;
                    dr["ymm"] = y;
                    dr["notch"] = n;
                    dr["serv_ts"] = _timeSyncProvider.ToSynchronizedKst(parsedTs);
                    dt.Rows.Add(dr);
                }
            }
            return dt;
        }

        /// <summary>
        /// 여러 형식의 날짜 문자열 파싱을 시도합니다.
        /// </summary>
        private bool TryParseDate(string dateString, out DateTime result)
        {
            // "MM-dd-yy HH:mm:ss" 와 같은 일반적인 형식을 먼저 시도합니다.
            string[] formats = { "MM-dd-yy HH:mm:ss", "M-d-yy HH:mm:ss" };
            if (DateTime.TryParseExact(dateString, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            {
                return true;
            }
            // 그 외 시스템에서 인식 가능한 다른 형식을 시도합니다.
            return DateTime.TryParse(dateString, out result);
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
