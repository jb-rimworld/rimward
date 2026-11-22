using Verse;
using Verse.AI.Group;

namespace RimWard
{
    internal class LordToilData_Patient : LordToilData
    {
        public float radius;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref this.radius, "radius", 100f);
        }
    }
}
