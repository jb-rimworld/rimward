// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Copyright (C) 2026 juliusberger00. Written for RimWard 2.0 (a GPL-3.0 fork of Hospital by Adamas and
// Hospital Fix And Tweak by assssssqwww). Licensed under the GNU General Public License version 3 (LICENSE);
// full source: https://github.com/jb-rimworld/rimward
#if JB_TESTS
// RimWard's Hospital tab suites (M8): rw.tab (fresh colony, readiness fixes, patients in every state, all pages at three screen sizes),
// rw.tabload (two phases: the tab after a save and load) and rw.longrun (three in-game days with real arrivals). JB_TESTS only.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JBFramework;
using JBFramework.Testing;
using RimWard.MainTab;
using RimWard.Utilities;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace RimWard.Tests
{
    internal static class TabT
    {
        internal static bool Inside(Rect inner, Rect outer, float slack = 0.5f)
            => inner.xMin >= outer.xMin - slack && inner.yMin >= outer.yMin - slack && inner.xMax <= outer.xMax + slack && inner.yMax <= outer.yMax + slack;

        internal static MainButtonDef TabDef => DefDatabase<MainButtonDef>.GetNamed("RimWard_Patients");
        internal static MainTabWindow_Hospital Window => Find.WindowStack.WindowOfType<MainTabWindow_Hospital>();

        internal static void Open(string page)
        {
            MainTabWindow_Hospital.SelectedPage = page;
            if (Find.MainTabsRoot.OpenTab != TabDef) Find.MainTabsRoot.SetCurrentTab(TabDef, false);
        }

        internal static void Close()
        {
            if (Find.MainTabsRoot.OpenTab != null) Find.MainTabsRoot.EscapeCurrentTab(false);
        }

        /// <summary>Draws one page for a few frames and checks frame, layout, width and errors. Optional screenshot.</summary>
        internal static IEnumerable<int> CheckPage(TestContext t, string page, string tag, string shot)
        {
            KitMod k = HospitalMod.K;
            int g0 = k.Log.ErrorCount, red0 = t.Mark();
            MainTabWindow_Hospital.SelectedPage = page;
            foreach (int y in t.Frames(3)) yield return y;
            int f0 = Time.frameCount;
            if (shot != null) t.Shot(shot);
            foreach (int y in t.Frames(2)) yield return y;
            MainTabWindow_Hospital w = Window;
            var L = MainTabWindow_Hospital.Last;
            t.Check(w != null && w.IsOpen, tag + " " + page + ": the tab is open");
            t.Check(L.Page == page && L.Frame >= f0 - 1, tag + " " + page + ": drawn (frame " + L.Frame + ")");
            t.Check(k.Log.ErrorCount == g0 && t.RedErrorsSince(red0).Count == 0, tag + " " + page + ": nothing threw" + string.Join("; ", t.RedErrorsSince(red0).Take(2)));
            t.Check(Inside(L.Header, L.In) && Inside(L.List, L.In) && Inside(L.Content, L.In) && L.List.xMax <= L.Content.xMin,
                tag + " " + page + ": header, page list and page inside the window");
            t.Check(L.SettingsButton.width <= 0f || (Inside(L.SettingsButton, L.Header, 1f) && (L.OpenChip.width <= 0f || L.OpenChip.xMax <= L.SettingsButton.xMin)),
                tag + " " + page + ": open switch and settings button in the header, not overlapping");
            t.Check(!L.Overflow, tag + " " + page + ": nothing placed outside the page width" + (L.Overflow ? " (" + L.OverflowWhat + ")" : ""));
            t.Check(L.Content.width >= 560f, tag + " " + page + ": page area wide enough (" + L.Content.width.ToString("0") + " px)");
            if (w != null)
            {
                Rect wr = w.windowRect;
                t.Check(wr.x >= -0.5f && wr.y >= -0.5f && wr.xMax <= UI.screenWidth + 0.5f && wr.yMax <= UI.screenHeight + 0.5f,
                    tag + " " + page + ": window " + wr + " inside the screen " + UI.screenWidth + "x" + UI.screenHeight);
            }
        }

        /// <summary>A closed room (walls and roof) with an inner area of w x h; returns the inner rect. Things go into RwT.Spawned.</summary>
        internal static bool BuildRoom(Map map, int w, int h, out CellRect inner)
        {
            inner = default;
            if (!TestCells.TryFindFreeRect(map, w + 2, h + 2, out CellRect outer)) return false;
            foreach (IntVec3 c in outer.EdgeCells)
            {
                Thing wall = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog);
                wall.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(wall, c, map);
                RwT.Spawned.Add(wall);
            }
            inner = outer.ContractedBy(1);
            foreach (IntVec3 c in outer) map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
            return true;
        }

        internal static Building_Bed BedAt(Map map, IntVec3 cell, bool medical, bool hospital)
        {
            Thing bed = ThingMaker.MakeThing(ThingDefOf.Bed, ThingDefOf.WoodLog);
            bed.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(bed, cell, map, Rot4.North);
            RwT.Spawned.Add(bed);
            Building_Bed b = (Building_Bed)bed;
            b.Medical = medical;
            CompHospitalBed c = b.TryGetComp<CompHospitalBed>();
            if (c != null) c.Hospital = hospital;
            return b;
        }

        internal static ReadinessItem Line(Map map, string key) => HospitalOverview.Readiness(map).FirstOrDefault(i => i.key == key);

        /// <summary>Patients in every status the "why still here" knows (and one pending offer). Returns the pawns made.</summary>
        internal static List<Pawn> EveryState(TestContext t, Map map, out Pawn cured)
        {
            HospitalMap comp = RwT.Comp(map);
            var made = new List<Pawn>();
            Pawn hurt = RwT.NewPatient(map, PatientType.Wounds, out _, true, false); made.Add(hurt);
            Pawn disease = RwT.NewPatient(map, PatientType.Disease, out _, true, false); made.Add(disease);
            Pawn surg = RwT.NewPatient(map, PatientType.Wounds, out PatientData ds, true, false); made.Add(surg);
            BillingSuite.MakeArmCase(surg, ds, out _, out _);
            Pawn retry = RwT.NewPatient(map, PatientType.Wounds, out PatientData dr, true, false); made.Add(retry);
            BillingSuite.MakeArmCase(retry, dr, out _, out _);
            dr.SurgeryAttempts = 1; dr.RetryAfterTick = RwT.Now + 30000;
            Pawn susp = RwT.NewPatient(map, PatientType.Wounds, out PatientData dsu, true, false); made.Add(susp);
            BillingSuite.MakeArmCase(susp, dsu, out Bill_Medical sb, out _);
            if (sb != null) sb.suspended = true;
            Pawn imp = RwT.NewPatient(map, PatientType.Wounds, out PatientData di, true, false); made.Add(imp);
            // refused: the policy no longer allows this operation (another recipe than the arm cases, which must stay allowed)
            if (MakeCase(imp, di, "InstallBionicEye", "BionicEye") && !comp.Policy.refusedOperations.Contains(di.CureRecipe)) comp.Policy.refusedOperations.Add(di.CureRecipe);
            Pawn down = RwT.NewPatient(map, PatientType.Wounds, out _, true, false); made.Add(down);
            down.health.AddHediff(DefDatabase<HediffDef>.GetNamed("Anesthetic"));
            cured = RwT.NewPatient(map, PatientType.Wounds, out PatientData dc, true, false); made.Add(cured);
            cured.health.RemoveAllHediffs(); dc.HasPendingSurgeryBill = false;
            Pawn leaving = RwT.NewPatient(map, PatientType.Wounds, out PatientData dl, true, false); made.Add(leaving);
            leaving.health.RemoveAllHediffs(); dl.HasPendingSurgeryBill = false;
            comp.DismissPatient(leaving);
            Pawn arriving = RwT.NewPatient(map, PatientType.Wounds, out PatientData da, false, false); made.Add(arriving);
            comp.PatientArrived(arriving, da);
            return made;
        }

        /// <summary>A surgery case for an installable part (the part on the map, the bill on the matching body part).</summary>
        internal static bool MakeCase(Pawn p, PatientData d, string recipe, string thing)
        {
            RecipeDef r = DefDatabase<RecipeDef>.GetNamedSilentFail(recipe);
            ThingDef td = DefDatabase<ThingDef>.GetNamedSilentFail(thing);
            p.health.RemoveAllHediffs();
            if (r == null || td == null) return false;
            BodyPartRecord part = r.Worker.GetPartsToApplyOn(p, r).FirstOrDefault();
            if (part == null) return false;
            foreach (Bill b in p.health.surgeryBills.Bills.ToList()) p.health.surgeryBills.Delete(b);
            HealthCardUtility.CreateSurgeryBill(p, r, part, null, false);
            Bill_Medical bill = p.health.surgeryBills.Bills.OfType<Bill_Medical>().FirstOrDefault(b => b.recipe == r);
            d.Type = PatientType.Surgery; d.CureRecipe = r; d.HasPendingSurgeryBill = bill != null; d.RetryAfterTick = -1; d.SurgeryAttempts = 0;
            RwT.Place(td, 1, p.Map);
            return bill != null;
        }

        internal static void CleanUpAll(Map map, IEnumerable<Pawn> made)
        {
            HospitalMap comp = RwT.Comp(map);
            comp.Policy.refusedOperations.Clear();
            foreach (Pawn p in made ?? Enumerable.Empty<Pawn>()) { comp.ForgetPatient(p); RwT.Discard(p); }
            RwT.CleanUp(map);
            OverviewPage.Invalidate();
        }
    }

    /// <summary>
    /// rw.tab (M8): the Hospital tab on a fresh colony without a hospital (checklist shown, no error), every readiness fix in action,
    /// patients in every state and a pending offer, every page at three screen sizes (UI scale 1.5 = 1280x720, 1 = 1920x1080,
    /// 0.75 = 2560x1440 on the virtual screen), with screenshots when JB_UI_SHOTS is set.
    /// </summary>
    sealed class TabSuite : KitTestSuite
    {
        public override string Name => "rw.tab";
        public override int MaxTicks => 40000;

        public override IEnumerable<int> Run(TestContext t)
        {
            Map map = t.Map;
            HospitalMap comp = RwT.Comp(map);
            HospitalSave gc = HospitalSave.Get;
            int red0 = t.Mark(), guard0 = HospitalMod.K.Log.ErrorCount;
            ArrivalScheduler.PausedForTests = true;
            string lang = LanguageDatabase.activeLanguage?.folderName ?? "?";
            string L = lang.StartsWith("German") ? "de" : "en";
            List<Pawn> made = null;
            object[] statsKept = null;
            var doctorPrio = new Dictionary<Pawn, int>();
            try
            {
                // ---- 1. fresh colony: no patient bed at all
                t.Check(HospitalOverview.PatientBeds(map).Count() == 0, "fresh colony: no patient bed yet");
                statsKept = gc.TestClearStats();   // earlier suites in the same game sent patients home and earned silver: a fresh colony has none
                t.Check(gc.IncomeThisQuadrum(Find.WorldGrid.LongLatOf(map.Tile).x) == 0 && gc.recentInvoices.Count == 0 && gc.renown == 0f, "fresh colony: no income, no invoice, no renown on the overview");
                // the welcome card (JB Framework's, with RimWard's pitch and two actions): drawn, translated, closed again
                Window card = t.OpenWelcomeCard();
                foreach (int y in t.Frames(4)) yield return y;
                t.Check(card != null && card.IsOpen, "fresh colony: the welcome card opens (" + card?.GetType().Name + ")");
                t.Check("RimWard.FirstStartPitch".CanTranslate() && "RimWard.Card.openHospital".CanTranslate() && "RimWard.Card.useBeds".CanTranslate(), "its pitch and actions are translated");
                t.Shot("rw_welcome_" + L);
                card?.Close(false);
                foreach (int y in t.Frames(2)) yield return y;
                List<ReadinessItem> ready = HospitalOverview.Readiness(map);
                t.Note("checklist: " + string.Join(" | ", ready.Select(i => (i.ok ? "ok " : "NO ") + i.key + (i.detail != null ? " (" + i.detail + ")" : "") + (i.fixLabel != null ? " [" + i.fixLabel + "]" : "") + (i.pointer != null ? " -> " + i.pointer : ""))));
                ReadinessItem beds = ready.FirstOrDefault(i => i.key == "Beds");
                t.Check(beds != null && !beds.ok && (beds.fix != null || !beds.pointer.NullOrEmpty()), "fresh colony: the Beds line is open and says what to do");
                t.Check(ready.All(i => !i.label.NullOrEmpty() && !i.label.Contains("RimWard.")) && ready.Where(i => !i.ok).All(i => i.fix != null || !i.pointer.NullOrEmpty() || !i.detail.NullOrEmpty()),
                    "every line is translated and every open line has a fix, a pointer or a reason");
                TabT.Open("overview");
                foreach (int y in t.Frames(4)) yield return y;
                foreach (int y in TabT.CheckPage(t, "overview", "fresh", "rw_tab_" + L + "_fresh_overview")) yield return y;
                foreach (int y in TabT.CheckPage(t, "patients", "fresh", "rw_tab_" + L + "_fresh_patients")) yield return y;
                // closed hospital: band and checklist say so, the fix opens it
                HospitalOverview.SetOpen(map, false);
                OverviewPage.Invalidate();
                foreach (int y in TabT.CheckPage(t, "overview", "closed", "rw_tab_" + L + "_closed_overview")) yield return y;
                ReadinessItem open = TabT.Line(map, "Open");
                t.Check(open != null && !open.ok && open.fix != null, "closed: the Open line has a fix (" + open?.fixLabel + ")");
                open?.fix();
                t.Check(comp.Policy.open && TabT.Line(map, "Open").ok, "the Open fix opens the hospital");
                // no opening hours
                for (int i = 0; i < 24; i++) comp.Policy.openingHours[i] = false;
                open = TabT.Line(map, "Open");
                t.Check(open != null && !open.ok && open.fix != null, "no opening hour: the Open line has a fix (" + open?.fixLabel + ")");
                open?.fix();
                t.Check(comp.Policy.OpenHourCount == 24, "the hours fix opens the hospital all day, the default (" + comp.Policy.OpenHourCount + ")");
                // intake area off
                HospitalMod.K.Current.SetSection(Sections.Intake, false);
                open = TabT.Line(map, "Open");
                t.Check(open != null && !open.ok && open.fix != null, "intake area off: the Open line has a fix (" + open?.fixLabel + ")");
                OverviewPage.Invalidate();
                foreach (int y in TabT.CheckPage(t, "overview", "intakeoff", null)) yield return y;
                open?.fix();
                t.Check(HospitalMod.K.On(Sections.Intake), "the intake fix switches the area back on");

                // ---- 2. readiness fixes
                RwT.SetUp(t, 0, 0);
                for (int i = 0; i < 24; i++) comp.Policy.openingHours[i] = true;
                t.Check(TabT.BuildRoom(map, 5, 5, out CellRect room), "a closed room for the bed fixes");
                yield return 5;
                // a medical bed with its hospital switch off → "use all medical beds"
                Building_Bed med = TabT.BedAt(map, room.Min + new IntVec3(0, 0, 1), true, false);
                ReadinessItem bl = TabT.Line(map, "Beds");
                t.Check(bl != null && !bl.ok && bl.fix != null, "a medical bed that takes no patients: fix offered (" + bl?.fixLabel + ")");
                bl?.fix();
                t.Check(med.TryGetComp<CompHospitalBed>().Hospital && TabT.Line(map, "Beds").ok, "the fix makes it a patient bed; the Beds line is done");
                // a spare colonist bed → "make a spare bed medical"
                med.TryGetComp<CompHospitalBed>().Hospital = false; med.Medical = false;
                Building_Bed spare = med;
                bl = TabT.Line(map, "Beds");
                t.Check(bl != null && !bl.ok && bl.fix != null && bl.fixLabel == "RimWard.Fix.SpareBed".Translate(), "only a spare bed: fix \"" + bl?.fixLabel + "\"");
                bl?.fix();
                t.Check(spare.Medical && spare.TryGetComp<CompHospitalBed>().Hospital && TabT.Line(map, "Beds").ok, "the spare bed is now a medical patient bed");
                // surgery bed
                comp.Policy.acceptSurgery = true;
                ReadinessItem sl = TabT.Line(map, "SurgeryBed");
                t.Check(sl != null && !sl.ok && sl.fix != null, "no surgery bed: fix offered (" + sl?.fixLabel + ")");
                sl?.fix();
                t.Check(comp.SurgeryBedCount() == 1 && TabT.Line(map, "SurgeryBed").ok, "the fix marks one patient bed for surgery");
                // doctor
                foreach (Pawn c in map.mapPawns.FreeColonistsSpawned) if (c.workSettings != null && c.workSettings.EverWork) { doctorPrio[c] = c.workSettings.GetPriority(WorkTypeDefOf.Doctor); c.workSettings.Disable(WorkTypeDefOf.Doctor); }
                ReadinessItem dl = TabT.Line(map, "Doctor");
                t.Check(dl != null && !dl.ok && dl.fix != null, "nobody does Doctor work: fix offered (" + dl?.fixLabel + ")");
                dl?.fix();
                t.Check(TabT.Line(map, "Doctor").ok, "the fix gives the best colonist Doctor work");
                foreach (var kv in doctorPrio) kv.Key.workSettings?.SetPriority(WorkTypeDefOf.Doctor, kv.Value);
                doctorPrio.Clear();
                // medicine: everything forbidden
                foreach (Thing m in map.listerThings.ThingsInGroup(ThingRequestGroup.Medicine)) m.SetForbidden(true, false);
                ReadinessItem ml = TabT.Line(map, "Medicine");
                t.Check(ml != null && !ml.ok && ml.fix != null, "all medicine forbidden: fix offered (" + ml?.fixLabel + ")");
                ml?.fix();
                t.Check(TabT.Line(map, "Medicine").ok, "the fix allows the medicine again");
                // landing spot: the fix picks the build designator
                ReadinessItem ll = TabT.Line(map, "Landing");
                t.Check(ll != null && ll.fix != null, "no landing spot placed: fix offered (" + ll?.fixLabel + ")");
                if (ll?.fix != null)
                {
                    ll.fix();
                    yield return 1;
                    Designator_Build des = Find.DesignatorManager.SelectedDesignator as Designator_Build;
                    t.Check(des != null && des.PlacingDef?.defName == "RimWard_PatientLandingSpot", "the fix selects the landing spot for placing (" + des?.PlacingDef?.defName + ")");
                    Find.DesignatorManager.Deselect();
                }
                List<ReadinessItem> after = HospitalOverview.Readiness(map);
                t.Note("checklist after the fixes: " + string.Join(" | ", after.Select(i => (i.ok ? "ok " : "NO ") + i.key)));
                t.Check(after.All(i => i.ok || i.key == "Safe" || i.key == "Landing"), "after the fixes every line is done (danger and drop spot depend on the map)");
                TabT.Open("overview");
                foreach (int y in t.Frames(3)) yield return y;
                OverviewPage.Invalidate();
                foreach (int y in TabT.CheckPage(t, "overview", "ready", "rw_tab_" + L + "_ready_overview")) yield return y;

                // ---- 3. patients in every state, a pending offer, income and a discharge
                // the random quicktest map may hold awake hostiles (an insect hive): they block arrivals and hold cured patients by design
                int cleared = 0;
                foreach (Pawn hp in map.mapPawns.AllPawnsSpawned.Where(x => x.HostileTo(Faction.OfPlayer) && !x.IsPrisoner && x.Faction != null).ToList()) { hp.Destroy(DestroyMode.Vanish); cleared++; }
                if (cleared > 0) { t.Note("removed " + cleared + " hostile pawns of the random map"); Threats.ClearCache(); }
                for (int i = 0; i < 14; i++) RwT.SpawnBed(map, false);
                float modeBefore = RwT.K.Get(Keys.IntakeMode);
                RwT.K.Set(Keys.IntakeMode, (float)(int)IntakeMode.Prompt);
                IncidentDef inc = DefDatabase<IncidentDef>.GetNamed("RimWard_PatientArrives");
                Threats.ClearCache();
                comp.Policy.acceptDanger = true;   // the random test map may hold a dormant hive or other danger
                for (int i = 0; i < 5 && comp.pendingOffers.Count == 0; i++) inc.Worker.TryExecute(new IncidentParms { target = map });
                comp.Policy.acceptDanger = false;
                RwT.K.Set(Keys.IntakeMode, modeBefore);
                t.Check(comp.pendingOffers.Count > 0, "a pending offer for the inbox (" + comp.pendingOffers.Count + "; " + (IncidentHelper.ArrivalBlockReason(map) ?? "no block") + ")");
                made = TabT.EveryState(t, map, out Pawn cured);
                Pawn payer = RwT.NewPatient(map, PatientType.Wounds, out PatientData dp, true, false);
                int q0 = gc.IncomeThisQuadrum(Find.WorldGrid.LongLatOf(map.Tile).x);
                Invoice inv = comp.Discharge(payer, dp, DischargeReason.Cured);
                int q1 = gc.IncomeThisQuadrum(Find.WorldGrid.LongLatOf(map.Tile).x);
                t.Check(inv != null && q1 - q0 == inv.TotalSilver, "a discharge adds its payout to the income of this quadrum (" + q0 + " -> " + q1 + ", invoice " + inv?.TotalSilver + ")");
                made.Add(payer);
                yield return 5;
                var seen = new HashSet<PatientStatus>(comp.Patients.Keys.Select(p => comp.StatusOf(p, out _)));
                t.Note("statuses: " + string.Join(", ", seen));
                foreach (PatientStatus want in new[] { PatientStatus.Arriving, PatientStatus.Acute, PatientStatus.WaitingForSurgery, PatientStatus.SurgeryRetry,
                    PatientStatus.SurgerySuspended, PatientStatus.TreatmentImpossible, PatientStatus.ReadyToLeave, PatientStatus.Leaving })
                    t.Check(seen.Contains(want), "patient in state " + want);
                t.Check(seen.Contains(PatientStatus.NeedsTending) || seen.Contains(PatientStatus.Resting), "a wounded patient needs tending or rests");

                // all pages at three screen sizes
                OverviewPage.Invalidate();
                var screens = new[] { (720, "1280x720"), (1080, "1920x1080"), (1440, "2560x1440") };
                foreach (var (height, tag) in screens)
                {
                    TabT.Close();
                    using (t.ScreenHeight(height))
                    {
                        foreach (int y in t.Frames(4)) yield return y;
                        t.Note(tag + ": virtual screen " + UI.screenWidth + "x" + UI.screenHeight);
                        TabT.Open("overview");
                        foreach (int y in t.Frames(4)) yield return y;
                        MainTabWindow_Hospital w = TabT.Window;
                        t.Check(w != null && w.windowRect.width >= Mathf.Min(1000f, UI.screenWidth) - 0.5f, tag + ": tab size " + w?.windowRect.size + " keeps its minimum width");
                        foreach (string page in MainTabWindow_Hospital.PageIds)
                            foreach (int y in TabT.CheckPage(t, page, tag, "rw_tab_" + L + "_" + tag + "_" + page)) yield return y;
                        TabT.Close();
                    }
                }
                foreach (int y in t.Frames(3)) yield return y;

                // held by danger: an awake hostile mechanoid, shown on the overview (band and checklist)
                if (Faction.OfMechanoids != null)
                {
                    cured = RwT.NewPatient(map, PatientType.Wounds, out PatientData dc2, true, false); made.Add(cured);
                    cured.health.RemoveAllHediffs(); dc2.HasPendingSurgeryBill = false;
                    Pawn mech = PawnGenerator.GeneratePawn(PawnKindDefOf.Mech_Scyther, Faction.OfMechanoids);
                    GenSpawn.Spawn(mech, CellFinder.RandomClosewalkCellNear(TestCells.Anchor(map), map, 30), map);
                    Threats.ClearCache(); OverviewPage.Invalidate();
                    t.Check(comp.StatusOf(cured, out _) == PatientStatus.HeldByDanger, "a cured patient is held by the danger");
                    t.Check(!TabT.Line(map, "Safe").ok, "the Safe line names the danger (" + TabT.Line(map, "Safe").detail + ")");
                    TabT.Open("overview");
                    foreach (int y in TabT.CheckPage(t, "overview", "danger", "rw_tab_" + L + "_danger_overview")) yield return y;
                    foreach (int y in TabT.CheckPage(t, "patients", "danger", "rw_tab_" + L + "_danger_patients")) yield return y;
                    mech.Destroy(DestroyMode.Vanish);
                    Threats.ClearCache();
                }
                // the welcome card's actions: they report what is done and do what is left
                HospitalOverview.SetOpen(map, false);
                t.Check(!HospitalOverview.WelcomeOpenDone(), "welcome card: \"open the hospital\" is open while the hospital is closed");
                HospitalOverview.WelcomeOpen();
                t.Check(HospitalOverview.WelcomeOpenDone() && comp.Policy.open, "and opens it");
                Building_Bed offBed = RwT.SpawnBed(map, false);
                CompHospitalBed offComp = offBed?.TryGetComp<CompHospitalBed>(); if (offComp != null) offComp.Hospital = false;
                t.Check(offBed == null || !HospitalOverview.WelcomeBedsDone(), "welcome card: \"use all medical beds\" is open while a medical bed takes no patients");
                HospitalOverview.WelcomeBeds();
                t.Check(HospitalOverview.WelcomeBedsDone(), "and sets every medical bed to take patients");
                // an operation whose part in stock is forbidden says so (doctors never fetch forbidden things)
                comp.Policy.refusedOperations.Clear();
                ThingDef legDef = ThingDefOf.WoodLog;   // a peg leg is made of wood on the spot
                if (DefDatabase<RecipeDef>.GetNamedSilentFail("InstallPegLeg") != null)
                {
                    foreach (Thing x in map.listerThings.ThingsOfDef(legDef).ToList()) x.SetForbidden(true, false);
                    Pawn fp = RwT.NewPatient(map, PatientType.Wounds, out PatientData fd, true, false); made.Add(fp);
                    bool okCase = TabT.MakeCase(fp, fd, "InstallPegLeg", "WoodLog");
                    RwT.Place(ThingDefOf.WoodLog, 60, map);
                    foreach (Thing x in map.listerThings.ThingsOfDef(legDef).ToList()) x.SetForbidden(true, false);
                    string why = comp.WhyStillHere(fp);
                    t.Check(okCase && comp.StatusOf(fp, out _) == PatientStatus.TreatmentImpossible && why.Contains(legDef.label), "a peg leg case whose wood is all forbidden reads as impossible and names it: " + why);
                    t.Check(SurgeryUtility.OnlyForbiddenIngredient(fd.CureRecipe, map) == legDef, "and new surgery cases skip it");
                    foreach (Thing x in map.listerThings.ThingsOfDef(legDef).ToList()) x.SetForbidden(false, false);
                    t.Check(comp.StatusOf(fp, out _) == PatientStatus.WaitingForSurgery, "allowed again, the case waits for its operation");
                }
                // the alert opens the inbox
                MainTabWindow_Hospital.OpenOn("inbox");
                foreach (int y in TabT.CheckPage(t, "inbox", "alert", null)) yield return y;
            }
            finally
            {
                foreach (var kv in doctorPrio) kv.Key.workSettings?.SetPriority(WorkTypeDefOf.Doctor, kv.Value);
                TabT.Close();
                TabT.CleanUpAll(map, made);
                gc.TestRestoreStats(statsKept);
            }
            t.Check(HospitalMod.K.Log.ErrorCount == guard0, "no fenced failure in the whole suite");
            t.CheckNoRedErrors(red0, "no red error of RimWard or the framework in the whole suite");
        }
    }

    /// <summary>rw.tabload (two phases, explicit): patients, an offer and income survive save and load, and the tab draws every page after the load.</summary>
    sealed class TabLoadSuite : KitTestSuite
    {
        public override string Name => "rw.tabload";
        public override int MaxTicks => 8000;
        public override bool ExplicitOnly => true;
        public override bool KeepsLoadedGame => true;
        const string SaveName = "rw_tab";

        public override IEnumerable<int> Run(TestContext t)
        {
            Map map = t.Map;
            HospitalMap comp = RwT.Comp(map);
            HospitalSave gc = HospitalSave.Get;
            float lon = Find.WorldGrid.LongLatOf(map.Tile).x;
            if (t.LoadedSave == null)
            {
                RwT.SetUp(t, 16, 2);
                for (int i = 0; i < 24; i++) comp.Policy.openingHours[i] = true;
                float modeBefore = RwT.K.Get(Keys.IntakeMode);
                RwT.K.Set(Keys.IntakeMode, (float)(int)IntakeMode.Prompt);
                IncidentDef inc = DefDatabase<IncidentDef>.GetNamed("RimWard_PatientArrives");
                Threats.ClearCache();
                comp.Policy.acceptDanger = true;   // the random test map may hold a dormant hive or other danger
                for (int i = 0; i < 5 && comp.pendingOffers.Count == 0; i++) inc.Worker.TryExecute(new IncidentParms { target = map });
                comp.Policy.acceptDanger = false;
                RwT.K.Set(Keys.IntakeMode, modeBefore);
                t.Check(comp.pendingOffers.Count > 0, "a pending offer before saving (" + (IncidentHelper.ArrivalBlockReason(map) ?? "no block") + ")");
                List<Pawn> made = TabT.EveryState(t, map, out _);
                Pawn payer = RwT.NewPatient(map, PatientType.Wounds, out PatientData dp, true, false);
                comp.Discharge(payer, dp, DischargeReason.Cured);
                TabT.Open("overview");
                foreach (int y in TabT.CheckPage(t, "overview", "before save", null)) yield return y;
                TabT.Close();
                yield return 2;
                var side = new Dictionary<string, string>
                {
                    ["patients"] = comp.Patients.Count.ToString(),
                    ["offers"] = comp.pendingOffers.Count.ToString(),
                    ["income"] = gc.IncomeThisQuadrum(lon).ToString(),
                    ["invoices"] = gc.recentInvoices.Count.ToString(),
                    ["invoiceSum"] = gc.recentInvoices.Sum(i => i.TotalSilver).ToString(),
                    ["statuses"] = string.Join(",", comp.Patients.Keys.Select(p => comp.StatusOf(p, out _)).Distinct().OrderBy(s => s).Select(s => s.ToString())),
                };
                t.Note("before save: " + string.Join(", ", side.Select(kv => kv.Key + "=" + kv.Value)));
                t.Check(t.SaveGame(SaveName), "saved as " + SaveName);
                RwT.WriteSide(SaveName, side);
                yield break;
            }
            var want = RwT.ReadSide(SaveName);
            t.Check(want != null, "phase 2: the side file exists");
            if (want == null) yield break;
            int red0 = 0;
            t.Check(comp.pendingOffers.Count.ToString() == want["offers"], "the offer came back (" + comp.pendingOffers.Count + ")");
            // a cured patient may leave (and pay) in the first ticks after the load: count what was paid since
            int paidSince = gc.recentInvoices.Sum(i => i.TotalSilver) - int.Parse(want["invoiceSum"]);
            t.Check(gc.IncomeThisQuadrum(lon) == int.Parse(want["income"]) + paidSince, "income this quadrum came back (" + gc.IncomeThisQuadrum(lon) + " = " + want["income"] + " saved + " + paidSince + " paid after the load)");
            t.Check(gc.recentInvoices.Count >= int.Parse(want["invoices"]), "the last discharges came back (" + gc.recentInvoices.Count + ", saved " + want["invoices"] + ")");
            t.Note("after load: patients " + comp.Patients.Count + " (was " + want["patients"] + "), statuses " + string.Join(",", comp.Patients.Keys.Select(p => comp.StatusOf(p, out _)).Distinct().OrderBy(s => s)));
            t.Check(comp.Patients.Count > 0 && comp.Patients.Count <= int.Parse(want["patients"]), "the patients came back (an arriving one may have been dropped)");
            TabT.Open("overview");
            foreach (int y in t.Frames(4)) yield return y;
            foreach (string page in MainTabWindow_Hospital.PageIds)
                foreach (int y in TabT.CheckPage(t, page, "after load", "rw_tabload_" + page)) yield return y;
            TabT.Close();
            yield return 1200;
            t.CheckNoRedErrors(red0, "load, tab and 1200 ticks: no red error of RimWard or the framework");
        }
    }

    /// <summary>
    /// rw.longrun (explicit): the "test hook" opens the hospital all day with three patient beds, a doctor and medicine, then the game plays
    /// at least three in-game days with nothing forced: arrivals come from RimWard's own scheduler. Passes when at least one patient was
    /// admitted, treated, billed and has left, with no red error of RimWard or the framework.
    /// </summary>
    sealed class LongRunSuite : KitTestSuite
    {
        public override string Name => "rw.longrun";
        public override int MaxTicks => 5 * GenDate.TicksPerDay;
        public override bool ExplicitOnly => true;

        public override IEnumerable<int> Run(TestContext t)
        {
            Map map = t.Map;
            HospitalMap comp = RwT.Comp(map);
            HospitalSave gc = HospitalSave.Get;
            RwT.SetUp(t, 3, 1);
            ArrivalScheduler.PausedForTests = false;   // the hook only sets the hospital up; arrivals are the real thing
            Place(map);
            var admitted = new HashSet<int>();
            var treatedTended = new HashSet<int>();
            int start = RwT.Now, treated0 = gc.patientsTreated, died0 = gc.patientsDied, inv0 = gc.recentInvoices.Count;
            long silver0 = gc.silverEarned;
            int days = 0;
            while (RwT.Now - start < 3 * GenDate.TicksPerDay || (gc.patientsTreated == treated0 && RwT.Now - start < 4 * GenDate.TicksPerDay + 30000))
            {
                yield return 2500;
                foreach (var kv in comp.Patients)
                {
                    if (kv.Key == null || kv.Value == null) continue;
                    admitted.Add(kv.Key.thingIDNumber);
                    if (kv.Value.Lines.Any(l => l.kind == InvoiceLineKind.Tend || l.kind == InvoiceLineKind.Surgery)) treatedTended.Add(kv.Key.thingIDNumber);
                }
                int d = (RwT.Now - start) / GenDate.TicksPerDay;
                if (d != days)
                {
                    days = d;
                    t.Note("day " + d + ": " + comp.Patients.Count + " patients now, " + admitted.Count + " admitted so far, sent home " + (gc.patientsTreated - treated0)
                        + ", died " + (gc.patientsDied - died0) + ", silver " + (gc.silverEarned - silver0) + ", arrivals: " + (IncidentHelper.ArrivalBlockReason(map) ?? "possible"));
                    Place(map);
                    foreach (var kv in comp.Patients.ToList())
                    {
                        Pawn p = kv.Key; PatientData pd = kv.Value;
                        if (p == null || pd == null) continue;
                        t.Note("  " + p.LabelShort + ": " + pd.Type + " " + pd.Diagnosis + " / " + pd.Cure + ", " + comp.WhyStillHere(p) + ", health " + p.health.summaryHealth.SummaryHealthPercent.ToStringPercent()
                            + ", inBed " + p.InBed() + ", needsTend " + p.health.HasHediffsNeedingTend(false) + ", seekRest " + RimWorld.HealthAIUtility.ShouldSeekMedicalRest(p)
                            + ", bills " + p.health.surgeryBills.Count + ", lord " + (p.GetLord()?.CurLordToil?.GetType().Name ?? "-") + ", job " + (p.CurJobDef?.defName ?? "-")
                            + ", hediffs " + string.Join(";", p.health.hediffSet.hediffs.Where(h => h.Visible).Select(h => h.def.defName + (h.TendableNow() ? "*" : ""))));
                    }
                }
            }
            List<Invoice> paid = gc.recentInvoices.Take(Mathf.Max(0, gc.recentInvoices.Count - inv0 + 10)).Where(i => i.tick >= start).ToList();
            foreach (Invoice i in paid.Take(5)) t.Note("invoice " + i.patientName + ": " + i.reason + ", " + i.lines.Count + " lines, " + i.TotalSilver + " silver");
            t.Note("played " + ((RwT.Now - start) / (float)GenDate.TicksPerDay).ToString("0.0") + " days");
            t.Check(RwT.Now - start >= 3 * GenDate.TicksPerDay, "three in-game days played");
            t.Check(admitted.Count >= 1, "at least one patient admitted by a real arrival (" + admitted.Count + ")");
            t.Check(treatedTended.Count >= 1 || paid.Any(i => i.lines.Any(l => l.kind == InvoiceLineKind.Tend || l.kind == InvoiceLineKind.Surgery)), "at least one patient was treated (tend or surgery on the bill)");
            t.Check(gc.patientsTreated > treated0 && paid.Any(i => i.TotalSilver > 0), "at least one patient was billed and sent home (" + (gc.patientsTreated - treated0) + ", silver " + (gc.silverEarned - silver0) + ")");
            List<Pawn> still = comp.Patients.Keys.Where(p => p != null).ToList();
            t.Check(still.All(p => comp.WhyStillHere(p).Length > 0), "every patient still here has a reason");
            t.CheckNoRedErrors(0, "no red error of RimWard or the framework in the whole run");
            RwT.CleanUp(map);
        }

        /// <summary>Keeps medicine in stock and the colonists fed (the quicktest colony would otherwise starve or run dry, not a RimWard question).</summary>
        static void Place(Map map)
        {
            int med = map.listerThings.ThingsInGroup(ThingRequestGroup.Medicine).Sum(x => x.stackCount);
            if (med < 15) RwT.Place(ThingDefOf.MedicineIndustrial, 15, map);
            if (map.listerThings.ThingsOfDef(ThingDefOf.MealSimple).Sum(x => x.stackCount) < 10) RwT.Place(ThingDefOf.MealSimple, 20, map);
        }
    }
}
#endif
#if JB_TESTS
namespace RimWard.Tests
{
    /// <summary>rw.woodcase (explicit, diagnosis): a peg leg case (wood ingredient) with wood in stock is operated within a day.</summary>
    sealed class WoodCaseSuite : JBFramework.Testing.KitTestSuite
    {
        public override string Name => "rw.woodcase";
        public override int MaxTicks => 70000;
        public override bool ExplicitOnly => true;
        public override System.Collections.Generic.IEnumerable<int> Run(JBFramework.Testing.TestContext t)
        {
            Verse.Map map = t.Map;
            var wood = map.listerThings.ThingsOfDef(RimWorld.ThingDefOf.WoodLog);
            int forb = 0, free = 0;
            foreach (var w in wood) { if (w.IsForbidden(RimWorld.Faction.OfPlayer)) forb += w.stackCount; else free += w.stackCount; }
            t.Note("quicktest wood: " + free + " allowed, " + forb + " forbidden");
            RwT.SetUp(t, 2, 2);
            RwT.Place(RimWorld.ThingDefOf.WoodLog, 60, map);
            HospitalMap comp = RwT.Comp(map);
            Verse.Pawn p = RwT.NewPatient(map, PatientType.Wounds, out PatientData d, true, false);
            bool ok = TabT.MakeCase(p, d, "InstallPegLeg", "WoodLog");
            t.Check(ok, "peg leg case set up (" + d.CureRecipe?.defName + ")");
            int start = RwT.Now;
            for (int i = 0; i < 24 && comp.Patients.ContainsKey(p) && d.HasPendingSurgeryBill; i++)
            {
                yield return 2500;
                var bill = Caretaker.CureBill(p, d);
                t.Note("h" + i + ": " + comp.WhyStillHere(p) + ", inBed " + p.InBed() + ", bill " + (bill != null) + (bill != null ? " suspended " + bill.suspended + " completable " + bill.CompletableEver + " deadline " + bill.ShouldDoNow() : "")
                    + ", doctors: " + string.Join("; ", map.mapPawns.FreeColonistsSpawned.Select(c => c.LabelShort + "=" + (c.CurJobDef?.defName ?? "-") + "/" + c.workSettings.GetPriority(RimWorld.WorkTypeDefOf.Doctor))));
            }
            t.Check(!d.HasPendingSurgeryBill || !comp.Patients.ContainsKey(p), "the peg leg was installed within a day (" + (RwT.Now - start) + " ticks)");
            RwT.CleanUp(map);
        }
    }
}
#endif
