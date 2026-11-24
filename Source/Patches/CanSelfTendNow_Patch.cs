// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
using HarmonyLib;
using RimWard.Utilities;
using RimWorld;
using Verse.AI;

namespace RimWard.Patches
{
    public class CanSelfTendNow_Patch
    {
        [HarmonyPatch(typeof(JobDriver_TendPatient), "TryMakePreToilReservations")]
        public class Satisfied
        {
            // Block a hospital patient from self-tending (deliveree == the doctor pawn).
            [HarmonyPrefix]
            public static bool Prefix(ref bool __result, JobDriver_TendPatient __instance)
            {
                if (__instance.pawn.IsPatient(out _, false)
                    && (__instance.job.GetTarget(TargetIndex.A).Thing == __instance.pawn || __instance.job.GetTarget(TargetIndex.B).Thing == __instance.pawn))
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }
    }
}
