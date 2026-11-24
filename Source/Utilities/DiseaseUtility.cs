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

        /// <summary>
        /// Diseases a complication may add: tendable sickness that can never kill (no lethal severity, no life-threatening or deadly stage)
        /// and never sends the patient berserk. In vanilla: gut worms and muscle parasites. Flu, plague and malaria are main diagnoses only.
        /// </summary>
        public static List<HediffDef> SecondaryDiseasePool()
        {
            return DefDatabase<HediffDef>.AllDefsListForReading.Where(def =>
                def.tendable && def.makesSickThought && def.lethalSeverity <= 0f
                && !def.defName.ToLower().Contains("scaria")
                && !def.defName.ToLower().Contains("mechanites")
                && !def.defName.ToLower().Contains("rot")
                && !def.defName.ToLower().Contains("animal")
                && !def.defName.ToLower().Contains("infant")
                && !def.defName.StartsWith("GR_") && !def.defName.StartsWith("VDE")
                && PatientComplexityGenerator.HasNoDangerousStage(def)
                && !def.chronic && !(HospitalMod.Settings?.DiseaseBlocked(def) ?? false)).ToList();
        }

        /// <summary>Adds a harmless secondary disease without touching the primary diagnosis (complexity layer). False when none was added.</summary>
        public static bool AddSecondaryDisease(Pawn pawn)
        {
            List<HediffDef> pool = SecondaryDiseasePool().Where(d => pawn.health.hediffSet.GetFirstHediffOfDef(d) == null).ToList();
            if (pool.Count == 0) return false;
            Hediff h = HediffMaker.MakeHediff(pool.RandomElement(), pawn);
            if (pawn.health.WouldDieAfterAddingHediff(h)) return false;
            pawn.health.AddHediff(h);
            return true;
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
