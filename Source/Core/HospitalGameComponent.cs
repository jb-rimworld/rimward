using UnityEngine;
using Verse;

namespace RimWard
{
    // Per-save global state: hospital renown + lifetime stats. Auto-instantiated GameComponent.
    public class HospitalGameComponent : GameComponent
    {
        public float renown;
        public int patientsTreated;
        public int patientsDied;
        public long silverEarned;

        public HospitalGameComponent(Game game) { }

        public static HospitalGameComponent Get => Current.Game?.GetComponent<HospitalGameComponent>();

        // Renown tiers: Field Aid -> Clinic -> Hospital -> Medical Hub
        public int Tier => renown < 50f ? 0 : (renown < 200f ? 1 : (renown < 600f ? 2 : 3));

        public float RenownPaymentMultiplier => 1f + 0.05f * Tier;   // small, compounding business growth

        public void RecordDischarge(bool alive, float score, float silver)
        {
            if (alive) patientsTreated++; else patientsDied++;
            silverEarned += (long)silver;
            // renown rises with good outcomes, dips a little on bad ones; never below 0
            renown = Mathf.Max(0f, renown + (score - 40f) * 0.05f);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref this.renown, "renown", 0f);
            Scribe_Values.Look(ref this.patientsTreated, "patientsTreated", 0);
            Scribe_Values.Look(ref this.patientsDied, "patientsDied", 0);
            Scribe_Values.Look(ref this.silverEarned, "silverEarned", 0L);
        }
    }
}
