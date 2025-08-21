// ITM_Agent.Core/PluginBase.cs
using System;
using System.Reflection;

namespace ITM_Agent.Core
{
    public abstract class PluginBase : IPlugin, IDisposable
    {
        protected IAppServiceProvider ServiceProvider { get; private set; } // IServiceProvider -> IAppServiceProvider
        protected ILogger Logger { get; private set; }

        public virtual string Name => Assembly.GetExecutingAssembly().GetName().Name;
        public virtual string Version => Assembly.GetExecutingAssembly().GetName().Version.ToString();

        // IPlugin 인터페이스와 시그니처를 정확히 일치시키고, virtual로 선언하여 재정의 가능하게 함
        public virtual void Initialize(IAppServiceProvider serviceProvider) // IServiceProvider -> IAppServiceProvider
        {
            this.ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            this.Logger = this.ServiceProvider.GetService<ILogger>();

            if (this.Logger == null)
            {
                throw new InvalidOperationException("ILogger 서비스를 찾을 수 없습니다.");
            }
        }

        public virtual void Start() { }
        public virtual void Stop() { }
        public abstract void Process(string path, params object[] args);
        public virtual void Dispose() { }
    }
}
