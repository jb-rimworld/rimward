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
