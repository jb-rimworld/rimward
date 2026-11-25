// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Copyright (C) 2026 juliusberger00. Written for RimWard 2.0 (a GPL-3.0 fork of Hospital by Adamas and
// Hospital Fix And Tweak by assssssqwww). Licensed under the GNU General Public License version 3 (LICENSE);
// full source: https://github.com/jb-rimworld/rimward
using System.Collections.Generic;
using System.Linq;
using JBFramework;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimWard
{
    /// <summary>
    /// "Prepare for removal" (on the Patient intake area): takes everything out of the running game that would name RimWard in a save,
    /// so the save loads without RimWard without a single red error. The framework switches intake off afterwards (no new patients).
    /// Patients are sent home (paid like an early discharge), their lords become vanilla "leave the map" lords, offers and RimWard letters
    /// go, the "turned a patient away" memories go, landing spots are deconstructed (refund). The data itself stays in the framework's
    /// save node (plain values) and comes back if RimWard returns.
    /// </summary>
    internal static class RemovalPrep
    {
        internal static void Run(List<string> report)
        {
            ModContentPack content = HospitalMod.K?.Content;
            int sent = 0, offers = 0, lords = 0, duties = 0;
            foreach (Map map in Find.Maps.ToList())
            {
                HospitalMap h = HospitalMap.For(map);
                if (h == null) continue;
                foreach (PatientOffer o in h.pendingOffers.ToList()) { h.pendingOffers.Remove(o); h.DiscardOffer(o); offers++; }
                foreach (KeyValuePair<Pawn, PatientData> kv in h.Patients.ToList())
                {
                    Pawn p = kv.Key;
                    if (p != null && kv.Value != null && !kv.Value.Dismissed && !p.Dead && !p.Discarded) { h.Discharge(p, kv.Value, DischargeReason.Dismissed); sent++; }
                }
                h.Patients.Clear();
                // every patient lord (also of pawns the list no longer knew) becomes vanilla's "leave the map"
                foreach (Lord lord in map.lordManager.lords.Where(l => l.LordJob is LordJob_VisitColonyAsPatient).ToList())
                {
                    List<Pawn> pawns = lord.ownedPawns.ToList();
                    map.lordManager.RemoveLord(lord);
                    lords++;
                    foreach (Pawn p in pawns.Where(x => x != null && x.Spawned && !x.Dead && x.Map == map && x.GetLord() == null))
                        LordMaker.MakeNewLord(p.Faction, new LordJob_ExitMapBest(LocomotionUrgency.Walk, true, true), map, new List<Pawn> { p });
                }
                foreach (Pawn p in map.mapPawns.AllPawnsSpawned.ToList())
                {
                    if (p.mindState?.duty?.def?.modContentPack != null && p.mindState.duty.def.modContentPack == content) { p.mindState.duty = null; duties++; }
                }
            }
            report.Add("Patients sent home (paid like an early discharge): " + sent + ", offers withdrawn: " + offers + ", patient lords handed to vanilla: " + lords
                + (duties > 0 ? ", duties cleared: " + duties : ""));

            int letters = 0;
            foreach (Letter l in Find.LetterStack.LettersListForReading.ToList())
                if (Ours(l)) { Find.LetterStack.RemoveLetter(l); letters++; }
            foreach (IArchivable a in Find.Archive.ArchivablesListForReading.ToList())
                if (a is Letter l && Ours(l)) { Find.Archive.Remove(a); letters++; }
            report.Add("RimWard letters removed (open and archived): " + letters);

            if (content != null)
            {
                KitRemoval.RemoveThoughts(d => d.modContentPack == content, report);
                KitRemoval.RemoveThings(d => d.modContentPack == content, report);
                KitRemoval.RemoveComponents(t => t.Assembly == typeof(RemovalPrep).Assembly, report);
            }
        }

        private static bool Ours(Letter l) => l != null && (l.GetType().Assembly == typeof(RemovalPrep).Assembly || (l.def?.modContentPack != null && l.def.modContentPack == HospitalMod.K?.Content));
    }
}
