using System.Linq;
using RimWard.Utilities;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimWard
{
    public class Trigger_SentAway : Trigger
    {
        public override bool ActivateOn(Lord lord, TriggerSignal signal)
        {
            if (signal.type != TriggerSignalType.Tick || Find.TickManager.TicksAbs % 250 != 0)
            {
                return false;
            }
            if (lord == null)
            {
                return false;
            }
            return lord.ownedPawns.Any(SentAway);
        }

        private static bool SentAway(Pawn pawn)
        {
            if (pawn?.Map == null)
            {
                return false;
            }
            bool pendingSurgery = false;
            if (pawn.IsPatient(out HospitalMapComponent hospital, false) && hospital.GetPatientData(pawn, out PatientData patientData))
            {
                pendingSurgery = patientData.HasPendingSurgeryBill;
            }
            bool canBeDismissed = !pawn.health.HasHediffsNeedingTend(false)
                && !HealthAIUtility.ShouldSeekMedicalRest(pawn)
                && pawn.health.surgeryBills.Count == 0
                && !pendingSurgery
                && pawn.health.State == PawnHealthState.Mobile;

            if (pawn.IsPatient(out _, false) && !canBeDismissed
                && (pawn.mindState.duty == null || !pawn.mindState.duty.def.defName.Equals("RimWard_Patient")))
            {
                pawn.mindState.duty = new PawnDuty(DefDatabase<DutyDef>.GetNamed("RimWard_Patient"), pawn.Position, 100f);
            }

            if (PatientUtility.DangersOnMap(pawn.Map, out _) && pawn.IsPatient(out _, false))
            {
                return false; // do not send cured patients into a dangerous map
            }
            return canBeDismissed || !pawn.IsPatient(out _, true);
        }
    }
}
