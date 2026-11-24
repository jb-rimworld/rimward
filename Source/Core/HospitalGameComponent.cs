// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
using System.Collections.Generic;
using RimWorld;
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

        // Payouts of the last 60 days (absolute tick, silver) for "income this quadrum" on the Hospital tab. Saves from before 2.0
        // start empty: the tab then counts from the first payout after the update.
        private List<int> payoutTicksAbs = new List<int>();
        private List<int> payoutSilver = new List<int>();
        public const int PayoutKeepDays = 60;

        /// <summary>The hospital start card on the Hospital tab was answered (per save).</summary>
        public bool startCardDone;

#if JB_TESTS
        /// <summary>Test-only: clears the stats a fresh colony would not have (earlier suites in the same game) and returns them for <see cref="TestRestoreStats"/>.</summary>
        internal object[] TestClearStats()
        {
            object[] keep = { this.renown, this.patientsTreated, this.patientsDied, this.silverEarned, this.payoutTicksAbs, this.payoutSilver, this.recentInvoices };
            this.renown = 0f; this.patientsTreated = 0; this.patientsDied = 0; this.silverEarned = 0;
            this.payoutTicksAbs = new List<int>(); this.payoutSilver = new List<int>(); this.recentInvoices = new List<Invoice>();
            return keep;
        }

        internal void TestRestoreStats(object[] k)
        {
            if (k == null || k.Length != 7) return;
            this.renown += (float)k[0]; this.patientsTreated += (int)k[1]; this.patientsDied += (int)k[2]; this.silverEarned += (long)k[3];
            ((List<int>)k[4]).AddRange(this.payoutTicksAbs); ((List<int>)k[5]).AddRange(this.payoutSilver);
            this.payoutTicksAbs = (List<int>)k[4]; this.payoutSilver = (List<int>)k[5];
            ((List<Invoice>)k[6]).InsertRange(0, this.recentInvoices); this.recentInvoices = (List<Invoice>)k[6];
        }
#endif

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
            if (silver >= 1f) this.RecordPayout((int)silver);
            // renown rises with good outcomes, dips a little on bad ones; never below 0
            renown = Mathf.Max(0f, renown + (score - 40f) * 0.05f);
        }

        public void RecordPayout(int silver)
        {
            if (this.payoutTicksAbs == null || this.payoutSilver == null || this.payoutTicksAbs.Count != this.payoutSilver.Count)
            {
                this.payoutTicksAbs = new List<int>();
                this.payoutSilver = new List<int>();
            }
            int now = Find.TickManager?.TicksAbs ?? 0;
            this.payoutTicksAbs.Add(now);
            this.payoutSilver.Add(silver);
            int keepFrom = now - PayoutKeepDays * GenDate.TicksPerDay;
            while (this.payoutTicksAbs.Count > 0 && this.payoutTicksAbs[0] < keepFrom)
            {
                this.payoutTicksAbs.RemoveAt(0);
                this.payoutSilver.RemoveAt(0);
            }
        }

        /// <summary>Silver paid out by patients in the current quadrum, at the given longitude (the current map's).</summary>
        public int IncomeThisQuadrum(float longitude)
        {
            if (this.payoutTicksAbs == null || this.payoutSilver == null) return 0;
            long now = Find.TickManager?.TicksAbs ?? 0;
            int want = QuadrumIndex(now, longitude), sum = 0;
            for (int i = 0; i < this.payoutTicksAbs.Count && i < this.payoutSilver.Count; i++)
                if (QuadrumIndex(this.payoutTicksAbs[i], longitude) == want) sum += this.payoutSilver[i];
            return sum;
        }

        private static int QuadrumIndex(long absTicks, float longitude) => GenDate.Year(absTicks, longitude) * 4 + (int)GenDate.Quadrum(absTicks, longitude);

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
            Scribe_Collections.Look(ref this.payoutTicksAbs, "payoutTicksAbs", LookMode.Value);
            Scribe_Collections.Look(ref this.payoutSilver, "payoutSilver", LookMode.Value);
            Scribe_Values.Look(ref this.startCardDone, "startCardDone", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (this.payoutTicksAbs == null || this.payoutSilver == null || this.payoutTicksAbs.Count != this.payoutSilver.Count)
                {
                    this.payoutTicksAbs = new List<int>();
                    this.payoutSilver = new List<int>();
                }
                if (this.recentInvoices == null) this.recentInvoices = new List<Invoice>();
                this.recentInvoices.RemoveAll(i => i == null);
                // a save written by 2.0+ already has its policy; only older saves migrate from their maps
                if (this.policy != null) this.policyMigrated = true;
            }
        }
    }
}
