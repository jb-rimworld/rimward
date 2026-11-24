// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
using HarmonyLib;
using RimWard.Utilities;
using RimWorld;
using Verse;

namespace RimWard.Patches
{
    /// <summary>
    /// The treatment a patient came for (their cure recipe, including the Luciferium critical case dose) is never a violation:
    /// the patient asked for it. Every other operation keeps vanilla's verdict, so harvesting a patient's organ angers their faction
    /// exactly as on any other guest (and is not billed: <see cref="Billing_Surgery_Patch"/>). Vanilla already treats removing a
    /// damaged part and installing a replacement as no violation.
    /// One patch per class: the four classes override IsViolationOnPawn independently.
    /// </summary>
    public class AllowOperationsPatchNoViolation
    {
        /// <summary>True when <paramref name="recipe"/> is the treatment <paramref name="pawn"/> came to our hospital for.</summary>
        public static bool IsRequestedTreatment(Pawn pawn, RecipeDef recipe)
        {
            if (recipe == null || !pawn.IsPatient(out HospitalMapComponent hospital, false)) return false;
            return hospital.GetPatientData(pawn, out PatientData data) && data.CureRecipe == recipe;
        }

        [HarmonyPatch(typeof(RecipeWorker), "IsViolationOnPawn")]
        public class AllowOperationsOnPatientNoViolation
        {
            [HarmonyPrefix]
            public static bool Prefix(RecipeWorker __instance, ref bool __result, Pawn pawn)
            {
                if (!IsRequestedTreatment(pawn, __instance?.recipe)) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(Recipe_AdministerIngestible), "IsViolationOnPawn")]
        public class AllowOperationsOnPatientNoViolation_Recipe_AdministerIngestible
        {
            [HarmonyPrefix]
            public static bool Prefix(RecipeWorker __instance, ref bool __result, Pawn pawn)
            {
                if (!IsRequestedTreatment(pawn, __instance?.recipe)) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(Recipe_InstallArtificialBodyPart), "IsViolationOnPawn")]
        public class AllowOperationsOnPatientNoViolation_Recipe_InstallArtificialBodyPart
        {
            [HarmonyPrefix]
            public static bool Prefix(RecipeWorker __instance, ref bool __result, Pawn pawn)
            {
                if (!IsRequestedTreatment(pawn, __instance?.recipe)) return true;
                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(Recipe_RemoveBodyPart), "IsViolationOnPawn")]
        public class AllowOperationsOnPatientNoViolation_Recipe_RemoveBodyPart
        {
            [HarmonyPrefix]
            public static bool Prefix(RecipeWorker __instance, ref bool __result, Pawn pawn)
            {
                if (!IsRequestedTreatment(pawn, __instance?.recipe)) return true;
                __result = false;
                return false;
            }
        }
    }
}
