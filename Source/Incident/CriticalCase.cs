using System.Collections.Generic;
using System.Linq;
using RimWard.Utilities;
using RimWorld;
using Verse;

namespace RimWard
{
    /// <summary>
    /// Luciferium critical case (area "luciferium": exists only with Luciferium Overhaul, off by default). A new patient arrives with a
    /// chronic condition only luciferium can mend; the cure is vanilla's own "Administer luciferium" bill, one dose from the colony's stock.
    /// The invoice bills the dose (materials line) plus a critical-case fee. Afterwards the patient is dependent on luciferium; their
    /// faction keeps them supplied (Luciferium Overhaul's addiction rules apply). Never offered without a dose in stock.
    /// </summary>
    public static class CriticalCase
    {
        private static readonly string[] Conditions = { "Dementia", "Alzheimers", "BadBack", "Frail", "Cataract", "HearingLoss", "Asthma" };
        private static RecipeDef recipe;
        private static bool resolved;

        /// <summary>Vanilla's generated "Administer luciferium" recipe (null without the drug).</summary>
        public static RecipeDef Recipe
        {
            get
            {
                if (!resolved) { recipe = DefDatabase<RecipeDef>.GetNamedSilentFail("Administer_Luciferium"); resolved = true; }
                return recipe;
            }
        }

        /// <summary>Can this map take a critical case now: area on, recipe exists, a dose in stock (reachable for a doctor).</summary>
        public static bool Possible(Map map)
        {
            Settings s = HospitalMod.Settings;
            if (s == null || !s.CriticalCasesOn || map == null || Recipe == null) return false;
            return !Recipe.PotentiallyMissingIngredients(null, map).Any();
        }

        /// <summary>Rolls the chance and, on a hit, turns the fresh patient into a critical case. False = a normal case follows.</summary>
        public static bool TryMake(Pawn pawn, PatientData data, HospitalMapComponent comp)
        {
            if (pawn?.health == null || data == null || comp == null || !pawn.RaceProps.Humanlike) return false;
            if (!Possible(comp.map) || !Rand.Chance(HospitalMod.Settings.CriticalCaseChance)) return false;
            return Make(pawn, data, comp);
        }

        /// <summary>Turns the patient into a critical case without the chance roll (also used by the tests).</summary>
        public static bool Make(Pawn pawn, PatientData data, HospitalMapComponent comp)
        {
            if (Recipe == null || pawn?.health == null) return false;
            Hediff condition = null;
            foreach (string name in Conditions.InRandomOrder())
            {
                HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(name);
                if (def == null || pawn.health.hediffSet.GetFirstHediffOfDef(def) != null) continue;
                BodyPartRecord part = PartFinder.FromHediffGivers(pawn, def);
                Hediff h = HediffMaker.MakeHediff(def, pawn, part);
                if (pawn.health.WouldDieAfterAddingHediff(h)) continue;
                pawn.health.AddHediff(h, part);
                condition = h;
                break;
            }
            if (condition == null) return false;
            data.Type = PatientType.Surgery;
            data.CriticalCase = true;
            data.Diagnosis = "RimWard.Critical.Diagnosis".Translate(condition.LabelCap);
            data.Cure = Recipe.LabelCap;
            data.CureRecipe = Recipe;
            HealthCardUtility.CreateSurgeryBill(pawn, Recipe, null, null, false);
            data.HasPendingSurgeryBill = pawn.health.surgeryBills.Bills.Any(b => b.recipe == Recipe);
            return data.HasPendingSurgeryBill;
        }

        /// <summary>The dose was given: the patient has luciferium in the blood (or is already dependent).</summary>
        public static bool Done(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return false;
            foreach (string name in new[] { "LuciferiumHigh", "LuciferiumAddiction" })
            {
                HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(name);
                if (def != null && pawn.health.hediffSet.GetFirstHediffOfDef(def) != null) return true;
            }
            return false;
        }

        /// <summary>The critical-case fee: the setting times the value of one luciferium.</summary>
        public static float Fee()
        {
            ThingDef luci = DefDatabase<ThingDef>.GetNamedSilentFail("Luciferium");
            float value = luci?.BaseMarketValue ?? 70f;
            return value * (HospitalMod.Settings?.CriticalCaseBonus ?? 2f);
        }
    }
}
