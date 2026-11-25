// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
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
            // a patient who now stands on another map (gravship, transport pod) is not this lord's to send away: the caretaker of that map
            // takes the entry over and lords them there
            return lord.ownedPawns.Any(p => p?.Map == lord.Map && SentAway(p));
        }

        private static bool SentAway(Pawn pawn)
        {
            if (pawn?.Map == null)
            {
                return false;
            }
            HospitalMap hospital = HospitalMap.For(pawn.Map);
            if (hospital == null || !hospital.GetPatientData(pawn, out PatientData data))
            {
                return true; // no longer our patient (forgotten by the caretaker): let the lord walk them out
            }
            if (data.Dismissed)
            {
                return true; // dismissed, discharged early, maximum stay: leave now (fixes "dismissed patients never leave")
            }

            bool pendingSurgery = data.HasPendingSurgeryBill || data.RetryAfterTick >= 0; // no walking out between a botch and the retry
            bool canBeDismissed = !pawn.health.HasHediffsNeedingTend(false)
                && !HealthAIUtility.ShouldSeekMedicalRest(pawn)
                && pawn.health.surgeryBills.Count == 0
                && !pendingSurgery
                && pawn.health.State == PawnHealthState.Mobile;

            if (!canBeDismissed)
            {
                data.DangerHoldSinceTick = -1;
                if (pawn.mindState.duty == null || !pawn.mindState.duty.def.defName.Equals("RimWard_Patient"))
                {
                    pawn.mindState.duty = new PawnDuty(DefDatabase<DutyDef>.GetNamed("RimWard_Patient"), pawn.Position, 100f);
                }
                return false;
            }

            // cured: wait for an active threat to pass, but never longer than the setting (dormant clusters and weather never hold anyone)
            return Caretaker.MayLeaveDespiteDanger(hospital, pawn, data);
        }
    }
}
