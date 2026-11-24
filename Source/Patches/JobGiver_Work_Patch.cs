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
    public class JobGiver_Work_Patch
    {
        [HarmonyPatch(typeof(JobGiver_Work), "PawnCanUseWorkGiver")]
        public class PawnCanUseWorkGiver
        {
            [HarmonyPrefix]
            public static bool Prefix(ref bool __result, Pawn pawn, WorkGiver giver)
            {
                if (!pawn.IsPatient(out _, false)) return true;
                if (!IsPatientWork(giver.def.workType))
                {
                    __result = false;
                    return false;
                }
                __result = true;
                return false;
            }

            private static bool IsPatientWork(WorkTypeDef workTypeDef)
            {
                return workTypeDef.defName.EqualsIgnoreCase("Patient") || workTypeDef.defName.EqualsIgnoreCase("PatientBedRest");
            }
        }
    }
}
