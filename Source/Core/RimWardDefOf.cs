using RimWorld;
using Verse;

namespace RimWard
{
    [DefOf]
    public static class RimWardDefOf
    {
        public static LetterDef RimWard_PatientOffer;

        static RimWardDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(RimWardDefOf));
        }
    }
}
