// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
using System;
using System.Linq;
using HarmonyLib;
using RimWard.Utilities;
using RimWorld;
using Verse;

namespace RimWard.Patches
{
    public class Patients_BedFinder_Patch
    {
        [HarmonyPatch(typeof(RestUtility), "FindBedFor", new Type[]
        {
            typeof(Pawn), typeof(Pawn), typeof(bool), typeof(bool), typeof(GuestStatus?)
        })]
        public class FindBedPatch
        {
            [HarmonyPostfix]
            public static void Postfix(ref Building_Bed __result, Pawn sleeper, Pawn traveler)
            {
                if (sleeper.IsPatient(out _, false) && !sleeper.InBed())
                {
                    __result = sleeper.Map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>()
                        .Where(bed => bed.Medical && bed.GetComp<CompHospitalBed>() != null && bed.GetComp<CompHospitalBed>().Hospital
                            && RestUtility.IsValidBedFor(bed, sleeper, traveler, false, false, false, null))
                        .FirstOrFallback(__result);
                    if (sleeper.health.surgeryBills.Count <= 0)
                    {
                        __result = sleeper.Map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>()
                            .Where(bed => bed.Medical && bed.GetComp<CompHospitalBed>() != null && bed.GetComp<CompHospitalBed>().Hospital
                                && !bed.GetComp<CompHospitalBed>().Surgery
                                && RestUtility.IsValidBedFor(bed, sleeper, traveler, false, false, false, null))
                            .FirstOrFallback(__result);
                    }
                    else
                    {
                        __result = sleeper.Map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>()
                            .Where(bed => bed.Medical && bed.GetComp<CompHospitalBed>() != null && bed.GetComp<CompHospitalBed>().Hospital
                                && bed.GetComp<CompHospitalBed>().Surgery
                                && RestUtility.IsValidBedFor(bed, sleeper, traveler, false, false, false, null))
                            .FirstOrFallback(__result);
                    }
                }
            }
        }
    }
}
