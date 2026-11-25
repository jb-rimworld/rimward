// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
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
            HospitalMap comp = HospitalMap.For(map);
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
