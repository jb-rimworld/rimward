// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
using System;
using System.Collections.Generic;
using System.Linq;
using RimWard.Utilities;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimWard
{
    public class IncidentWorker_MassCasualtyEvent : IncidentWorker
    {
        public enum MCEType
        {
            Pandemic,
            Raid,
            Crash,
            Fire
        }

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms)) return false;
            Map map = (Map)parms.target;
            HospitalMapComponent c = HospitalMapComponent.For(map);
            return c != null && c.MassCasualties && IncidentHelper.CanSpawnPatient(map);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            HospitalMapComponent component = HospitalMapComponent.For(map);
            if (component == null || !component.MassCasualties) return false;
            if (!IncidentHelper.CanSpawnPatient(map)) return false;

            // sized on FREE patient beds (occupied ones do not count), so the event never overfills the hospital
            int free = component.FreeMedicalBeds();
            if (free <= 0) return false;
            if (parms.pawnCount <= 0) parms.pawnCount = free + Rand.Range(-free / 5, 1);
            parms.pawnCount = UnityEngine.Mathf.Clamp(parms.pawnCount, 1, free);
            List<Faction> factions = Find.FactionManager.AllFactions.Where(f =>
                !f.IsPlayer && !f.defeated && !f.def.hidden && !f.HostileTo(Faction.OfPlayer)
                && f.def.humanlikeFaction && !f.def.defName.ToUpper().Contains("VREA") && !(HospitalMod.Settings?.FactionBlocked(f.def) ?? false)).ToList();
            if (factions.Count == 0) return false;
            parms.faction = factions.RandomElement();

            List<Pawn> pawns = new List<Pawn>();
            pawns.AddRange(IncidentHelper.GetKnownPawns(parms).InRandomOrder().Take((int)Math.Floor(parms.pawnCount * 0.2f)).ToList());
            int toGenerate = parms.pawnCount - pawns.Count;
            for (int i = 0; i < toGenerate; i++)
            {
                Pawn p = IncidentHelper.GenerateValidPatientPawn(parms.faction);
                if (p != null) pawns.Add(p);
            }
            pawns.RemoveAll(p => IncidentHelper.IsWeirdPawn(p));
            if (pawns.Count == 0) return false;

            MCEType type = (MCEType)Rand.Range(0, 4);
            foreach (Pawn pawn in pawns)
            {
                this.SpawnPatient(map, pawn, type);
                LordMaker.MakeNewLord(parms.faction, this.CreateLordJob(parms, pawns), map, new List<Pawn> { pawn });
            }

            TaggedString text = this.def.letterText.Formatted(("MCE_" + type).Translate(), pawns.Count);
            TaggedString label = this.def.letterLabel;
            this.SendStandardLetter(label, text, LetterDefOf.ThreatSmall, parms, pawns);
            return true;
        }

        protected virtual PatientData SpawnPatient(Map map, Pawn pawn, MCEType type)
        {
            IncidentHelper.SetUpNewPatient(pawn);
            PatientType patientType = (type == MCEType.Pandemic) ? PatientType.Disease : PatientType.Wounds;
            PatientData patientData = new PatientData(Find.TickManager.TicksGame, pawn.MarketValue, pawn.needs.mood.CurLevel, patientType);

            Building landing = map.listerBuildings.AllBuildingsColonistOfDef(ThingDef.Named("RimWard_PatientLandingSpot")).RandomElementWithFallback();
            IntVec3 cell = DropCellFinder.TryFindSafeLandingSpotCloseToColony(map, IntVec2.Two, null, 2);
            if (landing != null) cell = landing.Position;

            ActiveTransporterInfo info = new ActiveTransporterInfo();
            info.innerContainer.TryAdd(pawn, 1, true);
            info.openDelay = 60;
            info.leaveSlag = false;
            info.despawnPodBeforeSpawningThing = true;
            info.spawnWipeMode = WipeMode.Vanish;
            DropPodUtility.MakeDropPodAt(cell, map, info);

            HospitalMapComponent component = HospitalMapComponent.For(map);
            PatientUtility.DamagePawn(pawn, patientData, component, type);
            component.PatientArrived(pawn, patientData);
            pawn.jobs.StopAll();
            pawn.jobs.JobTrackerTick();
            return patientData;
        }

        protected virtual LordJob_VisitColonyAsPatient CreateLordJob(IncidentParms parms, List<Pawn> pawns)
        {
            return new LordJob_VisitColonyAsPatient(parms.faction);
        }
    }
}
