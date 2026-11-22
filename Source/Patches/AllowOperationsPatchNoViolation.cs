using HarmonyLib;
using RimWard.Utilities;
using RimWorld;
using Verse;

namespace RimWard.Patches
{
    public class AllowOperationsPatchNoViolation
    {
        [HarmonyPatch(typeof(RecipeWorker), "IsViolationOnPawn")]
        public class AllowOperationsOnPatientNoViolation
        {
            [HarmonyPrefix]
            public static bool Prefix(ref bool __result, Pawn pawn)
            {
                if (!pawn.IsPatient(out _, false)) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(Recipe_AdministerIngestible), "IsViolationOnPawn")]
        public class AllowOperationsOnPatientNoViolation_Recipe_AdministerIngestible
        {
            [HarmonyPrefix]
            public static bool Prefix(ref bool __result, Pawn pawn)
            {
                if (!pawn.IsPatient(out _, false)) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(Recipe_InstallArtificialBodyPart), "IsViolationOnPawn")]
        public class AllowOperationsOnPatientNoViolation_Recipe_InstallArtificialBodyPart
        {
            [HarmonyPrefix]
            public static bool Prefix(ref bool __result, Pawn pawn)
            {
                if (!pawn.IsPatient(out _, false)) return true;
                __result = false;
                return false;
            }
        }

        // #17 fix: Recipe_RemoveBodyPart overrides IsViolationOnPawn independently, so amputations done as
        // surgery prep would otherwise anger factions. Treat as no-violation for our patients.
        [HarmonyPatch(typeof(Recipe_RemoveBodyPart), "IsViolationOnPawn")]
        public class AllowOperationsOnPatientNoViolation_Recipe_RemoveBodyPart
        {
            [HarmonyPrefix]
            public static bool Prefix(ref bool __result, Pawn pawn)
            {
                if (!pawn.IsPatient(out _, false)) return true;
                __result = false;
                return false;
            }
        }
    }
}
