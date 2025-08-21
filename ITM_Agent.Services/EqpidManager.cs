// ITM_Agent.Services/EqpidManager.cs
using ITM_Agent.Core;
using System;
using System.Data;
using System.Globalization;
using System.Management; // WMI 사용을 위해 추가

namespace ITM_Agent.Services
{
    /// <summary>
    /// Eqpid 및 에이전트의 시스템 정보를 관리하고 데이터베이스에 등록하는 서비스입니다.
    /// UI에 대한 직접적인 의존성을 갖지 않습니다.
    /// </summary>
    public class EqpidManager
    {
        private readonly ILogger _logger;
        private readonly SettingsManager _settingsManager;
        private readonly DatabaseManager _databaseManager;
        private readonly string _appVersion;

        // 생성자에서 IAppServiceProvider를 주입받도록 수정
        public EqpidManager(IAppServiceProvider serviceProvider, string appVersion)
        {
            _logger = serviceProvider.GetService<ILogger>();
            _settingsManager = serviceProvider.GetService<SettingsManager>();
            _databaseManager = serviceProvider.GetService<DatabaseManager>();
            _appVersion = appVersion;

            if (_logger == null || _settingsManager == null || _databaseManager == null)
            {
                throw new InvalidOperationException("EqpidManager에 필수 서비스가 주입되지 않았습니다.");
            }
        }

        /// <summary>
        /// 설정에 EQPID가 존재하는지 확인합니다.
        /// </summary>
        /// <returns>EQPID가 존재하면 true, 없으면 false를 반환합니다.</returns>
        public bool CheckEqpidExists()
        {
            string eqpid = _settingsManager.GetValue("Eqpid", "Eqpid");
            return !string.IsNullOrEmpty(eqpid);
        }

        /// <summary>
        /// 새로 입력받은 EQPID와 Type을 설정하고, 관련 정보를 DB에 업로드합니다.
        /// </summary>
        /// <param name="eqpid">새로운 EQPID</param>
        /// <param name="type">새로운 장비 타입</param>
        public void RegisterNewEqpid(string eqpid, string type)
        {
            if (string.IsNullOrEmpty(eqpid) || string.IsNullOrEmpty(type))
            {
                throw new ArgumentNullException("EQPID와 Type은 null이거나 비어있을 수 없습니다.");
            }

            string upperEqpid = eqpid.ToUpper();
            _settingsManager.SetValue("Eqpid", "Eqpid", upperEqpid);
            _settingsManager.SetValue("Eqpid", "Type", type);
            _logger.LogEvent($"[EqpidManager] New EQPID registered: {upperEqpid}, Type: {type}");

            UploadAgentInfoToDatabase(upperEqpid, type);
        }

        private void UploadAgentInfoToDatabase(string eqpid, string type)
        {
            try
            {
                var dt = new DataTable("agent_info");
                dt.Columns.AddRange(new[]
                {
                    new DataColumn("eqpid", typeof(string)),
                    new DataColumn("type", typeof(string)),
                    new DataColumn("os", typeof(string)),
                    new DataColumn("system_type", typeof(string)),
                    new DataColumn("pc_name", typeof(string)),
                    new DataColumn("locale", typeof(string)),
                    new DataColumn("timezone", typeof(string)),
                    new DataColumn("app_ver", typeof(string)),
                    new DataColumn("reg_date", typeof(DateTime)),
                });

                dt.Rows.Add(
                    eqpid,
                    type,
                    GetOSVersion(),
                    Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit",
                    Environment.MachineName,
                    CultureInfo.CurrentCulture.Name,
                    TimeZoneInfo.Local.Id,
                    _appVersion,
                    DateTime.Now
                );

                // DatabaseManager의 Upsert와 유사한 기능이 필요
                _databaseManager.BulkInsert(dt, "agent_info");
                _logger.LogEvent($"[EqpidManager] Agent info for '{eqpid}' uploaded to database.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[EqpidManager] Failed to upload agent info to database. Error: {ex.Message}");
            }
        }

        private string GetOSVersion()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        return obj["Caption"]?.ToString() ?? "Unknown OS";
                    }
                }
            }
            catch { /* WMI 쿼리 실패 시 대비 */ }
            return Environment.OSVersion.VersionString;
        }
    }
}
