using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimWard
{
    // Non-modal accept/decline letter for a triage patient offer (sits in the letter stack).
    public class ChoiceLetter_PatientOffer : ChoiceLetter
    {
        public int offerId;
        public int mapId = -1;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                yield return new DiaOption("RimWard.Offer.Accept".Translate())
                {
                    action = () => Resolve(true),
                    resolveTree = true
                };
                yield return new DiaOption("RimWard.Offer.Decline".Translate())
                {
                    action = () => Resolve(false),
                    resolveTree = true
                };
                yield return new DiaOption("PostponeLetter".Translate())
                {
                    resolveTree = true
                };
            }
        }

        private void Resolve(bool accept)
        {
            Map map = Find.Maps.FirstOrDefault(m => m.uniqueID == this.mapId);
            HospitalMapComponent comp = HospitalMapComponent.For(map);
            // an accept that cannot be honoured right now (no free bed) keeps the letter, so the offer can still be taken later
            bool done = comp == null || comp.ResolveOffer(this.offerId, accept);
            if (done) Find.LetterStack.RemoveLetter(this);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref this.offerId, "offerId", 0);
            Scribe_Values.Look(ref this.mapId, "mapId", -1);
        }
    }
}
