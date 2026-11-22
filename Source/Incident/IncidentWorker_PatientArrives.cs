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
                && f.def.humanlikeFaction && !f.def.defName.ToUpper().Contains("VREA")).ToList();

            Pawn[] activePawns = map.mapPawns.AllPawnsSpawned.Where(p =>
                !p.Dead && !p.IsPrisoner && !p.Downed && !PatientUtility.IsFogged(p) && !p.InContainerEnclosed).ToArray();
            if (activePawns.Any(p => p.InAggroMentalState) && !map.GetComponent<HospitalMapComponent>().AcceptDanger)
            {
                return false;
            }
            factions.RemoveAll(faction => activePawns.Where(p => p.Faction != null).Select(p => p.Faction)
                .Any(f => f.HostileTo(Faction.OfPlayer) || f.HostileTo(faction)));
            if (factions.Count == 0) return false;

            parms.faction = factions.RandomElement();
            Pawn pawn = IncidentHelper.GenerateValidPatientPawn(parms.faction);
            if (pawn == null) return false;

            // Neutral layer: xenotype variety (80% baseliner / 20% random, incl. custom).
            CompatGen.ApplyXenotypeVariety(pawn);

            // Build the core case (apply the ailment) so the offer / letter can show a real diagnosis.
            HospitalMapComponent comp = map.GetComponent<HospitalMapComponent>();
            IncidentHelper.SetUpNewPatient(pawn);
            PatientType type = (PatientType)Rand.Range(1, comp.AcceptSurgery ? 4 : 3);
            PatientData data = new PatientData(Find.TickManager.TicksGame, pawn.MarketValue, pawn.needs.mood.CurLevel, type);
            PatientUtility.DamagePawn(pawn, data, comp);

            // Difficulty layer: budget-driven complications (extra ailments, frailty, addiction, royal, dependency, organ failure).
            PatientComplexityGenerator.ApplyComplexity(pawn, data, comp);

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
            TaggedString text = this.def.letterText.Formatted(pawn.Named("PAWN"), data.Diagnosis).AdjustedFor(pawn);
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
