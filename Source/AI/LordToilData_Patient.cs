// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
using Verse;
using Verse.AI.Group;

namespace RimWard
{
    internal class LordToilData_Patient : LordToilData
    {
        public float radius;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref this.radius, "radius", 100f);
        }
    }
}
