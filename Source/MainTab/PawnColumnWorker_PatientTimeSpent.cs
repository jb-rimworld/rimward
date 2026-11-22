using System;
using RimWard.Utilities;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard.MainTab
{
    public class PawnColumnWorker_PatientTimeSpent : PawnColumnWorker_Text
    {
        protected internal int ticks;
        private float lastTimeCached;
        private HospitalMapComponent hospital;

        protected override string GetTextFor(Pawn pawn)
        {
            if (this.hospital == null || Time.unscaledTime > this.lastTimeCached + 2f || Find.CurrentMap != this.hospital.map)
            {
                this.lastTimeCached = Time.unscaledTime;
                this.hospital = Find.CurrentMap.GetComponent<HospitalMapComponent>();
            }
            return pawn.GetPatientTimeInHospital(out this.ticks, this.hospital)
                ? Math.Truncate(this.ticks / 2500.0) + " h"
                : string.Empty;
        }
    }
}
