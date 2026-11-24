using RimWard.Utilities;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard.MainTab
{
    /// <summary>Expected payout if the patient were discharged cured now: the same <see cref="Invoice"/> the payout uses. Tooltip lists every factor.</summary>
    public class PawnColumnWorker_Bill : PawnColumnWorker_Text
    {
        private HospitalMapComponent Hospital => HospitalMapComponent.For(Find.CurrentMap);

        protected override string GetTextFor(Pawn pawn)
        {
            HospitalMapComponent h = this.Hospital;
            return h != null && h.GetPatientData(pawn, out PatientData patientData)
                ? ((float)Invoice.Build(pawn, patientData, DischargeReason.Estimate).TotalSilver).ToStringMoney()
                : string.Empty;
        }

        protected override string GetTip(Pawn pawn)
        {
            HospitalMapComponent h = this.Hospital;
            return h != null && h.GetPatientData(pawn, out PatientData patientData)
                ? Invoice.Build(pawn, patientData, DischargeReason.Estimate).ToText()
                : null;
        }
    }
}
