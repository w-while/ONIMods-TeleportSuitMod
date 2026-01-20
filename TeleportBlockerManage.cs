using UnityEngine;
using System.Collections.Generic;
using System;

namespace TeleportSuitMod
{
    #region 1. 保留接口（定义拦截契约）
    public interface ITeleportBlocker
    {
        bool ShouldBlockTeleport(Navigator navigator, int targetWorldId);
    }
    #endregion

    #region 2. 单例 Manager（继承 KMonoBehaviour，适配游戏生命周期）
    [DisallowMultipleComponent] // 禁止挂载多个实例
    public class TeleportBlockerManager : KMonoBehaviour
    {
        #region 单例核心逻辑
        // 静态实例（保证全局唯一）
        private static TeleportBlockerManager _instance;
        // 线程安全锁
        private static readonly object _lock = new object();

        // 对外暴露单例（懒加载）
        public static TeleportBlockerManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        // 查找场景中是否已有实例
                        _instance = FindObjectOfType<TeleportBlockerManager>();
                        if (_instance == null)
                        {
                            // 无实例则创建GameObject并挂载
                            GameObject managerObj = new GameObject("[TeleportBlockerManager]");
                            _instance = managerObj.AddComponent<TeleportBlockerManager>();
                            // 标记为 DontDestroyOnLoad，场景切换不销毁
                            DontDestroyOnLoad(managerObj);
                        }
                    }
                }
                return _instance;
            }
        }
        #endregion

        #region 拦截器存储（简化版：去掉资源回收）
        // 存储所有注册的拦截器（强引用，简化版）
        private readonly List<ITeleportBlocker> _registeredBlockers = new List<ITeleportBlocker>();
        #endregion

        #region KMonoBehaviour 生命周期（核心：自动初始化/注册）
        /// <summary>
        /// 游戏对象生成时调用（初始化+自动注册所有拦截器）
        /// </summary>
        protected override void OnSpawn()
        {
            base.OnSpawn();
            // 初始化：自动注册所有默认拦截器（核心：解决注册时机问题）
            RegisterDefaultBlockers();
        }

        /// <summary>
        /// 游戏对象销毁时调用（清理）
        /// </summary>
        protected override void OnCleanUp()
        {
            base.OnCleanUp();
            _registeredBlockers.Clear();
            _instance = null; // 清空单例
        }
        #endregion

        #region 拦截器管理方法（实例方法，非静态）
        /// <summary>
        /// 注册拦截器（外部调用）
        /// </summary>
        public void RegisterBlocker(ITeleportBlocker blocker)
        {
            if (blocker == null || _registeredBlockers.Contains(blocker))
                return;
            _registeredBlockers.Add(blocker);
        }

        /// <summary>
        /// 移除拦截器（外部调用）
        /// </summary>
        public void UnregisterBlocker(ITeleportBlocker blocker)
        {
            if (blocker == null)
                return;
            _registeredBlockers.Remove(blocker);
        }

        /// <summary>
        /// 检查是否触发拦截（核心调用方法）
        /// </summary>
        public bool IsTeleportBlocked(Navigator navigator, int targetWorldId)
        {
            if (navigator == null || navigator.gameObject == null)
                return true;

            // 遍历所有拦截器，只要有一个触发就返回true
            foreach (var blocker in _registeredBlockers)
            {
                if (blocker.ShouldBlockTeleport(navigator, targetWorldId))
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region 内部方法：自动注册默认拦截器（解决注册时机）
        private void RegisterDefaultBlockers()
        {
            // 自动注册所有默认拦截器，无需外部手动调用
            RegisterBlocker(RocketCabinRestriction.Instance);
            RegisterBlocker(new WalkBouldsTeleportBlocker());
        }
        #endregion
    }
    #endregion


}