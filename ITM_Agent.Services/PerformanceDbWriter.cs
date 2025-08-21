// ITM_Agent.Services/PerformanceDbWriter.cs
using ITM_Agent.Core;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;

namespace ITM_Agent.Services
{
    public class PerformanceDbWriter : IDisposable
    {
        private readonly ILogger _logger;
        private readonly DatabaseManager _databaseManager;
        private readonly PerformanceMonitor _performanceMonitor;
        private readonly TimeSyncProvider _timeSyncProvider;
        private readonly SettingsManager _settingsManager;

        private readonly List<PerformanceMetric> _buffer = new List<PerformanceMetric>();
        private readonly object _lock = new object();
        private Timer _flushTimer;
        private bool _isRunning = false;

        private const int BULK_INSERT_COUNT = 60;
        private const int FLUSH_INTERVAL_MS = 30000;

        public PerformanceDbWriter(IAppServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetService<ILogger>();
            _databaseManager = serviceProvider.GetService<DatabaseManager>();
            _performanceMonitor = serviceProvider.GetService<PerformanceMonitor>();
            _settingsManager = serviceProvider.GetService<SettingsManager>();
            _timeSyncProvider = TimeSyncProvider.Instance;

            if (_logger == null || _databaseManager == null || _performanceMonitor == null || _settingsManager == null)
            {
                throw new InvalidOperationException("PerformanceDbWriter에 필수 서비스가 주입되지 않았습니다.");
            }
        }

        public void Start()
        {
            if (_isRunning) return;

            if (_settingsManager.GetValue("Option", "EnablePerfoLog") == "1")
            {
                _isRunning = true;
                _performanceMonitor.OnSample += OnPerformanceSampled;
                _flushTimer = new Timer(_ => Flush(), null, FLUSH_INTERVAL_MS, FLUSH_INTERVAL_MS);
                _logger.LogEvent("[PerformanceDbWriter] Service started.");
            }
            else
            {
                _logger.LogEvent("[PerformanceDbWriter] Service did not start because performance logging is disabled in settings.");
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;
            _performanceMonitor.OnSample -= OnPerformanceSampled;
            _flushTimer?.Dispose();
            _flushTimer = null;
            Flush();
            _logger.LogEvent("[PerformanceDbWriter] Service stopped.");
        }

        private void OnPerformanceSampled(PerformanceMetric metric)
        {
            lock (_lock)
            {
                _buffer.Add(metric);
                if (_buffer.Count >= BULK_INSERT_COUNT)
                {
                    ThreadPool.QueueUserWorkItem(_ => Flush());
                }
            }
        }

        private void Flush()
        {
            List<PerformanceMetric> batch;
            lock (_lock)
            {
                if (_buffer.Count == 0) return;
                batch = new List<PerformanceMetric>(_buffer);
                _buffer.Clear();
            }

            try
            {
                string eqpid = _settingsManager.GetValue("Eqpid", "Eqpid");
                if (string.IsNullOrEmpty(eqpid)) return;

                var dt = new DataTable("eqp_perf");
                dt.Columns.AddRange(new[]
                {
                    new DataColumn("eqpid", typeof(string)),
                    new DataColumn("ts", typeof(DateTime)),
                    new DataColumn("serv_ts", typeof(DateTime)),
                    new DataColumn("cpu_usage", typeof(float)),
                    new DataColumn("mem_usage", typeof(float))
                });

                foreach (var metric in batch)
                {
                    // ★★★★★★★★★★★★ 수정된 부분 ★★★★★★★★★★★★
                    // DateTime 객체에서 밀리초를 제거하는 로직
                    DateTime originalTs = metric.Timestamp;
                    DateTime truncatedTs = new DateTime(originalTs.Year, originalTs.Month, originalTs.Day,
                                                        originalTs.Hour, originalTs.Minute, originalTs.Second);

                    DateTime synchronizedTs = _timeSyncProvider.ToSynchronizedKst(truncatedTs);
                    DateTime truncatedServTs = new DateTime(synchronizedTs.Year, synchronizedTs.Month, synchronizedTs.Day,
                                                            synchronizedTs.Hour, synchronizedTs.Minute, synchronizedTs.Second);
                    // ★★★★★★★★★★★★★★★★★★★★★★★★★★★★★

                    float cpu = (float)Math.Round(metric.CpuUsage, 2);
                    float mem = (float)Math.Round(metric.MemoryUsage, 2);

                    dt.Rows.Add(
                        eqpid,
                        truncatedTs,      // 밀리초가 제거된 장비 시간
                        truncatedServTs,  // 밀리초가 제거된 서버 보정 시간
                        cpu,
                        mem
                    );
                }

                _databaseManager.BulkInsert(dt, "eqp_perf");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[PerformanceDbWriter] Failed to flush performance data to DB. Error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
