using RimWorld;
using Verse;

namespace RimWard
{
    public enum OfferKind { DropPod, Ambient }

    // A generated/identified patient awaiting the player's accept/decline decision.
    public class PatientOffer : IExposable
    {
        public int id;
        public Pawn pawn;          // held as a world pawn (DropPod) or an existing map pawn (Ambient)
        public PatientData data;
        public Faction faction;
        public int expiresTick;
        public OfferKind kind = OfferKind.DropPod;

        public void ExposeData()
        {
            Scribe_Values.Look(ref this.id, "id", 0);
            Scribe_References.Look(ref this.pawn, "pawn");
            Scribe_Deep.Look(ref this.data, "data");
            Scribe_References.Look(ref this.faction, "faction");
            Scribe_Values.Look(ref this.expiresTick, "expiresTick", 0);
            Scribe_Values.Look(ref this.kind, "kind", OfferKind.DropPod);
        }
    }
}
