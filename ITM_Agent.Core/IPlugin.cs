// ITM_Agent.Core/IPlugin.cs
namespace ITM_Agent.Core
{
    // IPlugin 인터페이스는 변경 없음
    public interface IPlugin
    {
        string Name { get; }
        string Version { get; }
        void Initialize(IAppServiceProvider serviceProvider); // IServiceProvider -> IAppServiceProvider로 변경
        void Start();
        void Stop();
        void Dispose();
        void Process(string path, params object[] args);
    }

    /// <summary>
    /// 플러그인에 서비스를 제공하기 위한 표준 인터페이스입니다.
    /// (System.IServiceProvider와 이름 충돌을 피하기 위해 IAppServiceProvider로 명명)
    /// </summary>
    public interface IAppServiceProvider // IServiceProvider -> IAppServiceProvider로 변경
    {
        T GetService<T>() where T : class;
    }
}
