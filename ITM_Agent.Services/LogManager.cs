// ITM_Agent.Services/LogManager.cs
using ITM_Agent.Core;
using System;
using System.IO;
using System.Text;
using System.Threading;

namespace ITM_Agent.Services
{
    /// <summary>
    /// ILogger 인터페이스를 구현하여 파일 기반 로깅을 처리하는 중앙 서비스입니다.
    /// 이벤트, 오류, 디버그 로그를 별도의 파일에 기록하며, 로그 파일 크기 기반의 로테이션 기능을 포함합니다.
    /// </summary>
    public class LogManager : ILogger
    {
        private readonly string _logFolderPath;
        private const long MAX_LOG_SIZE = 5 * 1024 * 1024; // 5MB
        private static readonly object _fileLock = new object();

        private bool _isDebugMode = false;

        /// <summary>
        /// LogManager 인스턴스를 생성합니다.
        /// </summary>
        /// <param name="baseDirectory">로그 파일을 저장할 'Logs' 폴더가 생성될 기본 디렉터리입니다.</param>
        public LogManager(string baseDirectory)
        {
            _logFolderPath = Path.Combine(baseDirectory, "Logs");
            Directory.CreateDirectory(_logFolderPath);
        }

        /// <summary>
        /// 디버그 모드 활성화 여부를 설정합니다.
        /// </summary>
        public void SetDebugMode(bool isEnabled)
        {
            _isDebugMode = isEnabled;
        }

        /// <summary>
        /// 일반 이벤트를 'event.log' 파일에 기록합니다.
        /// </summary>
        public void LogEvent(string message)
        {
            string logFileName = $"{DateTime.Now:yyyyMMdd}_event.log";
            WriteLog("[Event]", message, logFileName);
        }

        /// <summary>
        /// 오류를 'error.log' 파일에 기록합니다.
        /// </summary>
        public void LogError(string message)
        {
            string logFileName = $"{DateTime.Now:yyyyMMdd}_error.log";
            WriteLog("[Error]", message, logFileName);
        }

        /// <summary>
        /// 디버그 모드가 활성화된 경우에만 'debug.log' 파일에 상세 정보를 기록합니다.
        /// </summary>
        public void LogDebug(string message)
        {
            if (!_isDebugMode) return;
            string logFileName = $"{DateTime.Now:yyyyMMdd}_debug.log";
            WriteLog("[Debug]", message, logFileName);
        }

        private void WriteLog(string logType, string message, string fileName)
        {
            // 여러 스레드에서 동시에 파일에 접근하는 것을 방지합니다.
            lock (_fileLock)
            {
                string filePath = Path.Combine(_logFolderPath, fileName);
                try
                {
                    RotateLogFileIfNeeded(filePath);

                    string logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {logType} {message}{Environment.NewLine}";

                    // 여러 프로세스나 스레드가 동시에 접근할 수 있도록 FileShare 옵션을 지정합니다.
                    using (var fileStream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    using (var streamWriter = new StreamWriter(fileStream, Encoding.UTF8))
                    {
                        streamWriter.Write(logLine);
                    }
                }
                catch (Exception)
                {
                    // 로깅 실패 시 프로그램 전체가 멈추는 것을 방지합니다.
                    // (필요 시 콘솔 출력 등으로 대체 가능)
                }
            }
        }

        private void RotateLogFileIfNeeded(string filePath)
        {
            if (!File.Exists(filePath)) return;

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length <= MAX_LOG_SIZE) return;

            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
            string extension = Path.GetExtension(filePath);
            int index = 1;
            string rotatedPath;

            do
            {
                rotatedPath = Path.Combine(_logFolderPath, $"{fileNameWithoutExt}_{index++}{extension}");
            } while (File.Exists(rotatedPath));

            File.Move(filePath, rotatedPath);
        }
    }
}
