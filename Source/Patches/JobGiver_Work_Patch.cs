using HarmonyLib;
using RimWard.Utilities;
using RimWorld;
using Verse;

namespace RimWard.Patches
{
    public class JobGiver_Work_Patch
    {
        [HarmonyPatch(typeof(JobGiver_Work), "PawnCanUseWorkGiver")]
        public class PawnCanUseWorkGiver
        {
            [HarmonyPrefix]
            public static bool Prefix(ref bool __result, Pawn pawn, WorkGiver giver)
            {
                if (!pawn.IsPatient(out _, false)) return true;
                if (!IsPatientWork(giver.def.workType))
                {
                    __result = false;
                    return false;
                }
                __result = true;
                return false;
            }

            private static bool IsPatientWork(WorkTypeDef workTypeDef)
            {
                return workTypeDef.defName.EqualsIgnoreCase("Patient") || workTypeDef.defName.EqualsIgnoreCase("PatientBedRest");
            }
        }
    }
}
