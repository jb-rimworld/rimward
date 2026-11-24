// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard
{
    // Parity arrival cadence (auto-instantiated MapComponent). Replaced by the target-concurrency
    // volume controller in Step 6; kept faithful here (bug fixed: uses this.map, not Find.CurrentMap).
    public class IncidentScheduler : MapComponent
    {
        private bool MCEtriggered;

        /// <summary>True while an in-game test suite drives arrivals itself (set only by the JB_TESTS suites).</summary>
        internal static bool PausedForTests;

        public IncidentScheduler(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref this.MCEtriggered, "MCEtriggered", false);
        }

        /// <summary>Checks per day (one every 42 ticks).</summary>
        public const float ChecksPerDay = GenDate.TicksPerDay / 42f;

        /// <summary>
        /// Arrival chance of one check (every 42 ticks). Grows with free patient beds up to 20; a hospital with at least one free bed
        /// gets a floor of 12% of the full rate, about one arrival attempt a day (the 1.x curve gave a two-bed hospital one a week).
        /// Busy and quiet days alternate. No free bed: no arrival.
        /// </summary>
        public static float ChancePerCheck(int freeBeds, int daysPassed)
        {
            if (freeBeds <= 0) return 0f;
            float chance = (daysPassed % 2 == 0) ? 0.008f : 0.004f;
            return chance * Mathf.Max(MinBedFactor, Mathf.InverseLerp(0f, 20f, freeBeds));
        }

        public const float MinBedFactor = 0.12f;

        /// <summary>Expected arrival attempts per day (two-day average) for <paramref name="freeBeds"/> free beds, all hours open.</summary>
        public static float ExpectedAttemptsPerDay(int freeBeds) => (ChancePerCheck(freeBeds, 0) + ChancePerCheck(freeBeds, 1)) / 2f * ChecksPerDay;

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (GenTicks.TicksGame % 42 != 0 || PausedForTests) return;

            try
            {
                int free = HospitalMapComponent.For(this.map).FreeMedicalBeds();
                if (Rand.Chance(ChancePerCheck(free, GenDate.DaysPassed)))
                {
                    IncidentParms parms = new IncidentParms { target = this.map };
                    DefDatabase<IncidentDef>.GetNamed("RimWard_PatientArrives").Worker.TryExecute(parms);
                }

                if (GenDate.DaysPassed % 7 == 0 && GenDate.DaysPassed > 0)
                {
                    if (!this.MCEtriggered)
                    {
                        IncidentParms parms2 = new IncidentParms { target = this.map };
                        this.MCEtriggered = DefDatabase<IncidentDef>.GetNamed("RimWard_MassCasualtyEvent").Worker.TryExecute(parms2);
                    }
                }
                else
                {
                    this.MCEtriggered = false;
                }
            }
            catch (System.Exception e)
            {
                Log.ErrorOnce("[RimWard] patient arrival scheduler failed on map " + this.map?.uniqueID + ": " + e, 0x61c0de ^ (this.map?.uniqueID ?? 0));
            }
        }
    }
}
