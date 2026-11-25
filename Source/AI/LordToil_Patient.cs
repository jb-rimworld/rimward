// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
using System.Linq;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimWard
{
    internal class LordToil_Patient : LordToil
    {
        // No toil data: a save then names one class of RimWard fewer per patient (LordToilData_Patient stays for older saves).
        public LordToil_Patient()
        {
        }

        public override void Init()
        {
            base.Init();
        }

        public override void Cleanup()
        {
            HospitalMap component = HospitalMap.For(base.Map);
            foreach (Pawn pawn in this.lord.ownedPawns.ToArray())
            {
                // only patients on this lord's map leave here (one who went along on a gravship is handled on the new map)
                if (pawn.MapHeld == base.Map) component?.PatientLeaves(pawn);
            }
            base.Cleanup();
        }

        public override void UpdateAllDuties()
        {
            foreach (Pawn pawn in this.lord.ownedPawns)
            {
                pawn.mindState.duty = new PawnDuty(DefDatabase<DutyDef>.GetNamed("RimWard_Patient"), pawn.Position, 0f);
            }
        }
    }
}
