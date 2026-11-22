using System;
using RimWard.Utilities;
using RimWorld;
using Verse;

namespace RimWard
{
    /// <summary>The stage of a patient's stay, as other mods (Hearth &amp; Harbor) see it.</summary>
    public enum PatientStage
    {
        /// <summary>Not a RimWard patient (or the pawn is gone).</summary>
        None,
        /// <summary>Needs a doctor or a bed now: downed, bleeding, urgent tending, a pending operation, cannot walk.</summary>
        Acute,
        /// <summary>Stable but still under treatment: wounds being tended, an illness running its course.</summary>
        Recovering,
        /// <summary>Nothing left to treat; the hospital is keeping an eye on the patient before discharge.</summary>
        Observation,
        /// <summary>Dismissed or discharged and on the way out.</summary>
        Discharged
    }

    /// <summary>
    /// Public, soft-coupled API for other mods (found by reflection, no hard dependency).
    /// Hearth &amp; Harbor calls <c>RimWard.PatientApi.IsWalkingPatient(Pawn)</c> to decide whether a patient may use guest facilities.
    /// Keep the signatures stable; bump <see cref="InteropVersion"/> when they change.
    /// </summary>
    public static class PatientApi
    {
        public const int InteropVersion = 1;

        /// <summary>True when the pawn is a current RimWard patient (not yet dismissed).</summary>
        public static bool IsPatient(Pawn pawn) => IsPatient(pawn, false);

        public static bool IsPatient(Pawn pawn, bool includeDismissed)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed) return false;
            try { return pawn.IsPatient(out _, includeDismissed); }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// A patient who is up and about: awake, on their feet, not bleeding, nothing urgent to treat and no operation waiting.
        /// They may be resting in a bed at the moment; this only says that nothing forbids them to walk to a spa, a cafe or a chapel.
        /// </summary>
        public static bool IsWalkingPatient(Pawn pawn)
        {
            PatientStage stage = StageOf(pawn);
            return stage == PatientStage.Recovering || stage == PatientStage.Observation;
        }

        /// <summary>True when the patient must be in a bed (or needs a doctor now): the opposite of <see cref="IsWalkingPatient"/> for current patients.</summary>
        public static bool NeedsBedRest(Pawn pawn)
        {
            return StageOf(pawn) == PatientStage.Acute;
        }

        /// <summary>Acute, Recovering, Observation, Discharged (or None for non-patients).</summary>
        public static string Stage(Pawn pawn) => StageOf(pawn).ToString();

        public static PatientStage StageOf(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed) return PatientStage.None;
            HospitalMapComponent hospital = null;
            PatientData data = null;
            try
            {
                hospital = pawn.Map?.GetComponent<HospitalMapComponent>();
                if (hospital == null || !hospital.GetPatientData(pawn, out data)) return PatientStage.None;
            }
            catch (Exception) { return PatientStage.None; }
            if (data.Dismissed) return PatientStage.Discharged;
            if (!pawn.Spawned) return PatientStage.Acute;
            if (IsAcute(pawn, data)) return PatientStage.Acute;
            if (HealthAIUtility.ShouldSeekMedicalRest(pawn) || pawn.health.HasHediffsNeedingTend(false)) return PatientStage.Recovering;
            return PatientStage.Observation;
        }

        private static bool IsAcute(Pawn pawn, PatientData data)
        {
            if (pawn.Downed || !pawn.Awake()) return true;
            if (data.HasPendingSurgeryBill || data.RetryAfterTick >= 0) return true;
            Pawn_HealthTracker health = pawn.health;
            if (health == null) return true;
            if (health.surgeryBills != null && health.surgeryBills.Count > 0) return true;
            if (!health.capacities.CapableOf(PawnCapacityDefOf.Moving)) return true;
            if (health.hediffSet.BleedRateTotal > 0.01f) return true;
            if (HealthAIUtility.ShouldBeTendedNowByPlayerUrgent(pawn)) return true;
            if (HealthAIUtility.ShouldHaveSurgeryDoneNow(pawn)) return true;
            return false;
        }
    }
}
