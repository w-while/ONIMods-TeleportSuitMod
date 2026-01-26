using HarmonyLib;
using STRINGS;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static Operational;

namespace TeleportSuitMod
{
    // 太空舱模块补丁（核心逻辑 + 完整日志）
    [HarmonyPatch]
    public static class PassengerModulePatches
    {
        #region 拦截ToggleCrewRequestState（召集/通行状态切换）
        [HarmonyPatch(typeof(SummonCrewSideScreen), "ToggleCrewRequestState", MethodType.Normal)]
        public static class SummonCrewSideScreen_ToggleCrewRequestState_Patch
        {
            private const string ModuleName = "SummonCrewSideScreen_ToggleCrewRequestState_Patch";
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
                        sycMinionStat(passengerModule, isSummoning);
                        // 同步舱召集状态到RocketCabinRestriction
                        var worldContainer = passengerModule.GetComponent<ClustercraftExteriorDoor>().GetTargetWorld();
                        if (worldContainer != null)
                        {
                            int cabinWorldId = worldContainer.id;
                            if (RocketCabinRestriction.Instance != null)
                            {
                                RocketCabinRestriction.Instance.UpdateCabinSummonState(cabinWorldId, isSummoning);
                                RocketCabinRestriction.MarkCrewForCabin(passengerModule);
                            }
                        }
                    }
                }
                return true;
            }
            public static void sycMinionStat(PassengerRocketModule Cabin, bool isRestrict)
            {
                for (int i = 0; i < Components.LiveMinionIdentities.Count; i++)
                {
                    RefreshAccessStatus(Cabin, Components.LiveMinionIdentities[i], isRestrict);
                }
            }
            private static void RefreshAccessStatus(PassengerRocketModule Cabin, MinionIdentity minion, bool restrict)
            {
                Component interiorDoor = Cabin.GetComponent<ClustercraftExteriorDoor>().GetInteriorDoor();
                AccessControl component = Cabin.GetComponent<AccessControl>();
                AccessControl component2 = interiorDoor.GetComponent<AccessControl>();
                if (!restrict)
                {
                    component.SetPermission(minion.assignableProxy.Get(), AccessControl.Permission.Both);
                    component2.SetPermission(minion.assignableProxy.Get(), AccessControl.Permission.Both);
                    return;
                }
                if (Game.Instance.assignmentManager.assignment_groups[Cabin.GetComponent<AssignmentGroupController>().AssignmentGroupID].HasMember(minion.assignableProxy.Get()))
                {
                    component.SetPermission(minion.assignableProxy.Get(), AccessControl.Permission.Both);
                    component2.SetPermission(minion.assignableProxy.Get(), AccessControl.Permission.Neither);
                    return;
                }
                component.SetPermission(minion.assignableProxy.Get(), AccessControl.Permission.Neither);
                component2.SetPermission(minion.assignableProxy.Get(), AccessControl.Permission.Both);
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
        //3.火箭落地是更新状态
        // ========== 核心 Patch：FinalizeLanding（触发Grounded状态的最终落地函数） ==========
        [HarmonyPatch(typeof(LaunchableRocketCluster.StatesInstance), nameof(LaunchableRocketCluster.StatesInstance.FinalizeLanding))]
        public static class LaunchableRocketCluster_FinalizeLanding_Patch
        {
            private static readonly string ModuleName = "LaunchableRocketCluster_FinalizeLanding_Patch";
            // ========== 调试开关：控制内部世界对象溯源逻辑的启用/禁用 ==========
            // 上线前设为 false，调试时设为 true
            private const bool EnableCabinTraceDebug = false;

            [HarmonyPostfix]
            public static void Postfix(LaunchableRocketCluster.StatesInstance __instance)
            {
                try
                {
                    // 仅处理当前落地的单枚火箭
                    if (__instance == null || __instance.master == null) return;

                    // 获取当前落地火箭本体
                    LaunchableRocketCluster currentRocket = __instance.master;

                    // 修正WorldContainer获取路径
                    WorldContainer rocketWorld = null;
                    RocketModuleCluster rocketModule = currentRocket.GetComponent<RocketModuleCluster>();
                    if (rocketModule != null)
                    {
                        rocketWorld = rocketModule.GetComponent<WorldContainer>() ??
                                      rocketModule.CraftInterface?.GetComponent<WorldContainer>();
                    }
                    if (rocketWorld == null)
                    {
                        ClusterTraveler traveler = currentRocket.GetComponent<ClusterTraveler>();
                        if (traveler != null)
                        {
                            rocketWorld = traveler.GetComponent<WorldContainer>();
                        }
                    }

                    if (rocketWorld == null)
                    {
                        //LogUtils.LogWarning(ModuleName, $"当前落地火箭[{currentRocket.name}]未找到WorldContainer组件");
                        return;
                    }
                    int rocketWorldId = rocketWorld.id;
                    LogUtils.LogDebug(ModuleName, $" 当前落地火箭[{currentRocket.name}] 内部世界ID：{rocketWorldId}");

                    // 启动协程处理当前火箭
                    currentRocket.StartCoroutine(UpdateCurrentRocketCabinState(currentRocket, rocketWorld));
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[{ModuleName}] FinalizeLanding Patch 异常：{e.Message}\n{e.StackTrace}");
                }
            }

            /// <summary>
            /// 核心：仅处理当前落地火箭，调用统一状态更新接口（调试模式下输出内部世界对象信息）
            /// </summary>
            private static IEnumerator UpdateCurrentRocketCabinState(LaunchableRocketCluster rocket, WorldContainer rocketWorld)
            {
                yield return new WaitForSeconds(0.5f); // 等待内部世界加载

                try
                {
                    int rocketWorldId = rocketWorld.id;

                    // ========== 调试逻辑：仅在 EnableCabinTraceDebug 为 true 时执行 ==========
                    if (EnableCabinTraceDebug)
                    {
                        List<GameObject> allWorldObjects = new List<GameObject>();

                        // 获取内部世界所有对象（用于溯源U57舱体真实命名）
                        allWorldObjects = Resources.FindObjectsOfTypeAll<GameObject>()
                            .Where(obj =>
                                Grid.IsValidCell(Grid.PosToCell(obj.transform.position)) &&
                                Grid.WorldIdx[Grid.PosToCell(obj.transform.position)] == rocketWorldId &&
                                obj.activeInHierarchy
                            ).ToList();

                        // 输出内部世界所有对象（关键：找到U57舱体真实命名）
                        LogUtils.LogDebug(ModuleName, $" 火箭[{rocket.name}] 内部世界ID[{rocketWorldId}] 所有对象：");
                        foreach (var obj in allWorldObjects)
                        {
                            Debug.Log($" - 对象名：{obj.name} | 组件：{string.Join("|", obj.GetComponents<Component>().Select(c => c.GetType().Name))}");
                        }

                        LogUtils.LogDebug(ModuleName, $" 当前落地火箭[{rocket.name}] 内部世界找到对象数量：{allWorldObjects.Count}");

                        // 无对象时的调试提示
                        if (allWorldObjects.Count == 0)
                        {
                            LogUtils.LogWarning(ModuleName, $"火箭[{rocket.name}] 内部世界ID[{rocketWorldId}] 无任何对象 | 请检查WorldContainer获取逻辑或火箭加载时机");
                        }
                    }

                    // ========== 核心业务逻辑：始终执行（不受调试开关影响） ==========
                    if (RocketCabinRestriction.Instance != null)
                    {
                        RocketCabinRestriction.Instance.UpdateCabinSummonState(rocketWorldId, false);
                        LogUtils.LogDebug(ModuleName, $" 已调用状态更新接口 | 火箭内部世界ID：{rocketWorldId} | 召集状态：false");
                    }
                    else
                    {
                        Debug.LogError($"[{ModuleName}] RocketCabinRestriction 单例未初始化，无法更新状态");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[{ModuleName}] 更新当前火箭舱体状态异常：{e.Message}\n{e.StackTrace}");
                }
            }
        }
    #endregion
}
}