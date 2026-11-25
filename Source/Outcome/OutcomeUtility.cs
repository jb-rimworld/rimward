// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
using System.Collections.Generic;
using RimWard.Utilities;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard
{
    // Computes the payment multiplier and goodwill from the whole stay: room tier, chief physician,
    // faction medicine preference, renown, and patient health outcome.
    public static class OutcomeUtility
    {
        /// <summary>Product of all outcome factors (room, chief physician, medicine preference, royal demand, renown). Not the payment dial.</summary>
        public static float PaymentMultiplier(Pawn pawn, PatientData data)
        {
            List<InvoiceFactor> f = new List<InvoiceFactor>();
            AddPaymentFactors(pawn, data, f);
            float m = 1f;
            foreach (InvoiceFactor x in f) m *= x.mult;
            return m;
        }

        /// <summary>The room tier that counts for pay: the best room the patient rested in, or the bed they lie in now if better.</summary>
        public static RoomTier EffectiveRoomTier(Pawn pawn, PatientData data)
        {
            RoomTier tier = data.BestRoomTier;
            if (pawn != null && pawn.Spawned && pawn.InBed())
            {
                RoomTier now = RoomTierUtility.Classify(pawn.CurrentBed(), pawn);
                if ((int)now > (int)tier) tier = now;
            }
            return tier;
        }

        /// <summary>Adds every outcome factor that applies to this patient (factors of 1.0 are left out except the room).</summary>
        public static void AddPaymentFactors(Pawn pawn, PatientData data, List<InvoiceFactor> into)
        {
            Settings s = HospitalMod.Settings;
            RoomTier tier = EffectiveRoomTier(pawn, data);
            into.Add(new InvoiceFactor { key = "RimWard.Invoice.Factor.Room", detail = ("RimWard.RoomTier." + tier).Translate().ToString(), mult = RoomTierUtility.Multiplier(tier) });

            if (s.ChiefPhysicianEnabled && data.BestDoctorSkill >= s.ChiefPhysicianMinSkill)
                into.Add(new InvoiceFactor { key = "RimWard.Invoice.Factor.Chief", mult = s.ChiefPhysicianMultiplier });

            float med = MedicinePrefMultiplier(pawn, data);
            if (Mathf.Abs(med - 1f) > 0.001f) into.Add(new InvoiceFactor { key = "RimWard.Invoice.Factor.MedPref", mult = med });

            // Royal / demanding patients: pay handsomely for a private suite, insulted by a shared ward.
            bool wantsPrivate = data.RequiresPrivateRoom
                || (ModsConfig.RoyaltyActive && pawn.royalty != null && pawn.royalty.AllTitlesForReading.Count > 0);
            if (s.RoyalRoomDemand && wantsPrivate)
                into.Add(new InvoiceFactor { key = "RimWard.Invoice.Factor.Royal", mult = (tier == RoomTier.Single) ? 1.4f : 0.75f });

            if (s.RenownEnabled)
            {
                HospitalSave gc = HospitalSave.Get;
                if (gc != null && gc.RenownPaymentMultiplier > 1.0001f)
                    into.Add(new InvoiceFactor { key = "RimWard.Invoice.Factor.Renown", detail = gc.TierLabel, mult = gc.RenownPaymentMultiplier });
            }
        }

        private static float MedicinePrefMultiplier(Pawn pawn, PatientData data)
        {
            Settings s = HospitalMod.Settings;
            if (s.FactionMedicinePref == MedicineStrictness.Off || pawn.Faction == null || data.BestMedicinePotency <= 0f)
            {
                return 1f;
            }
            bool tribal = pawn.Faction.def.techLevel <= TechLevel.Medieval;
            float strict = s.FactionMedicinePref == MedicineStrictness.Strict ? 2f : 1f;
            if (tribal)
            {
                return data.BestMedicinePotency > 1.1f ? 1f - 0.08f * strict : 1.03f;   // modern meds unwelcome
            }
            return data.BestMedicinePotency < 0.8f ? 1f - 0.08f * strict : 1.03f;        // herbal = substandard
        }

        // Composite goodwill: mood rating + bonuses for a private room, a chief physician, and a health improvement.
        public static int GoodwillFor(Pawn pawn, PatientData data)
        {
            int gw = PatientUtility.CalculateGoodwillToGain(pawn, data);
            if (data.BestRoomTier == RoomTier.Single) gw += 1;
            if (HospitalMod.Settings.ChiefPhysicianEnabled && data.BestDoctorSkill >= HospitalMod.Settings.ChiefPhysicianMinSkill) gw += 1;
            if (data.AdmitHealth >= 0f && pawn.health.summaryHealth.SummaryHealthPercent > data.AdmitHealth + 0.1f) gw += 1;
            return Mathf.Clamp(gw, -10, 12);
        }

        // 0..100 score used to drive renown.
        public static float DischargeScore(Pawn pawn, PatientData data, bool alive)
        {
            if (!alive) return 0f;
            float score = 50f;
            score += GoodwillFor(pawn, data) * 4f;
            return Mathf.Clamp(score, 0f, 100f);
        }
    }
}
