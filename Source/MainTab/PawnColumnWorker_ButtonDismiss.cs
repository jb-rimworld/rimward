// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard.MainTab
{
    public class PawnColumnWorker_ButtonDismiss : PawnColumnWorker
    {
        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            if (Widgets.ButtonText(rect, "Dismiss".Translate()))
            {
                CameraJumper.TryJumpAndSelect(pawn);
                HospitalMap.For(Find.CurrentMap).DismissPatient(pawn);
            }
        }

        public override int GetMinWidth(PawnTable table)
        {
            return Mathf.Max(base.GetMinWidth(table), 60);
        }

        public override int GetMaxWidth(PawnTable table)
        {
            return Mathf.Min(base.GetMaxWidth(table), this.GetMinWidth(table));
        }
    }
}
