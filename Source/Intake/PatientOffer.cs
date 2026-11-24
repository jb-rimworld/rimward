// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
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
