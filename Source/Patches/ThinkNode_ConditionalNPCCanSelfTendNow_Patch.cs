// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
using HarmonyLib;
using RimWard.Utilities;
using RimWorld;
using Verse;

namespace RimWard.Patches
{
    public class ThinkNode_ConditionalNPCCanSelfTendNow_Patch
    {
        [HarmonyPatch(typeof(ThinkNode_ConditionalNPCCanSelfTendNow), "Satisfied")]
        public class Satisfied
        {
            [HarmonyPostfix]
            public static void Postfix(ref bool __result, Pawn pawn)
            {
                if (!__result) return;
                if (pawn.IsPatient(out _, false))
                {
                    __result = false;
                }
            }
        }
    }
}
