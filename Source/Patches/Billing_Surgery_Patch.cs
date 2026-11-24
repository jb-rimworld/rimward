using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWard.Utilities;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard.Patches
{
    /// <summary>
    /// Bills EVERY medical operation on a patient (any recipe worker class, vanilla or modded), on the one place all of them
    /// pass through: <c>Bill_Medical.Notify_IterationCompleted</c>, which calls <c>recipe.Worker.ApplyOnPawn</c>.
    /// A failure is detected by <c>Recipe_Surgery.CheckSurgeryFail</c> returning true inside that call:
    /// then there is no fee, only (setting) the reimbursed medicine and parts the operation used up.
    /// </summary>
    public static class Billing_Surgery_Patch
    {
        private class Context
        {
            public Pawn patient;
            public bool failed;
            public Dictionary<ThingDef, int> anesthetic;
        }

        [ThreadStatic] private static Context current;

        [HarmonyPatch(typeof(Bill_Medical), nameof(Bill_Medical.Notify_IterationCompleted))]
        public static class IterationCompleted
        {
            [HarmonyPrefix]
            public static void Prefix(Bill_Medical __instance)
            {
                current = null;
                Pawn patient = __instance?.GiverPawn;
                if (patient == null || !patient.IsPatient(out _, false)) return;
                current = new Context
                {
                    patient = patient,
                    anesthetic = __instance.consumedMedicine != null ? new Dictionary<ThingDef, int>(__instance.consumedMedicine) : null
                };
            }

            [HarmonyPostfix]
            public static void Postfix(Bill_Medical __instance, Pawn billDoer, List<Thing> ingredients)
            {
                Context ctx = current;
                current = null;
                if (ctx == null || __instance?.recipe == null) return;
                try { BillFor(ctx, __instance.recipe, billDoer, ingredients); }
                catch (Exception e) { Log.ErrorOnce("[RimWard] surgery billing failed: " + e.Message, 0x5b11ed); }
            }
        }

        [HarmonyPatch(typeof(Recipe_Surgery), "CheckSurgeryFail")]
        public static class SurgeryFail
        {
            [HarmonyPostfix]
            public static void Postfix(bool __result, Pawn patient)
            {
                if (!__result || patient == null) return;
                Context ctx = current;
                if (ctx != null && ctx.patient == patient) ctx.failed = true;
                // schedule the retry now, before vanilla deletes the bill (deleting the cure bill would otherwise clear "pending")
                if (patient.IsPatient(out HospitalMapComponent hospital, false)) hospital.OnSurgeryBotched(patient);
            }
        }

        private static void BillFor(Context ctx, RecipeDef recipe, Pawn billDoer, List<Thing> ingredients)
        {
            Pawn pawn = ctx.patient;
            HospitalMapComponent hospital = HospitalMapComponent.For(pawn.MapHeld);
            if (hospital == null || !hospital.GetPatientData(pawn, out PatientData data) || data.Dismissed) return;

            float medicine = 0f, parts = 0f;
            Thing firstMedicine = null;
            if (ingredients != null)
            {
                foreach (Thing t in ingredients)
                {
                    if (t?.def == null) continue;
                    float v = t.def.BaseMarketValue * t.stackCount;
                    if (t.def.IsMedicine) { medicine += v; if (firstMedicine == null) firstMedicine = t; }
                    else parts += v;
                }
            }
            if (ctx.anesthetic != null)
                foreach (KeyValuePair<ThingDef, int> kv in ctx.anesthetic)
                    if (kv.Key != null) medicine += kv.Key.BaseMarketValue * kv.Value;
            medicine = Mathf.Clamp(medicine, 0f, 16000f);
            parts = Mathf.Clamp(parts, 0f, 16000f);

            PatientUtility.RecordTreatment(pawn, billDoer, firstMedicine);
            Settings s = HospitalMod.Settings;
            if (ctx.failed)
            {
                // no fee for a failure; the patient's people replace what the attempt used up (settings)
                if (s.ReimburseMedicineOnFailure) data.AddLine(InvoiceLineKind.ReimburseMedicine, recipe.label, medicine);
                if (s.ReimburseProstheticsOnFailure) data.AddLine(InvoiceLineKind.ReimburseParts, recipe.label, parts);
                return;
            }
            data.AddLine(InvoiceLineKind.Surgery, recipe.label, SurgeryFee(recipe));
            data.AddLine(InvoiceLineKind.Materials, recipe.label, medicine + parts);
            if (data.CriticalCase && recipe == CriticalCase.Recipe) data.AddLine(InvoiceLineKind.CriticalCase, recipe.label, CriticalCase.Fee());
        }

        /// <summary>Work fee of an operation (formula from Hospital Fix And Tweak: work, required Medicine skill, risk), 0..400 silver.</summary>
        public static float SurgeryFee(RecipeDef recipe)
        {
            if (recipe == null) return 0f;
            SkillRequirement sr = recipe.skillRequirements?.Find(r => r.skill == SkillDefOf.Medicine);
            float value = ((recipe.workAmount + 900f) * 0.0036f) * (1f + (sr?.minLevel ?? 0f) / 4f) * (1f + 4f * recipe.deathOnFailedSurgeryChance);
            return Mathf.Clamp(value, 0f, 400f);
        }

        [HarmonyPatch(typeof(BillStack), nameof(BillStack.Delete))]
        public static class ClearPendingSurgeryOnBillRemoval
        {
            // Only clear the pending-surgery flag when the deleted bill is the actual cure bill, not an incidental one,
            // and not while a retry is scheduled (the failed bill is deleted right after a botch).
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
