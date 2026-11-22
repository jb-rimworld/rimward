using HarmonyLib;
using RimWard.Utilities;
using RimWorld;
using Verse;

namespace RimWard.Patches
{
    public class ThinkNode_ConditionalNPCCanSelfTendNow_Patch
    {
        [HarmonyPatch(typeof(ThinkNode_ConditionalNPCCanSelfTendNow), "Satisfied")]
        public class Satisfied
        {
            [HarmonyPostfix]
            public static void Postfix(ref bool __result, Pawn pawn)
            {
                if (!__result) return;
                if (pawn.IsPatient(out _, false))
                {
                    __result = false;
                }
            }
        }
    }
}
