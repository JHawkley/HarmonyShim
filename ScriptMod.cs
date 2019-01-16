using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Reflection;
using UnityEngine;
using XRL;
using XRL.Core;
using XRL.World;
using XRL.UI;

namespace HarmonyShim
{

    /// <summary>
    /// Provides extremely basic patching capabilities utilizing Harmony.
    /// </summary>
    public static class Harmony
    {

        private static Assembly harmonyAssembly;

        private static Type harmonyInstanceType;
        private static Type harmonyMethodType;
        private static MethodInfo getPatchedMethodsMethodInfo;
        private static MethodInfo patchMethodInfo;
        private static MethodInfo unpatchMethodInfo;

        private static object harmonyInstance;

        static Harmony()
        {
            Debug.Log("[HarmonyShim] Starting initialization.");
            try
            {
                harmonyAssembly = null;
                var didInit = false;
                
                ModManager.ForEachFile("0harmony.dll", delegate (string filePath, ModInfo modInfo)
                {
                    if (!didInit)
                    {
                        harmonyAssembly = Assembly.LoadFrom(filePath);
                        didInit = true;
                    }
                });

                if (harmonyAssembly == null)
                    throw new FileNotFoundException("The Harmony library could not be loaded; it was not provided by any loaded mods.");

                harmonyInstanceType = harmonyAssembly.GetType("Harmony.HarmonyInstance");
                harmonyMethodType = harmonyAssembly.GetType("Harmony.HarmonyMethod");
                getPatchedMethodsMethodInfo = harmonyInstanceType.GetMethod("GetPatchedMethods");
                patchMethodInfo = harmonyInstanceType.GetMethod("Patch");
                unpatchMethodInfo = harmonyInstanceType.GetMethod("Unpatch", new Type[] { typeof(MethodBase), typeof(MethodInfo) });

                harmonyInstance = harmonyInstanceType.GetMethod("Create").Invoke(null, new object[] { "CoQ-Harmony-Shim" });

                // Patch to prevent/warn mod recompilation after Harmony has done its dirty work.
                ApplyPrefix(typeof(ModManager).GetMethod("BuildScriptMods"), typeof(Patch_PreventRecompilation).GetMethod("ModManager_BuildScriptMods_Prefix"));
                ApplyPrefix(typeof(XRLCore).GetMethod("RunGame"), typeof(Patch_PreventRecompilation).GetMethod("Core_RunGame_Prefix"));

                // Run the static constructors of all the types in the mod assembly.
                // I tried to do this with an attribute, but Mono didn't like it too much.  I think it is unhappy
                // with the fact the mod assembly is compiled in memory and has no disk presence.
                foreach (Type type in ModManager.modAssembly.GetTypes())
                    System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            }
            catch (Exception ex)
            {
                Debug.Log("[HarmonyShim] Exception occurred during initialization.");
                Debug.Log(ex);
            }
        }

        /// <summary>
        /// Does nothing.  Used by the `HarmonyShim_Noop` part to force static initialization of this class.
        /// </summary>
        public static void Noop() { }

        /// <summary>
        /// Patches a method, applying the given prefix, postfix, and transpiler methods as patches.
        /// </summary>
        /// <param name="original">The method to be patched.</param>
        /// <param name="prefix">The prefix method, or `null` if no prefix patch is to be applied.</param>
        /// <param name="postfix">The postfix method, or `null` if no postfix is to be applied.</param>
        /// <param name="transpiler">The transpiler method, or `null` if no transpiler is to be applied.</param>
        public static void Patch(MethodBase original, MethodInfo prefix = null, MethodInfo postfix = null, MethodInfo transpiler = null)
        {
            var hPrefix = prefix != null ? Activator.CreateInstance(harmonyMethodType, prefix) : null;
            var hPostfix = postfix != null ? Activator.CreateInstance(harmonyMethodType, postfix) : null;
            var hTranspiler = transpiler != null ? Activator.CreateInstance(harmonyMethodType, transpiler) : null;
            patchMethodInfo.Invoke(harmonyInstance, new object[] { original, hPrefix, hPostfix, hTranspiler });
        }

