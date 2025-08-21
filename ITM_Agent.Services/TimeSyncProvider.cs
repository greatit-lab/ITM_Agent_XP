// ITM_Agent.Services/TimeSyncProvider.cs
using ITM_Agent.Core;
using System;
using System.Threading;

namespace ITM_Agent.Services
{
    public sealed class TimeSyncProvider : IDisposable
    {
        // Lazy<T>를 사용하여 스레드에 안전하게 싱글턴 인스턴스를 생성
        private static readonly Lazy<TimeSyncProvider> _instance =
            new Lazy<TimeSyncProvider>(() => new TimeSyncProvider());

        // Instance 속성을 get-only 람다 속성으로 변경하여 항상 _instance.Value를 반환
        public static TimeSyncProvider Instance => _instance.Value;

        private readonly object _lock = new object();
        private TimeSpan _clockDifference = TimeSpan.Zero;
        private readonly TimeZoneInfo _koreaStandardTimezone;
        private Timer _syncTimer;

        // 의존성 서비스 필드
        private ILogger _logger;
        private DatabaseManager _databaseManager;

        // 생성자는 private으로 유지
        private TimeSyncProvider()
        {
            try
            {
                _koreaStandardTimezone = TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                _koreaStandardTimezone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul");
            }
        }

        /// <summary>
        /// 싱글턴 인스턴스에 필요한 서비스들을 주입하고 초기화합니다.
        /// 프로그램 시작 시 한 번만 호출되어야 합니다.
        /// </summary>
        public void Initialize(IAppServiceProvider serviceProvider)
        {
            // Instance 속성에 값을 할당하는 대신, 내부 필드에 서비스를 할당
            _logger = serviceProvider.GetService<ILogger>();
            _databaseManager = serviceProvider.GetService<DatabaseManager>();

            if (_logger == null || _databaseManager == null)
            {
                throw new InvalidOperationException("TimeSyncProvider 초기화에 필수 서비스가 필요합니다.");
            }

            // 서비스 주입 후 타이머 시작
            StartTimer();
        }

        private void StartTimer()
        {
            if (_syncTimer == null)
            {
                _syncTimer = new Timer(
                    callback: _ => SynchronizeWithServer(),
                    state: null,
                    dueTime: TimeSpan.Zero,
                    period: TimeSpan.FromMinutes(10)
                );
            }
        }

        // ... 이하 나머지 코드는 변경 없음 ...
        private void SynchronizeWithServer()
        {
            try
            {
                DateTime serverUtcTime = _databaseManager.GetServerUtcTime();
                DateTime clientUtcTime = DateTime.UtcNow;

                lock (_lock)
                {
                    _clockDifference = serverUtcTime - clientUtcTime;
                }
                _logger.LogDebug($"[TimeSyncProvider] 시간 동기화 완료. 서버-PC 시간 차: {_clockDifference.TotalMilliseconds}ms");
            }
            catch (Exception ex)
            {
                _logger.LogError($"[TimeSyncProvider] 서버 시간 동기화 실패: {ex.Message}");
            }
        }

        public DateTime ToSynchronizedKst(DateTime localTime)
        {
            DateTime utcTime;
            switch (localTime.Kind)
            {
                case DateTimeKind.Utc:
                    utcTime = localTime;
                    break;
                case DateTimeKind.Local:
                    utcTime = localTime.ToUniversalTime();
                    break;
                case DateTimeKind.Unspecified:
                default:
                    utcTime = TimeZoneInfo.ConvertTimeToUtc(localTime);
                    break;
            }

            DateTime synchronizedUtcTime;
            lock (_lock)
            {
                synchronizedUtcTime = utcTime.Add(_clockDifference);
            }

            return TimeZoneInfo.ConvertTimeFromUtc(synchronizedUtcTime, _koreaStandardTimezone);
        }

        public void Dispose()
        {
            _syncTimer?.Dispose();
        }
    }
}
