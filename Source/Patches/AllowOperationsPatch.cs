using HarmonyLib;
using RimWard.Utilities;
using RimWorld;
using Verse;

namespace RimWard.Patches
{
    public class AllowOperationsPatch
    {
        [HarmonyPatch(typeof(HealthCardUtility), "DrawHealthSummary")]
        public class AllowOperationsOnPatients
        {
            [HarmonyPrefix]
            public static void Prefix(Pawn pawn, ref bool allowOperations)
            {
                if (pawn.IsPatient(out _, false))
                {
                    allowOperations = true;
                }
            }
        }
    }
}
