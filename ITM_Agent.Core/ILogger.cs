// ITM_Agent.Core/ILogger.cs
namespace ITM_Agent.Core
{
    /// <summary>
    /// 애플리케이션 전체에서 사용할 표준 로깅 인터페이스입니다.
    /// 모든 서비스와 플러그인은 이 인터페이스에 의존하여 로그를 기록합니다.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// 일반적인 이벤트나 주요 동작 상태를 기록합니다.
        /// </summary>
        /// <param name="message">기록할 메시지</param>
        void LogEvent(string message);

        /// <summary>
        /// 오류나 예외 상황을 기록합니다.
        /// </summary>
        /// <param name="message">기록할 오류 메시지</param>
        void LogError(string message);

        /// <summary>
        /// 디버깅 목적의 상세 정보를 기록합니다.
        /// 이 로그는 디버그 모드가 활성화되었을 때만 기록됩니다.
        /// </summary>
        /// <param name="message">기록할 디버그 메시지</param>
        void LogDebug(string message);
    }
}
