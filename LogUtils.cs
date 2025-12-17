using System;
using UnityEngine;

namespace TeleportSuitMod
{
    using System;
    using System.Reflection;
    using UnityEngine;

    #region 日志核心工具类（支持 LogLevel 分级）
    public enum LogLevel
    {
        None = 0,    // 关闭所有日志
        Error = 1,   // 仅输出错误日志
        Warning = 2, // 输出错误 + 警告日志
        Info = 3,    // 输出错误 + 警告 + 信息日志
        Debug = 4    // 输出所有日志（默认）
    }

    public static class LogUtils
    {
        #region 全局配置（可在模组初始化时调整）
        // 模组统一前缀（区分不同模组日志）
        public static string ModPrefix { get; set; } = "[TeleportSuitMod]";
        // 全局日志级别（控制输出粒度）
        public static LogLevel GlobalLogLevel { get; set; } = LogLevel.Debug;
        // 是否在错误日志中附带调用栈（定位问题用）
        public static bool EnableErrorStackTrace { get; set; } = true;
        #endregion

        #region 核心日志方法（按级别输出）
        /// <summary>
        /// 调试日志（LogLevel.Debug 及以上才输出）
        /// </summary>
        public static void LogDebug(string module, string message)
        {
            WriteLog(LogLevel.Debug, LogType.Log, module, $"[DEBUG] {message}");
        }

        /// <summary>
        /// 信息日志（LogLevel.Info 及以上才输出）
        /// </summary>
        public static void LogInfo(string module, string message)
        {
            WriteLog(LogLevel.Info, LogType.Log, module, $"[INFO] {message}");
        }

        /// <summary>
        /// 警告日志（LogLevel.Warning 及以上才输出）
        /// </summary>
        public static void LogWarning(string module, string message)
        {
            WriteLog(LogLevel.Warning, LogType.Warning, module, $"[WARNING] {message}");
        }

        /// <summary>
        /// 错误日志（LogLevel.Error 及以上才输出）
        /// </summary>
        public static void LogError(string module, string message)
        {
            var errorMsg = $"[ERROR] {message}";
            if (EnableErrorStackTrace)
            {
                errorMsg += $"\n调用栈：{Environment.StackTrace}";
            }
            WriteLog(LogLevel.Error, LogType.Error, module, errorMsg);
        }

        /// <summary>
        /// 强制日志（无视 LogLevel，必输出）
        /// </summary>
        public static void LogForce(string module, string message)
        {
            WriteLog(LogLevel.None, LogType.Log, module, $"[FORCE] {message}", ignoreLevel: true);
        }
        #endregion

        #region 扩展日志方法（调试专用）
        /// <summary>
        /// 打印对象所有字段（仅 LogLevel.Debug 时输出）
        /// </summary>
        public static void LogObject(object obj, string module = "ObjectDebug")
        {
            if (!IsLevelAllowed(LogLevel.Debug)) return;

            if (obj == null)
            {
                LogWarning(module, "尝试打印空对象！");
                return;
            }

            LogDebug(module, $"===== 打印对象 [{obj.GetType().Name}] =====");
            var fields = obj.GetType().GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            );
            foreach (var field in fields)
            {
                try
                {
                    var value = field.GetValue(obj) ?? "null";
                    LogDebug(module, $"{field.Name} ({field.FieldType.Name}): {value}");
                }
                catch (Exception ex)
                {
                    LogWarning(module, $"获取字段 {field.Name} 失败：{ex.Message}");
                }
            }
            LogDebug(module, "===== 打印结束 =====");
        }
        #endregion

        #region 内部核心逻辑（分级过滤 + 日志写入）
        /// <summary>
        /// 检查日志级别是否允许输出
        /// </summary>
        private static bool IsLevelAllowed(LogLevel level)
        {
            return GlobalLogLevel >= level && GlobalLogLevel != LogLevel.None;
        }

        /// <summary>
        /// 统一写入日志（核心逻辑，对外隐藏）
        /// </summary>
        private static void WriteLog(LogLevel level, LogType logType, string module, string message, bool ignoreLevel = false)
        {
            // 分级过滤：非强制日志需检查级别
            if (!ignoreLevel && !IsLevelAllowed(level)) return;

            // 拼接标准化日志格式
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logContent = $"{ModPrefix}[{module}][{timestamp}] {message}";

            // 按类型输出到 Unity 控制台
            switch (logType)
            {
                case LogType.Log:
                    Debug.Log(logContent);
                    break;
                case LogType.Warning:
                    Debug.LogWarning(logContent);
                    break;
                case LogType.Error:
                    Debug.LogError(logContent);
                    break;
            }
        }

        // 复用 Unity 内置 LogType，避免重复定义
        private enum LogType
        {
            Log,
            Warning,
            Error
        }
        #endregion

        #region 快捷配置方法（简化级别设置）
        /// <summary>
        /// 快速设置全局日志级别（带日志反馈）
        /// </summary>
        public static void SetGlobalLogLevel(LogLevel level)
        {
            GlobalLogLevel = level;
            LogForce("LogConfig", $"全局日志级别已设置为：{level}");
        }
        #endregion
    }
    #endregion



}