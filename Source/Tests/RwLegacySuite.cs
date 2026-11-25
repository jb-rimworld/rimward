// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Copyright (C) 2026 juliusberger00. Written for RimWard 2.0 (a GPL-3.0 fork of Hospital by Adamas and
// Hospital Fix And Tweak by assssssqwww). Licensed under the GNU General Public License version 3 (LICENSE);
// full source: https://github.com/jb-rimworld/rimward
#if JB_TESTS
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JBFramework.Testing;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimWard.Tests
{
    /// <summary>
    /// rw.legacy (two phases, explicit): every piece of RimWard's per-save and per-map state survives a save and load. Phase 1 builds the
    /// state (save stats, invoices, payouts, the save policy, a map's own policy, patients mid-surgery and dismissed, an offer, the weekly
    /// mass-casualty flag) and saves "rw_legacy" with a side file of the expected values. With RW_LEGACY_FROM=&lt;folder&gt; phase 1 instead
    /// copies rw_legacy.rws and its side file from that folder (a save written by an OLDER build), so phase 2 proves that the old layout loads.
    /// </summary>
    sealed class LegacySuite : KitTestSuite
    {
        public override string Name => "rw.legacy";
        public override int MaxTicks => 6000;
        public override bool ExplicitOnly => true;
        public override bool KeepsLoadedGame => true;
        const string SaveName = "rw_legacy";

        static bool Mce(Map m) => HospitalMap.For(m).mceTriggered;
        static void SetMce(Map m, bool v) => HospitalMap.For(m).mceTriggered = v;

        public override IEnumerable<int> Run(TestContext t)
        {
            Map map = t.Map;
            if (t.LoadedSave == null)
            {
                string from = System.Environment.GetEnvironmentVariable("RW_LEGACY_FROM");
                if (!from.NullOrEmpty())
                {
                    string src = Path.Combine(from, SaveName + ".rws"), sideSrc = Path.Combine(from, SaveName + ".rw.txt");
                    t.Check(File.Exists(src) && File.Exists(sideSrc), "the older build's save and side file exist in " + from);
                    if (!File.Exists(src) || !File.Exists(sideSrc)) yield break;
                    File.Copy(src, GenFilePaths.FilePathForSavedGame(SaveName), true);
                    File.Copy(sideSrc, RwT.SidePath(SaveName), true);
                    File.SetLastWriteTimeUtc(GenFilePaths.FilePathForSavedGame(SaveName), System.DateTime.UtcNow.AddMinutes(1));
                    t.Note("copied the older build's save for phase 2");
                    yield break;
                }
                RwT.SetUp(t, 5, 1);
                HospitalMap comp = RwT.Comp(map);
                HospitalSave gc = HospitalSave.Get;
                HospitalPolicy pol = gc.Policy;
                for (int i = 0; i < 24; i++) pol.openingHours[i] = i % 3 != 0;
                RecipeDef refused = DefDatabase<RecipeDef>.GetNamedSilentFail("InstallBionicLeg");
                pol.refusedOperations.Clear(); if (refused != null) pol.refusedOperations.Add(refused);
                pol.medCare = MedicalCareCategory.HerbalOrWorse;
                // stats, invoices and payouts
                gc.renown = 123.5f; gc.patientsTreated = 7; gc.patientsDied = 2; gc.patientsDiedAtFault = 1; gc.patientsDischargedEarly = 3; gc.silverEarned = 4321;
                gc.RecordPayout(250); gc.RecordPayout(111);
                Invoice inv = new Invoice { patientName = "Legacy Test", factionName = "Test", reason = DischargeReason.Cured, subtotal = 99f, total = 98.5f, tick = RwT.Now };
                inv.lines.Add(new InvoiceLine { kind = InvoiceLineKind.Tend, detail = "x", amount = 99f, tick = RwT.Now });
                gc.RecordInvoice(inv);
                // an offer
                float modeBefore = RwT.K.Get(Keys.IntakeMode);
                RwT.K.Set(Keys.IntakeMode, (float)(int)IntakeMode.Prompt);
                IncidentDef incDef = DefDatabase<IncidentDef>.GetNamed("RimWard_PatientArrives");
                for (int i = 0; i < 5 && comp.pendingOffers.Count == 0; i++) incDef.Worker.TryExecute(new IncidentParms { target = map });
                RwT.K.Set(Keys.IntakeMode, modeBefore);
                if (comp.pendingOffers.Count == 0)
                {
                    Pawn op = RwT.NewPatient(map, PatientType.Wounds, out PatientData od, false, false);
                    if (op != null) comp.CreateDropPodOffer(op, od, op.Faction);
                }
                // patients
                Pawn surg = RwT.NewPatient(map, PatientType.Wounds, out PatientData ds, true, false);
                bool arm = BillingSuite.MakeArmCase(surg, ds, out _, out _);
                ds.SurgeryAttempts = 1; ds.RetryAfterTick = RwT.Now + 5000; ds.AddLine(InvoiceLineKind.Tend, "legacy", 33f);
                Pawn dis = RwT.NewPatient(map, PatientType.Disease, out PatientData dd, true, false);
                comp.DismissPatient(dis);
                // this map's own policy (differs from the save's) and the weekly mass-casualty flag
                comp.SetOwnPolicy(true);
                comp.Policy.openingHours[0] = true; comp.Policy.openingHours[1] = false; comp.Policy.acceptDanger = true;
                SetMce(map, true);
                yield return 1;
                var side = new Dictionary<string, string>
                {
                    ["renown"] = gc.renown.ToString("0.0"), ["stats"] = gc.patientsTreated + "/" + gc.patientsDied + "/" + gc.patientsDiedAtFault + "/" + gc.patientsDischargedEarly + "/" + gc.silverEarned,
                    ["income"] = gc.IncomeThisQuadrum(Find.WorldGrid.LongLatOf(map.Tile).x).ToString(), ["invoices"] = gc.recentInvoices.Count.ToString(),
                    ["invoice0"] = gc.recentInvoices[0].patientName + "|" + gc.recentInvoices[0].TotalSilver + "|" + gc.recentInvoices[0].lines.Count,
                    ["hours"] = RwT.Hours(pol), ["refused"] = refused?.defName ?? "", ["medCare"] = pol.medCare.ToString(), ["open"] = pol.open.ToString(),
                    ["mapHours"] = RwT.Hours(comp.Policy), ["mapDanger"] = comp.Policy.acceptDanger.ToString(),
                    ["offers"] = comp.pendingOffers.Count.ToString(), ["arm"] = arm.ToString(),
                    ["surg"] = surg.ThingID, ["surgLines"] = ds.Lines.Count.ToString(), ["surgRetry"] = ds.RetryAfterTick.ToString(), ["surgRecipe"] = ds.CureRecipe?.defName ?? "",
                    ["surgBill"] = ds.Bill.ToString("0.0"), ["dis"] = dis.ThingID, ["patients"] = comp.Patients.Count.ToString(), ["mce"] = Mce(map).ToString(),
                };
                t.Check(comp.Patients.Count >= 2 && comp.pendingOffers.Count >= 1 && comp.HasOwnPolicy && Mce(map), "the state to save is built (" + comp.Patients.Count + " patients, " + comp.pendingOffers.Count + " offers)");
                t.Check(t.SaveGame(SaveName), "saved as " + SaveName);
                RwT.WriteSide(SaveName, side);
                yield break;
            }
            // ---- phase 2
            var want = RwT.ReadSide(SaveName);
            t.Check(want != null && t.LoadedSave == SaveName, "phase 2: loaded " + t.LoadedSave + " with its side file");
            if (want == null) yield break;
            HospitalSave g2 = HospitalSave.Get;
            HospitalMap c2 = RwT.Comp(map);
            t.Check(g2 != null && c2 != null, "the hospital's save and map state exist");
            if (g2 == null || c2 == null) yield break;
            t.Check(RwT.LogLines("Null key", false).Count == 0 && t.RedErrorsSince(0).Count == 0, "no RimWard error and no \"Null key\" while loading");
            t.Check(!Current.Game.components.Any(c => c is HospitalGameComponent) && !map.components.Any(c => c is HospitalMapComponent || c is IncidentScheduler),
                "no legacy component left in the running game after the load");
            t.Check(g2.renown.ToString("0.0") == want["renown"], "renown " + g2.renown.ToString("0.0") + " (want " + want["renown"] + ")");
            string stats = g2.patientsTreated + "/" + g2.patientsDied + "/" + g2.patientsDiedAtFault + "/" + g2.patientsDischargedEarly + "/" + g2.silverEarned;
            t.Check(stats == want["stats"], "lifetime stats " + stats + " (want " + want["stats"] + ")");
            t.Check(g2.IncomeThisQuadrum(Find.WorldGrid.LongLatOf(map.Tile).x).ToString() == want["income"], "income this quadrum " + want["income"]);
            t.Check(g2.recentInvoices.Count.ToString() == want["invoices"] && g2.recentInvoices.Count > 0
                && g2.recentInvoices[0].patientName + "|" + g2.recentInvoices[0].TotalSilver + "|" + g2.recentInvoices[0].lines.Count == want["invoice0"], "the last discharges came back");
            HospitalPolicy p2 = g2.Policy;
            t.Check(RwT.Hours(p2) == want["hours"] && p2.open.ToString() == want["open"] && p2.medCare.ToString() == want["medCare"]
                && (want["refused"] == "" || p2.refusedOperations.Any(r => r.defName == want["refused"])), "the save's policy came back (hours, open, medicine, refused operation)");
            t.Check(c2.HasOwnPolicy && RwT.Hours(c2.Policy) == want["mapHours"] && c2.Policy.acceptDanger.ToString() == want["mapDanger"], "the map's own policy came back");
            t.Check(c2.pendingOffers.Count.ToString() == want["offers"] && c2.pendingOffers.All(o => o.pawn != null && o.data != null), "the pending offers came back (" + c2.pendingOffers.Count + ")");
            t.Check(c2.Patients.Count.ToString() == want["patients"] && c2.Patients.Keys.All(p => p != null && !p.Discarded), "the patient list came back (" + c2.Patients.Count + ")");
            Pawn s2 = c2.Patients.Keys.FirstOrDefault(p => p.ThingID == want["surg"]);
            t.Check(s2 != null && c2.GetPatientData(s2, out PatientData sd) && sd.Lines.Count.ToString() == want["surgLines"] && sd.RetryAfterTick.ToString() == want["surgRetry"]
                && (sd.CureRecipe?.defName ?? "") == want["surgRecipe"] && sd.Bill.ToString("0.0") == want["surgBill"], "the patient mid-surgery came back with lines, retry tick, cure and bill");
            t.Check(s2 != null && s2.GetLord()?.LordJob is LordJob_VisitColonyAsPatient, "the patient mid-surgery is still lorded as a patient");
            Pawn d2 = c2.Patients.Keys.FirstOrDefault(p => p.ThingID == want["dis"]);
            t.Check(d2 != null && c2.GetPatientData(d2, out PatientData dd2) && dd2.Dismissed, "the dismissed patient is still dismissed");
            yield return 600;
            t.Check(t.RedErrorsSince(0).Count == 0, "600 ticks after the load: no RimWard error");
            // the build under test writes the save again: no component class of RimWard in it, the data in the framework's nodes
            t.Check(t.SaveGame("rw_legacy_resaved"), "saved again as rw_legacy_resaved");
            string xml = File.ReadAllText(GenFilePaths.FilePathForSavedGame("rw_legacy_resaved"));
            string[] comps = { "RimWard.HospitalGameComponent", "RimWard.HospitalMapComponent", "RimWard.IncidentScheduler" };
            t.Check(!comps.Any(xml.Contains), "the new save names no component class of RimWard");
            t.Check(xml.Contains("<id>juliusberger.rimward</id>") && xml.Contains("<renown>") && xml.Contains("<patients>"), "RimWard's state sits in the framework's save and map nodes");
        }
    }
}
#endif
