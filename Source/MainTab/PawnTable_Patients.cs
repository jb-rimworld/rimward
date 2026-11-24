// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
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
