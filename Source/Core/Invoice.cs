using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard
{
    public enum InvoiceLineKind { Admission, Tend, Food, Surgery, Materials, ReimburseMedicine, ReimburseParts, Legacy, CriticalCase }

    /// <summary>Why a stay ends (or, for <see cref="Estimate"/>, that it has not ended yet).</summary>
    public enum DischargeReason { Estimate, Cured, Dismissed, TreatmentImpossible, MaxStay, DiedNoFault }

    /// <summary>One charge on a patient's bill. The label is built at display time from the kind and the detail, so it follows the language.</summary>
    public class InvoiceLine : IExposable
    {
        public InvoiceLineKind kind;
        public string detail;
        public float amount;
        public int tick;

        public string Label => ("RimWard.Invoice.Line." + this.kind).Translate(this.detail ?? string.Empty).ToString();

        public void ExposeData()
        {
            Scribe_Values.Look(ref this.kind, "kind", InvoiceLineKind.Legacy);
            Scribe_Values.Look(ref this.detail, "detail");
            Scribe_Values.Look(ref this.amount, "amount", 0f);
            Scribe_Values.Look(ref this.tick, "tick", 0);
        }
    }

    /// <summary>One multiplier on the subtotal (payment dial, room, chief physician, ...).</summary>
    public class InvoiceFactor : IExposable
    {
        public string key;      // translation key of the factor name
        public string detail;   // e.g. the room tier name
        public float mult = 1f;

        public string Label => this.key.Translate(this.detail ?? string.Empty).ToString();

        public void ExposeData()
        {
            Scribe_Values.Look(ref this.key, "key");
            Scribe_Values.Look(ref this.detail, "detail");
            Scribe_Values.Look(ref this.mult, "mult", 1f);
        }
    }

    /// <summary>
    /// THE bill. Built by one function (<see cref="Build"/>) for the table column, the offer letter and the payout, so the three
    /// can never disagree. Lines (what was done) x factors (how well and where) = total, capped by the setting.
    /// </summary>
    public class Invoice : IExposable
    {
        public string patientName;
        public string factionName;
        public DischargeReason reason;
        public List<InvoiceLine> lines = new List<InvoiceLine>();
        public List<InvoiceFactor> factors = new List<InvoiceFactor>();
        public float subtotal;
        public float total;
        public bool capped;
        public int tick;

        public int TotalSilver => Mathf.Max(0, Mathf.FloorToInt(this.total));

        public static Invoice Build(Pawn pawn, PatientData data, DischargeReason reason)
        {
            Invoice inv = new Invoice
            {
                reason = reason,
                tick = Find.TickManager?.TicksGame ?? 0,
                patientName = pawn?.LabelShortCap ?? "?",
                factionName = pawn?.Faction?.Name ?? "RimWard.NoFaction".Translate().ToString()
            };
            if (data == null) return inv;
            data.EnsureLines(false);
            foreach (InvoiceLine l in data.Lines) inv.lines.Add(new InvoiceLine { kind = l.kind, detail = l.detail, amount = l.amount, tick = l.tick });
            inv.subtotal = inv.lines.Sum(l => l.amount);

            Settings s = HospitalMod.Settings;
            float mult = 1f;
            if (!s.ChargePatients)
            {
                inv.factors.Add(new InvoiceFactor { key = "RimWard.Invoice.Factor.Charity", mult = 0f });
                mult = 0f;
            }
            else
            {
                inv.factors.Add(new InvoiceFactor { key = "RimWard.Invoice.Factor.Dial", detail = Settings.EnumLabel(s.Payment), mult = s.PaymentMultiplier });
                if (pawn != null) OutcomeUtility.AddPaymentFactors(pawn, data, inv.factors);
                float share = ShareFor(reason);
                if (share < 0.999f) inv.factors.Add(new InvoiceFactor { key = "RimWard.Invoice.Factor.Early", detail = ("RimWard.Invoice.Reason." + reason).Translate().ToString(), mult = share });
                foreach (InvoiceFactor f in inv.factors) mult *= f.mult;
            }
            float raw = inv.subtotal * mult;
            float cap = Mathf.Max(0f, s.BillCap);
            inv.capped = raw > cap;
            inv.total = Mathf.Clamp(raw, 0f, cap);
            return inv;
        }

        /// <summary>Share of the bill paid for this kind of ending.</summary>
        public static float ShareFor(DischargeReason reason)
        {
            switch (reason)
            {
                case DischargeReason.Dismissed:
                case DischargeReason.TreatmentImpossible:
                case DischargeReason.DiedNoFault:
                    return Mathf.Clamp01(HospitalMod.Settings.EarlyDischargePayFraction);
                default:
                    return 1f;
            }
        }

        /// <summary>The full itemised text (letter body, tooltip).</summary>
        public string ToText()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("RimWard.Invoice.Header".Translate(this.patientName, this.factionName));
            sb.AppendLine();
            foreach (IGrouping<string, InvoiceLine> g in this.lines.GroupBy(l => l.Label))
            {
                int n = g.Count();
                float sum = g.Sum(l => l.amount);
                string label = n > 1 ? g.Key + " x" + n : g.Key;
                sb.AppendLine("  " + label + ": " + sum.ToStringMoney());
            }
            if (this.lines.Count == 0) sb.AppendLine("  " + "RimWard.Invoice.NoLines".Translate());
            sb.AppendLine("RimWard.Invoice.Subtotal".Translate(this.subtotal.ToStringMoney()));
            foreach (InvoiceFactor f in this.factors) sb.AppendLine("  " + f.Label + ": x" + f.mult.ToString("0.00"));
            if (this.capped) sb.AppendLine("RimWard.Invoice.Capped".Translate(HospitalMod.Settings.BillCap.ToStringMoney()));
            sb.Append("RimWard.Invoice.Total".Translate(((float)this.TotalSilver).ToStringMoney()));
            return sb.ToString();
        }

        /// <summary>Short tooltip for the Bill column: the factors only.</summary>
        public string FactorsTooltip()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("RimWard.Invoice.Subtotal".Translate(this.subtotal.ToStringMoney()));
            foreach (InvoiceFactor f in this.factors) sb.AppendLine(f.Label + ": x" + f.mult.ToString("0.00"));
            if (this.capped) sb.AppendLine("RimWard.Invoice.Capped".Translate(HospitalMod.Settings.BillCap.ToStringMoney()));
            sb.Append("RimWard.Invoice.Estimate".Translate(((float)this.TotalSilver).ToStringMoney()));
            return sb.ToString();
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref this.patientName, "patientName");
            Scribe_Values.Look(ref this.factionName, "factionName");
            Scribe_Values.Look(ref this.reason, "reason", DischargeReason.Cured);
            Scribe_Collections.Look(ref this.lines, "lines", LookMode.Deep);
            Scribe_Collections.Look(ref this.factors, "factors", LookMode.Deep);
            Scribe_Values.Look(ref this.subtotal, "subtotal", 0f);
            Scribe_Values.Look(ref this.total, "total", 0f);
            Scribe_Values.Look(ref this.capped, "capped", false);
            Scribe_Values.Look(ref this.tick, "tick", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (this.lines == null) this.lines = new List<InvoiceLine>();
                if (this.factors == null) this.factors = new List<InvoiceFactor>();
                this.lines.RemoveAll(l => l == null);
                this.factors.RemoveAll(f => f == null || f.key == null);
            }
        }
    }
}
