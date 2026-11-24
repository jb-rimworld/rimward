using System.Collections.Generic;
using System.Linq;
using RimWard.Utilities;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimWard
{
    /// <summary>Where a patient's stay stands. Every value has a readable text (key RimWard.Why.&lt;value&gt;) for the "why still here" column.</summary>
    public enum PatientStatus
    {
        None,
        Arriving,
        Acute,
        NeedsTending,
        Resting,
        WaitingForSurgery,
        SurgeryRetry,
        SurgerySuspended,
        TreatmentImpossible,
        HeldByDanger,
        ReadyToLeave,
        Leaving
    }

    /// <summary>
    /// The caretaker: no patient ever gets stuck. Runs every 250 ticks per map (from <see cref="HospitalMapComponent.MapComponentTick"/>),
    /// repairs what it finds and counts every repair (for the diagnosis page). The same predicates feed the "why still here" text,
    /// so what the player reads is what the caretaker acts on.
    /// </summary>
    public static class Caretaker
    {
        public enum Fix { StaleEntry, MovedToOtherMap, PrisonerOrRecruited, Relorded, ExitLordGiven, LostCureBill, ImpossibleLetter, ImpossibleDischarge, MaxStay, DangerTimeout }

        private static readonly Dictionary<Fix, int> counts = new Dictionary<Fix, int>();
        private static readonly List<string> recent = new List<string>();
        public const int RecentCount = 20;

        /// <summary>How often each repair ran this session.</summary>
        public static IReadOnlyDictionary<Fix, int> Counts => counts;
        /// <summary>The last 20 repairs, newest first ("tick: fix: pawn").</summary>
        public static IReadOnlyList<string> Recent => recent;

        public static void Log(HospitalMapComponent comp, Fix fix, Pawn pawn)
        {
            counts[fix] = counts.TryGetValue(fix, out int n) ? n + 1 : 1;
            int tick = Find.TickManager?.TicksGame ?? 0;
            recent.Insert(0, tick + ": " + fix + ": " + (pawn?.LabelShort ?? "?"));
            while (recent.Count > RecentCount) recent.RemoveAt(recent.Count - 1);
            // FW-REQUEST: counted here (diagnosis page); KitMod.CaretakerFixed would log every routine repair as a warning (request 4)
            if (Prefs.DevMode) Verse.Log.Message("[RimWard] caretaker " + fix + " " + (pawn?.LabelShort ?? "?"));
        }

        /// <summary>The patient is gone for good (no map will see them again as our patient).</summary>
        public static bool IsGone(Pawn p)
        {
            return p == null || p.Discarded || p.Dead || (p.Destroyed && !p.Dead) || p.IsWorldPawn() || p.GetCaravan() != null;
        }

        public static void Run(HospitalMapComponent comp)
        {
            int now = Find.TickManager.TicksGame;
            Settings s = HospitalMod.Settings;
            foreach (KeyValuePair<Pawn, PatientData> kv in comp.Patients.ToList())
            {
                Pawn pawn = kv.Key;
                PatientData data = kv.Value;
                try
                {
                    // 1. stale entries: gone pawns, pawns on another map (gravship, transport pod)
                    if (data == null || IsGone(pawn))
                    {
                        if (pawn != null) comp.Patients.Remove(pawn);
                        Log(comp, Fix.StaleEntry, pawn);
                        continue;
                    }
                    Map held = pawn.MapHeld;
                    if (held == null) continue; // in transit (e.g. on a gravship in flight): wait, decide on arrival
                    if (held != comp.map)
                    {
                        comp.TransferPatientTo(pawn, HospitalMapComponent.For(held));
                        Log(comp, Fix.MovedToOtherMap, pawn);
                        continue;
                    }

                    // 7. arrested, enslaved or recruited: no longer a patient, status untouched (fixes "arrest is undone")
                    if (pawn.IsPrisoner || pawn.IsSlave || (pawn.Faction != null && pawn.Faction.IsPlayer))
                    {
                        comp.ForgetPatient(pawn);
                        Log(comp, Fix.PrisonerOrRecruited, pawn);
                        continue;
                    }

                    if (!pawn.Spawned) continue; // carried, in a pod, in a cryptosleep casket: nothing to repair now

                    if (data.Dismissed)
                    {
                        // 2. a dismissed patient without a lord would wander as a guest forever: walk them out
                        if (pawn.GetLord() == null && !pawn.Downed)
                        {
                            LordMaker.MakeNewLord(pawn.Faction, new LordJob_ExitMapBest(LocomotionUrgency.Walk), comp.map, new List<Pawn> { pawn });
                            Log(comp, Fix.ExitLordGiven, pawn);
                        }
                        continue;
                    }

                    // 6. a patient without our lord job (lost on map change, taken by another mod)
                    Lord lord = pawn.GetLord();
                    if (lord == null || !(lord.LordJob is LordJob_VisitColonyAsPatient) || lord.Map != comp.map)
                    {
                        lord?.RemovePawn(pawn);
                        LordMaker.MakeNewLord(pawn.Faction, new LordJob_VisitColonyAsPatient(pawn.Faction), comp.map, new List<Pawn> { pawn });
                        Log(comp, Fix.Relorded, pawn);
                    }

                    // 5. maximum stay
                    if (s.MaxStayDays >= 0.5f && now - data.ArrivedAtTick > (int)(s.MaxStayDays * GenDate.TicksPerDay))
                    {
                        Log(comp, Fix.MaxStay, pawn);
                        comp.Discharge(pawn, data, DischargeReason.MaxStay);
                        continue;
                    }

                    // the cure bill vanished without the cure (not deleted by the player: that clears the pending flag)
                    if (data.HasPendingSurgeryBill && data.RetryAfterTick < 0 && data.CureRecipe != null && CureBill(pawn, data) == null
                        && !HospitalMapComponent.CureAchieved(pawn, data) && comp.IsSurgeryRecipeAllowed(data.CureRecipe))
                    {
                        BodyPartRecord part = data.CureRecipe.targetsBodyPart ? data.CureRecipe.Worker.GetPartsToApplyOn(pawn, data.CureRecipe).FirstOrDefault() : null;
                        if (part != null || !data.CureRecipe.targetsBodyPart)
                        {
                            HealthCardUtility.CreateSurgeryBill(pawn, data.CureRecipe, part, null, false);
                            Log(comp, Fix.LostCureBill, pawn);
                        }
                    }

                    // 3. treatment that cannot be done: letter after N hours, discharge after 2N
                    string why = ImpossibleReason(comp, pawn, data);
                    if (why == null)
                    {
                        data.ImpossibleSinceTick = -1;
                        data.ImpossibleLetterSent = false;
                    }
                    else
                    {
                        if (data.ImpossibleSinceTick < 0) data.ImpossibleSinceTick = now;
                        int span = (int)(UnityEngine.Mathf.Max(1f, s.ImpossibleTreatmentHours) * GenDate.TicksPerHour);
                        int elapsed = now - data.ImpossibleSinceTick;
                        if (elapsed >= 2 * span)
                        {
                            Log(comp, Fix.ImpossibleDischarge, pawn);
                            comp.Discharge(pawn, data, DischargeReason.TreatmentImpossible);
                            continue;
                        }
                        if (elapsed >= span && !data.ImpossibleLetterSent)
                        {
                            data.ImpossibleLetterSent = true;
                            ChoiceLetter_TreatmentImpossible.Send(comp, pawn, data, why, span);
                            Log(comp, Fix.ImpossibleLetter, pawn);
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Verse.Log.ErrorOnce("[RimWard] caretaker failed for " + (pawn?.LabelShort ?? "?") + ": " + e, 0x52a7c0 ^ (pawn?.thingIDNumber ?? 0));
                }
            }
        }

        public static Bill_Medical CureBill(Pawn pawn, PatientData data)
        {
            if (data?.CureRecipe == null || pawn?.health?.surgeryBills == null) return null;
            foreach (Bill b in pawn.health.surgeryBills.Bills)
                if (b is Bill_Medical bm && bm.recipe == data.CureRecipe) return bm;
            return null;
        }

        /// <summary>Why the pending cure cannot be done right now (translated), or null when it can (or nothing is pending).</summary>
        public static string ImpossibleReason(HospitalMapComponent comp, Pawn pawn, PatientData data)
        {
            if (data == null || data.Dismissed || !data.HasPendingSurgeryBill || data.CureRecipe == null || data.RetryAfterTick >= 0) return null;
            if (!pawn.Spawned) return null;
            RecipeDef r = data.CureRecipe;
            if (!comp.IsSurgeryRecipeAllowed(r)) return "RimWard.Why.Impossible.Refused".Translate(r.LabelCap);
            Bill_Medical bill = CureBill(pawn, data);
            if (bill == null) return "RimWard.Why.Impossible.BillGone".Translate(r.LabelCap);
            if (bill.suspended) return null; // the player's choice, shown as SurgerySuspended
            if (!bill.CompletableEver) return "RimWard.Why.Impossible.NotPossible".Translate(r.LabelCap);
            bool surgeon = comp.map.mapPawns.PawnsInFaction(Faction.OfPlayer).Any(c => c.Spawned && !c.Downed && (c.IsFreeColonist || c.IsColonyMechPlayerControlled)
                && r.PawnSatisfiesSkillRequirements(c) && (c.workSettings == null || !c.workSettings.EverWork || c.workSettings.WorkIsActive(WorkTypeDefOf.Doctor)));
            if (!surgeon) return "RimWard.Why.Impossible.NoSurgeon".Translate();
            ThingDef missing = r.PotentiallyMissingIngredients(null, comp.map).FirstOrDefault();
            if (missing != null) return "RimWard.Why.Impossible.Missing".Translate(missing.label);
            ThingDef forbidden = SurgeryUtility.OnlyForbiddenIngredient(r, comp.map);
            if (forbidden != null) return "RimWard.Why.Impossible.Forbidden".Translate(forbidden.label);
            string care = SurgeryUtility.MedicineBlockedByPolicy(r, comp.map, pawn.playerSettings?.medCare ?? comp.Policy.medCare);
            if (care != null) return "RimWard.Why.Impossible.MedicineLevel".Translate(care);
            return null;
        }

        /// <summary>
        /// Cured patient, active threat on the map: may they go anyway? They wait at most <see cref="Settings.MaxDangerHoldHours"/>,
        /// then leave with a message. Returns true when they may leave now.
        /// </summary>
        public static bool MayLeaveDespiteDanger(HospitalMapComponent comp, Pawn pawn, PatientData data)
        {
            if (comp.Policy.acceptDanger || !Threats.AnyActiveThreat(comp.map, out string reasons))
            {
                data.DangerHoldSinceTick = -1;
                return true;
            }
            int now = Find.TickManager.TicksGame;
            if (data.DangerHoldSinceTick < 0) data.DangerHoldSinceTick = now;
            int hold = (int)(HospitalMod.Settings.MaxDangerHoldHours * GenDate.TicksPerHour);
            if (now - data.DangerHoldSinceTick < hold) return false;
            Log(comp, Fix.DangerTimeout, pawn);
            Messages.Message("RimWard.Msg.LeftDespiteDanger".Translate(pawn.LabelShortCap, reasons), pawn, MessageTypeDefOf.CautionInput, false);
            data.DangerHoldSinceTick = -1;
            return true;
        }

        public static PatientStatus StatusOf(HospitalMapComponent comp, Pawn pawn, PatientData data, out string detail)
        {
            detail = null;
            if (data == null) return PatientStatus.None;
            if (data.Dismissed) return PatientStatus.Leaving;
            if (!pawn.Spawned) return PatientStatus.Arriving;
            string imp = ImpossibleReason(comp, pawn, data);
            if (imp != null) { detail = imp; return PatientStatus.TreatmentImpossible; }
            if (pawn.Downed) return PatientStatus.Acute;
            if (data.RetryAfterTick >= 0) return PatientStatus.SurgeryRetry;
            if (data.HasPendingSurgeryBill || pawn.health.surgeryBills.Count > 0)
            {
                Bill_Medical bill = CureBill(pawn, data);
                if (bill != null && bill.suspended) return PatientStatus.SurgerySuspended;
                return PatientStatus.WaitingForSurgery;
            }
            if (pawn.health.HasHediffsNeedingTend(false)) return PatientStatus.NeedsTending;
            if (HealthAIUtility.ShouldSeekMedicalRest(pawn)) return PatientStatus.Resting;
            if (!comp.Policy.acceptDanger && Threats.AnyActiveThreat(comp.map, out string reasons))
            {
                detail = reasons;
                return PatientStatus.HeldByDanger;
            }
            return PatientStatus.ReadyToLeave;
        }

        public static string WhyText(HospitalMapComponent comp, Pawn pawn, PatientData data)
        {
            PatientStatus st = StatusOf(comp, pawn, data, out string detail);
            if (st == PatientStatus.None) return string.Empty;
            return ("RimWard.Why." + st).Translate(detail ?? string.Empty).ToString();
        }
    }
}
