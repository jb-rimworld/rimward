using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard.MainTab
{
    public class PawnColumnWorker_ButtonDisallow : PawnColumnWorker
    {
        private float lastTimeCached;
        private HospitalMapComponent hospital;

        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            if (Find.CurrentMap == null) return;
            if (this.hospital == null || Time.unscaledTime > this.lastTimeCached + 2f || Find.CurrentMap != this.hospital.map)
            {
                this.lastTimeCached = Time.unscaledTime;
                this.hospital = Find.CurrentMap.GetComponent<HospitalMapComponent>();
            }
            if (this.hospital.GetPatientData(pawn, out PatientData patientData) && patientData.CureRecipe != null
                && Widgets.ButtonText(rect, "Disallow".Translate()))
            {
                CameraJumper.TryJumpAndSelect(pawn);
                this.hospital.RefuseOperation(pawn, patientData.CureRecipe);
            }
        }

        public override void DoHeader(Rect rect, PawnTable table)
        {
            if (Widgets.ButtonText(rect, "HospitalMapSettings".Translate()))
            {
                Find.WindowStack.Add(new Dialog_MapSettings(Find.CurrentMap));
            }
        }

        public override int GetMinWidth(PawnTable table)
        {
            return Mathf.Max(base.GetMinWidth(table), 120);
        }

        public override int GetMaxWidth(PawnTable table)
        {
            return Mathf.Min(base.GetMaxWidth(table), this.GetMinWidth(table));
        }
    }
}