        /// <summary>
        /// Patches a method, applying the given method as a prefix patch.
        /// </summary>
        /// <param name="original">The method to be patched.</param>
        /// <param name="prefix">The prefix method.</param>
        public static void ApplyPrefix(MethodBase original, MethodInfo prefix)
        {
            if (prefix != null) Patch(original, prefix);
            else Debug.Log("[HarmonyShim] A null value was passed as the `prefix` argument to `ApplyPrefix`.");
        }

        /// <summary>
        /// Patches a method, applying the given method as a postfix patch.
        /// </summary>
        /// <param name="original">The method to be patched.</param>
        /// <param name="postfix">The postfix method.</param>
        public static void ApplyPostfix(MethodBase original, MethodInfo postfix)
        {
            if (postfix != null) Patch(original, null, postfix);
            else Debug.Log("[HarmonyShim] A null value was passed as the `postfix` argument to `ApplyPostfix`.");
        }

        /// <summary>
        /// Patches a method, applying the given method as a transpiler patch.
        /// </summary>
        /// <param name="original">The method to be patched.</param>
        /// <param name="transpiler">The transpiler method.</param>
        public static void ApplyTranspiler(MethodBase original, MethodInfo transpiler)
        {
            if (transpiler != null) Patch(original, null, null, transpiler);
            else Debug.Log("[HarmonyShim] A null value was passed as the `transpiler` argument to `ApplyTranspiler`.");
        }

        /// <summary>
        /// Unpatches a method when given the method that was used in its patch.
        /// </summary>
        /// <param name="original">The method to be unpatched.</param>
        /// <param name="patch">The method that was used as the patch.</param>
        public static void Unpatch(MethodBase original, MethodInfo patch)
        {
            unpatchMethodInfo.Invoke(harmonyInstance, new object[] { original, patch });
        }

        /// <summary>
        /// Gets an enumeration of all patched methods.
        /// </summary>
        /// <returns>An enumeration of all patched methods.</returns>
        public static IEnumerable<MethodBase> GetPatchedMethods()
        {
            return getPatchedMethodsMethodInfo.Invoke(harmonyInstance, new object[] { }) as IEnumerable<MethodBase>;
        }

    }

    /// <summary>
    /// Tracks if the game attempted to recompile the mod-assembly since HarmonyShim initialized, blocking those
    /// attempts to prevent undefined behavior.  If any attempt was made, a warning will be shown the next
    /// time the game-loop begins running.
    /// </summary>
    public static class Patch_PreventRecompilation
    {

        public static bool attemptedRecompile = false;

        public static bool ModManager_BuildScriptMods_Prefix()
        {
            // `ModManager.bCompiled` will be false if this is a legitimate recompilation attempt.
            if (!ModManager.bCompiled)
            {
                if (ModManager.modAssembly != null)
                {
                    // Force the game to reuse the previous mod-assembly.
                    ModManager.bCompiled = true;
                    attemptedRecompile = true;
                }
                else
                {
                    // Fail-safe, in case the game acts oddly and sets the mod-assembly to `null` somehow.
                    GameManager.Instance.Quit();
                }
            }

            return false;
        }

        public static bool Core_RunGame_Prefix()
        {
            if (!attemptedRecompile) return true;
            attemptedRecompile = false;
            var message = new string[]
            {
                "An attempt to recompile the mod-assembly was made since HarmonyShim initialized.",
                "Due to the intimate patches Harmony makes on the game, this attempt had to be blocked to prevent undefined behavior.",
                "Caves of Qud may recompile the mod-assembly unnecessarily, often when beginning a new game.  If you have adjusted your mods since Caves of Qud was launched, please restart the game; otherwise, you can disregard this warning."
            };
            Popup.Show(String.Join("\n\n", message), true);
            return true;
        }

    }

}

namespace XRL.World.Parts
{

    /// <summary>
    /// This dummy part is merely used to force the static initializer of `HarmonyShim.Harmony` to run.
    /// It is attached to all creatures, pointlessly.  I'm happy to entertain better injection schemes.
    /// </summary>
    public class HarmonyShim_Noop : IPart
    {

        public HarmonyShim_Noop()
        {
            HarmonyShim.Harmony.Noop();
        }

        public override bool AllowStaticRegistration() { return true; }

    }

}
