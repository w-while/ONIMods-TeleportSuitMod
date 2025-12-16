using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using STRINGS;

namespace TeleportSuitMod
{
    // 太空舱模块补丁（核心逻辑 + 完整日志）
    [HarmonyPatch]
    public static class PassengerModulePatches
    {
        #region 全局日志开关（方便调试）
        private const bool EnableDebugLog = true;
        #endregion

        #region 拦截ToggleCrewRequestState（召集/通行状态切换）
        [HarmonyPatch(typeof(SummonCrewSideScreen), "ToggleCrewRequestState", MethodType.Normal)]
        public static class SummonCrewSideScreen_ToggleCrewRequestState_Patch
        {
            private const string ModuleName = "ToggleCrewRequestState_Patch";
            public static bool Prefix(SummonCrewSideScreen __instance)
            {
                // 反射获取私有字段craftModuleInterface
                var craftModuleInterfaceField = typeof(SummonCrewSideScreen).GetField(
                    "craftModuleInterface",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );
                PassengerRocketModule passengerModule;
                if (craftModuleInterfaceField != null)
                {
                    CraftModuleInterface craftModuleInterface = (CraftModuleInterface)craftModuleInterfaceField.GetValue(__instance);
                    if (craftModuleInterface != null)
                    {
                        passengerModule = craftModuleInterface.GetPassengerModule();
                        bool isSummoning = false;

                        if (passengerModule.PassengersRequested == PassengerRocketModule.RequestCrewState.Release)
                        {
                            isSummoning = true;
                        }
                        // 同步舱召集状态到RocketCabinRestriction
                        var worldContainer = passengerModule.GetComponent<ClustercraftExteriorDoor>().GetTargetWorld();
                        if (worldContainer != null)
                        {
                            int cabinWorldId = worldContainer.id;
                            if (RocketCabinRestriction.Instance != null)
                            {
                                RocketCabinRestriction.Instance.UpdateCabinSummonState(passengerModule,cabinWorldId, isSummoning);
                                RocketCabinRestriction.MarkCrewForCabin(passengerModule);
                            }
                        }
                    }
                }
                return true;
            }
        }
        #endregion

        #region 拦截ShouldCrewGetIn（用于更新乘员飞行状态）
        [HarmonyPatch(typeof(PassengerRocketModule), nameof(PassengerRocketModule.ShouldCrewGetIn))]
        public class PassengerRocketModule_ShouldCrewGetIn_Patch
        {
            private const string ModuleName = "ShouldCrewGetIn_Patch";

            public static bool Prefix(PassengerRocketModule __instance)
            {
                // 反射获取私有字段passengersRequested
                var passengersRequestedField = AccessTools.Field(typeof(PassengerRocketModule), "passengersRequested");
                if (passengersRequestedField == null) return true; // 执行原生逻辑避免崩溃

                PassengerRocketModule.RequestCrewState passengersRequested;
                try
                {
                    passengersRequested = (PassengerRocketModule.RequestCrewState)passengersRequestedField.GetValue(__instance);
                }
                catch (Exception e)
                {
                    LogUtils.LogError(ModuleName, $"获取passengersRequested失败：{e.Message}");
                    return true;
                }

                // 获取CraftModuleInterface
                CraftModuleInterface craftInterface = null;
                try
                {
                    var rocketModuleCluster = __instance.GetComponent<RocketModuleCluster>();
                    if (rocketModuleCluster != null)
                    {
                        craftInterface = rocketModuleCluster.CraftInterface;
                    }
                }
                catch (Exception e)
                {
                    LogUtils.LogError(ModuleName, $"获取CraftInterface失败：{e.Message}");
                }

                bool isLaunchRequested = false;
                bool isPreppedForLaunch = false;
                if (craftInterface != null)
                {
                    try
                    {
                        isLaunchRequested = craftInterface.IsLaunchRequested();
                        isPreppedForLaunch = craftInterface.CheckPreppedForLaunch();
                    }
                    catch (Exception e)
                    {
                        LogUtils.LogError(ModuleName, $"调用CraftInterface方法失败：{e.Message}");
                    }
                }

                bool isrequested = passengersRequested == PassengerRocketModule.RequestCrewState.Request || (isLaunchRequested && isPreppedForLaunch);

                // 标记舱内船员
                if (isrequested)
                {
                    if (RocketCabinRestriction.Instance != null)
                        try
                        {
                            RocketCabinRestriction.MarkCrewForCabin(__instance);
                        }
                        catch (Exception e)
                        {
                            LogUtils.LogError(ModuleName, $"调用MarkCrewForCabin失败：{e.Message}\n{e.StackTrace}");
                        }
                }

                return true; 
            }
        }
        #endregion

        #region TODO: 后续实现 - 火箭发射/舱销毁状态重置
        // TODO: 1. 火箭发射时重置舱召集状态（CraftModuleInterface.RequestLaunch）
        // TODO: 2. 舱销毁时重置状态（PassengerRocketModule.OnCleanUp）
        #endregion
    }
}