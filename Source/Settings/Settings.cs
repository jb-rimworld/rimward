using System;
using System.Collections.Generic;
using JBFramework;
using Verse;

namespace RimWard
{
    public enum PaymentPreset { AlmostNothing, Charity, BreakEven, Fair, Generous, Tycoon, Custom }
    public enum MedicineStrictness { Off, Soft, Strict }
    public enum IntakeMode { Firehose, Hybrid, Prompt }

    /// <summary>
    /// RimWard's settings file (class name kept from 1.x: it names <c>Mod_RimWard_HospitalMod.xml</c>'s settings class).
    /// Every value lives in the JB Framework registry (per save, with the settings file as the template for new games); the
    /// properties below keep the names the rest of the code always used and fold in the switchable areas (sections), so a
    /// switched-off area reads as "off" everywhere. Non-numeric data (blacklists) stays in this file through <see cref="ExposeExtra"/>.
    /// </summary>
    public sealed class Settings : KitSettingsBase
    {
        // ---- blacklists (global; custom page "Blacklists")
        public List<string> blockedDiseases = new List<string>();
        public List<string> blockedSurgeries = new List<string>();
        public List<string> blockedFactions = new List<string>();

        protected override void ExposeExtra()
        {
            Scribe_Collections.Look(ref this.blockedDiseases, "blockedDiseases", LookMode.Value);
            Scribe_Collections.Look(ref this.blockedSurgeries, "blockedSurgeries", LookMode.Value);
            Scribe_Collections.Look(ref this.blockedFactions, "blockedFactions", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit || Scribe.mode == LoadSaveMode.LoadingVars)
            {
                if (this.blockedDiseases == null) this.blockedDiseases = new List<string>();
                if (this.blockedSurgeries == null) this.blockedSurgeries = new List<string>();
                if (this.blockedFactions == null) this.blockedFactions = new List<string>();
            }
        }

        private static KitMod K => HospitalMod.K;
        private static bool On(string section) => K != null && K.On(section);
        private static float F(string key) => K?.Get(key) ?? 0f;
        private static bool B(string key) => K != null && K.GetBool(key);
        private static int I(string key) => K?.GetInt(key) ?? 0;

        // ---- general (core)
        public bool ShowMessageAtArrival => B(Keys.ShowMessageAtArrival);
        public float Difficulty => F(Keys.Difficulty);
        public bool OpenInNewColonies => B(Keys.OpenInNewColonies);
        public bool AllowSpecialNeedsXenotypes => B(Keys.SpecialNeeds);
        public float MaxStayDays => F(Keys.MaxStayDays);
        public float ImpossibleTreatmentHours => F(Keys.ImpossibleHours);
        public float MaxDangerHoldHours => F(Keys.DangerHoldHours);

        // ---- money (core)
        public bool ChargePatients => B(Keys.ChargePatients);
        public PaymentPreset Payment => K?.GetEnum<PaymentPreset>(Keys.Payment) ?? PaymentPreset.BreakEven;
        public float CustomPaymentMultiplier => F(Keys.CustomPayment);
        public float BillCap => F(Keys.BillCap);
        public float SingleRoomMultiplier => F(Keys.SingleRoom);
        public float TwoBedMultiplier => F(Keys.TwoBed);
        public float BarracksMultiplier => F(Keys.Barracks);
        public bool ChiefPhysicianEnabled => B(Keys.Chief);
        public int ChiefPhysicianMinSkill => I(Keys.ChiefSkill);
        public float ChiefPhysicianMultiplier => F(Keys.ChiefMult);
        public MedicineStrictness FactionMedicinePref => K?.GetEnum<MedicineStrictness>(Keys.MedPref) ?? MedicineStrictness.Soft;

        // ---- intake
        /// <summary>The intake area is on: patients may arrive at all (per save the hospital must also be open).</summary>
        public bool IntakeOn => On(Sections.Intake);
        public IntakeMode Intake => K?.GetEnum<IntakeMode>(Keys.IntakeMode) ?? IntakeMode.Firehose;
        public float TriageDecisionHours => F(Keys.TriageHours);
        public bool AmbientContractsEnabled => IntakeOn && B(Keys.Ambient);

        // ---- surgery cases
        public bool SurgeryCasesOn => On(Sections.Surgery);
        public bool AutoRetryEnabled => B(Keys.AutoRetry);
        public int MaxSurgeryRetries => I(Keys.MaxRetries);

        // ---- areas that are only a switch
        public bool MassCasualtiesEnabled => On(Sections.MassCasualties);
        public bool ComplicationsEnabled => On(Sections.Complications);
        public bool RenownEnabled => On(Sections.Renown);
        public bool RoyalRoomDemand => On(Sections.Royalty) && ModsConfig.RoyaltyActive;
        public bool IdeologyGuilty => On(Sections.Ideology) && ModsConfig.IdeologyActive;

