using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimWard.Utilities
{
    public static class SurgeryUtility
    {
        // Returns true if generation FAILED (caller downgrades to a wound case). Parity port; the
        // defName string filters and body-part hacks are replaced by the property classifier in Step 3.
        public static bool AddRandomSurgeryBill(Pawn pawn, PatientData patientData, HospitalMapComponent hospital)
        {
            List<RecipeDef> list = new List<RecipeDef>();
            foreach (RecipeDef recipe in pawn.def.AllRecipes)
            {
                if (recipe.AvailableNow && hospital.IsSurgeryRecipeAllowed(recipe) && !recipe.isViolation
                    && !recipe.defName.ToLower().Contains("abasia")
                    && !recipe.defName.ToLower().Contains("coma")
                    && !recipe.defName.ToLower().Contains("remove")
                    && !recipe.defName.ToLower().Contains("anesthetize")
                    && !recipe.defName.ToLower().Contains("blood")
                    && !recipe.defName.ToLower().Contains("admin")
                    && !recipe.defName.ToLower().Contains("rib")
                    && !recipe.defName.ToLower().Contains("harvest")
                    && !recipe.defName.ToUpper().Contains("VREA")
                    && !recipe.defName.ToUpper().Contains("GR"))
                {
                    bool anyColonistQualifies = pawn.MapHeld.mapPawns.PawnsInFaction(Faction.OfPlayer)
                        .Where(p => p.IsFreeColonist || p.IsColonyMechPlayerControlled)
                        .Any(col => recipe.PawnSatisfiesSkillRequirements(col));
                    if (anyColonistQualifies && !recipe.PotentiallyMissingIngredients(null, pawn.MapHeld).Any())
                    {
                        list.Add(recipe);
                    }
                }
            }
            if (list.Count == 0)
            {
                return true;
            }

            RecipeDef recipeDef = list.RandomElement();
            BodyPartRecord part = null;
            if (recipeDef.appliedOnFixedBodyParts != null)
            {
                BodyPartDef bpd = recipeDef.appliedOnFixedBodyParts.RandomElement();
                foreach (BodyPartRecord rec in pawn.health.hediffSet.GetNotMissingParts())
                {
                    if (rec.def.Equals(bpd)) part = rec;
                }
            }
            if (part == null && recipeDef.Worker.GetPartsToApplyOn(pawn, recipeDef).Any())
            {
                part = recipeDef.Worker.GetPartsToApplyOn(pawn, recipeDef).RandomElement();
            }
            if (recipeDef.removesHediff != null)
            {
                if (recipeDef.removesHediff.defName.ToLower().Equals("appendicitis"))
                    part = pawn.health.hediffSet.GetNotMissingParts().Where(r => r.def.label.ToLower().Equals("torso")).FirstOrFallback(null);
                if (recipeDef.removesHediff.defName.ToLower().Equals("toothache"))
                    part = pawn.health.hediffSet.GetNotMissingParts().Where(r => r.def.label.ToLower().Equals("jaw")).FirstOrFallback(null);
            }
            if (recipeDef.defName.ToLower().Contains("kidney"))
                part = pawn.health.hediffSet.GetNotMissingParts().Where(r => r.def.label.ToLower().Equals("kidney")).FirstOrFallback(null);
            if (recipeDef.defName.ToLower().Contains("deaf"))
                part = pawn.health.hediffSet.GetNotMissingParts().Where(r => r.def.label.ToLower().Equals("kidney")).FirstOrFallback(null);
            if (recipeDef.defName.ToLower().Contains("glaucoma") || recipeDef.defName.ToLower().Contains("strabismus"))
                part = pawn.health.hediffSet.GetNotMissingParts().Where(r => r.def.label.ToLower().Equals("eye")).FirstOrFallback(null);
            if (recipeDef.defName.ToLower().Contains("arrhythmia") || recipeDef.defName.ToLower().Contains("heart"))
                part = pawn.health.hediffSet.GetNotMissingParts().Where(r => r.def.label.ToLower().Equals("heart")).FirstOrFallback(null);
            if (recipeDef.defName.ToLower().Contains("spinal") || recipeDef.defName.ToLower().Contains("hernia"))
                part = pawn.health.hediffSet.GetNotMissingParts().Where(r => r.def.label.ToLower().Equals("spine")).FirstOrFallback(null);
            if (recipeDef.defName.ToLower().Contains("gastritis"))
                part = pawn.health.hediffSet.GetNotMissingParts().Where(r => r.def.label.ToLower().Equals("stomach")).FirstOrFallback(null);

            if (part == null)
            {
                IEnumerable<BodyPartRecord> source = pawn.health.hediffSet.GetNotMissingParts()
                    .Where(x => pawn.health.hediffSet.GetPartHealth(x) >= 2f && x.def.canScarify);
                if (!source.Any())
                {
                    return true;
                }
                part = source.RandomElement();
            }

            if (recipeDef.removesHediff != null)
            {
                Hediff hediff = HediffMaker.MakeHediff(recipeDef.removesHediff, pawn);
                hediff.Part = part;
                pawn.health.AddHediff(hediff, part);
                patientData.Diagnosis = recipeDef.removesHediff.label;
            }
            else if (recipeDef.workerClass == typeof(Recipe_InstallArtificialBodyPart) || recipeDef.workerClass == typeof(Recipe_InstallNaturalBodyPart))
            {
                if (!part.def.canSuggestAmputation)
                {
                    float dmg = pawn.health.hediffSet.GetPartHealth(part) * 0.5f;
                    Hediff hediff = HediffMaker.MakeHediff(HealthUtility.GetHediffDefFromDamage(HealthUtility.RandomPermanentInjuryDamageType(part.def.frostbiteVulnerability > 0f && pawn.RaceProps.ToolUser), pawn, part), pawn, part);
                    hediff.Severity = dmg;
                    pawn.health.AddHediff(hediff, part);
                }
                else
                {
                    Hediff_MissingPart missing = (Hediff_MissingPart)HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, pawn);
                    missing.lastInjury = HealthUtility.GetHediffDefFromDamage(HealthUtility.RandomPermanentInjuryDamageType(part.def.frostbiteVulnerability > 0f && pawn.RaceProps.ToolUser), pawn, part);
                    missing.Part = part;
                    missing.IsFresh = false;
                    pawn.health.AddHediff(missing, part);
                }
                patientData.Diagnosis = "DiagnosisArtificialBodyPart".Translate((recipeDef.addsHediff != null) ? recipeDef.addsHediff.label : part.Label);
            }
            else
            {
                patientData.Diagnosis = "DiagnosisSurgeryOnPart".Translate(part.Label);
            }

            HealthCardUtility.CreateSurgeryBill(pawn, recipeDef, part, null, false);
            patientData.Cure = recipeDef.Worker.GetLabelWhenUsedOn(pawn, part);
            patientData.CureRecipe = recipeDef;
            if (pawn.health.surgeryBills.Count == 0)
            {
                return true;
            }
            patientData.HasPendingSurgeryBill = true;
            return false;
        }
    }
}
