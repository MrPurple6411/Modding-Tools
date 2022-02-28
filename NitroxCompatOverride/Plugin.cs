namespace NitroxCompatOverride
{
    using BepInEx;
    using BepInEx.Logging;
    using HarmonyLib;
    using Mono.Cecil;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEngine;

    public static class NitroxCompatOverride 
    {
        public static ManualLogSource LogSource { get;  } = BepInEx.Logging.Logger.CreateLogSource("NitroxCompatOverride");

        [Obsolete("Should not be used!", true)]
        public static void Initialize()
        {
            LogSource.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is Created!");
            MethodInfo NitroxCompatGetter = AccessTools.PropertyGetter(AccessTools.TypeByName("QModManager.Patching.QMod"), "NitroxCompat");
            HarmonyMethod postfix = new HarmonyMethod(typeof(NitroxCompatOverride).GetMethod(nameof(Postfix), BindingFlags.NonPublic | BindingFlags.Static));
            Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.Patch(NitroxCompatGetter, postfix: postfix);
            LogSource.LogInfo($"Patching Complete");
        }


        private static void Postfix(object __instance, ref bool __result)
        {
            __result = true;
        }

        /// <summary>
        /// For BepInEx to identify your patcher as a patcher, it must match the patcher contract as outlined in the BepInEx docs:
        /// https://bepinex.github.io/bepinex_docs/v5.0/articles/dev_guide/preloader_patchers.html#patcher-contract
        /// It must contain a list of managed assemblies to patch as a public static <see cref="IEnumerable{T}"/> property named TargetDLLs
        /// </summary>
        [Obsolete("Should not be used!", true)]
        public static IEnumerable<string> TargetDLLs { get; } = new string[0];

        /// <summary>
        /// For BepInEx to identify your patcher as a patcher, it must match the patcher contract as outlined in the BepInEx docs:
        /// https://bepinex.github.io/bepinex_docs/v5.0/articles/dev_guide/preloader_patchers.html#patcher-contract
        /// It must contain a public static void method named Patch which receives an <see cref="AssemblyDefinition"/> argument,
        /// which patches each of the target assemblies in the TargetDLLs list.
        /// 
        /// We don't actually need to patch any of the managed assemblies, so we are providing an empty method here.
        /// </summary>
        /// <param name="ad"></param>
        [Obsolete("Should not be used!", true)]
        public static void Patch(AssemblyDefinition ad) { }
    }
}
