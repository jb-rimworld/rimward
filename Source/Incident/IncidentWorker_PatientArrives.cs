// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
using System.Collections.Generic;
using System.Linq;
using RimWard.Utilities;
using RimWorld;
using Verse;

namespace RimWard
{
    public class IncidentWorker_PatientArrives : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return base.CanFireNowSub(parms) && IncidentHelper.CanSpawnPatient((Map)parms.target);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            if (!IncidentHelper.CanSpawnPatient(map)) return false;

            List<Faction> factions = Find.FactionManager.AllFactions.Where(f =>
                !f.IsPlayer && !f.defeated && !f.def.hidden && f.AllyOrNeutralTo(Faction.OfPlayer)
                && f.def.humanlikeFaction && !f.def.defName.ToUpper().Contains("VREA") && !(HospitalMod.Settings?.FactionBlocked(f.def) ?? false)).ToList();

            // never send a patient whose people are at war with someone actively fighting on this map
            IReadOnlyList<Faction> hostiles = Threats.ActiveHostileFactions(map);
            factions.RemoveAll(faction => hostiles.Any(f => f == faction || f.HostileTo(faction)));
            if (factions.Count == 0) return false;

            parms.faction = factions.RandomElement();
            Pawn pawn = IncidentHelper.GenerateValidPatientPawn(parms.faction);
            if (pawn == null) return false;

            // Build the core case (apply the ailment) so the offer / letter can show a real diagnosis.
            HospitalMap comp = HospitalMap.For(map);
            IncidentHelper.SetUpNewPatient(pawn);
            bool surgery = comp.AcceptSurgery && HospitalMod.Settings.SurgeryCasesOn;
            PatientType type = (PatientType)Rand.Range(1, surgery ? 4 : 3);
            PatientData data = new PatientData(Find.TickManager.TicksGame, pawn.MarketValue, pawn.needs.mood.CurLevel, type);
            if (!CriticalCase.TryMake(pawn, data, comp)) PatientUtility.DamagePawn(pawn, data, comp);

            // Difficulty layer: budget-driven complications (extra ailments, frailty, addiction, royal, dependency, organ failure).
            if (HospitalMod.Settings.ComplicationsEnabled) PatientComplexityGenerator.ApplyComplexity(pawn, data, comp);

            // Route by intake mode.
            IntakeMode mode = HospitalMod.Settings.Intake;
            bool highStakes = data.Type == PatientType.Surgery
                || (ModsConfig.RoyaltyActive && pawn.royalty != null && pawn.royalty.AllTitlesForReading.Count > 0);
            if (mode == IntakeMode.Prompt || (mode == IntakeMode.Hybrid && highStakes))
            {
                comp.CreateDropPodOffer(pawn, data, parms.faction);
                return true;
            }

            // Firehose / routine: deliver immediately + arrival notice.
            comp.DeliverPatient(pawn, data, parms.faction);
            TaggedString text = this.def.letterText.Formatted(pawn.Named("PAWN"), data.Diagnosis).AdjustedFor(pawn).CapitalizeFirst();
            TaggedString label = this.def.letterLabel.Formatted(pawn.Named("PAWN")).AdjustedFor(pawn);
            if (HospitalMod.Settings.ShowMessageAtArrival)
            {
                Messages.Message(label + ": " + text, MessageTypeDefOf.PositiveEvent, true);
            }
            else
            {
                this.SendStandardLetter(label, text, LetterDefOf.PositiveEvent, parms, pawn);
            }
            return true;
        }
    }
}
