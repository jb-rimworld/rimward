// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
using HarmonyLib;
using RimWard.Utilities;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimWard.Patches
{
    public class Billing_Feed_Patch
    {
        [HarmonyPatch(typeof(JobDriver_FoodFeedPatient), "MakeNewToils")]
        public class BillPatientsForFood
        {
            // Deliveree = job target B, Food = job target A (avoids touching protected JobDriver properties).
            [HarmonyPostfix]
            public static void Postfix(JobDriver_FoodFeedPatient __instance)
            {
                __instance.AddFinishAction(delegate (JobCondition x)
                {
                    Pawn deliveree = __instance.job.GetTarget(TargetIndex.B).Thing as Pawn;
                    Thing food = __instance.job.GetTarget(TargetIndex.A).Thing;
                    // billed only when the meal was actually fed (the job succeeded)
                    if (x == JobCondition.Succeeded && deliveree != null && food != null && deliveree.IsPatient(out HospitalMapComponent hospital, false)
                        && hospital.GetPatientData(deliveree, out PatientData data))
                    {
                        data.AddLine(InvoiceLineKind.Food, food.def.label, Mathf.Clamp(food.MarketValue * 1.1f, 0f, 70f));
                    }
                });
            }
        }
    }
}
