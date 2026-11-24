using Verse;

namespace RimWard
{
    public static class BedKinds
    {
        /// <summary>
        /// A MedPod-style bed (instant-heal pod): no surgery use and not a patient bed by default. Detected by its building class
        /// (the MedPod mod uses MedPod.Building_BedMedPod), not by a "Pod" in the defName, so modded beds named "...Pod..." stay normal beds.
        /// </summary>
        public static bool IsMedPod(ThingDef def)
        {
            if (def == null) return false;
            for (System.Type t = def.thingClass; t != null && t != typeof(Thing); t = t.BaseType)
            {
                if (t.Name.IndexOf("MedPod", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return def.defName == "MedPod";
        }
    }
}
