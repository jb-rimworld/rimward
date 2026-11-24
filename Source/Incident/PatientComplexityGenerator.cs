// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
using System;
using System.Collections.Generic;
using System.Linq;
using RimWard.Utilities;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard
{
    // Layers difficulty "complications" onto a freshly-generated patient using a points budget driven by the
    // central difficulty dial. Each complication costs points and is applied at most once until the budget runs out.
    // Risky mod-specific complications (drug dependency, organ failure, zombie bite) are soft-gated and isolated.
    public static class PatientComplexityGenerator
    {
        private class Complication
        {
            public int cost;
            public float weight;
            public Func<Pawn, bool> canApply;
            public Func<Pawn, PatientData, bool> apply; // returns true if actually applied
        }

        public static int Budget()
        {
            float diff = HospitalMod.Settings.Difficulty; // 0.5 .. 2.0
            float b = 1f + 3f * (diff - 0.5f) + Rand.Range(-0.5f, 1f);
            return Mathf.Max(0, Mathf.RoundToInt(b));
        }

        public static void ApplyComplexity(Pawn pawn, PatientData data, HospitalMapComponent hospital)
        {
            int budget = Budget();
            if (budget <= 0) return;
            List<Complication> pool = BuildPool();
            int guard = 0;
            while (budget > 0 && guard++ < 12 && pool.Count > 0)
            {
                List<Complication> affordable = pool.Where(c => c.cost <= budget && SafeCan(c, pawn)).ToList();
                if (affordable.Count == 0) break;
                Complication pick = affordable.RandomElementByWeight(c => c.weight);
                pool.Remove(pick);
                bool applied = false;
                try { applied = pick.apply(pawn, data); }
                catch (Exception e) { Log.Warning("[RimWard] complication failed: " + e.Message); }
                if (applied) budget -= pick.cost;
            }
        }

        private static bool SafeCan(Complication c, Pawn pawn)
        {
            try { return c.canApply == null || c.canApply(pawn); }
            catch { return false; }
        }

        private static List<Complication> BuildPool()
        {
            List<Complication> list = new List<Complication>
            {
                new Complication { cost = 2, weight = 1.0f, apply = (p, d) => DiseaseUtility.AddSecondaryDisease(p) },
                new Complication { cost = 1, weight = 0.8f, apply = (p, d) => ApplyFrailty(p) },
                new Complication { cost = 2, weight = 0.7f, canApply = p => p.RaceProps.Humanlike, apply = (p, d) => ApplyAddiction(p) },
            };
            // Difficulty/DLC/mod complications (soft-gated):
            if (ModsConfig.RoyaltyActive && HospitalMod.Settings.RoyalRoomDemand)
                list.Add(new Complication { cost = 2, weight = 0.5f, canApply = CompatGen.CanMakeRoyal, apply = (p, d) => CompatGen.MakeRoyal(p, d) });
            // Go-juice dependency (vanilla addiction; canApply checks the chemical exists).
            list.Add(new Complication { cost = 3, weight = 0.5f, canApply = CompatGen.CanAddDependency, apply = (p, d) => CompatGen.AddDrugDependency(p) });
            // (Organ failure generation deferred — see CompatGen.AddOrganFailure notes.)
            return list;
        }

        private static bool ApplyFrailty(Pawn pawn)
        {
            // No carcinoma: without a body part it cannot be excised and grows until it kills the patient on our ward.
            string[] chronic = { "Frail", "BadBack", "Asthma" };
            foreach (string name in chronic.InRandomOrder())
            {
                HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(name);
                if (def == null || pawn.health.hediffSet.GetFirstHediffOfDef(def) != null) continue;
                try
                {
                    Hediff h = HediffMaker.MakeHediff(def, pawn);
                    if (!pawn.health.WouldDieAfterAddingHediff(h)) { pawn.health.AddHediff(h); return true; }
                }
                catch { }
            }
            return false;
        }

        private static bool ApplyAddiction(Pawn pawn)
        {
            // A pre-existing addiction (withdrawal context), but only to chemicals whose addiction can neither kill
            // nor drive the patient berserk (no luciferium: the mod must not create deaths it then blames on you).
            List<ChemicalDef> chems = DefDatabase<ChemicalDef>.AllDefsListForReading.Where(c => IsSafeAddiction(c?.addictionHediff)).ToList();
            if (chems.Count == 0) return false;
            ChemicalDef chem = chems.RandomElement();
            if (pawn.health.hediffSet.GetFirstHediffOfDef(chem.addictionHediff) != null) return false;
            try
            {
                Hediff_Addiction add = (Hediff_Addiction)HediffMaker.MakeHediff(chem.addictionHediff, pawn);
                pawn.health.AddHediff(add);
                return true;
            }
            catch { return false; }
        }

        /// <summary>True for a hediff that can be given to a patient without risking death or a mental state: no lethal severity,
        /// no stage that is life threatening, has a death chance, gives an aggressive state or a drug binge, or forces mental breaks.</summary>
        public static bool IsSafeAddiction(HediffDef def)
        {
            if (def == null) return false;
            if (!typeof(Hediff_Addiction).IsAssignableFrom(def.hediffClass)) return false;
            if (def.lethalSeverity > 0f) return false;
            return HasNoDangerousStage(def);
        }

        public static bool HasNoDangerousStage(HediffDef def)
        {
            if (def?.stages == null) return true;
            foreach (HediffStage st in def.stages)
            {
                if (st == null) continue;
                if (st.lifeThreatening || st.deathMtbDays > 0f) return false;
                if (st.mentalStateGivers != null)
                {
                    // aggressive states (berserk) and drug binges (overdose) are out; food binges and wandering are harmless
                    foreach (MentalStateGiver g in st.mentalStateGivers)
                    {
                        MentalStateDef ms = g?.mentalState;
                        if (ms == null) continue;
                        if (ms.IsAggro || ms.defName.StartsWith("Binging_Drug", StringComparison.Ordinal)) return false;
                    }
                }
                if (st.mentalBreakMtbDays > 0f) return false;
            }
            return true;
        }
    }
}
