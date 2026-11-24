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
        public bool NoFaultDeathCanProfit = true;          // a no-fault death still pays the bill so far x EarlyDischargePayFraction

        // Billing (M3/M9)
        public bool ChargePatients = true;                 // master switch of the money area; off = charity ward (no silver, goodwill still counts)
        public float EarlyDischargePayFraction = 0.5f;     // share of the bill paid when a patient leaves uncured (dismissed, impossible treatment, no-fault death)
        public float InvoiceLetterThreshold = 500f;        // discharges paying at least this much get a letter with the itemised invoice; smaller ones a message

        // Caretaker (M5)
        public float MaxStayDays = 15f;                    // 0 = no limit; after this the patient is discharged with the bill so far
        public float ImpossibleTreatmentHours = 24f;       // a pending treatment that cannot be done this long sends a letter; the same time again discharges
        public float MaxDangerHoldHours = 24f;             // a cured patient waits this long for an active threat to pass, then leaves anyway

        // Intake (M6/M10)
        public bool OpenInNewColonies = true;              // a new colony starts with the hospital open (it still needs a patient bed before anyone comes)
        public bool AllowSpecialNeedsXenotypes = false;    // hemogen and deathrest xenotypes may come as patients
        public bool ComplicationsEnabled = true;           // extra ailments on arrival (budget follows the difficulty dial)
        public bool MassCasualtiesEnabled = true;          // master switch; each save still opts in under Hospital policies

        // Hearth & Harbor (soft integration; no effect without that mod)
        public bool CombinedBedButton = true;
        public bool PatientLeisure = true;

        private Vector2 scrollPos;
        private float viewHeight = 1900f;

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
            Scribe_Values.Look(ref this.NoFaultDeathCanProfit, "NoFaultDeathCanProfit", true);
            Scribe_Values.Look(ref this.ChargePatients, "ChargePatients", true);
            Scribe_Values.Look(ref this.EarlyDischargePayFraction, "EarlyDischargePayFraction", 0.5f);
            Scribe_Values.Look(ref this.InvoiceLetterThreshold, "InvoiceLetterThreshold", 500f);
            Scribe_Values.Look(ref this.MaxStayDays, "MaxStayDays", 15f);
            Scribe_Values.Look(ref this.ImpossibleTreatmentHours, "ImpossibleTreatmentHours", 24f);
            Scribe_Values.Look(ref this.MaxDangerHoldHours, "MaxDangerHoldHours", 24f);
            Scribe_Values.Look(ref this.OpenInNewColonies, "OpenInNewColonies", true);
            Scribe_Values.Look(ref this.AllowSpecialNeedsXenotypes, "AllowSpecialNeedsXenotypes", false);
            Scribe_Values.Look(ref this.ComplicationsEnabled, "ComplicationsEnabled", true);
            Scribe_Values.Look(ref this.MassCasualtiesEnabled, "MassCasualtiesEnabled", true);
            Scribe_Values.Look(ref this.CombinedBedButton, "CombinedBedButton", true);
            Scribe_Values.Look(ref this.PatientLeisure, "PatientLeisure", true);
        }

        public void DoWindowContents(Rect inRect)
        {
            Rect view = new Rect(0f, 0f, inRect.width - 24f, this.viewHeight);
            Widgets.BeginScrollView(inRect, ref this.scrollPos, view);
            Listing_Standard l = new Listing_Standard { maxOneColumn = true };
            l.Begin(view);

            Header(l, "RimWard.Set.General".Translate());
            l.CheckboxLabeled("ShowMessageAtArrival".Translate(), ref this.ShowMessageAtArrival, "ShowMessageAtArrivalTooltip".Translate());
            EnumRow(l, "RimWard.Set.IntakeMode".Translate(), this.Intake, v => this.Intake = v);
            if (this.Intake != IntakeMode.Firehose)
                l.SliderLabeled("RimWard.Set.TriageHours".Translate(), ref this.TriageDecisionHours, this.TriageDecisionHours.ToString("0") + " h", 1f, 24f);
            l.CheckboxLabeled("RimWard.Set.Ambient".Translate(), ref this.AmbientContractsEnabled, "RimWard.Set.AmbientTip".Translate());
            l.CheckboxLabeled("RimWard.Set.OpenInNew".Translate(), ref this.OpenInNewColonies, "RimWard.Set.OpenInNewTip".Translate());
            l.CheckboxLabeled("RimWard.Set.SpecialNeeds".Translate(), ref this.AllowSpecialNeedsXenotypes, "RimWard.Set.SpecialNeedsTip".Translate());
            l.CheckboxLabeled("RimWard.Set.MCEMaster".Translate(), ref this.MassCasualtiesEnabled, "RimWard.Set.MCEMasterTip".Translate());

            Header(l, "RimWard.Set.Difficulty".Translate());
            l.SliderLabeled("RimWard.Set.DifficultyDial".Translate(), ref this.Difficulty, this.Difficulty.ToString("0.0") + "x", 0.5f, 2.0f, "RimWard.Set.DifficultyDialTip".Translate());
            l.CheckboxLabeled("RimWard.Set.Complications".Translate(), ref this.ComplicationsEnabled, "RimWard.Set.ComplicationsTip".Translate());

            Header(l, "RimWard.Set.Economy".Translate());
            l.CheckboxLabeled("RimWard.Set.Charge".Translate(), ref this.ChargePatients, "RimWard.Set.ChargeTip".Translate());
            EnumRow(l, "RimWard.Set.PaymentPreset".Translate(), this.Payment, v => this.Payment = v);
            if (this.Payment == PaymentPreset.Custom)
                l.SliderLabeled("RimWard.Set.CustomMult".Translate(), ref this.CustomPaymentMultiplier, this.CustomPaymentMultiplier.ToString("0%"), 0.05f, 15f);
            l.Label("RimWard.Set.EffMult".Translate(this.PaymentMultiplier.ToString("0%")));
            l.SliderLabeled("RimWard.Set.BillCap".Translate(), ref this.BillCap, this.BillCap.ToString("0"), 1000f, 50000f);
            l.SliderLabeled("RimWard.Set.EarlyPay".Translate(), ref this.EarlyDischargePayFraction, this.EarlyDischargePayFraction.ToStringPercent(), 0f, 1f, "RimWard.Set.EarlyPayTip".Translate());
            l.SliderLabeled("RimWard.Set.InvoiceLetter".Translate(), ref this.InvoiceLetterThreshold, this.InvoiceLetterThreshold.ToString("0"), 0f, 5000f, "RimWard.Set.InvoiceLetterTip".Translate());

            Header(l, "RimWard.Set.Surgery".Translate());
            l.CheckboxLabeled("RimWard.Set.AutoRetry".Translate(), ref this.AutoRetryEnabled, "RimWard.Set.AutoRetryTip".Translate());
            l.SliderLabeled("RimWard.Set.MaxRetries".Translate(), ref this.MaxSurgeryRetries, this.MaxSurgeryRetries.ToString(), 0f, 3f);
            l.CheckboxLabeled("RimWard.Set.ReimburseMed".Translate(), ref this.ReimburseMedicineOnFailure, "RimWard.Set.ReimburseMedTip".Translate());
            l.CheckboxLabeled("RimWard.Set.ReimburseProsth".Translate(), ref this.ReimburseProstheticsOnFailure, "RimWard.Set.ReimburseProsthTip".Translate());

            Header(l, "RimWard.Set.Caretaker".Translate());
            l.SliderLabeled("RimWard.Set.MaxStay".Translate(), ref this.MaxStayDays, this.MaxStayDays < 0.5f ? (string)"RimWard.Set.Off".Translate() : this.MaxStayDays.ToString("0") + " d", 0f, 60f, "RimWard.Set.MaxStayTip".Translate());
            l.SliderLabeled("RimWard.Set.Impossible".Translate(), ref this.ImpossibleTreatmentHours, this.ImpossibleTreatmentHours.ToString("0") + " h", 6f, 96f, "RimWard.Set.ImpossibleTip".Translate());
            l.SliderLabeled("RimWard.Set.DangerHold".Translate(), ref this.MaxDangerHoldHours, this.MaxDangerHoldHours.ToString("0") + " h", 0f, 72f, "RimWard.Set.DangerHoldTip".Translate());

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

            Header(l, "RimWard.Set.Hearth".Translate());
            l.Label("RimWard.Set.HearthNote".Translate());
            l.CheckboxLabeled("RimWard.Set.CombinedBed".Translate(), ref this.CombinedBedButton, "RimWard.Set.CombinedBedTip".Translate());
            l.CheckboxLabeled("RimWard.Set.Leisure".Translate(), ref this.PatientLeisure, "RimWard.Set.LeisureTip".Translate());

            this.viewHeight = Mathf.Max(600f, l.CurHeight + 40f);
            l.End();
            Widgets.EndScrollView();
        }

        /// <summary>Translated name of a settings enum value: key RimWard.Enum.&lt;Type&gt;.&lt;Value&gt;, falling back to the C# name.</summary>
        public static string EnumLabel<T>(T value) where T : struct, Enum
        {
            string key = "RimWard.Enum." + typeof(T).Name + "." + value;
            return key.CanTranslate() ? key.Translate().ToString() : value.ToString();
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
            if (Widgets.ButtonText(r.RightHalf(), EnumLabel(value)))
            {
                List<FloatMenuOption> opts = new List<FloatMenuOption>();
                foreach (T v in Enum.GetValues(typeof(T)))
                {
                    T captured = v;
                    opts.Add(new FloatMenuOption(EnumLabel(captured), () => setter(captured)));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
        }
    }
}
