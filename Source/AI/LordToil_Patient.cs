using System.Linq;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimWard
{
    internal class LordToil_Patient : LordToil
    {
        public LordToilData_Patient Data => (LordToilData_Patient)this.data;

        public LordToil_Patient()
        {
            this.data = new LordToilData_Patient();
        }

        public override void Init()
        {
            base.Init();
        }

        public override void Cleanup()
        {
            HospitalMapComponent component = HospitalMapComponent.For(base.Map);
            foreach (Pawn pawn in this.lord.ownedPawns.ToArray())
            {
                component.PatientLeaves(pawn);
            }
            base.Cleanup();
        }

        public override void UpdateAllDuties()
        {
            foreach (Pawn pawn in this.lord.ownedPawns)
            {
                pawn.mindState.duty = new PawnDuty(DefDatabase<DutyDef>.GetNamed("RimWard_Patient"), pawn.Position, this.Data.radius);
            }
        }
    }
}
