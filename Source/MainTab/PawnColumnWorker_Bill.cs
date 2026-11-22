using RimWard.Utilities;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard.MainTab
{
    public class PawnColumnWorker_Bill : PawnColumnWorker_Text
    {
        private float lastTimeCached;
        private HospitalMapComponent hospital;

        protected override string GetTextFor(Pawn pawn)
        {
            if (this.hospital == null || Time.unscaledTime > this.lastTimeCached + 2f || Find.CurrentMap != this.hospital.map)
            {
                this.lastTimeCached = Time.unscaledTime;
                this.hospital = Find.CurrentMap.GetComponent<HospitalMapComponent>();
            }
            return this.hospital.GetPatientData(pawn, out PatientData patientData)
                ? PatientUtility.CalculateSilverToReceive(pawn, patientData).ToStringMoney()
                : string.Empty;
        }
    }
}
