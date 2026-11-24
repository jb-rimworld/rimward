using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimWard
{
    [StaticConstructorOnStartup]
    public class HospitalMod : Mod
    {
        public static Harmony harmonyInstance;
        public static Settings Settings;

        /// <summary>Harmony patch classes that failed to apply ("Type: reason"). Empty when everything is patched. Read by the diagnosis page.</summary>
        public static readonly List<string> PatchFailures = new List<string>();
        /// <summary>Number of patch classes that were applied.</summary>
        public static int PatchClassesApplied;

        public HospitalMod(ModContentPack content) : base(content)
        {
            HospitalMod.Settings = base.GetSettings<Settings>();
            HospitalMod.harmonyInstance = new Harmony("juliusberger.rimward");
            PatchPerClass();
        }

        /// <summary>
        /// One patch class at a time: a game or mod update that breaks one patch costs that feature only, not all of RimWard.
        /// Each failure is logged once and kept in <see cref="PatchFailures"/>.
        /// </summary>
        private static void PatchPerClass()
        {
            foreach (Type t in AccessTools.GetTypesFromAssembly(typeof(HospitalMod).Assembly))
            {
                if (t == null) continue;
                object[] attrs;
                try { attrs = t.GetCustomAttributes(typeof(HarmonyPatch), false); }
                catch (Exception) { continue; }
                if (attrs == null || attrs.Length == 0) continue;
                try
                {
                    harmonyInstance.CreateClassProcessor(t).Patch();
                    PatchClassesApplied++;
                }
                catch (Exception e)
                {
                    string reason = (e.InnerException ?? e).Message;
                    PatchFailures.Add(t.FullName + ": " + reason);
                    Log.Warning("[RimWard] Patch " + t.FullName + " could not be applied, that feature is off: " + reason);
                }
            }
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            HospitalMod.Settings.DoWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "RimWard";
        }
    }
}
