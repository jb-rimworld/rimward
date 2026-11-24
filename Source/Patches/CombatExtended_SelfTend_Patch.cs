using System;
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
