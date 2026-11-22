using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimWard.MainTab
{
    public class MainTabWindow_Hospital : MainTabWindow_PawnTable
    {
        private static PawnTableDef pawnTableDef;

        protected override PawnTableDef PawnTableDef => pawnTableDef ?? (pawnTableDef = DefDatabase<PawnTableDef>.GetNamed("RimWard_Patients"));

        protected override IEnumerable<Pawn> Pawns
        {
            get
            {
                HospitalMapComponent c = Find.CurrentMap?.GetComponent<HospitalMapComponent>();
                return c != null ? c.ActivePatientsList() : Enumerable.Empty<Pawn>();
            }
        }
    }
}
