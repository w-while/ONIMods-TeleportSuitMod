using System;
using UnityEngine;

namespace TeleportSuitMod
{
    // 日志级别枚举
    public enum LogLevel
    {
        None = 0,      // 关闭所有日志
        Error = 1,     // 仅错误日志
        Warning = 2,   // 错误+警告日志
        Debug = 3      // 所有日志（默认）
    }

    // 日志工具类（完整版：日志级别+全局开关+时间戳+调用栈）
    public static class LogUtils
    {
        #region 全局配置
        private const string Prefix = "[TeleportSuitMod]";

        // 全局日志开关（默认开启）
        private static bool _enableAllLogs = true;

        // 当前日志级别（默认Debug）
        private static LogLevel _currentLogLevel = LogLevel.Debug;
        #endregion

        #region 配置方法
        /// <summary>
        /// 设置全局日志开关
        /// </summary>
        /// <param name="enable">是否开启所有日志</param>
        public static void EnableAllLogs(bool enable)
        {
            _enableAllLogs = enable;
            // 打印配置变更日志（不受级别限制）
            Debug.Log($"{Prefix} 全局日志开关已{(enable ? "开启" : "关闭")}");
        }

        /// <summary>
        /// 设置日志级别
        /// </summary>
        /// <param name="level">日志级别</param>
        public static void SetLogLevel(LogLevel level)
        {
            _currentLogLevel = level;
            // 打印配置变更日志（不受级别限制）
            Debug.Log($"{Prefix} 日志级别已设置为：{level}");
        }
        #endregion

        #region 核心日志方法
        /// <summary>
        /// 调试日志（带时间戳）
        /// </summary>
        public static void LogDebug(string module, string message)
        {
            // 全局开关关闭 → 直接返回
            if (!_enableAllLogs) return;

            // 日志级别不足 → 直接返回
            if (_currentLogLevel < LogLevel.Debug) return;

            string timestamp = GetFormattedTimestamp();
            Debug.Log($"{Prefix}[{timestamp}][{module}] DEBUG: {message}");
        }

        /// <summary>
        /// 错误日志（带时间戳+调用栈）
        /// </summary>
        public static void LogError(string module, string message)
        {
            // 全局开关关闭 → 仍打印错误日志（关键错误不屏蔽）
            if (!_enableAllLogs && _currentLogLevel < LogLevel.Error) return;

            string timestamp = GetFormattedTimestamp();
            string stackTrace = Environment.StackTrace;
            Debug.LogError($"{Prefix}[{timestamp}][{module}] ERROR: {message}\n调用栈：{stackTrace}");
        }

        /// <summary>
        /// 警告日志（带时间戳）
        /// </summary>
        public static void LogWarning(string module, string message)
        {
            // 全局开关关闭 → 直接返回
            if (!_enableAllLogs) return;

            // 日志级别不足 → 直接返回
            if (_currentLogLevel < LogLevel.Warning) return;

            string timestamp = GetFormattedTimestamp();
            Debug.LogWarning($"{Prefix}[{timestamp}][{module}] WARNING: {message}");
        }
        #endregion

        #region 辅助方法
        /// <summary>
        /// 获取格式化时间戳（HH:mm:ss.fff）
        /// </summary>
        private static string GetFormattedTimestamp()
        {
            float time = UnityEngine.Time.realtimeSinceStartup;
            int hours = (int)(time / 3600) % 24;
            int minutes = (int)(time / 60) % 60;
            int seconds = (int)time % 60;
            int milliseconds = (int)((time - (int)time) * 1000);
            return $"{hours:D2}:{minutes:D2}:{seconds:D2}.{milliseconds:D3}";
        }

        /// <summary>
        /// 打印对象所有字段值（调试用）
        /// </summary>
        public static void LogObjectFields(string module, object obj)
        {
            if (!_enableAllLogs || _currentLogLevel < LogLevel.Debug) return;

            if (obj == null)
            {
                LogWarning(module, "LogObjectFields - obj为null");
                return;
            }

            LogDebug(module, $"===== 打印对象字段 [{obj.GetType().FullName}] =====");
            var fields = obj.GetType().GetFields(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance
            );

            foreach (var field in fields)
            {
                try
                {
                    object value = field.GetValue(obj);
                    LogDebug(module, $"{field.Name} ({field.FieldType.Name}): {value ?? "null"}");
                }
                catch (Exception e)
                {
                    LogWarning(module, $"获取字段{field.Name}失败：{e.Message}");
                }
            }
            LogDebug(module, "===== 打印对象字段结束 =====");
        }

        /// <summary>
        /// 强制打印日志（不受级别/开关限制，用于紧急调试）
        /// </summary>
        public static void ForceLog(string module, string message)
        {
            string timestamp = GetFormattedTimestamp();
            Debug.Log($"{Prefix}[{timestamp}][{module}] FORCE: {message}");
        }
        #endregion
    }
}