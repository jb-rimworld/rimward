// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Copyright (C) 2026 juliusberger00. Written for RimWard 2.0 (a GPL-3.0 fork of Hospital by Adamas and
// Hospital Fix And Tweak by assssssqwww). Licensed under the GNU General Public License version 3 (LICENSE);
// full source: https://github.com/jb-rimworld/rimward
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard
{
    public enum BedUse { Colonist, Guest, Patient, Surgery }

    /// <summary>
    /// The one compact bed button "Use: Colonist / Guest / Patient / Surgery" (KONZEPT 10.6 / 13.6, N20).
    /// Protocol with Hearth &amp; Harbor (both sides check, so exactly one set of buttons is shown):
    ///  - <see cref="Draws"/> is true when RimWard draws the selector for this bed. H&amp;H then hides its "Guest bed" toggle.
    ///  - If H&amp;H does not speak the protocol (older version), RimWard draws nothing new and both mods keep their own buttons.
    ///  - If H&amp;H draws the selector itself (<see cref="HearthBridge.HearthDrawsSelector"/>), RimWard only hides its toggles
    ///    and H&amp;H drives the bed through <see cref="BedApi"/>.
    /// </summary>
    public static class BedSelector
    {
        /// <summary>RimWard draws the selector for this bed (and H&amp;H must not draw its own guest toggle).</summary>
        public static bool Draws(Building_Bed bed)
        {
            if (bed == null || HospitalMod.Settings == null || !HospitalMod.Settings.CombinedBedButton) return false;
            if (bed.Faction != Faction.OfPlayer || bed.def.building == null || !bed.def.building.bed_humanlike) return false;
            if (!bed.ForColonists) return false; // prisoner and slave beds keep vanilla's buttons (and H&H's greyed-out guest toggle)
            if (bed.GetComp<CompHospitalBed>() == null) return false;
            if (HearthBridge.ProtocolVersion < 1) return false;
            if (HearthBridge.HearthDrawsSelector) return false;
            HearthBridge.LogOnce();
            return true;
        }

        /// <summary>RimWard's own Hospital/Surgery toggles are not shown (the selector, or H&amp;H's selector, replaces them).</summary>
        public static bool HidesOwnToggles(Building_Bed bed)
        {
            if (Draws(bed)) return true;
            if (bed == null || HospitalMod.Settings == null || !HospitalMod.Settings.CombinedBedButton) return false;
            return HearthBridge.ProtocolVersion >= 1 && HearthBridge.HearthDrawsSelector;
        }

        public static BedUse Current(Building_Bed bed)
        {
            CompHospitalBed comp = bed.GetComp<CompHospitalBed>();
            if (bed.Medical)
            {
                if (comp != null && comp.Surgery) return BedUse.Surgery;
                if (comp != null && comp.Hospital) return BedUse.Patient;
                return BedUse.Colonist;
            }
            return HearthBridge.IsGuestBed(bed) ? BedUse.Guest : BedUse.Colonist;
        }

        /// <summary>Null when the bed may be set to this use, otherwise the reason (shown greyed out).</summary>
        public static string BlockedReason(Building_Bed bed, BedUse use)
        {
            bool isPod = BedKinds.IsMedPod(bed.def);
            switch (use)
            {
                case BedUse.Colonist:
                    return null;
                case BedUse.Guest:
                    if (!HearthBridge.GuestAvailable(bed)) return "RimWard.Bed.GuestOff".Translate();
                    if (bed.Medical) return "RimWard.Bed.GuestNotMedical".Translate();
                    return HearthBridge.GuestBlockedReason(bed);
                case BedUse.Patient:
                    return bed.Medical ? null : (string)"RimWard.Bed.NeedsMedical".Translate();
                case BedUse.Surgery:
                    if (!bed.Medical) return "RimWard.Bed.NeedsMedical".Translate();
                    return isPod ? (string)"RimWard.Bed.NoPodSurgery".Translate() : null;
            }
            return null;
        }

        public static string Label(BedUse use) => ("RimWard.Bed.Use." + use).Translate();

        /// <summary>Entries worth showing for this bed: Colonist plus whatever exists in the current setup.</summary>
        public static List<BedUse> Entries(Building_Bed bed)
        {
            List<BedUse> list = new List<BedUse> { BedUse.Colonist };
            if (HearthBridge.GuestAvailable(bed)) list.Add(BedUse.Guest);
            list.Add(BedUse.Patient);
            if (!BedKinds.IsMedPod(bed.def)) list.Add(BedUse.Surgery);
            return list;
        }

        /// <summary>The selector gizmo for one bed, or null when there is nothing to choose (e.g. a plain bed and no guest module).</summary>
        public static Gizmo MakeGizmo(Building_Bed bed)
        {
            if (!Draws(bed)) return null;
            if (!bed.Medical && !HearthBridge.GuestAvailable(bed)) return null; // only "Colonist" would be left; vanilla owns the rest
            return new Command_BedUse(bed);
        }
    }

    /// <summary>What the rest of the world (and H&amp;H, if it ever draws the selector itself) uses to read and set RimWard bed state.</summary>
    public static class BedApi
    {
        public static bool IsHospital(Building_Bed bed) => bed?.GetComp<CompHospitalBed>()?.Hospital ?? false;
        public static bool IsSurgery(Building_Bed bed) => bed?.GetComp<CompHospitalBed>()?.Surgery ?? false;
        public static void SetHospital(Building_Bed bed, bool on) { CompHospitalBed c = bed?.GetComp<CompHospitalBed>(); if (c != null) c.Hospital = on; }
        public static void SetSurgery(Building_Bed bed, bool on) { CompHospitalBed c = bed?.GetComp<CompHospitalBed>(); if (c != null) c.Surgery = on; }

        /// <summary>Sets the use of a bed; returns false (and does nothing) if the use is not possible.</summary>
        public static bool Apply(Building_Bed bed, BedUse use)
        {
            if (bed == null || BedSelector.BlockedReason(bed, use) != null) return false;
            CompHospitalBed c = bed.GetComp<CompHospitalBed>();
            if (c == null) return false;
            switch (use)
            {
                case BedUse.Colonist:
                    if (HearthBridge.IsGuestBed(bed)) HearthBridge.SetGuestBed(bed, false);
                    if (bed.Medical) { c.Surgery = false; c.Hospital = false; }
                    break;
                case BedUse.Guest:
                    HearthBridge.SetGuestBed(bed, true);
                    break;
                case BedUse.Patient:
                    if (HearthBridge.IsGuestBed(bed)) HearthBridge.SetGuestBed(bed, false);
                    c.Surgery = false; c.Hospital = true;
                    break;
                case BedUse.Surgery:
                    if (HearthBridge.IsGuestBed(bed)) HearthBridge.SetGuestBed(bed, false);
                    c.Surgery = true;
                    break;
            }
            return true;
        }
    }

    public class Command_BedUse : Command_Action
    {
        private readonly Building_Bed bed;

        public Command_BedUse(Building_Bed bed)
        {
            this.bed = bed;
            this.defaultLabel = "RimWard.Bed.UseLabel".Translate(BedSelector.Label(BedSelector.Current(bed)));
            this.defaultDesc = "RimWard.Bed.UseDesc".Translate();
            this.icon = ContentFinder<Texture2D>.Get("UI/RimWard/Icon", false);
            this.action = this.OpenMenu;
        }

        public override bool GroupsWith(Gizmo other) => other is Command_BedUse;

        private List<Building_Bed> SelectedBeds()
        {
            List<Building_Bed> list = new List<Building_Bed>();
            if (Find.Selector != null)
                foreach (object o in Find.Selector.SelectedObjects)
                    if (o is Building_Bed b && BedSelector.Draws(b)) list.Add(b);
            if (!list.Contains(this.bed)) list.Add(this.bed);
            return list;
        }

        private void OpenMenu()
        {
            List<Building_Bed> beds = this.SelectedBeds();
            BedUse current = BedSelector.Current(this.bed);
            List<FloatMenuOption> opts = new List<FloatMenuOption>();
            foreach (BedUse use in BedSelector.Entries(this.bed))
            {
                BedUse u = use;
                string label = BedSelector.Label(u);
                if (u == current) label += " *";
                List<Building_Bed> ok = beds.Where(b => BedSelector.BlockedReason(b, u) == null).ToList();
                if (ok.Count == 0)
                {
                    string why = BedSelector.BlockedReason(beds[0], u);
                    opts.Add(new FloatMenuOption(label + (string.IsNullOrEmpty(why) ? "" : " (" + why + ")"), null));
                }
                else
                {
                    opts.Add(new FloatMenuOption(label, () => { foreach (Building_Bed b in ok) BedApi.Apply(b, u); }));
                }
            }
            Find.WindowStack.Add(new FloatMenu(opts));
        }
    }
}
