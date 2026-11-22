using HarmonyLib;
using RimWard.Utilities;
using RimWorld;
using Verse;

namespace RimWard.Patches
{
    public class TakeWoundedGuestPatch
    {
        [HarmonyPatch(typeof(KidnapAIUtility), "ReachableWoundedGuest")]
        public class HasJobOnThing_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(ref Pawn __result)
            {
                if (__result != null && __result.IsPatient(out _, false))
                {
                    __result = null;
                }
            }
        }
    }
}
