// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Copyright (C) 2026 juliusberger00. Written for RimWard 2.0 (a GPL-3.0 fork of Hospital by Adamas and
// Hospital Fix And Tweak by assssssqwww). Licensed under the GNU General Public License version 3 (LICENSE);
// full source: https://github.com/jb-rimworld/rimward
using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;

namespace RimWard
{
    /// <summary>
    /// Soft bridge to Hearth &amp; Harbor (reflection only, no reference, no hard dependency).
    /// Everything here returns "not there" when H&amp;H is absent, older than the protocol, or throws.
    /// The protocol is documented in README.md ("Works with Hearth &amp; Harbor").
    /// </summary>
    public static class HearthBridge
    {
        public const string GuestBedCompType = "Hearth.Guests.CompHH_GuestBed";

        private static bool resolved;
        private static Type compType;
        private static PropertyInfo pVersion, pDraws, pAvailable, pReason, pGuestBed;
        private static MethodInfo mSetGuest;
        private static bool loggedActive;

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            try
            {
                compType = GenTypes.GetTypeInAnyAssembly(GuestBedCompType);
                if (compType == null) return;
                BindingFlags st = BindingFlags.Public | BindingFlags.Static;
                BindingFlags inst = BindingFlags.Public | BindingFlags.Instance;
                pVersion = compType.GetProperty("BedSelectorVersion", st);
                pDraws = compType.GetProperty("DrawsBedSelector", st);
                pAvailable = compType.GetProperty("SelectorAvailable", inst);
                pReason = compType.GetProperty("SelectorBlockedReason", inst);
                pGuestBed = compType.GetProperty("IsGuestBed", inst);
                mSetGuest = compType.GetMethod("SetGuestBed", inst, null, new[] { typeof(bool), typeof(bool) }, null);
            }
            catch (Exception e)
            {
                Log.Warning("[RimWard] Hearth & Harbor bridge could not be resolved: " + e.Message);
                compType = null;
            }
        }

        /// <summary>Hearth &amp; Harbor's guest-bed type is loaded (the mod is active).</summary>
        public static bool HearthLoaded { get { Resolve(); return compType != null; } }

        /// <summary>Version of the combined-bed-selector protocol H&amp;H speaks (0 = none / too old).</summary>
        public static int ProtocolVersion
        {
            get
            {
                Resolve();
                if (compType == null || pVersion == null || mSetGuest == null || pGuestBed == null) return 0;
                try { return (int)pVersion.GetValue(null); } catch (Exception) { return 0; }
            }
        }

        /// <summary>H&amp;H itself draws the "Use: …" selector (a future shared foundation): RimWard then only hides its own toggles.</summary>
        public static bool HearthDrawsSelector
        {
            get
            {
                Resolve();
                if (pDraws == null) return false;
                try { return (bool)pDraws.GetValue(null); } catch (Exception) { return false; }
            }
        }

        public static ThingComp GuestComp(Building_Bed bed)
        {
            Resolve();
            if (compType == null || bed == null) return null;
            List<ThingComp> comps = bed.AllComps;
            for (int i = 0; i < comps.Count; i++)
                if (comps[i].GetType() == compType) return comps[i];
            return null;
        }

        /// <summary>The "Guest" entry is available for this bed (H&amp;H Inn module on, colony bed). False when H&amp;H is missing.</summary>
        public static bool GuestAvailable(Building_Bed bed)
        {
            ThingComp c = GuestComp(bed);
            if (c == null || pAvailable == null) return false;
            try { return (bool)pAvailable.GetValue(c); } catch (Exception) { return false; }
        }

        /// <summary>Why a bed cannot become a guest bed right now (null = it can).</summary>
        public static string GuestBlockedReason(Building_Bed bed)
        {
            ThingComp c = GuestComp(bed);
            if (c == null || pReason == null) return null;
            try { return (string)pReason.GetValue(c); } catch (Exception) { return null; }
        }

        public static bool IsGuestBed(Building_Bed bed)
        {
            ThingComp c = GuestComp(bed);
            if (c == null) return false;
            try { return (bool)pGuestBed.GetValue(c); } catch (Exception) { return false; }
        }

        public static void SetGuestBed(Building_Bed bed, bool on)
        {
            ThingComp c = GuestComp(bed);
            if (c == null || mSetGuest == null) return;
            try { mSetGuest.Invoke(c, new object[] { on, true }); }
            catch (Exception e) { Log.Warning("[RimWard] SetGuestBed failed: " + e.Message); }
        }

        internal static void LogOnce()
        {
            if (loggedActive) return;
            loggedActive = true;
            if (ProtocolVersion > 0) Log.Message("[RimWard] Hearth & Harbor found: one combined bed selector (\"Use: Colonist / Guest / Patient\"), patients may use H&H leisure facilities.");
            else if (HearthLoaded) Log.Message("[RimWard] Hearth & Harbor found, but without the bed selector protocol: both mods keep their own bed buttons.");
        }
    }
}
