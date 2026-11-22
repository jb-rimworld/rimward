using HarmonyLib;
using RimWard.Utilities;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard.Patches
{
    public class Billing_Tend_Patch
    {
        [HarmonyPatch(typeof(TendUtility), "DoTend")]
        public class BillPatientsForMedicine
        {
            [HarmonyPrefix]
            public static void Prefix(Pawn doctor, Pawn patient, Medicine medicine)
            {
                if (patient.health.HasHediffsNeedingTend(false) && doctor != null && doctor.Faction == Faction.OfPlayer
                    && doctor != patient && medicine != null && patient.IsPatient(out HospitalMapComponent hospital, false))
                {
                    patient.AddToBill(hospital, Mathf.Clamp(medicine.MarketValue * 1.2f, 0f, 70f));
                    PatientUtility.RecordTreatment(patient, doctor, medicine);
                }
            }
        }
    }
}
