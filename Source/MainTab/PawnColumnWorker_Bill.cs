// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard.MainTab
{
    /// <summary>Expected payout if the patient were discharged cured now: the same <see cref="Invoice"/> the payout uses. Tooltip lists every line and factor.</summary>
    public class PawnColumnWorker_Bill : PawnColumnWorker_Text
    {
        private readonly Dictionary<Pawn, KeyValuePair<float, Invoice>> cache = new Dictionary<Pawn, KeyValuePair<float, Invoice>>();

        private Invoice Estimate(Pawn pawn)
        {
            float now = Time.unscaledTime;
            if (this.cache.TryGetValue(pawn, out KeyValuePair<float, Invoice> kv) && now - kv.Key < 1f) return kv.Value;
            HospitalMapComponent h = HospitalMapComponent.For(Find.CurrentMap);
            Invoice inv = h != null && h.GetPatientData(pawn, out PatientData data) ? Invoice.Build(pawn, data, DischargeReason.Estimate) : null;
            if (this.cache.Count > 200) this.cache.Clear();
            this.cache[pawn] = new KeyValuePair<float, Invoice>(now, inv);
            return inv;
        }

        protected override string GetTextFor(Pawn pawn)
        {
            Invoice inv = this.Estimate(pawn);
            return inv != null ? ((float)inv.TotalSilver).ToStringMoney() : string.Empty;
        }

        protected override string GetTip(Pawn pawn) => this.Estimate(pawn)?.ToText();
    }
}
