using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimWard
{
    public enum PaymentPreset { AlmostNothing, Charity, BreakEven, Fair, Generous, Tycoon, Custom }
    public enum MedicineStrictness { Off, Soft, Strict }
    public enum IntakeMode { Firehose, Hybrid, Prompt }

    public class Settings : ModSettings
    {
        // General
        public bool ShowMessageAtArrival = false;
        public IntakeMode Intake = IntakeMode.Firehose;
        public float TriageDecisionHours = 6f;
        public bool AmbientContractsEnabled = true;

        // Difficulty (central dial 0.5 .. 2.0)
        public float Difficulty = 1.0f;

        // Economy / payment
        public PaymentPreset Payment = PaymentPreset.BreakEven;
        public float CustomPaymentMultiplier = 1.0f;
        public float BillCap = 20000f;

        // Surgery
        public bool AutoRetryEnabled = true;
        public int MaxSurgeryRetries = 3;
        public bool ReimburseMedicineOnFailure = true;
        public bool ReimburseProstheticsOnFailure = true;

        // Fair death penalty
        public bool DeathPenaltyEnabled = true;
        public int DeathPenaltyAmount = 5;
        public bool NoFaultSurgery = true;
        public bool NoFaultLeaving = true;
        public bool NoFaultUnwinnable = true;

        // Rooms & quality
        public float SingleRoomMultiplier = 1.35f;
        public float TwoBedMultiplier = 1.10f;
        public float BarracksMultiplier = 0.85f;
        public bool ChiefPhysicianEnabled = true;
        public int ChiefPhysicianMinSkill = 12;
        public float ChiefPhysicianMultiplier = 1.25f;
        public MedicineStrictness FactionMedicinePref = MedicineStrictness.Soft;

        // Features
        public bool RenownEnabled = true;
        public bool RoyalRoomDemand = true;
        public bool IdeologyGuilty = true;
        public bool SoftCompatEnabled = true;
        public bool ZombieContractsEnabled = false;       // opt-in, per the user
        public bool NoFaultDeathCanProfit = true;          // the black-comedy lever

        private Vector2 scrollPos;

        public float PaymentMultiplier
        {
            get
            {
                switch (this.Payment)
                {
                    case PaymentPreset.AlmostNothing: return 0.10f;
                    case PaymentPreset.Charity: return 0.40f;
                    case PaymentPreset.BreakEven: return 1.00f;
                    case PaymentPreset.Fair: return 1.50f;
                    case PaymentPreset.Generous: return 3.00f;
                    case PaymentPreset.Tycoon: return 10.0f;
                    case PaymentPreset.Custom: return this.CustomPaymentMultiplier;
                    default: return 1.0f;
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref this.ShowMessageAtArrival, "ShowMessageAtArrival", false);
            Scribe_Values.Look(ref this.Intake, "Intake", IntakeMode.Firehose);
            Scribe_Values.Look(ref this.TriageDecisionHours, "TriageDecisionHours", 6f);
            Scribe_Values.Look(ref this.AmbientContractsEnabled, "AmbientContractsEnabled", true);
            Scribe_Values.Look(ref this.Difficulty, "Difficulty", 1.0f);
            Scribe_Values.Look(ref this.Payment, "Payment", PaymentPreset.BreakEven);
            Scribe_Values.Look(ref this.CustomPaymentMultiplier, "CustomPaymentMultiplier", 1.0f);
            Scribe_Values.Look(ref this.BillCap, "BillCap", 20000f);
            Scribe_Values.Look(ref this.AutoRetryEnabled, "AutoRetryEnabled", true);
            Scribe_Values.Look(ref this.MaxSurgeryRetries, "MaxSurgeryRetries", 3);
            Scribe_Values.Look(ref this.ReimburseMedicineOnFailure, "ReimburseMedicineOnFailure", true);
            Scribe_Values.Look(ref this.ReimburseProstheticsOnFailure, "ReimburseProstheticsOnFailure", true);
            Scribe_Values.Look(ref this.DeathPenaltyEnabled, "DeathPenaltyEnabled", true);
            Scribe_Values.Look(ref this.DeathPenaltyAmount, "DeathPenaltyAmount", 5);
            Scribe_Values.Look(ref this.NoFaultSurgery, "NoFaultSurgery", true);
            Scribe_Values.Look(ref this.NoFaultLeaving, "NoFaultLeaving", true);
            Scribe_Values.Look(ref this.NoFaultUnwinnable, "NoFaultUnwinnable", true);
            Scribe_Values.Look(ref this.SingleRoomMultiplier, "SingleRoomMultiplier", 1.35f);
            Scribe_Values.Look(ref this.TwoBedMultiplier, "TwoBedMultiplier", 1.10f);
            Scribe_Values.Look(ref this.BarracksMultiplier, "BarracksMultiplier", 0.85f);
            Scribe_Values.Look(ref this.ChiefPhysicianEnabled, "ChiefPhysicianEnabled", true);
            Scribe_Values.Look(ref this.ChiefPhysicianMinSkill, "ChiefPhysicianMinSkill", 12);
            Scribe_Values.Look(ref this.ChiefPhysicianMultiplier, "ChiefPhysicianMultiplier", 1.25f);
            Scribe_Values.Look(ref this.FactionMedicinePref, "FactionMedicinePref", MedicineStrictness.Soft);
            Scribe_Values.Look(ref this.RenownEnabled, "RenownEnabled", true);
            Scribe_Values.Look(ref this.RoyalRoomDemand, "RoyalRoomDemand", true);
            Scribe_Values.Look(ref this.IdeologyGuilty, "IdeologyGuilty", true);
            Scribe_Values.Look(ref this.SoftCompatEnabled, "SoftCompatEnabled", true);
            Scribe_Values.Look(ref this.ZombieContractsEnabled, "ZombieContractsEnabled", false);
            Scribe_Values.Look(ref this.NoFaultDeathCanProfit, "NoFaultDeathCanProfit", true);
        }

        public void DoWindowContents(Rect inRect)
        {
            Rect view = new Rect(0f, 0f, inRect.width - 24f, 1320f);
            Widgets.BeginScrollView(inRect, ref this.scrollPos, view);
            Listing_Standard l = new Listing_Standard { maxOneColumn = true };
            l.Begin(view);

            Header(l, "RimWard.Set.General".Translate());
            l.CheckboxLabeled("ShowMessageAtArrival".Translate(), ref this.ShowMessageAtArrival, "ShowMessageAtArrivalTooltip".Translate());
            EnumRow(l, "RimWard.Set.IntakeMode".Translate(), this.Intake, v => this.Intake = v);
            if (this.Intake != IntakeMode.Firehose)
                l.SliderLabeled("RimWard.Set.TriageHours".Translate(), ref this.TriageDecisionHours, this.TriageDecisionHours.ToString("0") + " h", 1f, 24f);
            l.CheckboxLabeled("RimWard.Set.Ambient".Translate(), ref this.AmbientContractsEnabled, "RimWard.Set.AmbientTip".Translate());

            Header(l, "RimWard.Set.Difficulty".Translate());
            l.SliderLabeled("RimWard.Set.DifficultyDial".Translate(), ref this.Difficulty, this.Difficulty.ToString("0.0") + "x", 0.5f, 2.0f, "RimWard.Set.DifficultyDialTip".Translate());

            Header(l, "RimWard.Set.Economy".Translate());
            EnumRow(l, "RimWard.Set.PaymentPreset".Translate(), this.Payment, v => this.Payment = v);
            if (this.Payment == PaymentPreset.Custom)
                l.SliderLabeled("RimWard.Set.CustomMult".Translate(), ref this.CustomPaymentMultiplier, this.CustomPaymentMultiplier.ToString("0%"), 0.05f, 15f);
            l.Label("RimWard.Set.EffMult".Translate(this.PaymentMultiplier.ToString("0%")));
            l.SliderLabeled("RimWard.Set.BillCap".Translate(), ref this.BillCap, this.BillCap.ToString("0"), 1000f, 50000f);

            Header(l, "RimWard.Set.Surgery".Translate());
            l.CheckboxLabeled("RimWard.Set.AutoRetry".Translate(), ref this.AutoRetryEnabled, "RimWard.Set.AutoRetryTip".Translate());
            l.SliderLabeled("RimWard.Set.MaxRetries".Translate(), ref this.MaxSurgeryRetries, this.MaxSurgeryRetries.ToString(), 0f, 3f);
            l.CheckboxLabeled("RimWard.Set.ReimburseMed".Translate(), ref this.ReimburseMedicineOnFailure);
            l.CheckboxLabeled("RimWard.Set.ReimburseProsth".Translate(), ref this.ReimburseProstheticsOnFailure);

            Header(l, "RimWard.Set.Death".Translate());
            l.CheckboxLabeled("RimWard.Set.DeathPenalty".Translate(), ref this.DeathPenaltyEnabled);
            if (this.DeathPenaltyEnabled)
                l.SliderLabeled("RimWard.Set.DeathAmount".Translate(), ref this.DeathPenaltyAmount, this.DeathPenaltyAmount.ToString(), 0f, 25f);
            l.CheckboxLabeled("RimWard.Set.NoFaultSurgery".Translate(), ref this.NoFaultSurgery, "RimWard.Set.NoFaultSurgeryTip".Translate());
            l.CheckboxLabeled("RimWard.Set.NoFaultLeaving".Translate(), ref this.NoFaultLeaving);
            l.CheckboxLabeled("RimWard.Set.NoFaultUnwinnable".Translate(), ref this.NoFaultUnwinnable);
            l.CheckboxLabeled("RimWard.Set.NoFaultProfit".Translate(), ref this.NoFaultDeathCanProfit, "RimWard.Set.NoFaultProfitTip".Translate());

            Header(l, "RimWard.Set.Rooms".Translate());
            l.SliderLabeled("RimWard.Set.SingleMult".Translate(), ref this.SingleRoomMultiplier, this.SingleRoomMultiplier.ToString("0%"), 1f, 2f);
            l.SliderLabeled("RimWard.Set.TwoBedMult".Translate(), ref this.TwoBedMultiplier, this.TwoBedMultiplier.ToString("0%"), 0.8f, 1.5f);
            l.SliderLabeled("RimWard.Set.BarracksMult".Translate(), ref this.BarracksMultiplier, this.BarracksMultiplier.ToString("0%"), 0.5f, 1.2f);
            l.CheckboxLabeled("RimWard.Set.Chief".Translate(), ref this.ChiefPhysicianEnabled, "RimWard.Set.ChiefTip".Translate());
            if (this.ChiefPhysicianEnabled)
            {
                l.SliderLabeled("RimWard.Set.ChiefSkill".Translate(), ref this.ChiefPhysicianMinSkill, this.ChiefPhysicianMinSkill.ToString(), 1f, 20f);
                l.SliderLabeled("RimWard.Set.ChiefMult".Translate(), ref this.ChiefPhysicianMultiplier, this.ChiefPhysicianMultiplier.ToString("0%"), 1f, 2f);
            }
            EnumRow(l, "RimWard.Set.MedPref".Translate(), this.FactionMedicinePref, v => this.FactionMedicinePref = v);

            Header(l, "RimWard.Set.Features".Translate());
            l.CheckboxLabeled("RimWard.Set.Renown".Translate(), ref this.RenownEnabled);
            l.CheckboxLabeled("RimWard.Set.RoyalRoom".Translate(), ref this.RoyalRoomDemand);
            l.CheckboxLabeled("RimWard.Set.IdeoGuilty".Translate(), ref this.IdeologyGuilty);
            l.CheckboxLabeled("RimWard.Set.SoftCompat".Translate(), ref this.SoftCompatEnabled, "RimWard.Set.SoftCompatTip".Translate());
            l.CheckboxLabeled("RimWard.Set.Zombie".Translate(), ref this.ZombieContractsEnabled, "RimWard.Set.ZombieTip".Translate());

            l.End();
            Widgets.EndScrollView();
        }

        private static void Header(Listing_Standard l, string label)
        {
            l.Gap(6f);
            Text.Font = GameFont.Medium;
            l.Label(label);
            Text.Font = GameFont.Small;
            l.GapLine(4f);
        }

        private static void EnumRow<T>(Listing_Standard l, string label, T value, Action<T> setter) where T : struct, Enum
        {
            Rect r = l.GetRect(28f);
            Widgets.Label(r.LeftHalf(), label);
            if (Widgets.ButtonText(r.RightHalf(), value.ToString()))
            {
                List<FloatMenuOption> opts = new List<FloatMenuOption>();
                foreach (T v in Enum.GetValues(typeof(T)))
                {
                    T captured = v;
                    opts.Add(new FloatMenuOption(captured.ToString(), () => setter(captured)));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
        }
    }
}
