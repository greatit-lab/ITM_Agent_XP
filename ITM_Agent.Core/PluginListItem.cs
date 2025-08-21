// ITM_Agent.Core/PluginListItem.cs
namespace ITM_Agent.Core
{
    /// <summary>
    /// 로드된 플러그인의 메타데이터를 저장하는 데이터 클래스입니다.
    /// </summary>
    public class PluginListItem
    {
        /// <summary>
        /// 플러그인의 고유 이름입니다. (예: "Onto_ErrorData")
        /// </summary>
        public string PluginName { get; set; }

        /// <summary>
        /// 플러그인 DLL 파일의 전체 경로입니다.
        /// </summary>
        public string AssemblyPath { get; set; }

        /// <summary>
        /// 플러그인의 버전입니다. (예: "2.0.0")
        /// </summary>
        public string PluginVersion { get; set; }

        /// <summary>
        /// UI 목록 등에 표시될 때 사용될 문자열을 반환합니다.
        /// </summary>
        public override string ToString()
        {
            if (string.IsNullOrEmpty(PluginVersion))
            {
                return PluginName;
            }
            return $"{PluginName} (v{PluginVersion})";
        }
    }
}
