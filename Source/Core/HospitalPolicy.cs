// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Copyright (C) 2026 juliusberger00. Written for RimWard 2.0 (a GPL-3.0 fork of Hospital by Adamas and
// Hospital Fix And Tweak by assssssqwww). Licensed under the GNU General Public License version 3 (LICENSE);
// full source: https://github.com/jb-rimworld/rimward
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimWard
{
    /// <summary>
    /// The hospital's policies (open, hours, what it accepts, food and medicine for patients, refused operations).
    /// One per save in <see cref="HospitalSave"/>, so every map (and every gravship landing) uses the same rules;
    /// a map may carry its own copy as an override (<see cref="HospitalMap.HasOwnPolicy"/>).
    /// </summary>
    public class HospitalPolicy : IExposable
    {
        public bool open;
        public List<bool> openingHours = DefaultOpeningHours();
        public List<RecipeDef> refusedOperations = new List<RecipeDef>();
        public FoodPolicy foodPolicy;
        public bool massCasualties;
        public bool acceptSurgery = true;
        public bool acceptDanger;
        public MedicalCareCategory medCare = MedicalCareCategory.NormalOrWorse;

        /// <summary>Open all day: the default since 2.0 (the 1.x default of four single hours let about one patient a week through).</summary>
        public static List<bool> DefaultOpeningHours()
        {
            List<bool> h = new List<bool>();
            for (int i = 0; i < 24; i++) h.Add(true);
            return h;
        }

        /// <summary>The "day shift" choice in the policy editor: 6:00 to 21:59.</summary>
        public static List<bool> DayShiftHours()
        {
            List<bool> h = new List<bool>();
            for (int i = 0; i < 24; i++) h.Add(i >= 6 && i < 22);
            return h;
        }

        public static HospitalPolicy NewDefault(bool open)
        {
            // Normal medicine or worse: vanilla's default for visitors (herbal or worse) leaves every operation that needs medicine undone
            // in a colony without herbal medicine. The player changes it in the Hospital policies.
            return new HospitalPolicy { open = open, medCare = MedicalCareCategory.NormalOrWorse };
        }

        /// <summary>Food policy for patients; falls back to the game's default policy (never null while a game runs).</summary>
        public FoodPolicy FoodPolicy
        {
            get
            {
                if (this.foodPolicy == null && Current.Game?.foodRestrictionDatabase != null)
                    this.foodPolicy = Current.Game.foodRestrictionDatabase.DefaultFoodRestriction();
                return this.foodPolicy;
            }
            set => this.foodPolicy = value;
        }

        public bool IsOpenAtHour(int hour)
        {
            this.Repair();
            return this.open && hour >= 0 && hour < 24 && this.openingHours[hour];
        }

        public int OpenHourCount { get { this.Repair(); return this.openingHours.Count(b => b); } }

        /// <summary>The next hour (0-23, today or tomorrow) the hospital opens, -1 when it never opens.</summary>
        public int NextOpenHour(int nowHour)
        {
            this.Repair();
            if (!this.open) return -1;
            for (int i = 0; i < 24; i++)
            {
                int h = (nowHour + i) % 24;
                if (this.openingHours[h]) return h;
            }
            return -1;
        }

        public void Repair()
        {
            if (this.openingHours == null || this.openingHours.Count != 24)
            {
                List<bool> old = this.openingHours;
                this.openingHours = DefaultOpeningHours();
                if (old != null) for (int i = 0; i < 24 && i < old.Count; i++) this.openingHours[i] = old[i];
            }
            if (this.refusedOperations == null) this.refusedOperations = new List<RecipeDef>();
            this.refusedOperations.RemoveAll(r => r == null);
        }

        public HospitalPolicy Copy()
        {
            this.Repair();
            return new HospitalPolicy
            {
                open = this.open,
                openingHours = new List<bool>(this.openingHours),
                refusedOperations = new List<RecipeDef>(this.refusedOperations),
                foodPolicy = this.foodPolicy,
                massCasualties = this.massCasualties,
                acceptSurgery = this.acceptSurgery,
                acceptDanger = this.acceptDanger,
                medCare = this.medCare
            };
        }

        public bool SameAs(HospitalPolicy o)
        {
            if (o == null) return false;
            this.Repair(); o.Repair();
            return this.open == o.open && this.massCasualties == o.massCasualties && this.acceptSurgery == o.acceptSurgery
                && this.acceptDanger == o.acceptDanger && this.medCare == o.medCare && this.foodPolicy == o.foodPolicy
                && this.openingHours.SequenceEqual(o.openingHours)
                && this.refusedOperations.Count == o.refusedOperations.Count && !this.refusedOperations.Except(o.refusedOperations).Any();
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref this.open, "open", false);
            Scribe_Collections.Look(ref this.openingHours, "openingHours", LookMode.Value);
            Scribe_Collections.Look(ref this.refusedOperations, "refusedOperations", LookMode.Def);
            Scribe_References.Look(ref this.foodPolicy, "foodPolicy");
            Scribe_Values.Look(ref this.massCasualties, "massCasualties", false);
            Scribe_Values.Look(ref this.acceptSurgery, "acceptSurgery", true);
            Scribe_Values.Look(ref this.acceptDanger, "acceptDanger", false);
            Scribe_Values.Look(ref this.medCare, "medCare", MedicalCareCategory.NormalOrWorse);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) this.Repair();
        }
    }
}
