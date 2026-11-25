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
    // Parity outcome hooks. Step 4 reworks these into the fault-classified DeathLiability model.
    public class PatientOutcome_Patches
    {
        [HarmonyPatch(typeof(Pawn), "Kill")]
        public static class Kill
        {
            [HarmonyPrefix]
            public static void Prefix(Pawn __instance)
            {
                if (__instance.IsPatient(out HospitalMap hospital, false))
                {
                    hospital.PatientDied(__instance);
                }
            }
        }

        // A botched operation is detected in Billing_Surgery_Patch.SurgeryFail (Recipe_Surgery.CheckSurgeryFail), for every outcome.

        [HarmonyPatch(typeof(Faction), "Notify_MemberExitedMap")]
        public static class Notify_MemberExitedMap
        {
            [HarmonyPrefix]
            public static bool Prefix(Pawn member, bool freed)
            {
                HospitalMap hospital = HospitalMap.For(member?.Map);
                if (hospital != null && hospital.GetPatientData(member, out _))
                {
                    hospital.PatientLeftTheMap(member);
                    return false;
                }
                return true;
            }
        }
    }
}
