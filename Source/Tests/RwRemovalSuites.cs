// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Copyright (C) 2026 juliusberger00. Written for RimWard 2.0 (a GPL-3.0 fork of Hospital by Adamas and
// Hospital Fix And Tweak by assssssqwww). Licensed under the GNU General Public License version 3 (LICENSE);
// full source: https://github.com/jb-rimworld/rimward
#if JB_TESTS
// Removal suites (explicit): the build phase of `jbmod RimWard removal <label> --build <suite>`. rw.removalstate leaves a busy hospital in
// the save (unprepared removal), rw.removalprep runs "Prepare for removal" first and proves that the save then holds nothing of RimWard.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JBFramework.Testing;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimWard.Tests
{
    internal static class RemovalT
    {
        /// <summary>Everything RimWard puts into a running game: patients (one mid-surgery, one dismissed), an offer with its letter, a
        /// "treatment impossible" letter, a landing spot and a "turned a patient away" memory.</summary>
        internal static void BusyHospital(TestContext t, Map map)
        {
            RwT.SetUp(t, 5, 1);
            HospitalMap comp = RwT.Comp(map);
            Pawn surg = RwT.NewPatient(map, PatientType.Wounds, out PatientData ds, true, false);
            BillingSuite.MakeArmCase(surg, ds, out _, out _);
            RwT.NewPatient(map, PatientType.Wounds, out _, true, false);
            Pawn dis = RwT.NewPatient(map, PatientType.Disease, out _, true, false);
            comp.DismissPatient(dis);
            Pawn op = RwT.NewPatient(map, PatientType.Wounds, out PatientData od, false, false);
            if (op != null) comp.CreateDropPodOffer(op, od, op.Faction);
            if (surg != null) ChoiceLetter_TreatmentImpossible.Send(comp, surg, ds, "test", 60000);
            ThingDef spot = DefDatabase<ThingDef>.GetNamed("RimWard_PatientLandingSpot");
            Thing s = ThingMaker.MakeThing(spot, spot.MadeFromStuff ? GenStuff.DefaultStuffFor(spot) : null);
            s.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(s, CellFinder.StandableCellNear(TestCells.Anchor(map), map, 15f), map);
            ThoughtDef turned = DefDatabase<ThoughtDef>.GetNamedSilentFail("RimWard_TurnedAwayPatient");
            Pawn col = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
            if (turned != null && col?.needs?.mood != null) col.needs.mood.thoughts.memories.TryGainMemory(turned);
            ArrivalScheduler.PausedForTests = false;   // the save keeps a normal hospital (the pause is never saved anyway)
            t.Note("busy hospital: " + comp.Patients.Count + " patients, " + comp.pendingOffers.Count + " offers, "
                + Find.LetterStack.LettersListForReading.Count(Ours) + " RimWard letters, landing spot " + s.Spawned);
        }

        internal static bool Ours(Letter l) => l != null && l.GetType().Assembly == typeof(HospitalMod).Assembly;

        /// <summary>What a save file names of RimWard: classes and defs (the framework's plain data nodes do not count).</summary>
        internal static List<string> NamedInSave(string save)
        {
            string xml = File.ReadAllText(GenFilePaths.FilePathForSavedGame(save));
            var hits = new List<string>();
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(xml, @"Class=""RimWard\.[\w.]+""|>RimWard_\w+<"))
                if (!hits.Contains(m.Value)) hits.Add(m.Value);
            return hits;
        }
    }

    /// <summary>rw.removalstate (explicit): a busy hospital, saved by kit.saveload afterwards; the removal phase loads it WITHOUT RimWard.</summary>
    sealed class RemovalStateSuite : KitTestSuite
    {
        public override string Name => "rw.removalstate";
        public override bool ExplicitOnly => true;
        public override int MaxTicks => 3000;

        public override IEnumerable<int> Run(TestContext t)
        {
            Map map = t.Map;
            RemovalT.BusyHospital(t, map);
            yield return 300;
            HospitalMap comp = RwT.Comp(map);
            t.Check(comp.Patients.Count >= 2 && comp.pendingOffers.Count >= 1, "the hospital is busy before the save (" + comp.Patients.Count + " patients)");
            t.Check(t.SaveGame("rw_unprepared"), "saved");
            List<string> named = RemovalT.NamedInSave("rw_unprepared");
            t.Note("an unprepared save names: " + string.Join(", ", named));
            t.Check(!named.Any(n => n.Contains("HospitalGameComponent") || n.Contains("HospitalMapComponent") || n.Contains("IncidentScheduler")),
                "even unprepared, the save names no component class of RimWard (only what lives on pawns, lords and letters)");
        }
    }

    /// <summary>
    /// rw.removalprep (explicit): a busy hospital, then "Prepare for removal" (Patient intake area): patients are sent home on vanilla
    /// lords, offers and letters are gone, the landing spot is deconstructed, the memory removed, intake is off, and a save names nothing of RimWard.
    /// </summary>
    sealed class RemovalPrepSuite : KitTestSuite
    {
        public override string Name => "rw.removalprep";
        public override bool ExplicitOnly => true;
        public override int MaxTicks => 3000;

        public override IEnumerable<int> Run(TestContext t)
        {
            Map map = t.Map;
            RemovalT.BusyHospital(t, map);
            yield return 300;
            HospitalMap comp = RwT.Comp(map);
            List<Pawn> patients = comp.Patients.Keys.Where(p => p != null && p.Spawned).ToList();
            t.Check(patients.Count >= 2, "patients on the map before preparing (" + patients.Count + ")");
            int mark = t.Mark();
            IReadOnlyList<string> report = RwT.K.PrepareRemoval(Sections.Intake);
            foreach (string l in report) t.Note("report: " + l);
            t.Check(report.Count >= 3 && !report.Any(l => l.StartsWith("Error")), "the removal step wrote its report without an error");
            t.Check(!RwT.K.On(Sections.Intake), "patient intake is switched off");
            t.Check(comp.Patients.Count == 0 && comp.pendingOffers.Count == 0, "no patient and no offer left in RimWard's map state");
            t.Check(!map.lordManager.lords.Any(l => l.LordJob is LordJob_VisitColonyAsPatient), "no patient lord left");
            t.Check(patients.Where(p => p.Spawned && !p.Dead).All(p => p.GetLord() != null && p.GetLord().LordJob.GetType().Assembly == typeof(Pawn).Assembly),
                "every former patient still on the map has a vanilla lord");
            t.Check(!Find.LetterStack.LettersListForReading.Any(RemovalT.Ours) && !Find.Archive.ArchivablesListForReading.OfType<Letter>().Any(RemovalT.Ours), "no RimWard letter, open or archived");
            t.Check(!map.listerThings.AllThings.Any(x => x.def.modContentPack == RwT.K.Content), "no thing of RimWard on the map");
            t.Check(!map.mapPawns.AllPawnsSpawned.Any(p => p.needs?.mood?.thoughts?.memories?.Memories?.Any(m => m.def.modContentPack == RwT.K.Content) ?? false), "no RimWard memory");
            yield return 600;
            t.Check(!map.lordManager.lords.Any(l => l.LordJob is LordJob_VisitColonyAsPatient) && comp.Patients.Count == 0, "600 ticks later still nothing (intake off: nobody new came)");
            t.CheckNoRedErrors(mark, "preparing and 600 ticks after it: no red error of ours");
            t.Check(t.SaveGame("rw_prepared"), "saved");
            List<string> named = RemovalT.NamedInSave("rw_prepared");
            t.Check(named.Count == 0, "the prepared save names no class and no def of RimWard (" + string.Join(", ", named) + ")");
        }
    }
}
#endif
