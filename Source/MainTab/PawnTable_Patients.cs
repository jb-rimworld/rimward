using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimWard.MainTab
{
    public class PawnTable_Patients : PawnTable
    {
        public PawnTable_Patients(PawnTableDef def, Func<IEnumerable<Pawn>> pawnsGetter, int uiWidth, int uiHeight)
            : base(def, pawnsGetter, uiWidth, uiHeight)
        {
        }
    }
}
