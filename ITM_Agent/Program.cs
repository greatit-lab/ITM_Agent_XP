// ITM_Agent/Program.cs
using ITM_Agent.Core;
using ITM_Agent.Services;
using System;
// ... (다른 using 구문은 이전과 동일)

namespace ITM_Agent
{
    internal static class Program
    {
        // ... (Mutex 등 상단 내용은 변경 없음) ...
        [STAThread]
        static void Main()
        {
            // ... (Mutex, Application 초기화 등은 변경 없음) ...
            try
            {
                // 1. 서비스 생성 (DB 접속 등 무거운 작업 없는 것들)
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var settingsManager = new SettingsManager(Path.Combine(baseDir, "Settings.ini"));
                var logManager = new LogManager(baseDir);
                var databaseManager = new DatabaseManager(logManager);

                // 2. 서비스 프로바이더 생성 및 등록
                var serviceProvider = new AppServiceProvider();
                serviceProvider.Register<ILogger>(logManager);
                serviceProvider.Register<SettingsManager>(settingsManager);
                serviceProvider.Register<DatabaseManager>(databaseManager);

                // 3. 나머지 서비스 생성 및 등록 (생성자 자체는 가벼움)
                var pluginManager = new PluginManager(serviceProvider);
                serviceProvider.Register<PluginManager>(pluginManager);
                var fileWatcherService = new FileWatcherService(logManager);
                serviceProvider.Register<FileWatcherService>(fileWatcherService);
                var performanceMonitor = new PerformanceMonitor(logManager);
                serviceProvider.Register<PerformanceMonitor>(performanceMonitor);
                
                // ★★★★★★★★★★★★ 수정된 부분 ★★★★★★★★★★★★
                // 여기서 TimeSyncProvider를 초기화하면 DB 접속으로 인해 화면이 늦게 뜹니다.
                // 이 로직을 MainForm으로 이동시킵니다.
                // TimeSyncProvider.Instance.Initialize(serviceProvider); // <-- 이 줄 삭제
                // ★★★★★★★★★★★★★★★★★★★★★★★★★★
                
                // 4. 메인 폼 실행
                var mainForm = new MainForm(serviceProvider);
                Application.Run(mainForm);
            }
            // ... (catch, finally 등 하단 내용은 변경 없음) ...
        }
        // ... (나머지 메서드들은 변경 없음) ...
    }
}
