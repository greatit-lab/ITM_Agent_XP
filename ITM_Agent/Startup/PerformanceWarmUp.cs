// ITM_Agent/Startup/PerformanceWarmUp.cs
using ITM_Agent.Core;
using ITM_Agent.Services;
using System;
using System.Diagnostics;
using System.Threading;

namespace ITM_Agent.Startup
{
    /// <summary>
    /// 애플리케이션 시작 시 주요 구성 요소의 초기 로딩 시간을 줄이기 위한
    /// '준비 운동(Warm-Up)' 작업을 수행하는 정적 클래스입니다.
    /// </summary>
    internal static class PerformanceWarmUp
    {
        /// <summary>
        /// 준비 운동 작업을 실행합니다.
        /// </summary>
        /// <param name="serviceProvider">서비스에 접근하기 위한 AppServiceProvider</param>
        public static void Run(IAppServiceProvider serviceProvider) // IServiceProvider -> IAppServiceProvider
        {
            var logger = serviceProvider.GetService<ILogger>();
            logger?.LogDebug("[PerformanceWarmUp] Starting warm-up process...");

            // 1. 성능 카운터(PDH) 초기화
            try
            {
                using (var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"))
                {
                    cpuCounter.NextValue();
                    Thread.Sleep(100);
                    cpuCounter.NextValue();
                }
                logger?.LogDebug("[PerformanceWarmUp] Performance counters warmed up.");
            }
            catch (Exception ex)
            {
                logger?.LogError($"[PerformanceWarmUp] Failed to warm up performance counters. Error: {ex.Message}");
            }

            // 2. 데이터베이스 커넥션 풀 미리 열기
            try
            {
                var dbManager = serviceProvider.GetService<DatabaseManager>();
                if (dbManager != null)
                {
                    dbManager.TestConnection(); 
                    logger?.LogDebug("[PerformanceWarmUp] Database connection pool warmed up.");
                }
            }
            catch (Exception ex)
            {
                logger?.LogError($"[PerformanceWarmUp] Failed to warm up database connection. Error: {ex.Message}");
            }
        }
    }
}
