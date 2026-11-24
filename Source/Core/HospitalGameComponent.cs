using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimWard
{
    // Per-save global state: the hospital policies (shared by all maps), renown, lifetime stats and the last discharges.
    public class HospitalGameComponent : GameComponent
    {
        public float renown;
        public int patientsTreated;
        public int patientsDied;
        public int patientsDiedAtFault;
        public int patientsDischargedEarly;
        public long silverEarned;

        private HospitalPolicy policy;
        // FW-REQUEST (10): set while loading, never saved; the "tmp" prefix tells the save/load test harness it is rebuilt after a load
        private bool tmpLoadedFromSave;
        /// <summary>True once the old per-map policy fields of a pre-2.0 save were moved into <see cref="Policy"/>.</summary>
        public bool policyMigrated;
        public List<Invoice> recentInvoices = new List<Invoice>();
        public const int RecentInvoiceCount = 10;

        public HospitalGameComponent(Game game)
        {
            HospitalMapComponent.ClearCache();
            JobGiver_PatientLeisure.ClearCaches();
        }

        public static HospitalGameComponent Get => Current.Game?.GetComponent<HospitalGameComponent>();

        /// <summary>
        /// The save's hospital policy. A new colony starts from the settings (open if "Open in new colonies" is on);
        /// a pre-2.0 save gets the values its maps had (see <see cref="HospitalMapComponent"/> migration).
        /// </summary>
        public HospitalPolicy Policy
        {
            get
            {
                if (this.policy == null)
                {
                    bool open = !this.tmpLoadedFromSave && HospitalMod.Settings != null && HospitalMod.Settings.OpenInNewColonies;
                    this.policy = HospitalPolicy.NewDefault(open);
                }
                return this.policy;
            }
            set => this.policy = value;
        }

        public bool LoadedFromSave => this.tmpLoadedFromSave;

        // ---- renown (plain accessors for the overview page)
        public static readonly float[] TierThresholds = { 0f, 50f, 200f, 600f };

        // Renown tiers: Field Aid -> Clinic -> Hospital -> Medical Hub
        public int Tier => renown < 50f ? 0 : (renown < 200f ? 1 : (renown < 600f ? 2 : 3));

        public float RenownPaymentMultiplier => 1f + 0.05f * Tier;   // small, compounding business growth

        public string TierLabel => ("RimWard.Renown.Tier" + this.Tier).Translate().ToString();

        /// <summary>Renown needed for the next tier, or -1 at the top tier.</summary>
        public float NextTierAt => this.Tier >= 3 ? -1f : TierThresholds[this.Tier + 1];

        /// <summary>0..1 progress from the current tier to the next (1 at the top tier).</summary>
        public float TierProgress
        {
            get
            {
                if (this.Tier >= 3) return 1f;
                float lo = TierThresholds[this.Tier], hi = TierThresholds[this.Tier + 1];
                return Mathf.Clamp01((this.renown - lo) / (hi - lo));
            }
        }

        public void RecordDischarge(bool alive, float score, float silver)
        {
            if (alive) patientsTreated++; else patientsDied++;
            silverEarned += (long)silver;
            // renown rises with good outcomes, dips a little on bad ones; never below 0
            renown = Mathf.Max(0f, renown + (score - 40f) * 0.05f);
        }

        public void RecordInvoice(Invoice inv)
        {
            if (inv == null) return;
            if (this.recentInvoices == null) this.recentInvoices = new List<Invoice>();
            this.recentInvoices.Insert(0, inv);
            while (this.recentInvoices.Count > RecentInvoiceCount) this.recentInvoices.RemoveAt(this.recentInvoices.Count - 1);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.LoadingVars) this.tmpLoadedFromSave = true;
            Scribe_Values.Look(ref this.renown, "renown", 0f);
            Scribe_Values.Look(ref this.patientsTreated, "patientsTreated", 0);
            Scribe_Values.Look(ref this.patientsDied, "patientsDied", 0);
            Scribe_Values.Look(ref this.patientsDiedAtFault, "patientsDiedAtFault", 0);
            Scribe_Values.Look(ref this.patientsDischargedEarly, "patientsDischargedEarly", 0);
            Scribe_Values.Look(ref this.silverEarned, "silverEarned", 0L);
            Scribe_Deep.Look(ref this.policy, "policy");
            Scribe_Values.Look(ref this.policyMigrated, "policyMigrated", false);
            Scribe_Collections.Look(ref this.recentInvoices, "recentInvoices", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (this.recentInvoices == null) this.recentInvoices = new List<Invoice>();
                this.recentInvoices.RemoveAll(i => i == null);
                // a save written by 2.0+ already has its policy; only older saves migrate from their maps
                if (this.policy != null) this.policyMigrated = true;
            }
        }
    }
}
