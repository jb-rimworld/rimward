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