        // ---- malpractice
        public bool DeathPenaltyEnabled => On(Sections.Malpractice);
        public int DeathPenaltyAmount => I(Keys.DeathAmount);
        public bool NoFaultSurgery => B(Keys.NoFaultSurgery);
        public bool NoFaultLeaving => B(Keys.NoFaultLeaving);
        public bool NoFaultUnwinnable => B(Keys.NoFaultUnwinnable);

        // ---- billing extras (off = the plain bill: paid in full when cured, nothing when leaving early, no reimbursement, no letters)
        public bool BillingExtrasOn => On(Sections.Billing);
        public bool ReimburseMedicineOnFailure => BillingExtrasOn && B(Keys.ReimburseMedicine);
        public bool ReimburseProstheticsOnFailure => BillingExtrasOn && B(Keys.ReimburseParts);
        public bool NoFaultDeathCanProfit => BillingExtrasOn && B(Keys.NoFaultProfit);
        public float EarlyDischargePayFraction => BillingExtrasOn ? F(Keys.EarlyPay) : 0f;
        public float InvoiceLetterThreshold => BillingExtrasOn ? F(Keys.InvoiceLetter) : float.MaxValue;

        // ---- Hearth & Harbor bridge
        public bool CombinedBedButton => On(Sections.Hearth) && B(Keys.CombinedBed);
        public bool PatientLeisure => On(Sections.Hearth) && B(Keys.Leisure);

        // ---- Luciferium critical case (Luciferium Overhaul only, off by default)
        public bool CriticalCasesOn => On(Sections.Luciferium);
        public float CriticalCaseChance => F(Keys.CriticalChance);
        public float CriticalCaseBonus => F(Keys.CriticalBonus);

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

        /// <summary>Translated name of a settings enum value: key RimWard.Enum.&lt;Type&gt;.&lt;Value&gt;, falling back to the C# name.</summary>
        public static string EnumLabel<T>(T value) where T : struct, Enum
        {
            string key = "RimWard.Enum." + typeof(T).Name + "." + value;
            return key.CanTranslate() ? key.Translate().ToString() : value.ToString();
        }

        // ---- blacklist checks
        public bool DiseaseBlocked(Def d) => d != null && this.blockedDiseases.Contains(d.defName);
        public bool SurgeryBlocked(Def d) => d != null && this.blockedSurgeries.Contains(d.defName);
        public bool FactionBlocked(Def d) => d != null && this.blockedFactions.Contains(d.defName);
    }

    /// <summary>Section ids (storage keys: never rename).</summary>
    public static class Sections
    {
        public const string Intake = "intake";
        public const string Surgery = "surgery";
        public const string MassCasualties = "mce";
        public const string Complications = "complications";
        public const string Billing = "billing";
        public const string Malpractice = "malpractice";
        public const string Renown = "renown";
        public const string Royalty = "royalty";
        public const string Ideology = "ideology";
        public const string Hearth = "hearth";
        public const string CombatExtended = "ce";
        public const string Luciferium = "luciferium";
    }

    /// <summary>Setting keys (storage keys: never rename).</summary>
    public static class Keys
    {
        public const string ShowMessageAtArrival = "rw.messageAtArrival";
        public const string Difficulty = "rw.difficulty";
        public const string OpenInNewColonies = "rw.openInNewColonies";
        public const string SpecialNeeds = "rw.specialNeedsXenotypes";
        public const string MaxStayDays = "rw.maxStayDays";
        public const string ImpossibleHours = "rw.impossibleHours";
        public const string DangerHoldHours = "rw.dangerHoldHours";
        public const string ChargePatients = "rw.chargePatients";
        public const string Payment = "rw.payment";
        public const string CustomPayment = "rw.customPayment";
        public const string BillCap = "rw.billCap";
        public const string SingleRoom = "rw.singleRoom";
        public const string TwoBed = "rw.twoBedRoom";
        public const string Barracks = "rw.barracks";
        public const string Chief = "rw.chiefPhysician";
        public const string ChiefSkill = "rw.chiefSkill";
        public const string ChiefMult = "rw.chiefMultiplier";
        public const string MedPref = "rw.medicinePreference";
        public const string IntakeMode = "rw.intakeMode";
        public const string TriageHours = "rw.triageHours";
        public const string Ambient = "rw.contracts";
        public const string AutoRetry = "rw.autoRetry";
        public const string MaxRetries = "rw.maxRetries";
        public const string DeathAmount = "rw.deathPenalty";
        public const string NoFaultSurgery = "rw.noFaultSurgery";
        public const string NoFaultLeaving = "rw.noFaultLeaving";
        public const string NoFaultUnwinnable = "rw.noFaultDoomed";
        public const string ReimburseMedicine = "rw.reimburseMedicine";
        public const string ReimburseParts = "rw.reimburseParts";
        public const string NoFaultProfit = "rw.noFaultDeathPays";
        public const string EarlyPay = "rw.earlyPay";
        public const string InvoiceLetter = "rw.invoiceLetter";
        public const string CombinedBed = "rw.combinedBedButton";
        public const string Leisure = "rw.patientLeisure";
        public const string CriticalChance = "rw.criticalChance";
        public const string CriticalBonus = "rw.criticalBonus";
    }
}
