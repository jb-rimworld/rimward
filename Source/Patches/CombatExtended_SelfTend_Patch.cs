// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Copyright (C) 2026 juliusberger00. Written for RimWard 2.0 (a GPL-3.0 fork of Hospital by Adamas and
// Hospital Fix And Tweak by assssssqwww). Licensed under the GNU General Public License version 3 (LICENSE);
// full source: https://github.com/jb-rimworld/rimward
using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWard.Utilities;
using Verse;
using Verse.AI;

namespace RimWard.Patches
{
    /// <summary>
    /// Combat Extended (soft, only when loaded): CE gives non-player pawns a tactical "tend self" job
    /// (CombatExtended.AI.CompTend.TryGiveTacticalJob, job def TendSelf). Patients are treated by your doctors, so for them it returns no job.
    /// Verified against the CE source in the local mod cache (workshop 1631756268); without CE, Prepare() skips this patch.
    /// The vanilla-side guard (CanSelfTendNow_Patch on JobDriver_TendPatient, which CE's TendSelf also uses) stays as the second line.
    /// </summary>
    [HarmonyPatch]
    [JBFramework.KitCompat(HospitalMod.CombatExtendedId)]
    public static class CombatExtended_SelfTend_Patch
    {
        private static MethodInfo target;
        private static MethodInfo selPawnGetter;

        public static bool Prepare()
        {
            Type t = AccessTools.TypeByName("CombatExtended.AI.CompTend");
            if (t == null) return false;
            target = AccessTools.Method(t, "TryGiveTacticalJob");
            selPawnGetter = AccessTools.PropertyGetter(t, "SelPawn");
            return target != null && selPawnGetter != null;
        }

        public static MethodBase TargetMethod() => target;

        /// <summary>
        /// True when the prefix really sits on CE's method. Feeds the Compatibility entry (HealthyWhen): a CE version without
        /// CompTend.TryGiveTacticalJob shows "installed, but the integration reports a problem" instead of claiming the block works.
        /// // FW-REQUEST (17): the framework should report skipped [KitCompat] classes itself.
        /// </summary>
        public static bool Applied
        {
            get
            {
                if (target == null && !Prepare()) return false;
                HarmonyLib.Patches info = Harmony.GetPatchInfo(target);
                MethodInfo ours = AccessTools.Method(typeof(CombatExtended_SelfTend_Patch), nameof(Prefix));
                return info != null && info.Prefixes.Any(p => p.PatchMethod == ours);
            }
        }

        public static bool Prefix(object __instance, ref Job __result)
        {
            Pawn pawn = null;
            try { pawn = selPawnGetter?.Invoke(__instance, null) as Pawn; } catch (Exception) { }
            if (pawn != null && RimWardAreas.IsOn(FeatureArea.CombatExtended) && pawn.IsPatient(out _, false))
            {
                __result = null;
                return false;
            }
            return true;
        }
    }
}
