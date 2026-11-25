// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Copyright (C) 2026 juliusberger00. Written for RimWard 2.0 (a GPL-3.0 fork of Hospital by Adamas and
// Hospital Fix And Tweak by assssssqwww). Licensed under the GNU General Public License version 3 (LICENSE);
// full source: https://github.com/jb-rimworld/rimward
#if JB_TESTS
// RimWard's in-game suites (rw.*), the 20 checks owed by the logic wave (handoff/RimWard.md section 4) plus the area switches.
// Compiled only with -p:JBTests=true (jbmod test builds); never part of a release DLL.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using JBFramework;
using JBFramework.Testing;
using RimWard.Utilities;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimWard.Tests
{
    /// <summary>rw.smoke (case 20, 14a): patches all applied, defaults as registered, a new colony starts open, no RimWard warning in the log.</summary>
    sealed class SmokeSuite : KitTestSuite
    {
        public override string Name => "rw.smoke";
        public override int MaxTicks => 3000;
        public override IEnumerable<int> Run(TestContext t)
        {
            KitMod k = RwT.K;
            t.Check(k != null && !k.Blocked, "RimWard's framework context exists and is not blocked");
            t.Check(k != null && k.PatchFailures.Count == 0, "every Harmony patch class applied (" + (k == null ? "-" : string.Join("; ", k.PatchFailures)) + ")");
            t.Check(RwT.S != null, "the settings class is RimWard.Settings");
            HospitalSave gc = HospitalSave.Get;
            t.Check(gc != null && !gc.LoadedFromSave, "a new game has the hospital game component");
            t.Check(gc != null && gc.Policy.open == RwT.S.OpenInNewColonies, "case 14: a new colony starts with the hospital " + (gc != null && gc.Policy.open ? "open" : "closed") + " (setting: " + RwT.S.OpenInNewColonies + ")");
            t.Check(RwT.S.Payment == PaymentPreset.BreakEven && Mathf.Approximately(RwT.S.Difficulty, 1f) && RwT.S.ChargePatients, "defaults: break-even, difficulty 1, patients pay");
            t.Check(!RwT.S.CriticalCasesOn, "Luciferium critical cases are off by default (and absent without Luciferium Overhaul)");
            t.Check(k.On(Sections.Intake) && k.On(Sections.Surgery) && k.On(Sections.Billing) && k.On(Sections.Malpractice), "core areas are on by default");
            yield return 2500;
            List<string> ours = RwT.LogLines("[RimWard]", true).Concat(RwT.LogLines("RimWard.", false)).Distinct().ToList();
            foreach (string l in ours.Take(5)) t.Note("log: " + l.Split('\n')[0]);
            t.Check(ours.Count == 0, "case 20: no RimWard warning or error in the game log after 2500 ticks");
        }
    }

    /// <summary>rw.arrival (case 1): surgery, disease and wound cases are generated without error; surgery cases get a bill on a matching part; real arrivals land.</summary>
    sealed class ArrivalSuite : KitTestSuite
    {
        public override string Name => "rw.arrival";
        public override int MaxTicks => 6000;
        public override IEnumerable<int> Run(TestContext t)
        {
            Map map = t.Map;
            RwT.SetUp(t, 3, 1);
            HospitalMap comp = RwT.Comp(map);
            int ok = 0, fallback = 0, wrongPart = 0, noBill = 0, errors = 0;
            var recipes = new Dictionary<string, int>();
            for (int i = 0; i < 30; i++)
            {
                Pawn p = null;
                try
                {
                    p = RwT.NewPatient(map, PatientType.Surgery, out PatientData d, false, false);
                    if (p == null) { errors++; continue; }
                    if (d.Type != PatientType.Surgery) { fallback++; continue; }
                    Bill_Medical bill = Caretaker.CureBill(p, d);
                    if (bill == null) { noBill++; continue; }
                    recipes[d.CureRecipe.defName] = recipes.TryGetValue(d.CureRecipe.defName, out int n) ? n + 1 : 1;
                    if (d.CureRecipe.targetsBodyPart)
                    {
                        List<BodyPartRecord> valid = d.CureRecipe.Worker.GetPartsToApplyOn(p, d.CureRecipe).ToList();
                        if (bill.Part == null || !valid.Contains(bill.Part)) { wrongPart++; t.Note("part " + bill.Part?.def?.defName + " not valid for " + d.CureRecipe.defName); continue; }
                    }
                    ok++;
                }
                catch (Exception e) { errors++; t.Note("generation threw: " + e.Message); }
                finally { RwT.Discard(p); }
            }
            t.Note("surgery cases: " + ok + " with a bill, " + fallback + " fell back to wounds; recipes " + string.Join(", ", recipes.Select(kv => kv.Key + "×" + kv.Value)));
            t.Check(errors == 0, "30 surgery cases generated without an exception (the old NRE on the unspawned pawn)");
            t.Check(ok >= 10, "at least 10 of 30 surgery rolls became real surgery cases (" + ok + ")");
            t.Check(wrongPart == 0 && noBill == 0, "every surgery case has its bill on a part the recipe accepts (wrong part " + wrongPart + ", no bill " + noBill + ")");

            // parts by defName and tag: every fixed-part recipe lands on one of its parts, the bionic ear on an ear (language-independent)
            Pawn probe = RwT.NewPatient(map, PatientType.Wounds, out _, false, false);
            int fixedChecked = 0, fixedBad = 0;
            foreach (RecipeDef r in probe.def.AllRecipes.Where(r => r.appliedOnFixedBodyParts != null && r.appliedOnFixedBodyParts.Count > 0 && SurgeryUtility.IsCandidate(r, null)))
            {
                BodyPartRecord part = SurgeryUtility.ChoosePart(probe, r);
                fixedChecked++;
                if (part == null || !r.appliedOnFixedBodyParts.Contains(part.def)) { fixedBad++; t.Note("fixed part wrong for " + r.defName + ": " + part?.def?.defName); }
            }
            t.Check(fixedChecked > 0 && fixedBad == 0, "fixed-part operations choose one of their parts (" + fixedChecked + " recipes, language " + LanguageDatabase.activeLanguage?.folderName + ")");
            RecipeDef ear = DefDatabase<RecipeDef>.GetNamedSilentFail("InstallBionicEar");
            if (ear != null) t.Check(SurgeryUtility.ChoosePart(probe, ear)?.def.defName == "Ear", "the bionic ear goes on an ear");
            BodyPartRecord deaf = PartFinder.ForKeyword(probe, "Deafness");
            t.Check(deaf != null && (deaf.def.defName == "Ear" || deaf.def.tags.Any(tg => tg.defName == "HearingSource")), "a deafness case is placed on an ear (" + deaf?.def?.defName + ")");
            RwT.Discard(probe);

            int dz = 0, wz = 0;
            for (int i = 0; i < 10; i++)
            {
                Pawn a = RwT.NewPatient(map, PatientType.Disease, out PatientData da, false, false);
                if (a != null && !da.Diagnosis.NullOrEmpty()) dz++;
                RwT.Discard(a);
                Pawn b = RwT.NewPatient(map, PatientType.Wounds, out PatientData db, false, false);
                if (b != null && (b.health.hediffSet.GetInjuredParts().Any() || !db.Diagnosis.NullOrEmpty())) wz++;
                RwT.Discard(b);
            }
            t.Check(dz == 10 && wz == 10, "disease and wound cases get a diagnosis (" + dz + "/10, " + wz + "/10)");

            // real arrivals through the incident (Firehose: drop pod, letter)
            float oldMode = RwT.K.Get(Keys.IntakeMode);
            RwT.K.Set(Keys.IntakeMode, (float)(int)IntakeMode.Firehose);
            IncidentDef inc = DefDatabase<IncidentDef>.GetNamed("RimWard_PatientArrives");
            int before = comp.Patients.Count, fired = 0;
            for (int i = 0; i < 3; i++)
            {
                Threats.ClearCache();
                string block = IncidentHelper.ArrivalBlockReason(map);
                if (block != null) { t.Note("arrival blocked: " + block); break; }
                if (inc.Worker.TryExecute(new IncidentParms { target = map })) fired++;
            }
            t.Check(fired == 3 && comp.Patients.Count == before + 3, "three real arrivals through the incident (" + fired + ")");
            yield return 400;
            List<Pawn> arrived = comp.ActivePatientsList().ToList();
            t.Check(arrived.Count >= 3 && arrived.All(p => p.Spawned), "the arrivals left their drop pods (" + arrived.Count(p => p.Spawned) + "/" + arrived.Count + ")");
            foreach (Pawn p in arrived) if (comp.GetPatientData(p, out PatientData ad)) t.Note(p.LabelShort + ": " + ad.Type + ", " + ad.Diagnosis + ", lines " + ad.Lines.Count + ", " + comp.WhyStillHere(p));
            t.Check(arrived.All(p => comp.GetPatientData(p, out PatientData d) && !d.Diagnosis.NullOrEmpty()), "every arrival has a diagnosis");
            t.Check(arrived.All(p => p.GetLord()?.LordJob is LordJob_VisitColonyAsPatient), "every arrival is lorded as a patient");
            t.Check(IncidentHelper.ArrivalBlockReason(map) == "RimWard.Block.Full".Translate(), "with three patients in three beds the hospital reports itself full");
            RwT.K.Set(Keys.IntakeMode, oldMode);
            RwT.CleanUp(map);
        }
    }

    /// <summary>rw.offer (case 2): prompt mode letter shows the estimate and the stock it uses; accepting with all beds full is refused and the offer stays.</summary>
    sealed class OfferSuite : KitTestSuite
    {
        public override string Name => "rw.offer";
        public override int MaxTicks => 3000;
        public override IEnumerable<int> Run(TestContext t)
        {
            Map map = t.Map;
            RwT.SetUp(t, 1, 1);
            HospitalMap comp = RwT.Comp(map);
            float oldMode = RwT.K.Get(Keys.IntakeMode);
            RwT.K.Set(Keys.IntakeMode, (float)(int)IntakeMode.Prompt);
            IncidentDef inc = DefDatabase<IncidentDef>.GetNamed("RimWard_PatientArrives");
            PatientOffer surgeryOffer = null;
            // a surgery case that uses stock is random: 12 rolls missed it in 3 of about 35 recorded runs; up to 40 rolls (no ticks pass,
            // the loop stops at the first one)
            int rolls = 0;
            for (int i = 0; i < 40 && surgeryOffer == null; i++, rolls++)
            {
                inc.Worker.TryExecute(new IncidentParms { target = map });
                surgeryOffer = comp.pendingOffers.FirstOrDefault(o => o.data.Type == PatientType.Surgery && o.data.CureRecipe?.ingredients?.Count > 0);
            }
            t.Check(comp.pendingOffers.Count > 0, "prompt mode makes offers instead of arrivals (" + comp.pendingOffers.Count + ")");
            t.Check(surgeryOffer != null, "a surgery offer that uses something from stock came up (" + rolls + " of at most 40 rolls)");
            if (surgeryOffer != null)
            {
                ChoiceLetter_PatientOffer letter = Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_PatientOffer>().FirstOrDefault(l => l.offerId == surgeryOffer.id);
                string text = letter?.Text.ToString() ?? "";
                int est = Invoice.Build(surgeryOffer.pawn, surgeryOffer.data, DischargeReason.Estimate).TotalSilver;
                string stockWord = "RimWard.Offer.UsesStock".Translate("§", "§").ToString().Split('§')[0].Trim();
                t.Check(letter != null, "the offer has its letter");
                t.Check(text.Contains(est.ToString()), "the letter shows the estimate (" + est + ")");
                t.Check(stockWord.Length > 0 && text.Contains(stockWord), "the letter says what the operation uses from your stock");

                // fill every bed, accept: refused, offer and letter stay
                Pawn filler = RwT.NewPatient(map, PatientType.Wounds, out PatientData fd, true, false);
                t.Check(comp.IsFull(), "the only bed is taken by another patient");
                bool accepted = comp.ResolveOffer(surgeryOffer.id, true);
                t.Check(!accepted && comp.pendingOffers.Contains(surgeryOffer), "accepting with all beds full is refused and the offer stays");
                t.Check(Find.LetterStack.LettersListForReading.Contains(letter), "the letter stays");
                comp.ForgetPatient(filler); RwT.Discard(filler);
                accepted = comp.ResolveOffer(surgeryOffer.id, true);
                t.Check(accepted && !comp.pendingOffers.Contains(surgeryOffer) && comp.Patients.ContainsKey(surgeryOffer.pawn), "with a free bed the same offer is accepted and the patient admitted");
                yield return 300;
                t.Check(surgeryOffer.pawn.Spawned, "the accepted patient arrives by drop pod");
            }
            // declining is free: the held pawn is discarded, goodwill unchanged
            PatientOffer other = comp.pendingOffers.FirstOrDefault();
            if (other != null)
            {
                int gw = other.faction?.PlayerGoodwill ?? 0;
                comp.ResolveOffer(other.id, false);
                t.Note("declined: kind " + other.kind + ", discarded " + other.pawn.Discarded + ", world pawn " + other.pawn.IsWorldPawn() + ", goodwill " + gw + " → " + (other.faction?.PlayerGoodwill ?? 0));
                t.Check(other.pawn.Discarded && (other.faction?.PlayerGoodwill ?? 0) == gw, "declining discards the held patient and costs no goodwill");
            }
            RwT.K.Set(Keys.IntakeMode, oldMode);
            RwT.CleanUp(map);
            yield return 1;
        }
    }

    /// <summary>rw.complications (case 3): 200 patients at the highest difficulty never get a dangerous addiction, carcinoma or a rage-causing illness; xenotypes are the faction's.</summary>
    sealed class ComplicationsSuite : KitTestSuite
    {
        public override string Name => "rw.complications";

        static readonly HashSet<string> DangerousAddictions = new HashSet<string>
            { "LuciferiumAddiction", "GoJuiceAddiction", "PsychiteAddiction", "AlcoholAddiction", "SmokeleafAddiction", "WakeUpAddiction" };

        static bool RawCanKill(HediffDef def)
            => def.lethalSeverity > 0f || (def.stages != null && def.stages.Any(st => st != null && (st.lifeThreatening || st.deathMtbDays > 0f)));

        static bool RawAggro(HediffDef def)
            => def.stages != null && def.stages.Any(st => st?.mentalStateGivers != null && st.mentalStateGivers.Any(g => g?.mentalState != null && g.mentalState.IsAggro));
        public override int MaxTicks => 3000;
        public override IEnumerable<int> Run(TestContext t)
        {
            Map map = t.Map;
            RwT.SetUp(t, 1, 0);
            float oldDiff = RwT.K.Get(Keys.Difficulty);
            RwT.K.Set(Keys.Difficulty, 2f);
            int made = 0, badAddiction = 0, carcinoma = 0, aggro = 0, hemogen = 0, xenoOff = 0, extra = 0, lethalComplication = 0;
            HediffDef carc = DefDatabase<HediffDef>.GetNamedSilentFail("Carcinoma");
            HospitalMap comp = RwT.Comp(map);
            Faction fac = RwT.PatientFaction();
            for (int i = 0; i < 200; i++)
            {
                // the incident's steps, with a snapshot after generation: age conditions vanilla gives the pawn are not RimWard's doing
                Pawn p = IncidentHelper.GenerateValidPatientPawn(fac);
                if (p == null) continue;
                IncidentHelper.SetUpNewPatient(p);
                var before = new HashSet<Hediff>(p.health.hediffSet.hediffs);
                PatientData d = new PatientData(RwT.Now, p.MarketValue, 0.5f, (PatientType)(1 + i % 2));
                PatientUtility.DamagePawn(p, d, comp);
                var afterCase = new HashSet<Hediff>(p.health.hediffSet.hediffs);
                PatientComplexityGenerator.ApplyComplexity(p, d, comp);
                made++;
                // independent of the generator's own filters (IsSafeAddiction, NoAggroStage): a fixed list of vanilla's dangerous
                // addictions and the raw def data (lethal severity, deadly or life-threatening stages, aggressive mental states)
                foreach (Hediff h in p.health.hediffSet.hediffs.Where(h => !before.Contains(h)))
                {
                    if (h is Hediff_Addiction && DangerousAddictions.Contains(h.def.defName)) { badAddiction++; t.Note("dangerous addiction " + h.def.defName); }
                    if (carc != null && h.def == carc) carcinoma++;
                    if (h.def.makesSickThought && RawAggro(h.def)) { aggro++; t.Note("rage illness " + h.def.defName); }
                    if (!afterCase.Contains(h) && RawCanKill(h.def)) { lethalComplication++; t.Note("complication that can kill: " + h.def.defName); }
                }
                if (p.health.hediffSet.hediffs.Count > 2) extra++;
                if (ModsConfig.BiotechActive && p.genes != null)
                {
                    if (p.genes.GenesListForReading.Any(g => g.Active && (g is Gene_Hemogen || g is Gene_Deathrest))) hemogen++;
                    XenotypeSet set = p.Faction?.def?.xenotypeSet;
                    if (set != null && p.genes.Xenotype != null && p.genes.Xenotype != XenotypeDefOf.Baseliner
                        && !Enumerable.Range(0, set.Count).Any(j => set[j].xenotype == p.genes.Xenotype)) xenoOff++;
                }
                RwT.Discard(p);
                if (i % 50 == 49) yield return 1;
            }
            t.Note(made + " patients, " + extra + " with more than two health conditions");
            t.Check(made >= 190, "at least 190 of 200 patients generated (" + made + ")");
            t.Check(badAddiction == 0, "no luciferium, psychite or other dangerous addiction (" + badAddiction + ")");
            t.Check(carcinoma == 0, "RimWard adds no carcinoma, as a case or a complication (" + carcinoma + ")");
            t.Check(aggro == 0, "no illness that causes a berserk rage (" + aggro + ")");
            t.Check(lethalComplication == 0, "no complication can kill (lethal severity, deadly or life-threatening stage) (" + lethalComplication + ")");
            if (ModsConfig.BiotechActive)
            {
                t.Check(hemogen == 0, "no hemogen or deathrest xenotype while the setting is off (" + hemogen + ")");
                t.Check(xenoOff == 0, "every patient has a xenotype of their faction (" + xenoOff + " others)");
            }
            RwT.K.Set(Keys.Difficulty, oldDiff);
            RwT.CleanUp(map);
        }
    }

    /// <summary>rw.billing (cases 4, 5, 6): every charge, the failure rules and retries, and the invoice that equals the silver paid.</summary>
    sealed class BillingSuite : KitTestSuite
    {
        public override string Name => "rw.billing";
        public override int MaxTicks => 9000;
        public override IEnumerable<int> Run(TestContext t)
        {
            Map map = t.Map;
            Pawn doc = RwT.SetUp(t, 3, 2);
            HospitalMap comp = RwT.Comp(map);
            t.Check(doc != null, "a doctor");
            if (doc == null) { RwT.CleanUp(map); yield break; }

            // ---- case 4: tend, feed (completed and interrupted), operation + materials
            Pawn p = RwT.NewPatient(map, PatientType.Wounds, out PatientData d, true, false);
            Thing med = RwT.Place(ThingDefOf.MedicineIndustrial, 3, map);
            int tends = RwT.LinesOf(d, InvoiceLineKind.Tend);
            TendUtility.DoTend(doc, p, (Medicine)med);
            t.Check(RwT.LinesOf(d, InvoiceLineKind.Tend) == tends + 1, "a tend with medicine adds a Tend line");

            Building_Bed bed = map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().FirstOrDefault(b => RwT.Spawned.Contains(b) && b.CurOccupants.Count() == 0);
            HediffDef anest = DefDatabase<HediffDef>.GetNamed("Anesthetic");
            p.health.AddHediff(anest);
            yield return 5;
            if (bed != null) p.jobs.Notify_TuckedIntoBed(bed);   // what RestUtility.TuckIntoBed does at the end of a rescue
            yield return 2;
            if (p.needs?.food != null) p.needs.food.CurLevel = 0.1f;
            Thing meal = RwT.Place(ThingDefOf.MealSimple, 2, map);
            int foods = RwT.LinesOf(d, InvoiceLineKind.Food);
            Job interrupted = JobMaker.MakeJob(JobDefOf.FeedPatient, meal, p); interrupted.count = 1;
            doc.jobs.StartJob(interrupted, JobCondition.InterruptForced);
            yield return 2;
            doc.jobs.EndCurrentJob(JobCondition.InterruptForced);
            t.Check(RwT.LinesOf(d, InvoiceLineKind.Food) == foods, "an interrupted feeding adds nothing");
            t.Note("patient in bed: " + p.InBed() + ", should be fed: " + FeedPatientUtility.ShouldBeFed(p));
            Job feed = JobMaker.MakeJob(JobDefOf.FeedPatient, meal, p); feed.count = 1;
            doc.jobs.StartJob(feed, JobCondition.InterruptForced);
            for (int i = 0; i < 40 && RwT.LinesOf(d, InvoiceLineKind.Food) == foods; i++)
            {
                yield return 100;
                if (doc.CurJobDef != JobDefOf.FeedPatient && RwT.LinesOf(d, InvoiceLineKind.Food) == foods && !meal.Destroyed)
                {   // the doctor may have been pulled away (a need, another job): hand the feeding back
                    Job again = JobMaker.MakeJob(JobDefOf.FeedPatient, meal, p); again.count = 1;
                    doc.jobs.StartJob(again, JobCondition.InterruptForced);
                }
            }
            t.Check(RwT.LinesOf(d, InvoiceLineKind.Food) == foods + 1, "a completed feeding adds one Food line");
            Hediff an = p.health.hediffSet.GetFirstHediffOfDef(anest); if (an != null) p.health.RemoveHediff(an);

            // operation through the method every operation passes (Bill_Medical.Notify_IterationCompleted), forced success
            if (!MakeArmCase(p, d, out Bill_Medical armBill, out Thing arm)) { t.Check(false, "an arm operation could be set up"); RwT.CleanUp(map); yield break; }
            Test_ForceSurgeryOutcome.Force = ForcedOutcome.Success;
            // ingredients as the job hands them over: single, unspawned things (a placed stack could merge with stock)
            Thing oneMed = ThingMaker.MakeThing(ThingDefOf.MedicineIndustrial);
            Thing oneArm = ThingMaker.MakeThing(arm.def);
            armBill.Notify_IterationCompleted(doc, new List<Thing> { oneArm, oneMed });
            Test_ForceSurgeryOutcome.Force = ForcedOutcome.None;
            t.Check(RwT.LinesOf(d, InvoiceLineKind.Surgery) == 1 && RwT.LinesOf(d, InvoiceLineKind.Materials) == 1, "a successful operation adds an Operation and a Materials line");
            float mat = d.Lines.Where(l => l.kind == InvoiceLineKind.Materials).Sum(l => l.amount);
            t.Check(Mathf.Abs(mat - (oneArm.def.BaseMarketValue + oneMed.def.BaseMarketValue)) < 1f, "materials are billed at base value (" + mat.ToString("0") + ")");
            t.Check(HospitalMap.CureAchieved(p, d), "the arm is installed (cure achieved)");

            // ---- case 6: the Bill column one tick before == invoice total == silver dropped
            d.HasPendingSurgeryBill = false;
            int estimate = PatientUtility.CalculateSilverToReceive(p, d) is float f ? (int)f : 0;
            int silver0 = RwT.SilverOnMap(map);
            Invoice inv = comp.Discharge(p, d, DischargeReason.Cured);
            int dropped = RwT.SilverOnMap(map) - silver0;
            t.Check(inv != null && inv.TotalSilver == estimate && dropped == estimate, "invoice parity: estimate " + estimate + ", invoice " + inv?.TotalSilver + ", silver dropped " + dropped);
            t.Check(HospitalSave.Get.recentInvoices.FirstOrDefault() == inv, "the invoice is kept with the last discharges");

            // ---- case 5: failure: no fee, reimbursement, retry later, patient waits, three failures end it without penalty
            Pawn q = RwT.NewPatient(map, PatientType.Wounds, out PatientData e, true, false);
            if (!MakeArmCase(q, e, out Bill_Medical qBill, out Thing qArm)) { t.Check(false, "a second arm case"); RwT.CleanUp(map); yield break; }
            int gw = q.Faction.PlayerGoodwill;
            Test_ForceSurgeryOutcome.Force = ForcedOutcome.Failure;
            qBill.Notify_IterationCompleted(doc, new List<Thing> { ThingMaker.MakeThing(qArm.def) });
            Test_ForceSurgeryOutcome.Force = ForcedOutcome.None;
            t.Check(RwT.LinesOf(e, InvoiceLineKind.Surgery) == 0, "a failed operation charges no fee");
            t.Check(RwT.LinesOf(e, InvoiceLineKind.ReimburseParts) == 1, "the arm it used up is reimbursed (setting on)");
            t.Check(e.RetryAfterTick > RwT.Now && e.HasPendingSurgeryBill && e.SurgeryAttempts == 1, "a retry is scheduled (" + (e.RetryAfterTick - RwT.Now) + " ticks)");
            yield return 300;
            t.Check(q.GetLord()?.CurLordToil is LordToil_Patient, "the patient does not leave between the failure and the retry");
            yield return 260;   // the retry runs on the map component's 250-tick beat after its time
            t.Note("before the retry: retryAt " + e.RetryAfterTick + " now " + RwT.Now + ", pending " + e.HasPendingSurgeryBill + ", bills " + q.health.surgeryBills.Count);
            yield return 400;
            Bill_Medical retry = Caretaker.CureBill(q, e);
            t.Note("after: retryAt " + e.RetryAfterTick + ", pending " + e.HasPendingSurgeryBill + ", bills " + string.Join(",", q.health.surgeryBills.Bills.Select(b => b.recipe.defName))
                + ", parts " + string.Join(",", e.CureRecipe.Worker.GetPartsToApplyOn(q, e.CureRecipe).Select(x => x.def.defName)) + ", dismissed " + e.Dismissed + ", status " + comp.WhyStillHere(q));
            t.Check(retry != null && e.RetryAfterTick < 0, "the cure bill is back after the wait");
            for (int i = 2; i <= 3 && retry != null; i++)
            {
                Test_ForceSurgeryOutcome.Force = ForcedOutcome.Failure;
                retry.Notify_IterationCompleted(doc, new List<Thing> { ThingMaker.MakeThing(qArm.def) });
                Test_ForceSurgeryOutcome.Force = ForcedOutcome.None;
                t.Note("failure " + i + ": attempts " + e.SurgeryAttempts + ", retryAt " + e.RetryAfterTick + ", pending " + e.HasPendingSurgeryBill);
                yield return 860;
                retry = Caretaker.CureBill(q, e);
            }
            Test_ForceSurgeryOutcome.Force = ForcedOutcome.Failure;
            t.Note("last attempt: bill " + (retry != null) + ", attempts " + e.SurgeryAttempts);
            retry?.Notify_IterationCompleted(doc, new List<Thing> { ThingMaker.MakeThing(qArm.def) });
            Test_ForceSurgeryOutcome.Force = ForcedOutcome.None;
            t.Note("after the last failure: attempts " + e.SurgeryAttempts + ", retryAt " + e.RetryAfterTick + ", pending " + e.HasPendingSurgeryBill + ", bills " + q.health.surgeryBills.Count);
            yield return 260;
            t.Check(!e.HasPendingSurgeryBill && e.RetryAfterTick < 0, "after the last allowed retry nothing is pending (attempts " + e.SurgeryAttempts + ", pending " + e.HasPendingSurgeryBill + ", retryAt " + e.RetryAfterTick + ")");
            t.Check(!q.Dead && HospitalSave.Get.patientsDiedAtFault == 0, "nobody died through the failures");
            t.Note("goodwill of " + q.Faction.Name + ": " + gw + " → " + q.Faction.PlayerGoodwill);
            t.Check(q.Faction.PlayerGoodwill >= gw, "the failures cost no goodwill");
            RwT.CleanUp(map);
        }

        /// <summary>Takes an arm off the patient and gives them the bionic arm operation as their case.</summary>
        internal static bool MakeArmCase(Pawn p, PatientData d, out Bill_Medical bill, out Thing arm)
        {
            bill = null; arm = null;
            RecipeDef r = DefDatabase<RecipeDef>.GetNamedSilentFail("InstallBionicArm");
            p.health.RemoveAllHediffs();   // a clean case: no wounds that could down the patient or block the part
            if (r == null) return false;
            BodyPartRecord shoulder = r.Worker.GetPartsToApplyOn(p, r).FirstOrDefault()
                ?? p.health.hediffSet.GetNotMissingParts().FirstOrDefault(x => x.def.defName == "Shoulder");
            if (shoulder == null) return false;
            if (!p.health.hediffSet.PartIsMissing(shoulder))
            {
                Hediff_MissingPart miss = (Hediff_MissingPart)HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, p, shoulder);
                miss.IsFresh = false;
                p.health.AddHediff(miss, shoulder);
            }
            foreach (Bill b in p.health.surgeryBills.Bills.ToList()) p.health.surgeryBills.Delete(b);
            HealthCardUtility.CreateSurgeryBill(p, r, shoulder, null, false);
            bill = p.health.surgeryBills.Bills.OfType<Bill_Medical>().FirstOrDefault(b => b.recipe == r);
            d.Type = PatientType.Surgery; d.CureRecipe = r; d.HasPendingSurgeryBill = bill != null; d.RetryAfterTick = -1; d.SurgeryAttempts = 0;
            arm = RwT.Place(DefDatabase<ThingDef>.GetNamed("BionicArm"), 1, p.Map);
            return bill != null;
        }
    }

    /// <summary>rw.surgeryflow (case 1, end to end): a surgery patient goes to bed and a colonist performs the operation by the normal job.</summary>
    sealed class SurgeryFlowSuite : KitTestSuite
    {
        public override string Name => "rw.surgeryflow";
        // 26000 ticks for the operation + 15000 for the discharge + set-up; the suite cleans up itself before the runner's timeout
        public override int MaxTicks => 43000;
        public override IEnumerable<int> Run(TestContext t)
        {
            Map map = t.Map;
            Pawn doc = RwT.SetUp(t, 2, 2);
            HospitalMap comp = RwT.Comp(map);
            // Installing a bionic arm needs Medicine 5. Before, only SetUp's doctor had it, so one mental break (a daze of several hours on a
            // random map, after earlier suites hurt the mood) left nobody able to operate (flake of the final round: no doctor for 26000 ticks).
            // Every colonist who can doctor gets the skill and priority 1, and a doctor who breaks down is brought back at once.
            List<Pawn> doctors = map.mapPawns.FreeColonistsSpawned.Where(c => !c.WorkTypeIsDisabled(WorkTypeDefOf.Doctor)).ToList();
            foreach (Pawn c in doctors)
            {
                c.workSettings?.SetPriority(WorkTypeDefOf.Doctor, 1);
                SkillRecord med = c.skills?.GetSkill(SkillDefOf.Medicine);
                if (med != null && med.Level < 8) med.Level = 8;
            }
            t.Check(doctors.Count > 0, "colonists who can operate: " + doctors.Count);
            Pawn p = RwT.NewPatient(map, PatientType.Wounds, out PatientData d, true, false);
            p.health.RemoveAllHediffs();
            t.Check(BillingSuite.MakeArmCase(p, d, out Bill_Medical bill, out Thing arm), "a bionic arm operation as the patient's case");
            Test_ForceSurgeryOutcome.Force = ForcedOutcome.Success;
            WorkGiver_Scanner surgery = DefDatabase<WorkGiverDef>.GetNamedSilentFail("DoBillsMedicalHumanOperation")?.Worker as WorkGiver_Scanner;
            int start = RwT.Now, recovered = 0;
            while (RwT.Now - start < 26000 && !HospitalMap.CureAchieved(p, d))
            {
                yield return 250;
                foreach (Pawn c in doctors)
                    if (c.Spawned && !c.Dead && c.InMentalState) { t.Note("doctor " + c.LabelShort + " broke down (" + c.MentalStateDef?.defName + "): brought back"); c.MentalState.RecoverFromState(); recovered++; }
                if ((RwT.Now - start) % 5000 < 250)
                {
                    string why = "";
                    if (surgery != null && doc != null && doc.Spawned && p.Spawned)
                    {
                        JobFailReason.Clear();
                        Job job = surgery.JobOnThing(doc, p, false);
                        why = job != null ? "could start " + job.def.defName : (JobFailReason.Reason ?? "no job, no reason");
                        JobFailReason.Clear();
                    }
                    t.Note("t+" + (RwT.Now - start) + ": job " + p.CurJobDef?.defName + ", duty " + p.mindState?.duty?.def?.defName + ", toil " + p.GetLord()?.CurLordToil?.GetType().Name
                        + ", seekRest " + HealthAIUtility.ShouldSeekMedicalRest(p) + ", surgeryNow " + HealthAIUtility.ShouldHaveSurgeryDoneNow(p)
                        + ", bed " + RestUtility.FindBedFor(p, p, false)?.ThingID + ", doc job " + doc?.CurJobDef?.defName + " (" + why + ")"
                        + ", arm reachable " + (arm != null && arm.Spawned && doc != null && doc.CanReach(arm, PathEndMode.ClosestTouch, doc.NormalMaxDanger()))
                        + ", outdoor " + map.mapTemperature.OutdoorTemp.ToString("F0") + " C, pending " + d.HasPendingSurgeryBill + ", bills " + p.health.surgeryBills.Count);
                }
            }
            Test_ForceSurgeryOutcome.Force = ForcedOutcome.None;
            t.Note("status after " + (RwT.Now - start) + " ticks: " + comp.WhyStillHere(p) + ", in bed " + p.InBed() + ", doctors brought back from a break " + recovered);
            bool cured = HospitalMap.CureAchieved(p, d);
            t.Check(cured, "a colonist installed the bionic arm by the normal surgery job");
            t.Check(RwT.LinesOf(d, InvoiceLineKind.Surgery) == 1 && RwT.LinesOf(d, InvoiceLineKind.Materials) == 1, "the real operation billed fee and materials");
            if (!cured) { RwT.CleanUp(map); yield break; }   // no discharge to wait for; never leave beds behind for the next suite
            int silver0 = RwT.SilverOnMap(map);
            int due = Invoice.Build(p, d, DischargeReason.Estimate).TotalSilver;
            int waited = 0;
            while (comp.GetPatientData(p, out PatientData dd) && !dd.Dismissed && waited < 15000) { yield return 250; waited += 250; }
            t.Check(comp.GetPatientData(p, out PatientData after) && after.Dismissed && after.PaidOnDischarge, "the cured patient was discharged by itself after " + waited + " ticks (" + comp.WhyStillHere(p) + ")");
            t.Check(RwT.SilverOnMap(map) - silver0 >= due - 1, "and paid about the estimate (" + (RwT.SilverOnMap(map) - silver0) + " of " + due + ")");
            RwT.CleanUp(map);
        }
    }

    /// <summary>rw.dismiss (case 7): a dismissed uncured patient pays the share and walks out, also after being downed; Guilty ideoligions mind it.</summary>
    sealed class DismissSuite : KitTestSuite
    {
        public override string Name => "rw.dismiss";
        public override int MaxTicks => 9000;
        public override IEnumerable<int> Run(TestContext t)
        {
            Map map = t.Map;
            Pawn doc = RwT.SetUp(t, 3, 0);
            HospitalMap comp = RwT.Comp(map);
            Pawn p = RwT.NewPatient(map, PatientType.Disease, out PatientData d, true, false);
            d.AddLine(InvoiceLineKind.Tend, "test", 120f);
            MemeDef guilty = DefDatabase<MemeDef>.GetNamedSilentFail("Guilty");
            Ideo ideo = Faction.OfPlayer.ideos?.PrimaryIdeo;
            bool addedMeme = false;
            if (ModsConfig.IdeologyActive && guilty != null && ideo != null && !ideo.memes.Contains(guilty)) { ideo.memes.Add(guilty); addedMeme = true; }
            ThoughtDef turned = DefDatabase<ThoughtDef>.GetNamed("RimWard_TurnedAwayPatient");
            int thoughtsBefore = map.mapPawns.FreeColonistsSpawned.Sum(c => c.needs?.mood?.thoughts?.memories?.NumMemoriesOfDef(turned) ?? 0);
            Invoice full = Invoice.Build(p, d, DischargeReason.Estimate);
            int silver0 = RwT.SilverOnMap(map);
            comp.DismissPatient(p);
            int paid = RwT.SilverOnMap(map) - silver0;
            int expect = Mathf.FloorToInt(full.total * RwT.S.EarlyDischargePayFraction);
            t.Check(d.Dismissed && Math.Abs(paid - expect) <= 1, "an uncured dismissed patient pays " + RwT.S.EarlyDischargePayFraction.ToStringPercent() + " of the bill so far (" + paid + ", expected " + expect + ")");
            if (ModsConfig.IdeologyActive && guilty != null && ideo != null)
            {
                int thoughtsAfter = map.mapPawns.FreeColonistsSpawned.Sum(c => c.needs?.mood?.thoughts?.memories?.NumMemoriesOfDef(turned) ?? 0);
                t.Check(thoughtsAfter > thoughtsBefore, "colonists of a Guilty ideoligion feel bad about it");
                if (addedMeme) ideo.memes.Remove(guilty);
            }
            int w = 0;
            while (p.Spawned && p.GetLord()?.CurLordToil is LordToil_Patient && w < 1500) { yield return 250; w += 250; }
            t.Check(!p.Spawned || !(p.GetLord()?.CurLordToil is LordToil_Patient), "the dismissed patient walks out (lord left the patient toil after " + w + " ticks)");

            // downed first: stays until mobile, then leaves
            Pawn q = RwT.NewPatient(map, PatientType.Wounds, out PatientData e, true, false);
            HediffDef anest = DefDatabase<HediffDef>.GetNamed("Anesthetic");
            q.health.AddHediff(anest);
            yield return 5;
            t.Check(q.Downed, "the second patient is downed");
            comp.DismissPatient(q);
            yield return 300;
            t.Check(q.MapHeld == map && e.Dismissed, "a downed dismissed patient is not lost (still on the map or carried to a bed, dismissed)");
            Hediff an = q.health.hediffSet.GetFirstHediffOfDef(anest); if (an != null) q.health.RemoveHediff(an);
            w = 0;
            while (q.Spawned && q.GetLord()?.CurLordToil is LordToil_Patient && w < 2500) { yield return 250; w += 250; }
            t.Check(!q.Spawned || !(q.GetLord()?.CurLordToil is LordToil_Patient), "once mobile they leave too (" + w + " ticks; lord " + q.GetLord()?.LordJob?.GetType().Name + ")");
            RwT.CleanUp(map);
        }
    }

    /// <summary>rw.impossible (case 8): an operation nobody can do sends a letter after N hours; "keep waiting" restarts; no answer discharges after 2N with partial pay and no goodwill change.</summary>
    sealed class ImpossibleSuite : KitTestSuite
    {
        public override string Name => "rw.impossible";
        public override int MaxTicks => 6000;
        public override IEnumerable<int> Run(TestContext t)
        {
            Map map = t.Map;
            RwT.SetUp(t, 2, 1);
            HospitalMap comp = RwT.Comp(map);
            while (RwT.Now < 2 * 2500 + 600) yield return 250;   // clocks below 0 mean "not set": the game must be older than 2 × 1 h
            float oldHours = RwT.K.Get(Keys.ImpossibleHours);
            RwT.K.Set(Keys.ImpossibleHours, 1f);   // 1 h = 2500 ticks, so the clock can be set back inside a young test game
            Pawn p = RwT.NewPatient(map, PatientType.Wounds, out PatientData d, true, false);
            t.Check(BillingSuite.MakeArmCase(p, d, out _, out _), "a bionic arm case");
            var prio = new Dictionary<Pawn, int>();
            foreach (Pawn c in map.mapPawns.FreeColonistsSpawned.Where(c => !c.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))) { prio[c] = c.workSettings?.GetPriority(WorkTypeDefOf.Doctor) ?? 0; c.workSettings?.SetPriority(WorkTypeDefOf.Doctor, 0); }
            yield return 260;
            string why = Caretaker.ImpossibleReason(comp, p, d);
            t.Check(why == "RimWard.Why.Impossible.NoSurgeon".Translate(), "with no doctor the treatment is impossible: " + why);
            t.Check(comp.StatusOf(p, out _) == PatientStatus.TreatmentImpossible, "the status says so");
            int span = (int)(RwT.S.ImpossibleTreatmentHours * GenDate.TicksPerHour);
            d.ImpossibleSinceTick = RwT.Now - span - 1;
            yield return 260;
            ChoiceLetter_TreatmentImpossible letter = Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_TreatmentImpossible>().FirstOrDefault(l => l.patient == p);
            t.Check(d.ImpossibleLetterSent && letter != null, "the letter came after " + RwT.S.ImpossibleTreatmentHours + " h");
            DiaOption wait = letter?.Choices.Skip(1).FirstOrDefault();
            wait?.action?.Invoke();
            t.Check(!d.ImpossibleLetterSent && RwT.Now - d.ImpossibleSinceTick < 10, "\"Keep waiting\" restarts the clock");
            int gw = p.Faction.PlayerGoodwill;
            d.ImpossibleSinceTick = RwT.Now - 2 * span - 1;
            int silver0 = RwT.SilverOnMap(map);
            Invoice est = Invoice.Build(p, d, DischargeReason.Estimate);
            yield return 260;
            t.Check(d.Dismissed && d.PaidOnDischarge, "without an answer the patient is discharged after 2 × " + RwT.S.ImpossibleTreatmentHours + " h");
            int paid = RwT.SilverOnMap(map) - silver0, expect = Mathf.FloorToInt(est.total * RwT.S.EarlyDischargePayFraction);
            t.Check(Math.Abs(paid - expect) <= 1, "partial pay (" + paid + ", expected " + expect + ")");
            t.Check(p.Faction.PlayerGoodwill == gw, "no goodwill change");
            foreach (var kv in prio) kv.Key.workSettings?.SetPriority(WorkTypeDefOf.Doctor, kv.Value);
            RwT.K.Set(Keys.ImpossibleHours, oldHours);
            RwT.CleanUp(map);
        }
    }

    /// <summary>rw.maxstay (case 9): after the maximum stay the patient is discharged with the full bill.</summary>
    sealed class MaxStaySuite : KitTestSuite
    {
        public override string Name => "rw.maxstay";
        public override int MaxTicks => 2000;
        public override IEnumerable<int> Run(TestContext t)
        {
            Map map = t.Map;
            RwT.SetUp(t, 2, 0);
            HospitalMap comp = RwT.Comp(map);
            float old = RwT.K.Get(Keys.MaxStayDays);
            RwT.K.Set(Keys.MaxStayDays, 1f);
            Pawn p = RwT.NewPatient(map, PatientType.Disease, out PatientData d, true, false);
            d.AddLine(InvoiceLineKind.Tend, "test", 80f);
            d.ArrivedAtTick = RwT.Now - (int)(1.1f * GenDate.TicksPerDay);
            Invoice est = Invoice.Build(p, d, DischargeReason.MaxStay);
            int silver0 = RwT.SilverOnMap(map);
            yield return 260;
            t.Check(d.Dismissed && d.PaidOnDischarge, "a patient over the maximum stay (1 day) is discharged");
            int paid = RwT.SilverOnMap(map) - silver0;
            t.Check(paid == est.TotalSilver && est.TotalSilver > 0, "with the full bill (" + paid + " of " + est.TotalSilver + ")");
            t.Check(Caretaker.Counts.TryGetValue(Caretaker.Fix.MaxStay, out int n) && n > 0, "the caretaker counted it");
            RwT.K.Set(Keys.MaxStayDays, old);
            RwT.CleanUp(map);
        }
    }

    /// <summary>rw.danger (case 10): dormant mechs block nothing; an active hostile blocks arrivals and holds cured patients at most N hours; toxic fallout blocks arrivals only.</summary>
    sealed class DangerSuite : KitTestSuite
    {
        public override string Name => "rw.danger";
        public override int MaxTicks => 3000;
        public override IEnumerable<int> Run(TestContext t)
        {
            Map map = t.Map;
            RwT.SetUp(t, 2, 0);
            HospitalMap comp = RwT.Comp(map);
            float oldHold = RwT.K.Get(Keys.DangerHoldHours);
            RwT.K.Set(Keys.DangerHoldHours, 1f);   // 1 h, so the hold clock can be set back inside a young test game
            Pawn cured = RwT.NewPatient(map, PatientType.Wounds, out PatientData d, true, false);
            cured.health.RemoveAllHediffs();
            d.HasPendingSurgeryBill = false;
            IntVec3 far = CellFinder.RandomClosewalkCellNear(TestCells.Anchor(map), map, 25);
            Pawn mech = null;
            if (ModsConfig.BiotechActive || Faction.OfMechanoids != null)
            {
                mech = PawnGenerator.GeneratePawn(PawnKindDefOf.Mech_Scyther, Faction.OfMechanoids);
                GenSpawn.Spawn(mech, far, map);
                CompCanBeDormant dorm = mech.GetComp<CompCanBeDormant>();
                if (dorm != null) dorm.ToSleep();
                Threats.ClearCache();
                t.Check(dorm != null && !dorm.Awake, "a dormant mechanoid stands on the map");
                t.Check(Threats.ArrivalBlock(map, comp.Policy) == null, "a dormant mech blocks no arrival");
                t.Check(Caretaker.MayLeaveDespiteDanger(comp, cured, d), "a dormant mech holds no cured patient");
                dorm?.WakeUp();
                Threats.ClearCache();
                t.Check(Threats.ArrivalBlock(map, comp.Policy) != null, "an awake hostile mech blocks arrivals (" + Threats.ArrivalBlock(map, comp.Policy) + ")");
                t.Check(!Caretaker.MayLeaveDespiteDanger(comp, cured, d), "and holds the cured patient");
                t.Check(comp.StatusOf(cured, out _) == PatientStatus.HeldByDanger, "status: held by danger");
                d.DangerHoldSinceTick = RwT.Now - (int)(RwT.S.MaxDangerHoldHours * GenDate.TicksPerHour) - 1;
                t.Check(Caretaker.MayLeaveDespiteDanger(comp, cured, d), "after " + RwT.S.MaxDangerHoldHours + " h they leave anyway");
                mech.Destroy(DestroyMode.Vanish);
            }
            Threats.ClearCache();
            GameCondition fallout = GameConditionMaker.MakeConditionPermanent(GameConditionDefOf.ToxicFallout);
            map.gameConditionManager.RegisterCondition(fallout);
            Threats.ClearCache();
            t.Check(IncidentHelper.ArrivalBlockReason(map) != null, "toxic fallout blocks arrivals (" + IncidentHelper.ArrivalBlockReason(map) + ")");
            t.Check(Caretaker.MayLeaveDespiteDanger(comp, cured, d), "but never holds a cured patient");
            fallout.End();
            RwT.K.Set(Keys.DangerHoldHours, oldHold);
            Threats.ClearCache();
            RwT.CleanUp(map);
            yield return 1;
        }
    }

    /// <summary>rw.status (case 11): arrested, enslaved or recruited patients keep their new status and leave the patient list.</summary>
    sealed class StatusSuite : KitTestSuite
    {
        public override string Name => "rw.status";
        public override int MaxTicks => 2000;
        public override IEnumerable<int> Run(TestContext t)
        {
            Map map = t.Map;
            RwT.SetUp(t, 3, 0);
            HospitalMap comp = RwT.Comp(map);
            Pawn a = RwT.NewPatient(map, PatientType.Wounds, out _, true, false);
            Pawn s = ModsConfig.IdeologyActive ? RwT.NewPatient(map, PatientType.Wounds, out _, true, false) : null;
            Pawn r = RwT.NewPatient(map, PatientType.Wounds, out _, true, false);
            a.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner);
            s?.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Slave);
            RecruitUtility.Recruit(r, Faction.OfPlayer);
            yield return 260;
            t.Check(a.IsPrisoner && !comp.Patients.ContainsKey(a), "an arrested patient stays a prisoner and leaves the patient list");
            if (s != null) t.Check(s.IsSlave && !comp.Patients.ContainsKey(s), "an enslaved patient stays a slave and leaves the list");
            t.Check(r.Faction == Faction.OfPlayer && !comp.Patients.ContainsKey(r), "a recruited patient stays a colonist and leaves the list");
            foreach (Pawn x in new[] { a, s, r }) RwT.Discard(x);
            RwT.CleanUp(map);
        }
    }

    /// <summary>rw.why (caretaker reasons): every status the "why still here" text knows is reached and reads as a translated sentence.</summary>
    sealed class WhySuite : KitTestSuite
    {
        public override string Name => "rw.why";
        public override int MaxTicks => 2000;
        public override IEnumerable<int> Run(TestContext t)
        {
            Map map = t.Map;
            RwT.SetUp(t, 3, 1);
            HospitalMap comp = RwT.Comp(map);
            var seen = new Dictionary<PatientStatus, string>();
            void Look(Pawn p) { PatientStatus st = comp.StatusOf(p, out _); seen[st] = comp.WhyStillHere(p); }
            Pawn hurt = RwT.NewPatient(map, PatientType.Wounds, out PatientData dh, true, false);
            // random wounds may down this one (then it reads as Acute): a flu is a case that needs tending but no rescue
            hurt.health.RemoveAllHediffs(); hurt.health.AddHediff(DefDatabase<HediffDef>.GetNamed("Flu"));
            Look(hurt);
            Pawn surg = RwT.NewPatient(map, PatientType.Wounds, out PatientData ds, true, false);
            t.Check(BillingSuite.MakeArmCase(surg, ds, out _, out _), "a bionic arm case for the surgery statuses");
            Look(surg);
            ds.RetryAfterTick = RwT.Now + 600; Look(surg); ds.RetryAfterTick = -1;
            Pawn down = RwT.NewPatient(map, PatientType.Wounds, out _, true, false);
            down.health.AddHediff(DefDatabase<HediffDef>.GetNamed("Anesthetic"));
            yield return 5;
            Look(down);
            Pawn fine = RwT.NewPatient(map, PatientType.Wounds, out PatientData df, true, false);
            fine.health.RemoveAllHediffs(); df.HasPendingSurgeryBill = false;
            Look(fine);
            comp.DismissPatient(fine);
            Look(fine);
            foreach (var kv in seen) t.Note(kv.Key + ": " + kv.Value);
            foreach (PatientStatus want in new[] { PatientStatus.WaitingForSurgery, PatientStatus.SurgeryRetry, PatientStatus.Acute, PatientStatus.ReadyToLeave, PatientStatus.Leaving })
                t.Check(seen.ContainsKey(want), "status " + want + " reached");
            t.Check(seen.ContainsKey(PatientStatus.NeedsTending) || seen.ContainsKey(PatientStatus.Resting), "a wounded patient reads as needs tending or resting");
            t.Check(seen.Values.All(v => !v.NullOrEmpty() && !v.Contains("RimWard.Why")), "every reason is a translated sentence");
            RwT.CleanUp(map);
        }
    }

    /// <summary>rw.mce (case 16): a mass casualty event never sends more patients than there are free patient beds.</summary>
    sealed class MceSuite : KitTestSuite
    {
        public override string Name => "rw.mce";
        public override int MaxTicks => 2000;
        public override IEnumerable<int> Run(TestContext t)
        {
            Map map = t.Map;
            RwT.SetUp(t, 5, 0);
            HospitalMap comp = RwT.Comp(map);
            HospitalSave.Get.Policy.massCasualties = true;
            RwT.NewPatient(map, PatientType.Wounds, out _, true, false);
            RwT.NewPatient(map, PatientType.Wounds, out _, true, false);
            int free = comp.FreeMedicalBeds(), before = comp.Patients.Count;
            t.Check(free == comp.BedCount() - 2 && free >= 3, "5 test beds, 2 taken (free " + free + " of " + comp.BedCount() + ")");
            IncidentDef mce = DefDatabase<IncidentDef>.GetNamed("RimWard_MassCasualtyEvent");
            Threats.ClearCache();
            bool fired = mce.Worker.TryExecute(new IncidentParms { target = map });
            int came = comp.Patients.Count - before;
            t.Check(fired && came >= 1 && came <= free, "the event sent " + came + " patients for " + free + " free beds");
            yield return 300;
            RwT.K.Current.SetSection(Sections.MassCasualties, false);
            t.Check(!comp.MassCasualties && !mce.Worker.CanFireNow(new IncidentParms { target = map }), "switching the area off stops the event");
            RwT.K.Current.SetSection(Sections.MassCasualties, true);
            HospitalSave.Get.Policy.massCasualties = false;
            RwT.CleanUp(map);
        }
    }

    /// <summary>rw.sections (G13a): each area switch does what its text says.</summary>
    sealed class SectionsSuite : KitTestSuite
    {
        public override string Name => "rw.sections";
        public override int MaxTicks => 2000;
        public override IEnumerable<int> Run(TestContext t)
        {
            Map map = t.Map;
            RwT.SetUp(t, 3, 1);
            HospitalMap comp = RwT.Comp(map);
            KitProfile pr = RwT.K.Current;
            IncidentDef inc = DefDatabase<IncidentDef>.GetNamed("RimWard_PatientArrives");

            pr.SetSection(Sections.Intake, false);
            t.Check(IncidentHelper.ArrivalBlockReason(map) == "RimWard.Block.IntakeOff".Translate() && !inc.Worker.TryExecute(new IncidentParms { target = map }), "intake off: no arrival, the reason says so");
            t.Check(!RwT.S.AmbientContractsEnabled && !RimWardAreas.IsOn(FeatureArea.Intake, map), "intake off: no contracts, area reads off");
            pr.SetSection(Sections.Intake, true);
            t.Check(IncidentHelper.ArrivalBlockReason(map) == null, "intake on again: arrivals possible");

            float oldMode = RwT.K.Get(Keys.IntakeMode);
            RwT.K.Set(Keys.IntakeMode, (float)(int)IntakeMode.Prompt);
            pr.SetSection(Sections.Surgery, false);
            for (int i = 0; i < 15; i++) inc.Worker.TryExecute(new IncidentParms { target = map });
            int surg = comp.pendingOffers.Count(o => o.data.Type == PatientType.Surgery);
            t.Check(comp.pendingOffers.Count >= 10 && surg == 0, "surgery cases off: no surgery case among " + comp.pendingOffers.Count + " offers");
            pr.SetSection(Sections.Surgery, true);
            RwT.K.Set(Keys.IntakeMode, oldMode);

            pr.SetSection(Sections.Billing, false);
            t.Check(RwT.S.EarlyDischargePayFraction == 0f && !RwT.S.ReimburseMedicineOnFailure && !RwT.S.ReimburseProstheticsOnFailure && RwT.S.InvoiceLetterThreshold > 1e9f,
                "billing extras off: no share when leaving early, no reimbursement, no invoice letters");
            pr.SetSection(Sections.Billing, true);
            t.Check(Mathf.Approximately(RwT.S.EarlyDischargePayFraction, RwT.K.Get(Keys.EarlyPay)), "billing extras on again");

            pr.SetSection(Sections.Malpractice, false);
            t.Check(!RwT.S.DeathPenaltyEnabled && !RimWardAreas.IsOn(FeatureArea.Malpractice), "malpractice off: no goodwill penalty");
            pr.SetSection(Sections.Malpractice, true);

            RwT.K.Set(Keys.ChargePatients, false);
            Pawn p = RwT.NewPatient(map, PatientType.Wounds, out PatientData d, true, false);
            d.AddLine(InvoiceLineKind.Tend, "test", 100f);
            t.Check(Invoice.Build(p, d, DischargeReason.Estimate).TotalSilver == 0, "patients pay off (charity): the invoice is 0");
            RwT.K.Set(Keys.ChargePatients, true);
            t.Check(Invoice.Build(p, d, DischargeReason.Estimate).TotalSilver > 0, "patients pay on: the invoice is above 0");

            HediffDef flu = DefDatabase<HediffDef>.GetNamed("Flu");
            RwT.S.blockedDiseases.Add(flu.defName);
            int flus = 0;
            for (int i = 0; i < 40; i++) { Pawn x = RwT.NewPatient(map, PatientType.Disease, out _, false, false); if (x?.health.hediffSet.GetFirstHediffOfDef(flu) != null) flus++; RwT.Discard(x); }
            RwT.S.blockedDiseases.Remove(flu.defName);
            t.Check(flus == 0, "a blacklisted disease (flu) never comes (" + flus + " in 40)");
            Faction fac = RwT.PatientFaction();
            RwT.S.blockedFactions.Add(fac.def.defName);
            int fromIt = 0;
            RwT.K.Set(Keys.IntakeMode, (float)(int)IntakeMode.Prompt);
            foreach (PatientOffer o in comp.pendingOffers.ToList()) comp.ResolveOffer(o.id, false);
            for (int i = 0; i < 10; i++) inc.Worker.TryExecute(new IncidentParms { target = map });
            fromIt = comp.pendingOffers.Count(o => o.faction?.def == fac.def);
            RwT.S.blockedFactions.Remove(fac.def.defName);
            RwT.K.Set(Keys.IntakeMode, oldMode);
            t.Check(fromIt == 0, "a blacklisted faction (" + fac.def.defName + ") sends nobody (" + comp.pendingOffers.Count + " offers from others)");
            RwT.CleanUp(map);
            yield return 1;
        }
    }

    /// <summary>rw.critical: the Luciferium critical case (exists only with Luciferium Overhaul, off by default).</summary>
    sealed class CriticalSuite : KitTestSuite
    {
        public override string Name => "rw.critical";
        public override int MaxTicks => 3000;
        public override IEnumerable<int> Run(TestContext t)
        {
            Map map = t.Map;
            bool lo = KitDetect.IsActive(HospitalMod.LuciferiumOverhaulId);
            t.Check(!RwT.K.On(Sections.Luciferium), "the area is off by default" + (lo ? "" : " and absent without Luciferium Overhaul"));
            if (!lo)
            {
                t.Check(RwT.K.WhyOff(Sections.Luciferium) != null, "its reason names the missing mod: " + RwT.K.WhyOff(Sections.Luciferium));
                t.Check(!CriticalCase.Possible(map), "no critical case can happen");
                yield break;
            }
            Pawn doc = RwT.SetUp(t, 2, 1);
            HospitalMap comp = RwT.Comp(map);
            RwT.K.Current.SetSection(Sections.Luciferium, true);
            t.Check(!CriticalCase.Possible(map), "without luciferium in stock no critical case");
            Thing luci = RwT.Place(ThingDef.Named("Luciferium"), 2, map);
            t.Check(CriticalCase.Possible(map), "with a dose in stock a critical case is possible");
            Pawn p = RwT.NewPatient(map, PatientType.Wounds, out PatientData d, true, false);
            foreach (Bill b in p.health.surgeryBills.Bills.ToList()) p.health.surgeryBills.Delete(b);
            t.Check(CriticalCase.Make(p, d, comp), "a patient turned into a critical case: " + d.Diagnosis);
            Bill_Medical bill = Caretaker.CureBill(p, d);
            t.Check(bill != null && bill.recipe == CriticalCase.Recipe, "the cure is the vanilla administer-luciferium bill");
            Thing dose = luci.SplitOff(1);
            bill?.Notify_IterationCompleted(doc, new List<Thing> { dose });
            t.Check(CriticalCase.Done(p) && HospitalMap.CureAchieved(p, d), "after the dose the case counts as cured");
            t.Note("invoice lines: " + string.Join(", ", d.Lines.Select(l => l.kind + " " + l.amount.ToString("0"))) + "; violation " + (bill?.recipe?.Worker?.IsViolationOnPawn(p, bill.Part, Faction.OfPlayer) ?? false)
                + ", patient " + p.IsPatient(out _, false));
            t.Check(RwT.LinesOf(d, InvoiceLineKind.CriticalCase) == 1 && RwT.LinesOf(d, InvoiceLineKind.Materials) == 1, "the invoice has the dose and the critical-case fee");
            RwT.K.Current.SetSection(Sections.Luciferium, false);
            RwT.CleanUp(map);
            yield return 1;
        }
    }

    /// <summary>rw.integration (case 17): with Hearth &amp; Harbor one bed button; with Hospital Beds Gen 2 its beds count as patient beds. Skips what is not loaded.</summary>
    sealed class IntegrationSuite : KitTestSuite
    {
        public override string Name => "rw.integration";
        public override int MaxTicks => 2000;
        public override IEnumerable<int> Run(TestContext t)
        {
            Map map = t.Map;
            RwT.SetUp(t, 1, 0);
            HospitalMap comp = RwT.Comp(map);
            bool hh = HearthBridge.HearthLoaded;
            bool hbg = KitDetect.IsActive(HospitalMod.HospitalBedsId);   // also a local copy in a jbmod slot (".slotN")
            t.Note("Hearth & Harbor " + (hh ? "loaded" : "absent") + ", Hospital Beds Gen 2 " + (hbg ? "loaded" : "absent"));
            t.Check(RwT.K.On(Sections.Hearth) == hh, "the Hearth & Harbor bridge area exists exactly when H&H is loaded");
            Building_Bed bed = RwT.Spawned.OfType<Building_Bed>().FirstOrDefault();
            if (!hh && bed != null)
            {
                // without H&H: RimWard's own two bed switches are the ones shown
                t.Check(!BedSelector.HidesOwnToggles(bed), "without Hearth & Harbor RimWard shows its own bed switches");
                List<string> labels = bed.GetGizmos().OfType<Command>().Select(c => c.defaultLabel ?? "").ToList();
                t.Check(labels.Contains("CommandBedSetAsHospitalLabel".Translate()), "the bed has the Hospital Bed switch (" + string.Join(" | ", labels.Take(12)) + ")");
            }
            bool ce = KitDetect.IsActive(HospitalMod.CombatExtendedId);
            System.Reflection.MethodBase ceTarget = HarmonyLib.AccessTools.Method("CombatExtended.AI.CompTend:TryGiveTacticalJob");
            bool ceApplied = ceTarget != null && (HarmonyLib.Harmony.GetPatchInfo(ceTarget)?.Prefixes.Any(px => px.owner == HospitalMod.PackageId) ?? false);
            t.Check(ceApplied == ce, "the Combat Extended self-tend patch is applied exactly when CE is loaded (CE " + (ce ? "loaded" : "absent") + ")");
            if (!hh && !hbg && !ce) t.Note("no other mod loaded: the H&H, HBG2 and CE branches did not run (run with extra mods for them)");
            if (hh && bed != null)
            {
                t.Check(HearthBridge.ProtocolVersion > 0, "H&H speaks the bed selector protocol (v" + HearthBridge.ProtocolVersion + ")");
                t.Check(BedSelector.HidesOwnToggles(bed), "RimWard hides its own bed toggles and shows the combined button");
                List<Gizmo> gizmos = bed.GetGizmos().ToList();
                int selectors = gizmos.Count(g => g is Command c && (c.defaultLabel ?? "").Contains("RimWard.Bed.Use.Patient".Translate()) || g.GetType().Name.Contains("Selector"));
                t.Note("bed gizmos: " + string.Join(" | ", gizmos.OfType<Command>().Select(c => c.defaultLabel)));
                foreach (BedUse u in new[] { BedUse.Patient, BedUse.Surgery, BedUse.Colonist })
                    t.Check(BedSelector.Entries(bed).Contains(u), "the bed button lists " + u);
                RwT.K.Current.SetSection(Sections.Hearth, false);
                t.Check(!BedSelector.HidesOwnToggles(bed), "bridge off: RimWard's own toggles come back");
                RwT.K.Current.SetSection(Sections.Hearth, true);
            }
            if (hbg)
            {
                ThingDef hb = DefDatabase<ThingDef>.AllDefsListForReading.FirstOrDefault(d => d.IsBed && d.modContentPack != null && KitDetect.Same(d.modContentPack.PackageId, HospitalMod.HospitalBedsId)
                    && d.building.bed_humanlike && d.Size.x == 1);
                t.Check(hb != null, "a single Hospital Beds Gen 2 bed def exists (" + hb?.defName + ")");
                if (hb != null && TestCells.TryFindFreeRect(map, 3, 4, out CellRect r))
                {
                    int before = comp.BedCount();
                    Thing th = ThingMaker.MakeThing(hb, hb.MadeFromStuff ? GenStuff.DefaultStuffFor(hb) : null);
                    th.SetFaction(Faction.OfPlayer);
                    GenSpawn.Spawn(th, r.CenterCell, map, Rot4.North);
                    RwT.Spawned.Add(th);
                    ((Building_Bed)th).Medical = true;
                    t.Check(comp.BedCount() == before + 1, "a medical Hospital Beds Gen 2 bed counts as a patient bed (" + before + " → " + comp.BedCount() + ")");
                }
            }
            RwT.CleanUp(map);
            yield return 1;
        }
    }

    /// <summary>rw.gravship (case 15, simulated): a patient on another map is moved there and re-lorded; that map uses the save's policy.</summary>
    sealed class GravshipSuite : KitTestSuite
    {
        public override string Name => "rw.gravship";
        public override int MaxTicks => 3000;
        public override IEnumerable<int> Run(TestContext t)
        {
            Map home = t.Map;
            RwT.SetUp(t, 2, 0);
            HospitalPolicy pol = HospitalSave.Get.Policy;
            pol.openingHours[3] = false; pol.openingHours[4] = false;
            Pawn p = RwT.NewPatient(home, PatientType.Wounds, out PatientData d, true, false);
            // random wounds may down the patient or let them bleed out on the bare new map: a flu keeps them a mobile patient who stays
            p?.health.RemoveAllHediffs(); p?.health.AddHediff(DefDatabase<HediffDef>.GetNamed("Flu"));
            Map other = null;
            Pawn keeper = null;
            try
            {
                // a tile without map features: vanilla's ancient-structure mutator logs "Failed to find location structure" on a small map
                PlanetTile tile = PlanetTile.Invalid;
                for (int i = 0; i < 30; i++)
                    if (TileFinder.TryFindNewSiteTile(out PlanetTile cand, home.Tile, 2, 12) && (Find.WorldGrid[cand].Mutators?.Count ?? 0) == 0) { tile = cand; break; }
                if (!tile.Valid && TileFinder.TryFindNewSiteTile(out PlanetTile any, home.Tile, 2, 12)) { tile = any; t.ExpectRedError("Failed to find location structure"); }
                if (tile.Valid)
                    other = GetOrGenerateMapUtility.GetOrGenerateMap(tile, new IntVec3(75, 1, 75), WorldObjectDefOf.Camp);
                if (other != null)
                {
                    // a colonist on the new map keeps it from being abandoned (as the gravship crew would)
                    keeper = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                    GenSpawn.Spawn(keeper, CellFinder.RandomClosewalkCellNear(other.Center, other, 8), other);
                }
            }
            catch (Exception e) { t.Note("second map failed: " + e.Message); }
            t.Check(other != null, "a second map exists (stands in for the map a gravship lands on)");
            if (other == null) { RwT.CleanUp(home); yield break; }
            HospitalMap there = HospitalMap.For(other);
            t.Check(there != null && RwT.Hours(there.Policy) == RwT.Hours(pol) && there.Policy.open == pol.open, "the new map's hospital uses the save's policy (open, same hours)");
            p.DeSpawn();   // leaves the map the way pawns board a gravship (the lord loses them)
            yield return 260;
            t.Check(RwT.Comp(home).GetPatientData(p, out PatientData inTransit) && !inTransit.Dismissed, "while in transit the patient keeps the entry (not dismissed)");
            GenSpawn.Spawn(p, CellFinder.RandomClosewalkCellNear(other.Center, other, 10), other);
            yield return 260;
            t.Check(!RwT.Comp(home).Patients.ContainsKey(p) && there.Patients.ContainsKey(p), "the patient's entry moved with them");
            yield return 260;
            there.GetPatientData(p, out PatientData moved);
            t.Note("on the new map: lord " + p.GetLord()?.LordJob?.GetType().Name + ", toil " + p.GetLord()?.CurLordToil?.GetType().Name + ", dismissed " + moved?.Dismissed
                + ", spawned " + p.Spawned + ", map " + p.MapHeld?.uniqueID + "/" + other.uniqueID + ", faction " + p.Faction?.Name + ", guest " + p.GuestStatus);
            t.Check(p.GetLord()?.LordJob is LordJob_VisitColonyAsPatient && moved != null && !moved.Dismissed, "and they are lorded as a patient on the new map");
            there.ForgetPatient(p); RwT.Discard(p);
            if (keeper != null && !keeper.Destroyed) { keeper.DeSpawn(); RwT.Discard(keeper); }
            try { if (Find.Maps.Contains(other)) Current.Game.DeinitAndRemoveMap(other, false); } catch (Exception e) { t.Note("removing the map: " + e.Message); }
            try { if (other.Parent != null && !other.Parent.Destroyed) other.Parent.Destroy(); } catch { }
            Current.Game.CurrentMap = home;
            pol.openingHours[3] = true; pol.openingHours[4] = true;
            RwT.CleanUp(home);
        }
    }

    /// <summary>rw.zlog (runs last): the whole game log of the run holds no RimWard warning or error and no "Tried to discard" (a pawn RimWard leaked).</summary>
    sealed class LogSuite : KitTestSuite
    {
        public override string Name => "rw.zlog";
        public override int MaxTicks => 300;
        public override IEnumerable<int> Run(TestContext t)
        {
            List<string> ours = RwT.LogLines("[RimWard]", true);
            List<string> discard = RwT.LogLines("Tried to discard", true);
            foreach (string l in ours.Concat(discard).Take(8)) t.Note("log: " + l.Split('\n')[0]);
            t.Check(ours.Count == 0, "no [RimWard] warning or error in the whole run (" + ours.Count + ")");
            t.Check(discard.Count == 0, "no \"Tried to discard\" warning: every pawn RimWard throws away is discarded properly (" + discard.Count + ")");
            yield return 1;
        }
    }

    /// <summary>rw.saveload (case 12, two phases, explicit): an offer, a patient mid-surgery, a dismissed patient and a discarded pawn survive save and load.</summary>
    sealed class StateSaveLoadSuite : KitTestSuite
    {
        public override string Name => "rw.saveload";
        public override int MaxTicks => 6000;
        public override bool ExplicitOnly => true;
        public override bool KeepsLoadedGame => true;
        const string SaveName = "rw_state";
        public override IEnumerable<int> Run(TestContext t)
        {
            Map map = t.Map;
            if (t.LoadedSave == null)
            {
                RwT.SetUp(t, 4, 1);
                HospitalMap comp = RwT.Comp(map);
                float modeBefore = RwT.K.Get(Keys.IntakeMode);
                RwT.K.Set(Keys.IntakeMode, (float)(int)IntakeMode.Prompt);
                IncidentDef inc = DefDatabase<IncidentDef>.GetNamed("RimWard_PatientArrives");
                for (int i = 0; i < 5 && comp.pendingOffers.Count == 0; i++) inc.Worker.TryExecute(new IncidentParms { target = map });
                RwT.K.Set(Keys.IntakeMode, modeBefore);
                Pawn surg = RwT.NewPatient(map, PatientType.Wounds, out PatientData ds, true, false);
                BillingSuite.MakeArmCase(surg, ds, out _, out _);
                ds.SurgeryAttempts = 1; ds.RetryAfterTick = RwT.Now + 5000;
                ds.AddLine(InvoiceLineKind.Tend, "test", 33f);
                Pawn dis = RwT.NewPatient(map, PatientType.Disease, out PatientData dd, true, false);
                comp.DismissPatient(dis);
                Pawn gone = RwT.NewPatient(map, PatientType.Wounds, out PatientData dg, true, false);
                gone.GetLord()?.RemovePawn(gone); IncidentHelper.DiscardGenerated(gone);
                t.Check(comp.Patients.ContainsKey(gone) && gone.Discarded, "a patient entry whose pawn was discarded exists before saving");
                yield return 1;
                var side = new Dictionary<string, string>
                {
                    ["offers"] = comp.pendingOffers.Count.ToString(),
                    ["surg"] = surg.ThingID, ["surgLines"] = ds.Lines.Count.ToString(), ["surgRetry"] = ds.RetryAfterTick.ToString(), ["surgRecipe"] = ds.CureRecipe.defName,
                    ["dis"] = dis.ThingID, ["hours"] = RwT.Hours(HospitalSave.Get.Policy),
                    ["patients"] = (comp.Patients.Count - 1).ToString(),
                };
                t.Check(t.SaveGame(SaveName), "saved as " + SaveName);
                RwT.WriteSide(SaveName, side);
                t.Check(!comp.Patients.ContainsKey(gone), "saving dropped the entry of the discarded pawn (no null key later)");
                yield break;
            }
            // phase 2
            var want = RwT.ReadSide(SaveName);
            t.Check(want != null, "phase 2: the side file of the save exists");
            if (want == null) yield break;
            HospitalMap c2 = RwT.Comp(map);
            t.Check(RwT.LogLines("Null key", false).Count == 0, "no \"Null key\" error while loading");
            t.Check(c2.pendingOffers.Count.ToString() == want["offers"] && c2.pendingOffers.All(o => o.pawn != null && o.data != null), "the pending offer came back (" + c2.pendingOffers.Count + ")");
            Pawn s2 = c2.Patients.Keys.FirstOrDefault(p => p.ThingID == want["surg"]);
            t.Check(s2 != null && c2.GetPatientData(s2, out PatientData sd) && sd.Lines.Count.ToString() == want["surgLines"] && sd.RetryAfterTick.ToString() == want["surgRetry"]
                && sd.CureRecipe?.defName == want["surgRecipe"], "the patient mid-surgery came back with lines, retry tick and cure");
            Pawn d2 = c2.Patients.Keys.FirstOrDefault(p => p.ThingID == want["dis"]);
            t.Check(d2 == null || (c2.GetPatientData(d2, out PatientData dd2) && dd2.Dismissed), "the dismissed patient is still dismissed (or already gone)");
            t.Check(c2.Patients.Keys.All(p => p != null && !p.Discarded), "no entry with a missing pawn");
            t.Check(RwT.Hours(HospitalSave.Get.Policy) == want["hours"], "the policy came back");
            yield return 2500;
            t.Check(t.RedErrorsSince(0).Count == 0, "2500 ticks after the load: no RimWard error");
        }
    }

    /// <summary>rw.oldsave (case 13, two phases, explicit): a save in the 1.x format (policy fields on the map) loads with the same policy.</summary>
    sealed class OldSaveSuite : KitTestSuite
    {
        public override string Name => "rw.oldsave";
        public override int MaxTicks => 4000;
        public override bool ExplicitOnly => true;
        public override bool KeepsLoadedGame => true;
        const string Base = "rw_new", Old = "rw_old";
        public override IEnumerable<int> Run(TestContext t)
        {
            Map map = t.Map;
            if (t.LoadedSave == null)
            {
                HospitalPolicy pol = HospitalSave.Get.Policy;
                pol.Repair();
                pol.open = true; pol.acceptSurgery = false; pol.acceptDanger = true; pol.massCasualties = true;
                for (int i = 0; i < 24; i++) pol.openingHours[i] = i == 3 || i == 9 || i == 21;
                RecipeDef refused = DefDatabase<RecipeDef>.GetNamedSilentFail("InstallBionicArm");
                pol.refusedOperations.Clear(); if (refused != null) pol.refusedOperations.Add(refused);
                FoodPolicy fp = Current.Game.foodRestrictionDatabase.AllFoodRestrictions.LastOrDefault();
                pol.foodPolicy = fp;
                yield return 1;
                t.Check(t.SaveGame(Base), "saved a 2.0 save to rewrite");
                var doc = new System.Xml.XmlDocument();
                doc.Load(GenFilePaths.FilePathForSavedGame(Base));
                // a 2.0 save keeps RimWard's state in the framework's components; RimWard 1.x kept it in its own components with the policy
                // fields on the map component: move the data into those legacy component nodes and the policy onto the map
                var gameComps = doc.SelectSingleNode("//game/components");
                var fwData = doc.SelectSingleNode("//game/components/li[@Class='JBFramework.FrameworkGame']/mods/li[id='juliusberger.rimward']/data");
                var gcNode = doc.CreateElement("li"); gcNode.SetAttribute("Class", "RimWard.HospitalGameComponent");
                if (fwData != null) { gcNode.InnerXml = fwData.InnerXml; fwData.ParentNode.RemoveChild(fwData); }
                gameComps?.AppendChild(gcNode);
                var polNode = gcNode.SelectSingleNode("policy");
                string foodRef = polNode?.SelectSingleNode("foodPolicy")?.InnerText;
                if (polNode != null) gcNode.RemoveChild(polNode);
                var migNode = gcNode.SelectSingleNode("policyMigrated");
                if (migNode != null) gcNode.RemoveChild(migNode);
                var mapComps = doc.SelectSingleNode("//maps/li/components");
                var mapEntry = doc.SelectSingleNode("//maps/li/components/li[@Class='JBFramework.FrameworkMap']/mods/li[id='juliusberger.rimward']");
                var mapData = mapEntry?.SelectSingleNode("data");
                var mapComp = doc.CreateElement("li"); mapComp.SetAttribute("Class", "RimWard.HospitalMapComponent");
                // a save without RimWard's map data has no entry for it in the framework's map component (as 1.x and 2.0 beta saves)
                if (mapData != null) { mapComp.InnerXml = mapData.InnerXml; mapEntry.ParentNode.RemoveChild(mapEntry); }
                var mce = mapComp.SelectSingleNode("MCEtriggered"); if (mce != null) mapComp.RemoveChild(mce);
                mapComps?.AppendChild(mapComp);
                void Add(string name, string inner) { var e = doc.CreateElement(name); e.InnerXml = inner; mapComp.AppendChild(e); }
                var own = mapComp.SelectSingleNode("ownPolicy"); if (own != null) mapComp.RemoveChild(own);
                Add("openForBusiness", "True");
                Add("openingHours", string.Concat(pol.openingHours.Select(b => "<li>" + b + "</li>")));
                Add("refusedOperations", refused != null ? "<li>" + refused.defName + "</li>" : "");
                if (!foodRef.NullOrEmpty()) Add("PatientFoodPolicy", foodRef);
                Add("massCasualties", "True"); Add("acceptSurgery", "False"); Add("AcceptDanger", "True");
                t.Check(fwData != null && polNode != null && mapData != null && gameComps != null && mapComps != null,
                    "rewrote the save into the 1.x layout (RimWard's own components, policy on the map component)");
                doc.Save(GenFilePaths.FilePathForSavedGame(Old));
                bool foodFound = !foodRef.NullOrEmpty();
                RwT.WriteSide(Old, new Dictionary<string, string>
                {
                    ["hours"] = RwT.Hours(pol), ["refused"] = refused?.defName ?? "", ["food"] = fp?.label ?? "", ["foodFound"] = foodFound.ToString(),
                });
                yield break;
            }
            var want = RwT.ReadSide(Old);
            t.Check(want != null && t.LoadedSave == Old, "phase 2: loaded the 1.x-layout save " + t.LoadedSave);
            if (want == null) yield break;
            HospitalSave gc = HospitalSave.Get;
            HospitalPolicy p2 = gc.Policy;
            t.Check(gc.policyMigrated && gc.LoadedFromSave, "the old per-map policy was migrated");
            t.Check(p2.open && RwT.Hours(p2) == want["hours"], "open with the same hours (" + RwT.Hours(p2) + ")");
            t.Check(!p2.acceptSurgery && p2.acceptDanger && p2.massCasualties, "accept surgery / danger / mass casualties as before");
            t.Check(p2.refusedOperations.Any(r => r.defName == want["refused"]), "the refused operation came along");
            if (want["foodFound"] == "True") t.Check(p2.FoodPolicy?.label == want["food"], "the food policy came along (" + p2.FoodPolicy?.label + ")");
            t.Check(!RwT.Comp(map).HasOwnPolicy, "a single map gets no own copy");
            yield return 1;
            t.Check(!Current.Game.components.Any(c => c is HospitalGameComponent) && !map.components.Any(c => c is HospitalMapComponent || c is IncidentScheduler),
                "the legacy components were dropped after the load (they never reach a new save)");
            t.Check(t.RedErrorsSince(0).Count == 0, "no RimWard error while loading");
            yield return 1;
        }
    }

    /// <summary>rw.addmid (case 14b, two phases, explicit): a save without any RimWard data (mod added mid-game) opens the hospital per the setting.</summary>
    sealed class AddMidSuite : KitTestSuite
    {
        public override string Name => "rw.addmid";
        public override int MaxTicks => 3000;
        public override bool ExplicitOnly => true;
        public override bool KeepsLoadedGame => true;
        public override IEnumerable<int> Run(TestContext t)
        {
            if (t.LoadedSave == null)
            {
                HospitalSave.Get.Policy.open = false;
                yield return 1;
                t.Check(t.SaveGame("rw_base"), "saved");
                var doc = new System.Xml.XmlDocument();
                doc.Load(GenFilePaths.FilePathForSavedGame("rw_base"));
                int removed = 0;
                foreach (System.Xml.XmlNode n in doc.SelectNodes("//li[starts-with(@Class,'RimWard.')] | //mods/li[id='juliusberger.rimward']").Cast<System.Xml.XmlNode>().ToList())
                { n.ParentNode.RemoveChild(n); removed++; }
                t.Check(removed >= 2 && !doc.OuterXml.Contains("RimWard.") && !doc.OuterXml.Contains("<id>juliusberger.rimward</id>"),
                    "the rewritten save holds no RimWard data (" + removed + " nodes removed: the framework's entries of RimWard and its lords)");
                doc.Save(GenFilePaths.FilePathForSavedGame("rw_nomod"));
                yield break;
            }
            HospitalSave gc = HospitalSave.Get;
            t.Check(t.LoadedSave == "rw_nomod", "phase 2: loaded the save without RimWard data");
            t.Check(gc != null && !gc.LoadedFromSave, "the hospital component was created fresh");
            t.Check(gc.Policy.open == RwT.S.OpenInNewColonies, "the hospital is " + (gc.Policy.open ? "open" : "closed") + " as the setting says (" + RwT.S.OpenInNewColonies + ")");
            yield return 600;
            t.Check(t.RedErrorsSince(0).Count == 0, "no RimWard error");
        }
    }
}
#endif
