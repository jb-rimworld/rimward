using HarmonyLib;
using RimWard.Utilities;
using RimWorld;
using Verse;

namespace RimWard.Patches
{
    // Parity outcome hooks. Step 4 reworks these into the fault-classified DeathLiability model.
    public class PatientOutcome_Patches
    {
        [HarmonyPatch(typeof(Pawn), "Kill")]
        public static class Kill
        {
            [HarmonyPrefix]
            public static void Prefix(Pawn __instance)
            {
                if (__instance.IsPatient(out HospitalMapComponent hospital, false))
                {
                    hospital.PatientDied(__instance);
                }
            }
        }

        // A botched operation is detected in Billing_Surgery_Patch.SurgeryFail (Recipe_Surgery.CheckSurgeryFail), for every outcome.

        [HarmonyPatch(typeof(Faction), "Notify_MemberExitedMap")]
        public static class Notify_MemberExitedMap
        {
            [HarmonyPrefix]
            public static bool Prefix(Pawn member, bool freed)
            {
                HospitalMapComponent hospital = HospitalMapComponent.For(member?.Map);
                if (hospital != null && hospital.GetPatientData(member, out _))
                {
                    hospital.PatientLeftTheMap(member);
                    return false;
                }
                return true;
            }
        }
    }
}
