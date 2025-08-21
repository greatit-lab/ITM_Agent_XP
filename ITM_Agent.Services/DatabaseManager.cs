// ITM_Agent.Services/DatabaseManager.cs
using ITM_Agent.Core;
using Npgsql;
using System;
using System.Data;
using System.Linq;

namespace ITM_Agent.Services
{
    /// <summary>
    /// 데이터베이스 연결, 쿼리 실행, 대량 데이터 업로드 등 PostgreSQL과 관련된 모든 작업을 중앙에서 처리하는 서비스입니다.
    /// </summary>
    public class DatabaseManager
    {
        private readonly string _connectionString;
        private readonly ILogger _logger;

        // DB 연결 정보
        private const string SERVER = "00.000.00.00";
        private const string DATABASE = "itm";
        private const string USER_ID = "userid";
        private const string PASSWORD = "pw";
        private const int PORT = 5432;

        public DatabaseManager(ILogger logger)
        {
            _logger = logger ?? new LogManager(AppDomain.CurrentDomain.BaseDirectory);

            // Npgsql 2.x 버전에 맞는 속성 이름으로 최종 수정
            _connectionString = new NpgsqlConnectionStringBuilder
            {
                Host = SERVER,
                Database = DATABASE,
                UserName = USER_ID,
                Password = PASSWORD,
                Port = PORT,
                SSL = false,           // "Ssl" -> "SSL" (대문자)로 수정
                SearchPath = "public"
            }.ConnectionString;
        }

        public void TestConnection()
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
            }
        }

        public DateTime GetServerUtcTime()
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand("SELECT NOW() AT TIME ZONE 'UTC'", conn))
                {
                    return Convert.ToDateTime(cmd.ExecuteScalar());
                }
            }
        }

        public DataTable ExecuteQuery(string query)
        {
            var dt = new DataTable();
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand(query, conn))
                    {
                        using (var adapter = new NpgsqlDataAdapter(cmd))
                        {
                            adapter.Fill(dt);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[DatabaseManager] ExecuteQuery failed. Query: {query}, Error: {ex.Message}");
            }
            return dt;
        }

        public void BulkInsert(DataTable dataTable, string tableName)
        {
            if (dataTable == null || dataTable.Rows.Count == 0)
            {
                _logger.LogDebug($"[DatabaseManager] BulkInsert skipped for '{tableName}' - data table is empty.");
                return;
            }

            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        var columns = dataTable.Columns.Cast<DataColumn>().Select(c => $"\"{c.ColumnName}\"").ToArray();
                        string columnList = string.Join(", ", columns);
                        string paramList = string.Join(", ", columns.Select(c => $":{c.Trim('\"')}"));

                        string sql = $"INSERT INTO public.\"{tableName}\" ({columnList}) VALUES ({paramList})";

                        using (var cmd = new NpgsqlCommand(sql, conn, transaction))
                        {
                            foreach (DataColumn column in dataTable.Columns)
                            {
                                cmd.Parameters.Add(new NpgsqlParameter($":{column.ColumnName}", DBNull.Value));
                            }

                            foreach (DataRow row in dataTable.Rows)
                            {
                                foreach (DataColumn column in dataTable.Columns)
                                {
                                    cmd.Parameters[$":{column.ColumnName}"].Value = row[column] ?? DBNull.Value;
                                }
                                try
                                {
                                    cmd.ExecuteNonQuery();
                                }
                                // "PostgresException" 대신 일반 "Exception"으로 받고, 메시지 내용으로 중복 오류를 확인
                                catch(Exception ex)
                                {
                                    // 23505는 unique_violation (중복 키) 에러 코드
                                    if (ex.Message.Contains("23505"))
                                    {
                                        _logger.LogDebug($"[DatabaseManager] Duplicate row skipped for table '{tableName}'.");
                                    }
                                    else throw; // 중복이 아닌 다른 오류는 상위로 전달
                                }
                            }
                        }
                        transaction.Commit();
                        _logger.LogEvent($"[DatabaseManager] {dataTable.Rows.Count} rows successfully processed for table '{tableName}'.");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        _logger.LogError($"[DatabaseManager] BulkInsert failed for table '{tableName}'. Error: {ex.Message}");
                        throw;
                    }
                }
            }
        }
    }
}
