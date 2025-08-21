// ITM_Agent.Services/PerformanceMonitor.cs
using ITM_Agent.Core;
using System;
using System.Diagnostics;
using System.Management;
using System.Threading;

namespace ITM_Agent.Services
{
    /// <summary>
    /// 시스템의 CPU 및 메모리 사용량을 주기적으로 샘플링하는 서비스입니다.
    /// 과부하 상태를 감지하여 샘플링 주기를 동적으로 조절하며, 측정된 데이터는 이벤트를 통해 외부로 전달됩니다.
    /// </summary>
    public sealed class PerformanceMonitor : IDisposable
    {
        /// <summary>
        /// 성능 데이터가 샘플링될 때 발생하는 이벤트입니다.
        /// </summary>
        public event Action<PerformanceMetric> OnSample;

        private readonly ILogger _logger;
        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter _memCounter;
        private readonly float _totalMemoryMb;
        private Timer _timer;
        private bool _isOverloaded = false;

        private int NormalInterval { get; } = 5000; // 5초
        private int OverloadInterval { get; } = 1000; // 1초

        public PerformanceMonitor(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // PDH 카운터 초기화
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _memCounter = new PerformanceCounter("Memory", "Available MBytes");
            _totalMemoryMb = GetTotalMemoryMb();

            // 첫 값을 읽어오기 위한 초기 호출
            _cpuCounter.NextValue();
        }

        /// <summary>
        /// 성능 모니터링을 시작합니다.
        /// </summary>
        public void Start()
        {
            if (_timer != null) return;
            _timer = new Timer(_ => Sample(), null, 0, NormalInterval);
            _logger.LogEvent("[PerformanceMonitor] Started.");
        }

        /// <summary>
        /// 성능 모니터링을 중지합니다.
        /// </summary>
        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
            _logger.LogEvent("[PerformanceMonitor] Stopped.");
        }

        private void Sample()
        {
            try
            {
                // CPU 사용률은 두 번 측정해야 정확한 값을 얻을 수 있습니다.
                Thread.Sleep(1000);
                float cpuUsage = _cpuCounter.NextValue();
                float availableMemoryMb = _memCounter.NextValue();
                float memoryUsage = 0f;
                if (_totalMemoryMb > 0)
                {
                    memoryUsage = ((_totalMemoryMb - availableMemoryMb) / _totalMemoryMb) * 100f;
                }

                var metric = new PerformanceMetric(cpuUsage, memoryUsage);
                OnSample?.Invoke(metric);

                // 과부하 상태 감지 및 샘플링 간격 조절
                bool currentlyOverloaded = (cpuUsage > 75f) || (memoryUsage > 80f);
                if (currentlyOverloaded && !_isOverloaded)
                {
                    _isOverloaded = true;
                    _timer.Change(0, OverloadInterval);
                    _logger.LogDebug("[PerformanceMonitor] System overload detected. Switching to 1-second interval.");
                }
                else if (!currentlyOverloaded && _isOverloaded)
                {
                    _isOverloaded = false;
                    _timer.Change(0, NormalInterval);
                    _logger.LogDebug("[PerformanceMonitor] System stable. Switching back to 5-second interval.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[PerformanceMonitor] Failed to sample performance data: {ex.Message}");
            }
        }

        private float GetTotalMemoryMb()
        {
            try
            {
                // .NET Framework 4는 WMI를 사용하는 것이 가장 안정적입니다.
                using (var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        // KB 단위를 MB로 변환
                        return Convert.ToSingle(obj["TotalVisibleMemorySize"]) / 1024f;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[PerformanceMonitor] Failed to get total physical memory: {ex.Message}");
            }
            return 0f;
        }

        public void Dispose()
        {
            Stop();
            _cpuCounter?.Dispose();
            _memCounter?.Dispose();
        }
    }

    /// <summary>
    /// 특정 시점의 성능 데이터를 나타내는 읽기 전용 구조체입니다.
    /// </summary>
    public readonly struct PerformanceMetric
    {
        public DateTime Timestamp { get; }
        public float CpuUsage { get; }
        public float MemoryUsage { get; }

        public PerformanceMetric(float cpuUsage, float memoryUsage)
        {
            Timestamp = DateTime.Now;
            CpuUsage = cpuUsage;
            MemoryUsage = memoryUsage;
        }
    }
}
