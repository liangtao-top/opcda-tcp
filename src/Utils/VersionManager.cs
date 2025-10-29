using System;
using System.Reflection;

namespace OpcDAToMSA.Utils
{
    /// <summary>
    /// 全局版本管理器
    /// 统一管理应用程序版本信息，确保版本号一致性
    /// </summary>
    public static class VersionManager
    {
        #region 版本常量

        /// <summary>
        /// 主版本号
        /// </summary>
        public const int MAJOR_VERSION = 2;

        /// <summary>
        /// 次版本号
        /// </summary>
        public const int MINOR_VERSION = 1;

        /// <summary>
        /// 修订版本号
        /// </summary>
        public const int PATCH_VERSION = 0;

        /// <summary>
        /// 构建版本号
        /// </summary>
        public const int BUILD_VERSION = 0;

        /// <summary>
        /// 版本标识符（如：alpha, beta, rc, release）
        /// </summary>
        public const string VERSION_SUFFIX = "";

        #endregion

        #region 版本属性

        /// <summary>
        /// 完整版本号 (如: 2.1.0.0)
        /// </summary>
        public static string FullVersion => $"{MAJOR_VERSION}.{MINOR_VERSION}.{PATCH_VERSION}.{BUILD_VERSION}";

        /// <summary>
        /// 语义化版本号 (如: 2.1.0)
        /// </summary>
        public static string SemanticVersion => $"{MAJOR_VERSION}.{MINOR_VERSION}.{PATCH_VERSION}";

        /// <summary>
        /// 带标识符的版本号 (如: 2.1.0-beta)
        /// </summary>
        public static string VersionWithSuffix => string.IsNullOrEmpty(VERSION_SUFFIX) 
            ? SemanticVersion 
            : $"{SemanticVersion}-{VERSION_SUFFIX}";

        /// <summary>
        /// 显示版本号 (如: v2.1.0)
        /// </summary>
        public static string DisplayVersion => $"v{SemanticVersion}";

        /// <summary>
        /// 应用程序标题
        /// </summary>
        public const string APPLICATION_TITLE = "OPC DA 企业级数据网关";

        /// <summary>
        /// 带版本的应用程序标题
        /// </summary>
        public static string ApplicationTitleWithVersion => $"{APPLICATION_TITLE} {DisplayVersion}";

        #endregion

        #region 动态标题方法

        /// <summary>
        /// 生成应用程序标题
        /// </summary>
        /// <returns>应用程序标题</returns>
        public static string GenerateTitle()
        {
            return ApplicationTitleWithVersion;
        }

        /// <summary>
        /// 生成欢迎信息
        /// </summary>
        /// <returns>欢迎信息</returns>
        public static string GenerateWelcomeMessage()
        {
            return $"🚀 Welcome {ApplicationTitleWithVersion}";
        }

        /// <summary>
        /// 生成关于窗口标题
        /// </summary>
        /// <returns>关于窗口标题</returns>
        public static string GenerateAboutTitle()
        {
            return $"关于 {APPLICATION_TITLE}";
        }

        #endregion

        #region 程序集版本信息

        /// <summary>
        /// 获取程序集版本
        /// </summary>
        /// <returns>程序集版本</returns>
        public static string GetAssemblyVersion()
        {
            try
            {
                return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? FullVersion;
            }
            catch
            {
                return FullVersion;
            }
        }

        /// <summary>
        /// 获取程序集信息版本
        /// </summary>
        /// <returns>信息版本</returns>
        public static string GetAssemblyInformationalVersion()
        {
            try
            {
                var attribute = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                return attribute?.InformationalVersion ?? VersionWithSuffix;
            }
            catch
            {
                return VersionWithSuffix;
            }
        }

        #endregion

        #region 版本比较

        /// <summary>
        /// 比较版本号
        /// </summary>
        /// <param name="version1">版本1</param>
        /// <param name="version2">版本2</param>
        /// <returns>比较结果：-1(小于), 0(等于), 1(大于)</returns>
        public static int CompareVersions(string version1, string version2)
        {
            try
            {
                var v1 = new Version(version1);
                var v2 = new Version(version2);
                return v1.CompareTo(v2);
            }
            catch
            {
                return string.Compare(version1, version2, StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// 检查是否为更新版本
        /// </summary>
        /// <param name="currentVersion">当前版本</param>
        /// <param name="newVersion">新版本</param>
        /// <returns>是否为新版本</returns>
        public static bool IsNewerVersion(string currentVersion, string newVersion)
        {
            return CompareVersions(newVersion, currentVersion) > 0;
        }

        #endregion
    }
}
