// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimWard.Utilities
{
    public static class SurgeryUtility
    {
        // Recipes that are never offered as a hospital case. Matched against the lowercase defName.
        private static readonly string[] SkipWords = { "abasia", "coma", "remove", "anesthetize", "blood", "admin", "rib", "harvest", "euthan", "terminate" };

        /// <summary>
        /// Picks a surgery the colony can actually do and gives the (not yet spawned) patient the matching problem.
        /// The map is passed in: a freshly generated pawn has no MapHeld yet. Returns true if generation FAILED
        /// (the caller then downgrades the case to wounds).
        /// </summary>
        public static bool AddRandomSurgeryBill(Pawn pawn, PatientData patientData, HospitalMap hospital, Map map)
        {
            if (pawn?.health == null || map == null) return true;
            List<Pawn> doctors = map.mapPawns.PawnsInFaction(Faction.OfPlayer).Where(p => p.IsFreeColonist || p.IsColonyMechPlayerControlled).ToList();
            List<RecipeDef> list = new List<RecipeDef>();
            foreach (RecipeDef recipe in pawn.def.AllRecipes)
            {
                if (!IsCandidate(recipe, hospital)) continue;
                if (!doctors.Any(col => recipe.PawnSatisfiesSkillRequirements(col))) continue;
                if (recipe.PotentiallyMissingIngredients(null, map).Any()) continue;
                if (OnlyForbiddenIngredient(recipe, map) != null) continue;
                if (MedicineBlockedByPolicy(recipe, map, hospital?.Policy.medCare ?? MedicalCareCategory.Best) != null) continue;
                list.Add(recipe);
            }
            if (list.Count == 0) return true;

            RecipeDef recipeDef = list.RandomElement();
            BodyPartRecord part = ChoosePart(pawn, recipeDef);
            if (part == null) return true;

            if (recipeDef.removesHediff != null)
            {
                Hediff hediff = HediffMaker.MakeHediff(recipeDef.removesHediff, pawn, part);
                pawn.health.AddHediff(hediff, part);
                patientData.Diagnosis = recipeDef.removesHediff.label;
            }
            else if (recipeDef.Worker is Recipe_InstallArtificialBodyPart || recipeDef.Worker is Recipe_InstallNaturalBodyPart)
            {
                HediffDef injury = HealthUtility.GetHediffDefFromDamage(HealthUtility.RandomPermanentInjuryDamageType(part.def.frostbiteVulnerability > 0f && pawn.RaceProps.ToolUser), pawn, part);
                if (!part.def.canSuggestAmputation)
                {
                    float dmg = pawn.health.hediffSet.GetPartHealth(part) * 0.5f;
                    Hediff hediff = HediffMaker.MakeHediff(injury, pawn, part);
                    hediff.Severity = dmg;
                    if (pawn.health.WouldDieAfterAddingHediff(hediff)) return true;
                    pawn.health.AddHediff(hediff, part);
                }
                else
                {
                    Hediff_MissingPart missing = (Hediff_MissingPart)HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, pawn);
                    missing.lastInjury = injury;
                    missing.Part = part;
                    missing.IsFresh = false;
                    if (pawn.health.WouldDieAfterAddingHediff(missing)) return true;
                    pawn.health.AddHediff(missing, part);
                }
                patientData.Diagnosis = "DiagnosisArtificialBodyPart".Translate((recipeDef.addsHediff != null) ? recipeDef.addsHediff.label : part.Label);
            }
            else
            {
                patientData.Diagnosis = "DiagnosisSurgeryOnPart".Translate(part.Label);
            }

            // After the problem exists, ask the recipe itself where it applies (catches the "treat X on part Y" recipes exactly).
            BodyPartRecord billPart = part;
            List<BodyPartRecord> valid = recipeDef.Worker.GetPartsToApplyOn(pawn, recipeDef).ToList();
            if (valid.Count > 0 && !valid.Contains(part)) billPart = valid.RandomElement();
            if (recipeDef.targetsBodyPart && valid.Count == 0 && recipeDef.removesHediff == null) return true;

            HealthCardUtility.CreateSurgeryBill(pawn, recipeDef, recipeDef.targetsBodyPart ? billPart : null, null, false);
            patientData.Cure = recipeDef.Worker.GetLabelWhenUsedOn(pawn, billPart);
            patientData.CureRecipe = recipeDef;
            if (pawn.health.surgeryBills.Count == 0) return true;
            patientData.HasPendingSurgeryBill = true;
            return false;
        }

        /// <summary>
        /// The operation needs medicine, but none in stock is allowed by the patient's medicine level (hospital policy): the doctors would
        /// never start it. Returns the level's label then, else null. (Vanilla's PotentiallyMissingIngredients ignores the medicine level.)
        /// </summary>
        public static string MedicineBlockedByPolicy(RecipeDef recipe, Map map, MedicalCareCategory care)
        {
            if (recipe?.ingredients == null || map == null) return null;
            foreach (IngredientCount ic in recipe.ingredients)
            {
                if (ic?.filter == null || !ic.filter.AllowedThingDefs.Any(d => d.IsMedicine)) continue;
                if (ic.filter.AllowedThingDefs.Any(d => !d.IsMedicine)) continue; // a mixed filter can be met without medicine
                bool any = map.listerThings.ThingsInGroup(ThingRequestGroup.Medicine)
                    .Any(t => ic.filter.Allows(t) && care.AllowsMedicine(t.def) && !t.IsForbidden(Faction.OfPlayer));
                if (!any) return care.GetLabel();
            }
            return null;
        }

        /// <summary>
        /// An ingredient (not medicine: see <see cref="MedicineBlockedByPolicy"/>) whose whole stock on the map is forbidden or fogged:
        /// the doctors would never fetch it. Returns that thing, else null. (Vanilla's PotentiallyMissingIngredients counts forbidden stock.)
        /// </summary>
        public static ThingDef OnlyForbiddenIngredient(RecipeDef recipe, Map map)
        {
            if (recipe?.ingredients == null || map == null) return null;
            foreach (IngredientCount ic in recipe.ingredients)
            {
                if (ic?.filter == null) continue;
                List<ThingDef> defs = ic.filter.AllowedThingDefs.ToList();
                if (defs.Count == 0 || defs.All(d => d.IsMedicine)) continue;
                ThingDef forbiddenOne = null;
                bool usable = false;
                foreach (ThingDef d in defs)
                {
                    foreach (Thing t in map.listerThings.ThingsOfDef(d))
                    {
                        if (!ic.filter.Allows(t)) continue;
                        if (!t.IsForbidden(Faction.OfPlayer) && !t.PositionHeld.Fogged(map)) { usable = true; break; }
                        forbiddenOne = forbiddenOne ?? d;
                    }
                    if (usable) break;
                }
                if (!usable && forbiddenOne != null) return forbiddenOne;
            }
            return null;
        }

        public static bool IsCandidate(RecipeDef recipe, HospitalMap hospital)
        {
            if (recipe == null || !recipe.AvailableNow || recipe.isViolation) return false;
            if (hospital != null && !hospital.IsSurgeryRecipeAllowed(recipe)) return false;
            if (HospitalMod.Settings?.SurgeryBlocked(recipe) ?? false) return false;
            string lower = recipe.defName.ToLowerInvariant();
            foreach (string w in SkipWords) if (lower.Contains(w)) return false;
            // Mod prefixes that need special pawns: VRE Android (VREA_), Genetic Rim (GR_).
            if (recipe.defName.StartsWith("VREA", System.StringComparison.OrdinalIgnoreCase)) return false;
            if (recipe.defName.StartsWith("GR_", System.StringComparison.Ordinal)) return false;
            return true;
        }

        /// <summary>The part a case is placed on: fixed parts of the recipe, the parts its worker lists, the part the name points to, the parts vanilla's givers use.</summary>
        public static BodyPartRecord ChoosePart(Pawn pawn, RecipeDef recipeDef)
        {
            BodyPartRecord part = null;
            if (recipeDef.appliedOnFixedBodyParts != null && recipeDef.appliedOnFixedBodyParts.Count > 0)
            {
                List<BodyPartRecord> fixedParts = pawn.health.hediffSet.GetNotMissingParts().Where(r => recipeDef.appliedOnFixedBodyParts.Contains(r.def)).ToList();
                if (fixedParts.Count > 0) part = fixedParts.RandomElement();
            }
            if (part == null && recipeDef.removesHediff != null)
            {
                part = PartFinder.ForKeyword(pawn, recipeDef.removesHediff.defName)
                    ?? PartFinder.FromHediffGivers(pawn, recipeDef.removesHediff)
                    ?? PartFinder.ForKeyword(pawn, recipeDef.defName);
            }
            if (part == null)
            {
                List<BodyPartRecord> listed = recipeDef.Worker.GetPartsToApplyOn(pawn, recipeDef).ToList();
                if (listed.Count > 0) part = listed.RandomElement();
            }
            if (part == null) part = PartFinder.ForKeyword(pawn, recipeDef.defName);
            if (part == null)
            {
                List<BodyPartRecord> source = pawn.health.hediffSet.GetNotMissingParts()
                    .Where(x => pawn.health.hediffSet.GetPartHealth(x) >= 2f && x.def.canScarify).ToList();
                if (source.Count == 0) return null;
                part = source.RandomElement();
            }
            return part;
        }
    }
}
