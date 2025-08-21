// ITM_Agent/Program.cs
using ITM_Agent.Core;
using ITM_Agent.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace ITM_Agent
{
    internal static class Program
    {
        private static Mutex _mutex = null;
        private const string AppGuid = "c0a76b5a-12ab-45c5-b9d9-d693faa6e7b9"; // 고유 ID

        [STAThread]
        static void Main()
        {
            // 중복 실행 방지
            _mutex = new Mutex(true, AppGuid, out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show("ITM Agent가 이미 실행 중입니다.", "실행 확인", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // OS 언어에 따른 문화권 설정
            SetCulture();

            // 'Library' 폴더의 DLL을 동적으로 참조하기 위한 설정
            AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolveHandler;

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
            catch (Exception ex)
            {
                // 초기화 과정에서 발생하는 모든 예외를 처리
                MessageBox.Show($"프로그램 초기화 중 심각한 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _mutex?.ReleaseMutex();
            }
        }

        private static void SetCulture()
        {
            var uiCulture = CultureInfo.CurrentUICulture;
            if (!uiCulture.Name.StartsWith("ko", StringComparison.OrdinalIgnoreCase))
            {
                Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
            }
        }

        private static Assembly AssemblyResolveHandler(object sender, ResolveEventArgs args)
        {
            string assemblyName = new AssemblyName(args.Name).Name + ".dll";
            string libraryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Library", assemblyName);
            return File.Exists(libraryPath) ? Assembly.LoadFrom(libraryPath) : null;
        }
    }

    /// <summary>
    /// 간단한 서비스 프로바이더(DI 컨테이너) 구현
    /// </summary>
    public class ServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public void Register<T>(T service) where T : class
        {
            _services[typeof(T)] = service;
        }

        public T GetService<T>() where T : class
        {
            _services.TryGetValue(typeof(T), out object service);
            return service as T;
        }
    }
}
