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
