// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWard
{
    // Injects CompHospitalBed onto every medical-capable humanlike bed def (vanilla + modded + HBG2 + medpods),
    // by capability rather than by name, so any bed mod is first-class for free.
    [StaticConstructorOnStartup]
    public static class InjectHospitalBed
    {
        static InjectHospitalBed()
        {
            List<ThingDef> beds = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => def.IsBed && def.building != null && def.building.bed_canBeMedical && def.building.bed_humanlike)
                .ToList();
            beds.RemoveDuplicates();
            foreach (ThingDef def in beds)
            {
                if (def.comps == null)
                {
                    def.comps = new List<CompProperties>();
                }
                if (!def.comps.Any(c => c.GetType() == typeof(CompProperties_HospitalBed)))
                {
                    def.comps.Add((CompProperties_HospitalBed)Activator.CreateInstance(typeof(CompProperties_HospitalBed)));
                }
            }
        }
    }
}
