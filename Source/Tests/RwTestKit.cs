#if JB_TESTS
// Test-only helpers for RimWard's in-game suites (compiled only with -p:JBTests=true; never in a release DLL).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using JBFramework;
using JBFramework.Testing;
using RimWard.Utilities;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimWard.Tests
{
    /// <summary>What a test wants the next surgery outcome to be (vanilla rolls it).</summary>
    internal enum ForcedOutcome { None, Success, Failure }

    /// <summary>Test-only: lets a suite decide the outcome of the next operation (no damage on a forced failure).</summary>
    [HarmonyPatch(typeof(SurgeryOutcomeEffectDef), nameof(SurgeryOutcomeEffectDef.GetOutcome))]
    internal static class Test_ForceSurgeryOutcome
    {
        internal static ForcedOutcome Force = ForcedOutcome.None;

        static bool Prefix(ref SurgeryOutcome __result)
        {
            if (Force == ForcedOutcome.None) return true;
            __result = Force == ForcedOutcome.Failure ? new HarmlessFailure() : null;
            return false;
        }
    }

    /// <summary>A failed operation without any damage (the forced-failure outcome of the tests).</summary>
    internal sealed class HarmlessFailure : SurgeryOutcome
    {
        public HarmlessFailure() { this.failure = true; }
        public override bool Apply(float quality, RecipeDef recipe, Pawn surgeon, Pawn patient, BodyPartRecord part) => true;
    }

    internal static class RwT
    {
        internal static readonly List<Thing> Spawned = new List<Thing>();

        internal static HospitalMapComponent Comp(Map m) => HospitalMapComponent.For(m);
        internal static KitMod K => HospitalMod.K;
        internal static Settings S => HospitalMod.Settings;
        internal static int Now => Find.TickManager.TicksGame;
        internal static string LoadedSave => KitTestEnv.Get(KitTestEnv.Load);

        /// <summary>A hospital ready for patients: open all day, surgery accepted, beds, a doctor with Medicine 8, medicine and one bionic arm.</summary>
        internal static Pawn SetUp(TestContext t, int beds, int surgeryBeds)
        {
            IncidentScheduler.PausedForTests = true;
            Map map = t.Map;
            HospitalPolicy pol = HospitalGameComponent.Get.Policy;
            Comp(map).SetOwnPolicy(false);
            pol.open = true;
            pol.Repair();
            for (int i = 0; i < 24; i++) pol.openingHours[i] = true;
            pol.acceptSurgery = true; pol.acceptDanger = false; pol.massCasualties = false;
            pol.refusedOperations.Clear();
            for (int i = 0; i < beds; i++) SpawnBed(map, i < surgeryBeds);
            Pawn doc = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(c => !c.WorkTypeIsDisabled(WorkTypeDefOf.Doctor));
            if (doc != null)
            {
                doc.skills.GetSkill(SkillDefOf.Medicine).Level = 8;
                doc.workSettings?.SetPriority(WorkTypeDefOf.Doctor, 1);
            }
            Place(ThingDefOf.MedicineIndustrial, 20, map);
            ThingDef arm = DefDatabase<ThingDef>.GetNamedSilentFail("BionicArm");
            if (arm != null) Place(arm, 1, map);
            Threats.ClearCache();
            return doc;
        }

        internal static Building_Bed SpawnBed(Map map, bool surgery)
        {
            if (!TestCells.TryFindFreeRect(map, 3, 4, out CellRect r)) return null;
            Thing bed = ThingMaker.MakeThing(ThingDefOf.Bed, ThingDefOf.WoodLog);
            bed.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(bed, r.CenterCell, map, Rot4.North);
            Spawned.Add(bed);
            Building_Bed b = (Building_Bed)bed;
            b.Medical = true;
            CompHospitalBed c = b.TryGetComp<CompHospitalBed>();
            if (c != null) { c.Hospital = true; if (surgery) c.Surgery = true; }
            return b;
        }

        internal static Thing Place(ThingDef def, int count, Map map)
        {
            Thing th = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
            th.stackCount = count;
            GenPlace.TryPlaceThing(th, TestCells.Anchor(map), map, ThingPlaceMode.Near, out Thing placed);
            Spawned.Add(placed ?? th);
            return placed ?? th;
        }

        internal static Faction PatientFaction()
            => Find.FactionManager.AllFactionsListForReading.Where(f => !f.IsPlayer && !f.defeated && !f.def.hidden && f.def.humanlikeFaction
                && !f.HostileTo(Faction.OfPlayer) && !f.def.defName.ToUpper().Contains("VREA")).OrderBy(f => f.loadID).FirstOrDefault();

        /// <summary>A new patient built by the same steps as the arrival incident (pawn, guest status, case, optional complications).</summary>
        internal static Pawn NewPatient(Map map, PatientType type, out PatientData data, bool spawn, bool complications)
        {
            data = null;
            HospitalMapComponent comp = Comp(map);
            Faction fac = PatientFaction();
            if (fac == null) return null;
            Pawn p = IncidentHelper.GenerateValidPatientPawn(fac);
            if (p == null) return null;
            IncidentHelper.SetUpNewPatient(p);
            data = new PatientData(Now, p.MarketValue, p.needs?.mood?.CurLevel ?? 0.5f, type);
            PatientUtility.DamagePawn(p, data, comp);
            if (complications) PatientComplexityGenerator.ApplyComplexity(p, data, comp);
            if (spawn) Admit(map, p, data);
            return p;
        }

        internal static void Admit(Map map, Pawn p, PatientData data)
        {
            HospitalMapComponent comp = Comp(map);
            IntVec3 cell = CellFinder.StandableCellNear(TestCells.Anchor(map), map, 12f);
            GenSpawn.Spawn(p, cell.IsValid ? cell : TestCells.Anchor(map), map);
            comp.PatientArrived(p, data);
            p.jobs?.StopAll();
            LordMaker.MakeNewLord(p.Faction, new LordJob_VisitColonyAsPatient(p.Faction), map, new List<Pawn> { p });
        }

        internal static void Discard(Pawn p)
        {
            if (p == null || p.Discarded) return;
            p.GetLord()?.RemovePawn(p);
            IncidentHelper.DiscardGenerated(p);
        }

        /// <summary>Removes every patient, offer and thing the suites made, and switches the scheduler back on.</summary>
        internal static void CleanUp(Map map)
        {
            HospitalMapComponent comp = Comp(map);
            if (comp != null)
            {
                foreach (Pawn p in comp.Patients.Keys.ToList()) { comp.ForgetPatient(p); Discard(p); }
                foreach (PatientOffer o in comp.pendingOffers.ToList()) { comp.pendingOffers.Remove(o); if (o.kind == OfferKind.DropPod) Discard(o.pawn); }
            }
            foreach (Thing th in Spawned) if (th != null && !th.Destroyed) th.Destroy(DestroyMode.Vanish);
            Spawned.Clear();
            foreach (Thing pod in map.listerThings.ThingsInGroup(ThingRequestGroup.ActiveTransporter).ToList())
                if (!pod.Destroyed) pod.Destroy(DestroyMode.Vanish);
            Test_ForceSurgeryOutcome.Force = ForcedOutcome.None;
            IncidentScheduler.PausedForTests = false;
            Threats.ClearCache();
        }

        internal static int SilverOnMap(Map map) => map.listerThings.ThingsOfDef(ThingDefOf.Silver).Sum(s => s.stackCount);

        internal static int LinesOf(PatientData d, InvoiceLineKind kind) => d.Lines.Count(l => l.kind == kind);

        /// <summary>Game log lines (errors, and warnings when asked) containing <paramref name="fragment"/>.</summary>
        internal static List<string> LogLines(string fragment, bool warningsToo)
            => Verse.Log.Messages.Where(m => (m.type == LogMessageType.Error || (warningsToo && m.type == LogMessageType.Warning)) && (m.text ?? "").Contains(fragment))
                .Select(m => m.text).ToList();

        // FW-REQUEST: the harness keeps its save and log helpers internal (request 7); these are small copies for RimWard's suites.
        /// <summary>Saves the running game as <paramref name="name"/> without the test runner component (as the kit does).</summary>
        internal static bool Save(string name)
        {
            List<GameComponent> comps = Current.Game.components;
            List<GameComponent> runners = comps.Where(c => c != null && c.GetType().Assembly == typeof(KitTestSuite).Assembly).ToList();
            try
            {
                foreach (GameComponent r in runners) comps.Remove(r);
                GameDataSaveLoader.SaveGame(name);
            }
            finally { foreach (GameComponent r in runners) if (!comps.Contains(r)) comps.Add(r); }
            return File.Exists(GenFilePaths.FilePathForSavedGame(name));
        }

        internal static string SidePath(string save) => Path.Combine(GenFilePaths.SavedGamesFolderPath, save + ".rw.txt");
        internal static void WriteSide(string save, Dictionary<string, string> v) => File.WriteAllLines(SidePath(save), v.Select(kv => kv.Key + "=" + kv.Value));
        internal static Dictionary<string, string> ReadSide(string save)
        {
            var d = new Dictionary<string, string>();
            if (!File.Exists(SidePath(save))) return null;
            foreach (string l in File.ReadAllLines(SidePath(save))) { int i = l.IndexOf('='); if (i > 0) d[l.Substring(0, i)] = l.Substring(i + 1); }
            return d;
        }

        internal static string Hours(HospitalPolicy p) { p.Repair(); return string.Join("", p.openingHours.Select(b => b ? "1" : "0")); }
    }
}
#endif
