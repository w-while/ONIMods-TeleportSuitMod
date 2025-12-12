using HarmonyLib;
using PeterHan.PLib.Actions;
using PeterHan.PLib.AVC;
using PeterHan.PLib.Buildings;
using PeterHan.PLib.Core;
using PeterHan.PLib.Database;
using PeterHan.PLib.Options;
using PeterHan.PLib.PatchManager;
using System;
using System.Reflection;
using TeleportSuitMod.PeterHan.BulkSettingsChange;
using UnityEngine;

namespace TeleportSuitMod
{
    public class TeleportSuitMod : KMod.UserMod2
    {
        static Harmony ModHarmony = null;

        public override void OnLoad(Harmony harmony)
        {
            // 1. 开发调试：开启所有日志
            LogUtils.EnableAllLogs();

            // 2. 生产环境：仅显示错误日志
            LogUtils.SetLogLevel(LogUtils.LogLevel.Debug);

            ModHarmony = harmony;
            base.OnLoad(harmony);
            PUtil.InitLibrary();

            new PPatchManager(harmony).RegisterPatchClass(typeof(TeleportSuitMod));
            new POptions().RegisterOptions(this, typeof(TeleportSuitOptions));
            new global::TeleportSuitMod.SanchozzONIMods.Lib.KAnimGroupManager().RegisterInteractAnims("anim_teleport_suit_teleporting_kanim");
            new PLocalization().Register();
            new PVersionCheck().Register(this, new SteamVersionChecker());
            BulkChangePatches.BulkChangeAction = new PActionManager().CreateAction(TeleportSuitStrings.TELEPORT_RESTRICT_TOOL.ACTION_KEY,
                TeleportSuitStrings.TELEPORT_RESTRICT_TOOL.ACTION_TITLE);


            GameObject gameObject = new GameObject(nameof(TeleportSuitWorldCountManager));
            gameObject.AddComponent<TeleportSuitWorldCountManager>();
            gameObject.SetActive(true);
        }
        [PLibMethod(RunAt.BeforeDbInit)]
        internal static void BeforeDbInit()
        {
            SanchozzONIMods.Lib.Utils.InitLocalization(typeof(TeleportSuitStrings),true);
            var icon = SpriteRegistry.GetToolIcon();
            Assets.Sprites.Add(icon.name, icon);
        }
        [PLibMethod(RunAt.AfterModsLoad)]
        internal static void AfterModsLoad()
        {
            //todo: 适配fast track
            Assembly[] lists = new Assembly[10];
            foreach (var mod in Global.Instance.modManager.mods)
            {
                if (mod.IsActive() && mod.staticID == "PeterHan.FastTrack")
                {
                    mod.loaded_mod_data.dlls.CopyTo(lists, 0);
                    break;
                }
            }
            if (lists[0] != null)
            {
                Assembly assembly = lists[0];
                if (assembly == null)
                {
                    return;
                }
                MethodInfo method1 = assembly.GetType("PeterHan.FastTrack.SensorPatches.FastGroupProber").GetMethod("IsReachable", new Type[] { typeof(int) });
                MethodInfo method2 = assembly.GetType("PeterHan.FastTrack.SensorPatches.FastGroupProber").GetMethod("IsReachable", new Type[] { typeof(int), typeof(CellOffset[]) });
                if (ModHarmony != null && method1 != null && method2 != null)
                {
                    ModHarmony.Patch(original: method1, postfix: new HarmonyMethod(typeof(TeleportSuitMod),
                        nameof(NavigationPatches.PeterHan_FastTrack_SensorPatches_IsReachable_Postfix_single)));
                    ModHarmony.Patch(original: method2, postfix: new HarmonyMethod(typeof(TeleportSuitMod),
                        nameof(NavigationPatches.PeterHan_FastTrack_SensorPatches_IsReachable_Postfix_multiple)));
                }
            }
        }
    }
}
