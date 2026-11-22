using RimWard.Utilities;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard.MainTab
{
    public class PawnColumnWorker_PatientRating : PawnColumnWorker_Text
    {
        protected internal float score;
        private float lastTimeCached;
        private HospitalMapComponent hospital;

        protected override string GetTextFor(Pawn pawn)
        {
            if (this.hospital == null || Time.unscaledTime > this.lastTimeCached + 2f || Find.CurrentMap != this.hospital.map)
            {
                this.lastTimeCached = Time.unscaledTime;
                this.hospital = Find.CurrentMap.GetComponent<HospitalMapComponent>();
            }
            return pawn.GetPatientRating(out this.score, this.hospital) ? Mathf.Clamp01(this.score).ToStringPercent() : string.Empty;
        }
    }
}
