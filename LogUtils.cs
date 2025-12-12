using UnityEngine;

namespace TeleportSuitMod
{
    /// <summary>
    /// 日志工具类（支持日志级别控制）
    /// </summary>
    public static class LogUtils
    {
        /// <summary>
        /// 日志级别枚举
        /// </summary>
        public enum LogLevel
        {
            None = 0,       // 关闭所有日志
            Error = 1,      // 仅错误日志
            Warning = 2,    // 错误+警告日志
            Debug = 3       // 所有日志（错误+警告+调试）
        }

        // 全局日志级别（默认显示所有日志）
        public static LogLevel CurrentLogLevel = LogLevel.Error;

        // 模块名称常量（可选：统一日志前缀）
        private const string GlobalPrefix = "[火箭舱限制]";

        /// <summary>
        /// 调试日志（仅在Debug级别下显示）
        /// </summary>
        public static void LogDebug(string module, string msg)
        {
            if (CurrentLogLevel >= LogLevel.Debug)
                Debug.Log($"{GlobalPrefix}[{module}] [调试] {msg}");
        }

        /// <summary>
        /// 警告日志（仅在Warning及以上级别下显示）
        /// </summary>
        public static void LogWarning(string module, string msg)
        {
            if (CurrentLogLevel >= LogLevel.Warning)
                Debug.LogWarning($"{GlobalPrefix}[{module}] [警告] {msg}");
        }

        /// <summary>
        /// 错误日志（仅在Error及以上级别下显示）
        /// </summary>
        public static void LogError(string module, string msg)
        {
            if (CurrentLogLevel >= LogLevel.Error)
                Debug.LogError($"{GlobalPrefix}[{module}] [错误] {msg}");
        }

        /// <summary>
        /// 快速设置日志级别（便捷方法）
        /// </summary>
        /// <param name="level">目标级别</param>
        public static void SetLogLevel(LogLevel level)
        {
            CurrentLogLevel = level;
            LogDebug("日志工具", $"日志级别已切换为：{level}");
        }

        /// <summary>
        /// 关闭所有日志（快捷方法）
        /// </summary>
        public static void DisableAllLogs()
        {
            SetLogLevel(LogLevel.None);
        }

        /// <summary>
        /// 开启所有日志（快捷方法）
        /// </summary>
        public static void EnableAllLogs()
        {
            SetLogLevel(LogLevel.Debug);
        }
    }
}