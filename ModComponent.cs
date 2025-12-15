using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeleportSuitMod
{
    public abstract class ModComponent : KMonoBehaviour
    {
        // 每个组件必须定义唯一模块名（日志标识）
        protected abstract string ModuleName { get; }

        #region 封装日志调用（无需重复传 ModuleName，自动适配 LogLevel）
        protected void LogDebug(string message) => LogUtils.LogDebug(ModuleName, message);
        protected void LogInfo(string message) => LogUtils.LogInfo(ModuleName, message);
        protected void LogWarning(string message) => LogUtils.LogWarning(ModuleName, message);
        protected void LogError(string message) => LogUtils.LogError(ModuleName, message);
        protected void LogForce(string message) => LogUtils.LogForce(ModuleName, message);
        protected void LogObject(object obj) => LogUtils.LogObject(obj, ModuleName);
        #endregion
    }
    public abstract class ModReactableComponent : KMonoBehaviour
    {
        // 每个组件必须定义唯一模块名（日志标识）
        protected abstract string ModuleName { get; }

        #region 封装日志调用（无需重复传 ModuleName，自动适配 LogLevel）
        protected void LogDebug(string message) => LogUtils.LogDebug(ModuleName, message);
        protected void LogInfo(string message) => LogUtils.LogInfo(ModuleName, message);
        protected void LogWarning(string message) => LogUtils.LogWarning(ModuleName, message);
        protected void LogError(string message) => LogUtils.LogError(ModuleName, message);
        protected void LogForce(string message) => LogUtils.LogForce(ModuleName, message);
        protected void LogObject(object obj) => LogUtils.LogObject(obj, ModuleName);
        #endregion
    }
}
