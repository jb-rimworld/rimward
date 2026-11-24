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
                this.hospital = HospitalMapComponent.For(Find.CurrentMap);
            }
            return pawn.GetPatientTimeInHospital(out this.ticks, this.hospital)
                ? this.ticks.ToStringTicksToPeriod(allowSeconds: false, shortForm: true)
                : string.Empty;
        }
    }
}
