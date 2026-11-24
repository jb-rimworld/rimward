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
