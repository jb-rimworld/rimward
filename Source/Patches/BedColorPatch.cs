// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard.Patches
{
    public class BedColorPatch
    {
        private static readonly Color SheetColorHospital = new Color(0.3882353f, 0.3882353f, 0.8862745f);
        private static readonly Color SheetColorSurgery = new Color(0f, 0.9f, 1f);

        [HarmonyPatch(typeof(Building_Bed), "DrawColorTwo", MethodType.Getter)]
        public class DrawColorTwo
        {
            [HarmonyPostfix]
            public static void Postfix(ref Color __result, Building_Bed __instance)
            {
                if (!__instance.Medical || __instance.ForPrisoners || __instance.def == ThingDefOf.RoyalBed)
                {
                    return;
                }
                CompHospitalBed comp = __instance.TryGetComp<CompHospitalBed>();
                if (comp == null) return;
                if (comp.Surgery)
                {
                    __result = SheetColorSurgery;
                }
                else if (comp.Hospital)
                {
                    __result = SheetColorHospital;
                }
            }
        }
    }
}
