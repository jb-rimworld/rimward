// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimWard
{
    // Ensures the travel/exit toil has a destination. Reimplemented cleanly with RCellFinder
    // (the original used an obfuscated flood-fill; the best-exit-spot search is equivalent and robust).
    public class TransitionAction_EnsureHaveNearbyExitDestination : TransitionAction
    {
        public override void DoAction(Transition trans)
        {
            LordToil_Travel toil = (LordToil_Travel)trans.target;
            if (toil.HasDestination())
            {
                return;
            }
            Pawn searcher = toil.lord.ownedPawns.RandomElement();
            if (searcher == null)
            {
                return;
            }
            if (!RCellFinder.TryFindBestExitSpot(searcher, out IntVec3 exit, TraverseMode.ByPawn))
            {
                RCellFinder.TryFindRandomPawnEntryCell(out exit, searcher.Map, 0f);
            }
            toil.SetDestination(exit);
        }
    }
}
