using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimWard.Utilities
{
    public static class DiseaseUtility
    {
        public static bool AddRandomDisease(Pawn pawn, PatientData patientData)
        {
            bool retry = true;
            float lowered = 0f;
            List<HediffDef> list = DefDatabase<HediffDef>.AllDefsListForReading.Where(def =>
                def.tendable && def.makesSickThought
                && !def.defName.ToLower().Contains("abasia")
                && !def.defName.ToLower().Contains("sepsis")
                && !def.defName.ToLower().Contains("animal")
                && !def.defName.ToLower().Contains("gene")
                && !def.defName.ToLower().Contains("infant")
                && !def.defName.ToLower().Contains("scaria")
                && !def.defName.ToLower().Contains("mechanites") // folded in from Hospital: Disease Patch
                && !def.defName.ToLower().Contains("rot")        // folded in from Hospital: Disease Patch
                && !def.defName.StartsWith("GR_")
                && !def.defName.StartsWith("VDE")
                && NoAggroStage(def) && !def.chronic && !(HospitalMod.Settings?.DiseaseBlocked(def) ?? false)).ToList();
            if (list.Count == 0) return true;

            Hediff hediff = null;
            while (retry)
            {
                hediff = HediffMaker.MakeHediff(list.RandomElement(), pawn);
                float severity = Rand.Range(hediff.def.lethalSeverity / 25f, hediff.def.lethalSeverity / 10f) * HospitalMod.Settings.Difficulty;
                hediff.Severity = severity - lowered;
                if (!pawn.health.WouldDieAfterAddingHediff(hediff))
                {
                    if (hediff.def.defName.ToLower().Contains("infection"))
                    {
                        IEnumerable<BodyPartRecord> source = pawn.health.hediffSet.GetNotMissingParts()
                            .Where(x => pawn.health.hediffSet.GetPartHealth(x) >= 2f && x.def.canScarify);
                        if (!source.Any()) break;
                        BodyPartRecord part = source.RandomElement();
                        WoundsUtility.DamagePart(pawn, 5, part, out _, DamageDefOf.Bite);
                        pawn.health.AddHediff(hediff, part);
                    }
                    else if (hediff.def.defName.ToLower().Contains("lung"))
                    {
                        IEnumerable<BodyPartRecord> lungs = pawn.health.hediffSet.GetNotMissingParts()
                            .Where(x => x.def.defName.ToLower().Contains("lung"));
                        if (!lungs.Any()) break;
                        pawn.health.AddHediff(hediff, lungs.RandomElement());
                    }
                    else
                    {
                        pawn.health.AddHediff(hediff);
                    }
                    retry = !pawn.health.HasHediffsNeedingTend(false) && !HealthAIUtility.ShouldSeekMedicalRest(pawn);
                }
                else
                {
                    lowered += 0.1f;
                    if (lowered > 0.9f) return true;
                }
            }
            patientData.Bill = 10f;
            patientData.Diagnosis = hediff.Label;
            patientData.Cure = "CureDisease".Translate();
            return false;
        }

        // Adds a low-severity secondary disease without touching the patient's primary diagnosis (complexity layer).
        public static void AddSecondaryDisease(Pawn pawn)
        {
            List<HediffDef> pool = DefDatabase<HediffDef>.AllDefsListForReading.Where(def =>
                def.tendable && def.makesSickThought
                && !def.defName.ToLower().Contains("scaria")
                && !def.defName.ToLower().Contains("mechanites")
                && !def.defName.ToLower().Contains("rot")
                && !def.defName.ToLower().Contains("animal")
                && !def.defName.StartsWith("GR_") && !def.defName.StartsWith("VDE")
                && NoAggroStage(def) && !def.chronic && !(HospitalMod.Settings?.DiseaseBlocked(def) ?? false)).ToList();
            if (pool.Count == 0) return;
            HediffDef chosen = pool.RandomElement();
            if (chosen.lethalSeverity <= 0f || pawn.health.hediffSet.GetFirstHediffOfDef(chosen) != null) return;
            Hediff h = HediffMaker.MakeHediff(chosen, pawn);
            h.Severity = Rand.Range(chosen.lethalSeverity / 30f, chosen.lethalSeverity / 15f) * HospitalMod.Settings.Difficulty;
            if (!pawn.health.WouldDieAfterAddingHediff(h)) pawn.health.AddHediff(h);
        }

        /// <summary>No stage of this hediff drives the patient into an aggressive mental state (berserk on the ward).</summary>
        public static bool NoAggroStage(HediffDef def)
        {
            if (def?.stages == null) return true;
            foreach (HediffStage st in def.stages)
            {
                if (st?.mentalStateGivers == null) continue;
                foreach (MentalStateGiver g in st.mentalStateGivers)
                    if (g?.mentalState != null && g.mentalState.IsAggro) return false;
            }
            return true;
        }

        public static void AddPlague(Pawn pawn, PatientData patientData)
        {
            bool retry = true;
            float lowered = 0f;
            Hediff hediff = HediffMaker.MakeHediff(HediffDefOf.Plague, pawn);
            while (retry)
            {
                float severity = Rand.Range(hediff.def.lethalSeverity / 20f, hediff.def.lethalSeverity / 10f) * HospitalMod.Settings.Difficulty;
                hediff.Severity = severity - lowered;
                if (!pawn.health.WouldDieAfterAddingHediff(hediff))
                {
                    pawn.health.AddHediff(hediff);
                    retry = !pawn.health.HasHediffsNeedingTend(false) && !HealthAIUtility.ShouldSeekMedicalRest(pawn);
                    if (retry)
                    {
                        hediff = HediffMaker.MakeHediff(HediffDefOf.Hangover, pawn);
                    }
                }
                else
                {
                    lowered += 0.1f;
                }
            }
            if (!retry)
            {
                patientData.Bill = 10f;
            }
            patientData.Diagnosis = hediff.Label;
            patientData.Cure = "CureDisease".Translate();
        }
    }
}
