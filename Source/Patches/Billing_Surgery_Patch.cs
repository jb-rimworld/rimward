using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWard.Utilities;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard.Patches
{
    public class Billing_Surgery_Patch
    {
        [HarmonyPatch(typeof(Recipe_InstallArtificialBodyPart), "ApplyOnPawn")]
        public class BillPatientsForArtificialTransplant
        {
            [HarmonyPostfix]
            public static void Postfix(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
            {
                PatientUtility.RecordTreatment(pawn, billDoer, ingredients?.FirstOrDefault(t => t is Medicine));
                BillingHelper.BillForSurgery(pawn, bill);
            }
        }

        [HarmonyPatch(typeof(Recipe_InstallNaturalBodyPart), "ApplyOnPawn")]
        public class BillPatientsForNaturalTransplant
        {
            [HarmonyPostfix]
            public static void Postfix(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
            {
                PatientUtility.RecordTreatment(pawn, billDoer, ingredients?.FirstOrDefault(t => t is Medicine));
                BillingHelper.BillForSurgery(pawn, bill);
            }
        }

        [HarmonyPatch(typeof(Recipe_AdministerIngestible), "ApplyOnPawn")]
        public class BillPatientsForAdministeredIngestible
        {
            [HarmonyPostfix]
            public static void Postfix(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
            {
                PatientUtility.RecordTreatment(pawn, billDoer, ingredients?.FirstOrDefault(t => t is Medicine));
                BillingHelper.BillForSurgery(pawn, bill);
            }
        }

        public static class BillingHelper
        {
            // Formula folded in from HospitalTweak (work clamp 0..400, ingredient clamp 0..16000, risk premium).
            public static void BillForSurgery(Pawn pawn, Bill bill)
            {
                if (!pawn.IsPatient(out HospitalMapComponent hospital, false) || !hospital.GetPatientData(pawn, out PatientData patientData) || bill?.recipe == null)
                {
                    return;
                }
                float value = 0f;
                if (bill.recipe.skillRequirements != null)
                {
                    SkillRequirement sr = bill.recipe.skillRequirements.Find(r => r.skill == SkillDefOf.Medicine);
                    float deathChance = bill.recipe.deathOnFailedSurgeryChance;
                    value = ((bill.recipe.workAmount + 900f) * 0.0036f) * (1f + (sr?.minLevel ?? 0f) / 4f) * (1f + 4f * deathChance);
                }
                float ingredientValue = 0f;
                if (bill.recipe.ingredients != null)
                {
                    foreach (IngredientCount item in bill.recipe.ingredients)
                    {
                        if (item.IsFixedIngredient)
                        {
                            ingredientValue += item.FixedIngredient.BaseMarketValue * item.GetBaseCount();
                        }
                        else
                        {
                            ThingDef cheapest = item.filter.AllowedThingDefs.MinBy(i => i.BaseMarketValue);
                            if (cheapest != null) ingredientValue += cheapest.BaseMarketValue * item.GetBaseCount();
                        }
                    }
                }
                float amount = Mathf.Clamp(value, 0f, 400f) + Mathf.Clamp(ingredientValue, 0f, 16000f);
                hospital.AddSurgeryBill(pawn, amount);
                patientData.HasPendingSurgeryBill = false;
            }
        }

        [HarmonyPatch(typeof(BillStack), "Delete")]
        public class ClearPendingSurgeryOnBillRemoval
        {
            // #62 fix: only clear the pending-surgery flag when the deleted bill is the actual cure bill,
            // not an incidental tend bill.
            [HarmonyPrefix]
            public static void Prefix(BillStack __instance, Bill bill)
            {
                if (bill is Bill_Medical medical && __instance.billGiver is Pawn pawn
                    && pawn.IsPatient(out HospitalMapComponent hospital, false) && hospital.GetPatientData(pawn, out PatientData patientData))
                {
                    if ((patientData.CureRecipe == null || medical.recipe == patientData.CureRecipe) && patientData.RetryAfterTick < 0)
                    {
                        patientData.HasPendingSurgeryBill = false;
                    }
                }
            }
        }
    }
}
