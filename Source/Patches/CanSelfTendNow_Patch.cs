using HarmonyLib;
using RimWard.Utilities;
using RimWorld;
using Verse.AI;

namespace RimWard.Patches
{
    public class CanSelfTendNow_Patch
    {
        [HarmonyPatch(typeof(JobDriver_TendPatient), "TryMakePreToilReservations")]
        public class Satisfied
        {
            // Block a hospital patient from self-tending (deliveree == the doctor pawn).
            [HarmonyPrefix]
            public static bool Prefix(ref bool __result, JobDriver_TendPatient __instance)
            {
                if (__instance.pawn.IsPatient(out _, false)
                    && (__instance.job.GetTarget(TargetIndex.A).Thing == __instance.pawn || __instance.job.GetTarget(TargetIndex.B).Thing == __instance.pawn))
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }
    }
}
