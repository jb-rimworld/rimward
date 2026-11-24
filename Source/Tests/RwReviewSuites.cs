// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Copyright (C) 2026 juliusberger00. Written for RimWard 2.0 (a GPL-3.0 fork of Hospital by Adamas and
// Hospital Fix And Tweak by assssssqwww). Licensed under the GNU General Public License version 3 (LICENSE);
// full source: https://github.com/jb-rimworld/rimward
#if JB_TESTS
// Regression suites for the independent review of 24.11.2026 (rw.review in "all"; rw.longrun.defaults explicit). JB_TESTS only.
using System;
using System.Collections.Generic;
using System.Linq;
using JBFramework.Testing;
using RimWard.Patches;
using RimWard.Utilities;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace RimWard.Tests
{
    /// <summary>
    /// rw.review: one block per defect the review found. Defaults bring patients (1), the intake asks before operations (4),
    /// complications never kill (3), harvesting a patient is a violation and unpaid (5), a hostile patient is let go (7),
    /// a throwing patient tick is fenced (8), unspawned discharges pay (12), no vanilla disease letters for patients (16),
    /// the arrival letter starts with a capital (14), the pitch is translated (10).
    /// </summary>
    sealed class ReviewSuite : KitTestSuite
    {
        public override string Name => "rw.review";
        public override int MaxTicks => 6000;

        public override IEnumerable<int> Run(TestContext t)
        {
            Map map = t.Map;
            HospitalMapComponent comp = RwT.Comp(map);

            // ---- (1) defaults bring patients
            HospitalPolicy fresh = HospitalPolicy.NewDefault(true);
            t.Check(fresh.OpenHourCount == 24, "a new hospital is open all day (" + fresh.OpenHourCount + " hours)");
            float one = IncidentScheduler.ExpectedAttemptsPerDay(1), two = IncidentScheduler.ExpectedAttemptsPerDay(2), twenty = IncidentScheduler.ExpectedAttemptsPerDay(20);
            t.Note("expected arrival attempts a day: 1 bed " + one.ToString("0.00") + ", 2 beds " + two.ToString("0.00") + ", 20 beds " + twenty.ToString("0.00"));
            t.Check(one >= 0.8f && two >= 0.8f, "one or two free beds still bring about one patient a day");
            t.Check(twenty > two && twenty <= 10f, "more free beds bring more patients, within reason");
            t.Check(IncidentScheduler.ChancePerCheck(0, 0) == 0f, "no free bed: no arrival");
            // a simulated week on the default policy with two free beds: every 42 ticks a check, open hours as the policy says
            int arrivals = 0;
            Rand.PushState(424242);
            try
            {
                for (int tick = 0; tick < 7 * GenDate.TicksPerDay; tick += 42)
                {
                    int day = tick / GenDate.TicksPerDay, hour = (tick % GenDate.TicksPerDay) / GenDate.TicksPerHour;
                    if (Rand.Chance(IncidentScheduler.ChancePerCheck(2, day)) && fresh.IsOpenAtHour(hour)) arrivals++;
                }
            }
            finally { Rand.PopState(); }
            t.Check(arrivals >= 4, "a simulated week on defaults with two free beds brings at least four patients (" + arrivals + ")");

            // ---- (4) the default intake asks first for surgery cases
            t.Check(RwT.S.Intake == IntakeMode.Hybrid, "default intake: Hybrid (a letter asks before an operation) (" + RwT.S.Intake + ")");

            RwT.SetUp(t, 4, 2);
            Pawn doc = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(c => !c.WorkTypeIsDisabled(WorkTypeDefOf.Doctor));
            IncidentDef inc = DefDatabase<IncidentDef>.GetNamed("RimWard_PatientArrives");
            {
                int offers0 = comp.pendingOffers.Count, surgeryDelivered = 0, tries = 0;
                var seen = new HashSet<Pawn>(comp.Patients.Keys);
                bool oldAccept = comp.Policy.acceptDanger;
                comp.Policy.acceptDanger = true;
                Threats.ClearCache();
                for (; tries < 25 && comp.pendingOffers.Count == offers0; tries++)
                {
                    inc.Worker.TryExecute(new IncidentParms { target = map });
                    foreach (var kv in comp.Patients.ToList())
                        if (!seen.Contains(kv.Key)) { seen.Add(kv.Key); if (kv.Value.Type == PatientType.Surgery) surgeryDelivered++; }
                }
                comp.Policy.acceptDanger = oldAccept;
                List<PatientOffer> offers = comp.pendingOffers.Skip(offers0).ToList();
                t.Note(tries + " arrivals tried, " + offers.Count + " offers");
                t.Check(surgeryDelivered == 0, "Hybrid: no surgery case arrived without a letter (" + surgeryDelivered + ")");
                t.Check(offers.Count == 0 || offers.All(o => o.data != null && o.data.Type == PatientType.Surgery || (o.pawn?.royalty?.AllTitlesForReading.Count ?? 0) > 0),
                    "Hybrid: offers are surgery cases or nobles");
                // (14) the arrival letter or message of a delivered patient starts with a capital letter
                Letter arrival = Find.LetterStack.LettersListForReading.LastOrDefault(l => l.def == LetterDefOf.PositiveEvent);
                string text = (arrival as ChoiceLetter)?.Text.ToString();
                if (!text.NullOrEmpty()) t.Check(char.IsUpper(text[0]), "the arrival letter starts with a capital (" + text.Truncate(60) + ")");
                RwT.CleanUp(map);
                RwT.SetUp(t, 4, 2);
            }

            // ---- (3) the secondary disease of a complication can never kill
            List<HediffDef> pool = DiseaseUtility.SecondaryDiseasePool();
            t.Note("secondary diseases: " + string.Join(", ", pool.Select(d => d.defName)));
            t.Check(pool.All(d => d.lethalSeverity <= 0f && (d.stages == null || d.stages.All(st => st == null || (!st.lifeThreatening && st.deathMtbDays <= 0f)))),
                "no secondary disease can kill (no flu, plague or malaria)");
            HediffDef flu = DefDatabase<HediffDef>.GetNamedSilentFail("Flu");
            t.Check(flu == null || !pool.Contains(flu), "flu is not a complication");

            // ---- (5) harvesting a patient's organ: a violation, and not billed
            Pawn h = RwT.NewPatient(map, PatientType.Wounds, out PatientData hd, true, false);
            if (h != null && doc != null)
            {
                BillingSuite.MakeArmCase(h, hd, out Bill_Medical armBill, out _);
                BodyPartRecord shoulder = armBill?.Part;
                t.Check(armBill != null && !armBill.recipe.Worker.IsViolationOnPawn(h, shoulder, Faction.OfPlayer), "the treatment the patient came for is no violation");
                BodyPartRecord kidney = h.health.hediffSet.GetNotMissingParts().FirstOrDefault(p => p.def.defName == "Kidney");
                RecipeDef remove = RecipeDefOf.RemoveBodyPart;
                t.Check(kidney != null && remove.Worker.IsViolationOnPawn(h, kidney, Faction.OfPlayer), "removing a healthy kidney from a patient is a violation (as on any guest)");
                if (kidney != null)
                {
                    Bill_Medical harvest = HealthCardUtility.CreateSurgeryBill(h, remove, kidney, null, false);
                    int lines0 = hd.Lines.Count;
                    int gw0 = h.Faction.PlayerGoodwill;
                    Test_ForceSurgeryOutcome.Force = ForcedOutcome.Success;
                    try { harvest.Notify_IterationCompleted(doc, new List<Thing> { ThingMaker.MakeThing(ThingDefOf.MedicineIndustrial) }); }
                    finally { Test_ForceSurgeryOutcome.Force = ForcedOutcome.None; }
                    t.Check(hd.Lines.Count == lines0, "the harvest adds no line to the patient's bill (" + (hd.Lines.Count - lines0) + " new)");
                    t.Check(h.Faction.PlayerGoodwill < gw0, "the patient's faction loses goodwill over the harvest (" + gw0 + " -> " + h.Faction.PlayerGoodwill + ")");
                    if (h.Faction.PlayerGoodwill < gw0) h.Faction.ChangeGoodwill_Debug(Faction.OfPlayer, gw0 - h.Faction.PlayerGoodwill);
                }
            }
            yield return 30;

            // ---- (12) a discharge while the patient is not on the map still pays, at the trade drop spot
            Pawn u = RwT.NewPatient(map, PatientType.Wounds, out PatientData ud, true, false);
            if (u != null)
            {
                ud.AddLine(InvoiceLineKind.Tend, "test", 200f);
                ud.HasPendingSurgeryBill = false;
                u.DeSpawn();
                int silver0 = RwT.SilverOnMap(map);
                long earned0 = HospitalGameComponent.Get.silverEarned;
                Invoice inv = comp.Discharge(u, ud, DischargeReason.Cured);
                int dropped = RwT.SilverOnMap(map) - silver0;
                long counted = HospitalGameComponent.Get.silverEarned - earned0;
                t.Check(inv != null && inv.TotalSilver > 0 && dropped == inv.TotalSilver && counted == dropped,
                    "unspawned discharge: invoice " + inv?.TotalSilver + ", silver on the map " + dropped + ", counted " + counted);
                comp.ForgetPatient(u);
                RwT.Discard(u);
            }

            // ---- (7) a patient whose faction turns hostile is let go and not made a patient again
            Pawn a = RwT.NewPatient(map, PatientType.Wounds, out PatientData ad, true, false);
            Pawn b = RwT.NewPatient(map, PatientType.Wounds, out PatientData bd, true, false);
            if (a != null && b != null && a.Faction == b.Faction)
            {
                Faction fac = a.Faction;
                int gw0 = fac.PlayerGoodwill;
                b.guest.SetGuestStatus(null, GuestStatus.Guest);   // a patient vanilla does not hold as our guest (no arrest)
                int relord0 = Caretaker.Counts.TryGetValue(Caretaker.Fix.Relorded, out int n0) ? n0 : 0;
                try
                {
                    fac.ChangeGoodwill_Debug(Faction.OfPlayer, -200);
                    t.Check(fac.HostileTo(Faction.OfPlayer), "the patients' faction is hostile now");
                    for (int i = 0; i < 3; i++) { Caretaker.Run(comp); yield return 250; }
                    int relord = Caretaker.Counts.TryGetValue(Caretaker.Fix.Relorded, out int n1) ? n1 : 0;
                    t.Check(!comp.Patients.ContainsKey(a) && a.IsPrisoner, "a hostile faction's guest patient is arrested by vanilla and no longer a patient");
                    t.Check(!comp.Patients.ContainsKey(b) && !b.IsPrisoner, "a hostile patient who is not our guest is no longer a patient either");
                    t.Check(!(b.GetLord()?.LordJob is LordJob_VisitColonyAsPatient), "and is not lorded as a patient again (" + b.GetLord()?.LordJob?.GetType().Name + ")");
                    t.Check(relord == relord0, "the caretaker did not re-lord anyone (" + (relord - relord0) + ")");
                }
                finally
                {
                    fac.ChangeGoodwill_Debug(Faction.OfPlayer, gw0 - fac.PlayerGoodwill);
                    comp.ForgetPatient(a); comp.ForgetPatient(b);
                    RwT.Discard(a); RwT.Discard(b);
                }
                t.Check(!fac.HostileTo(Faction.OfPlayer), "the faction is back to its old goodwill (" + fac.PlayerGoodwill + ")");
            }
            else { t.Check(false, "two patients of one faction for the hostility case"); RwT.Discard(a); RwT.Discard(b); }

            // ---- (16) no vanilla "new disease" letter for a patient; a message instead
            Pawn s = RwT.NewPatient(map, PatientType.Wounds, out PatientData sd, true, false);
            HediffDef infection = DefDatabase<HediffDef>.GetNamedSilentFail("WoundInfection");   // vanilla's "Disease: Infection" letter
            if (s != null && infection != null)
            {
                int letters0 = Find.LetterStack.LettersListForReading.Count;
                BodyPartRecord leg = s.health.hediffSet.GetNotMissingParts().FirstOrDefault(x => x.def.defName == "Leg") ?? s.RaceProps.body.corePart;
                Hediff inf = HediffMaker.MakeHediff(infection, s, leg); inf.Severity = 0.5f;   // past the stage where it becomes visible
                s.health.AddHediff(inf, leg);
                yield return 120;
                List<Letter> newLetters = Find.LetterStack.LettersListForReading.Skip(letters0).ToList();
                t.Check(!newLetters.Any(l => l.lookTargets != null && l.lookTargets.targets.Any(x => x.Thing == s)), "no vanilla disease letter for an admitted patient (" + string.Join(", ", newLetters.Select(l => l.Label.ToString())) + ")");
                HediffComp_Discoverable disc = s.health.hediffSet.GetFirstHediffOfDef(infection)?.TryGetComp<HediffComp_Discoverable>();
                List<Message> live = (List<Message>)HarmonyLib.AccessTools.Field(typeof(Messages), "liveMessages").GetValue(null);
                t.Note("infection visible " + s.health.hediffSet.GetFirstHediffOfDef(infection)?.Visible + ", discoverable " + (disc != null) + " (letter " + disc?.Props.sendLetterWhenDiscovered + "), messages: " + string.Join(" | ", live.Select(m => m.text).Reverse().Take(4)));
                t.Check(((List<Message>)HarmonyLib.AccessTools.Field(typeof(Messages), "liveMessages").GetValue(null)).Any(m => m.text != null && m.text.Contains(s.LabelShortCap) && m.text.Contains(infection.label)), "a message tells the player instead");
                // a patient being made: no letter at all
                int letters1 = Find.LetterStack.LettersListForReading.Count;
                Pawn g = RwT.NewPatient(map, PatientType.Disease, out PatientData gd, false, true);
                t.Check(Find.LetterStack.LettersListForReading.Count == letters1, "building a disease case sends no vanilla letter (" + (Find.LetterStack.LettersListForReading.Count - letters1) + ")");
                RwT.Discard(g);
                comp.ForgetPatient(s); RwT.Discard(s);
            }

            // ---- (8) a patient tick that throws is fenced: logged once, the other patients still tick
            Pawn x1 = RwT.NewPatient(map, PatientType.Wounds, out PatientData xd1, true, false);
            Pawn x2 = RwT.NewPatient(map, PatientType.Wounds, out PatientData xd2, true, false);
            if (x1 != null && x2 != null)
            {
                int ticked = 0, errors0 = Log.Messages.Count(m => m.type == LogMessageType.Error);
                RwT.ExpectRedError("[JBTEST-EXPECTED]");
                HospitalMapComponent.TestTickHook = p =>
                {
                    if (p == x1) throw new InvalidOperationException("[JBTEST-EXPECTED] thrown by rw.review");
                    if (p == x2) ticked++;
                };
                try { yield return 760; }   // three real hospital ticks (every 250 ticks)
                finally { HospitalMapComponent.TestTickHook = null; }
                int errors = Log.Messages.Count(m => m.type == LogMessageType.Error) - errors0;
                Find.WindowStack.TryRemove(typeof(LudeonTK.EditWindow_Log), false);   // the expected error opened the debug log; later screenshots need a clear screen
                t.Check(ticked >= 1, "the other patient still ticks (" + ticked + ")");
                t.Check(errors <= 1, "logged once, not every 250 ticks (" + errors + " errors)");
                comp.ForgetPatient(x1); comp.ForgetPatient(x2); RwT.Discard(x1); RwT.Discard(x2);
            }

            // ---- (10) the welcome pitch follows the game language
            t.Check(PitchTranslation.Applied && RwT.K.Info.FirstStartPitch == "RimWard.FirstStartPitch".Translate().ToString(),
                "the welcome card's pitch is the translated text (" + RwT.K.Info.FirstStartPitch.Truncate(50) + ")");

            RwT.CleanUp(map);
        }
    }

    /// <summary>
    /// rw.longrun.defaults (explicit): three in-game days on the defaults a new player gets (open all day, Hybrid intake, two free patient
    /// beds), real arrivals from the scheduler. Offers are accepted as a player would. Passes with at least two patients (arrived or offered).
    /// The 1.x defaults (four opening hours, no floor) gave about one patient a week here.
    /// </summary>
    sealed class LongRunDefaultsSuite : KitTestSuite
    {
        public override string Name => "rw.longrun.defaults";
        public override int MaxTicks => 4 * GenDate.TicksPerDay;
        public override bool ExplicitOnly => true;

        public override IEnumerable<int> Run(TestContext t)
        {
            Map map = t.Map;
            HospitalMapComponent comp = RwT.Comp(map);
            RwT.SetUp(t, 2, 0);
            HospitalPolicy pol = HospitalGameComponent.Get.Policy;
            HospitalPolicy def = HospitalPolicy.NewDefault(true);
            pol.openingHours = def.openingHours; pol.acceptSurgery = def.acceptSurgery; pol.acceptDanger = def.acceptDanger;
            t.Check(RwT.S.Intake == IntakeMode.Hybrid && pol.OpenHourCount == 24, "defaults: Hybrid intake, open all day");
            IncidentScheduler.PausedForTests = false;
            var seen = new HashSet<int>();
            int start = RwT.Now, accepted = 0;
            while (RwT.Now - start < 3 * GenDate.TicksPerDay)
            {
                yield return 1250;
                foreach (Pawn p in comp.Patients.Keys) if (p != null) seen.Add(p.thingIDNumber);
                foreach (PatientOffer o in comp.pendingOffers.ToList())
                {
                    if (o.pawn != null) seen.Add(o.pawn.thingIDNumber);
                    if (comp.ResolveOffer(o.id, true)) accepted++;
                }
                if (map.listerThings.ThingsInGroup(ThingRequestGroup.Medicine).Sum(x => x.stackCount) < 10) RwT.Place(ThingDefOf.MedicineIndustrial, 15, map);
                if (map.listerThings.ThingsOfDef(ThingDefOf.MealSimple).Sum(x => x.stackCount) < 10) RwT.Place(ThingDefOf.MealSimple, 20, map);
            }
            t.Note("3 days on defaults: " + seen.Count + " patients (" + accepted + " offers accepted), now " + comp.Patients.Count + " here");
            t.Check(seen.Count >= 2, "at least two patients in three days on the defaults (" + seen.Count + ")");
            List<string> red = TabT.RedSince(0);
            foreach (string l in red.Take(5)) t.Note("red: " + l);
            t.Check(red.Count == 0, "no red error of RimWard or the framework (" + red.Count + ")");
            RwT.CleanUp(map);
        }
    }
}
#endif
