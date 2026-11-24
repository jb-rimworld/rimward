using System.Collections.Generic;
using System.Linq;
using JBFramework;
using RimWard.Utilities;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard
{
    /// <summary>
    /// RimWard's Mod class (name kept from 1.x: it names the settings file <c>Mod_RimWard_HospitalMod.xml</c>). Settings window, per-save
    /// values, switchable areas, presets, compatibility page, Harmony (class by class) and diagnosis come from JB Framework.
    /// </summary>
    public sealed class HospitalMod : KitModBase
    {
        public const string PackageId = "juliusberger.rimward";
        public const string HearthHarborId = "juliusberger00.hearthharbor";
        public const string LuciferiumOverhaulId = "juliusberger00.luciferiumoverhaul";
        public const string HospitalBedsId = "juliusberger.hospitalbedsgen2";
        public const string CombatExtendedId = "ceteam.combatextended";

        private static KitMod kit;
        private static Settings settings;

        /// <summary>RimWard's framework context (null only before RimWorld created the mod class).</summary>
        public static KitMod K => kit ?? (kit = Framework.Of<HospitalMod>());

        /// <summary>The settings (values read through the framework registry; see <see cref="RimWard.Settings"/>).</summary>
        public static Settings Settings => settings ?? (settings = K?.Settings as Settings);

        public HospitalMod(ModContentPack content) : base(content, new KitInfo
        {
            Name = "RimWard",
            ShortId = "RW",
            KeyPrefix = "RimWard",
            Version = "2.0.0",
            PerSave = true,
            WorkshopId = "3819557978",
            Url = "https://github.com/jb-rimworld/rimward",
            LicenseLine = "GPL-3.0 (fork of Hospital by Adamas and Hospital Fix And Tweak by assssssqwww)",
            HarmonyId = PackageId,
            FirstStartPitch = "RimWard turns your medical beds into a hospital that strangers pay to be patched up in. Clinic is the normal game, "
                + "Charity treats for free, Meat shop pays more and forgives deaths. Everything can be changed later in Mod settings.",
            Theme = new KitTheme
            {
                Accent = new Color(0.40f, 0.74f, 0.70f),   // hospital teal
                MarkTex = "UI/RimWard/IconSmall",
                Icons =
                {
                    ["section:intake"] = "UI/RimWard/IconSmall",
                },
            },
        }) { }

        protected override void Register(KitRegistry r)
        {
            kit = r.Kit;
            RegisterSections(r);
            RegisterSettings(r);
            RegisterPresets(r);
            RegisterCompat(r);
            r.Page("policies", "Hospital policies (this colony)", SettingsPages.DrawPolicies).WithOrder(5).InSection(Sections.Intake);
            r.Page("pricing", "Pricing explained", SettingsPages.DrawPricing).WithOrder(30);
            r.Page("blacklists", "Blacklists", SettingsPages.DrawBlacklists).WithOrder(40);
        }

        private static void RegisterSections(KitRegistry r)
        {
            r.Section(Sections.Intake, "Patient intake",
                "Patients arrive by drop pod while the hospital is open. Off: nobody new comes and no contracts are offered; patients already here are treated and leave as usual.");
            r.Section(Sections.Surgery, "Surgery cases",
                "Some patients come for an operation your colony can do (a bionic, a cataract, a carcinoma). Off: no new surgery patients; operations already planned still happen.");
            r.Section(Sections.MassCasualties, "Mass casualties",
                "Now and then a pandemic, raid, crash or fire sends several patients at once, never more than you have free patient beds. Each colony still opts in under Hospital policies.")
                .LegacyEnabled("MassCasualtiesEnabled");
            r.Section(Sections.Complications, "Complications",
                "Patients may bring a second illness, a chronic condition or a harmless addiction. Never anything that kills them or sends them berserk.")
                .LegacyEnabled("ComplicationsEnabled");
            r.Section(Sections.Billing, "Billing extras",
                "Partial pay when a patient leaves early, reimbursement after a failed operation, invoice letters for big payouts. Off: the plain bill, paid in full when cured and nothing otherwise.");
            r.Section(Sections.Malpractice, "Malpractice",
                "A patient who dies through your fault costs goodwill with their faction. Deaths you could not prevent never do.")
                .LegacyEnabled("DeathPenaltyEnabled");
            r.Section(Sections.Renown, "Renown",
                "Good outcomes build the hospital's renown in four tiers (Field Aid, Clinic, Hospital, Medical Hub), each paying 5% more.")
                .LegacyEnabled("RenownEnabled");
            r.Section(Sections.Royalty, "Noble patients (Royalty)",
                "Titled patients want a private room: they pay 40% more for one and 25% less for a shared one.")
                .OnlyWith(KitDetect.RoyaltyId, "Royalty").LegacyEnabled("RoyalRoomDemand");
            r.Section(Sections.Ideology, "Guilty ideoligions (Ideology)",
                "Colonists whose ideoligion has the Guilty meme feel bad when the hospital turns the sick away.")
                .OnlyWith(KitDetect.IdeologyId, "Ideology").LegacyEnabled("IdeologyGuilty");
            r.Section(Sections.Hearth, "Hearth & Harbor bridge",
                "With Hearth & Harbor: one bed button for Colonist / Guest / Patient / Surgery patient, and patients who are up and about may use its spa.")
                .OnlyWith(HearthHarborId, "Hearth & Harbor");
            r.Section(Sections.CombatExtended, "Combat Extended",
                "With Combat Extended: patients never tend themselves with CE's tactical self-tend; your doctors treat them.")
                .OnlyWith(CombatExtendedId, "Combat Extended");
            r.Section(Sections.Luciferium, "Luciferium critical cases",
                "With Luciferium Overhaul: now and then a patient comes whom only luciferium can help. A doctor administers one dose from your stock and the patient pays very well; they go home dependent on it, and their people keep them supplied.")
                .OnlyWith(LuciferiumOverhaulId, "Luciferium Overhaul").OffByDefault();
        }

        private static void RegisterSettings(KitRegistry r)
        {
            string core = KitMod.Core;
            // ---- General: patients
            r.Float(core, Keys.Difficulty, 1f, 0.5f, 2f, "Case difficulty", "Scales how badly patients are wounded or ill and how many complications they bring.")
                .InGroup("Patients").WithStep(0.1f).Legacy("Difficulty");
            r.Bool(core, Keys.OpenInNewColonies, true, "Open the hospital in new colonies",
                "A new colony starts with the hospital open. Nobody comes until there is a medical bed set to Patient. Existing saves keep what they had.")
                .InGroup("Patients").Legacy("OpenInNewColonies");
            r.Bool(core, Keys.SpecialNeeds, false, "Special-needs xenotypes as patients",
                "Off: patients who live on hemogen or need a deathrest casket are not sent. Their faction's own xenotype is always kept.")
                .InGroup("Patients").RequiresOn(KitDetect.BiotechId, "Needs Biotech.").Legacy("AllowSpecialNeedsXenotypes");
            r.Bool(core, Keys.ShowMessageAtArrival, false, "Arrival as a message instead of a letter",
                "Routine arrivals show a short message at the top of the screen instead of a letter.")
                .InGroup("Patients").Legacy("ShowMessageAtArrival");
            // ---- General: nobody gets stuck
            r.Float(core, Keys.MaxStayDays, 15f, 0f, 60f, "Maximum stay (days)", "After this many days a patient is discharged with the full bill so far. 0 = no limit.")
                .InGroup("Nobody gets stuck").WithStep(1f).Legacy("MaxStayDays");
            r.Float(core, Keys.ImpossibleHours, 24f, 6f, 96f, "Impossible treatment: letter after (hours)",
                "A pending operation that cannot be done (part gone, nobody skilled enough, missing ingredient, refused) sends a letter after this time and discharges the patient after the same time again. No penalty.")
                .InGroup("Nobody gets stuck").WithStep(1f).Legacy("ImpossibleTreatmentHours");
            r.Float(core, Keys.DangerHoldHours, 24f, 0f, 72f, "Cured patients wait for danger at most (hours)",
                "Only active threats count (raiders, manhunters). Dormant mech clusters and weather never hold anyone.")
                .InGroup("Nobody gets stuck").WithStep(1f).Legacy("MaxDangerHoldHours");
            // ---- General: payment
            r.Bool(core, Keys.ChargePatients, true, "Patients pay", "Off: a charity ward. Nobody pays, goodwill still counts.")
                .InGroup("Payment").Legacy("ChargePatients");
            r.Enum(core, Keys.Payment, PaymentPreset.BreakEven, "Payment scale",
                "How much patients pay compared with what their treatment cost you. Break-even covers medicine and work.")
                .InGroup("Payment").Legacy("Payment");
            r.Percent(core, Keys.CustomPayment, 1f, 0.05f, 15f, "Custom payment scale", "Used when the payment scale is Custom.")
                .InGroup("Payment").Legacy("CustomPaymentMultiplier");
            r.Silver(core, Keys.BillCap, 20000, 1000, 50000, "Maximum payout per patient", "No patient pays more than this, however long the bill.")
                .InGroup("Payment").Legacy("BillCap");
            r.Enum(core, Keys.MedPref, MedicineStrictness.Soft, "Faction medicine preference",
                "Tribal patients dislike glitterworld medicine, modern ones dislike herbal medicine: they pay less when treated with it. Strict doubles the effect.")
                .InGroup("Payment").Legacy("FactionMedicinePref");
            // ---- General: rooms and doctors
            r.Percent(core, Keys.SingleRoom, 1.35f, 1f, 2f, "Private room pay", "Pay multiplier for a patient who rested in a room of their own.")
                .InGroup("Rooms and doctors").Legacy("SingleRoomMultiplier");
            r.Percent(core, Keys.TwoBed, 1.10f, 0.8f, 1.5f, "Two-bed room pay", "Pay multiplier for a room with two beds.")
                .InGroup("Rooms and doctors").Legacy("TwoBedMultiplier");
            r.Percent(core, Keys.Barracks, 0.85f, 0.5f, 1.2f, "Ward pay", "Pay multiplier for a room with three or more beds. A bed in a hallway pays 85% of this.")
                .InGroup("Rooms and doctors").Legacy("BarracksMultiplier");
            r.Bool(core, Keys.Chief, true, "Chief physician bonus", "Patients treated by a highly skilled doctor pay more and leave happier.")
                .InGroup("Rooms and doctors").Legacy("ChiefPhysicianEnabled");
            r.Int(core, Keys.ChiefSkill, 12, 1, 20, "Chief physician: minimum Medicine skill", "The best doctor who treated the patient needs at least this Medicine skill.")
                .InGroup("Rooms and doctors").Legacy("ChiefPhysicianMinSkill");
            r.Percent(core, Keys.ChiefMult, 1.25f, 1f, 2f, "Chief physician pay", "Pay multiplier when the chief physician bonus applies.")
                .InGroup("Rooms and doctors").Legacy("ChiefPhysicianMultiplier");

            // ---- Intake
            r.Enum(Sections.Intake, Keys.IntakeMode, IntakeMode.Firehose, "Intake mode",
                "Firehose: patients just arrive. Hybrid: a letter asks first for surgery cases and nobles. Prompt: a letter for every case, with diagnosis and estimated pay.")
                .InGroup("Arrivals").Legacy("Intake");
            r.Float(Sections.Intake, Keys.TriageHours, 6f, 1f, 24f, "Offer decision time (hours)", "An offer letter you do not answer expires after this time. Declining is free.")
                .InGroup("Arrivals").WithStep(1f).Legacy("TriageDecisionHours");
            r.Bool(Sections.Intake, Keys.Ambient, true, "Treat injured visitors on the map (contracts)",
                "When visitors, traders or allies get hurt on your map, they offer to pay for treatment.")
                .InGroup("Arrivals").Legacy("AmbientContractsEnabled");

            // ---- Surgery cases
            r.Bool(Sections.Surgery, Keys.AutoRetry, true, "Retry failed operations", "A botched operation is attempted again instead of sending the patient home uncured.")
                .InGroup("Operations").Legacy("AutoRetryEnabled");
            r.Int(Sections.Surgery, Keys.MaxRetries, 3, 0, 3, "Retries at most", "How often a failed operation is attempted again.")
                .InGroup("Operations").Legacy("MaxSurgeryRetries");

            // ---- Billing extras
            r.Percent(Sections.Billing, Keys.EarlyPay, 0.5f, 0f, 1f, "Share paid when leaving early",
                "Share of the bill so far that a patient pays when sent home early, when treatment is impossible, or after a death that is not your fault.")
                .InGroup("Leaving early").Legacy("EarlyDischargePayFraction");
            r.Bool(Sections.Billing, Keys.NoFaultProfit, true, "A death that is not your fault still pays",
                "The bill so far times the share above is paid when a patient dies and it was not your fault.")
                .InGroup("Leaving early").Legacy("NoFaultDeathCanProfit");
            r.Bool(Sections.Billing, Keys.ReimburseMedicine, true, "Reimburse medicine after a failed operation",
                "After a failed operation the patient's people pay for the medicine it used up. There is no fee for the operation itself.")
                .InGroup("Failed operations").Legacy("ReimburseMedicineOnFailure");
            r.Bool(Sections.Billing, Keys.ReimburseParts, true, "Reimburse parts after a failed operation",
                "After a failed operation the patient's people pay for the parts it used up (prosthetics, organs).")
                .InGroup("Failed operations").Legacy("ReimburseProstheticsOnFailure");
            r.Silver(Sections.Billing, Keys.InvoiceLetter, 500, 0, 5000, "Invoice letter from",
                "Discharges that pay at least this much silver send a letter with the itemised invoice. Smaller ones send a message.")
                .InGroup("Invoices").Legacy("InvoiceLetterThreshold");

            // ---- Malpractice
            r.Int(Sections.Malpractice, Keys.DeathAmount, 5, 0, 25, "Goodwill lost per death at fault", "Goodwill the patient's faction takes off when a death is your fault.")
                .InGroup("Penalty").Legacy("DeathPenaltyAmount");
            r.Bool(Sections.Malpractice, Keys.NoFaultSurgery, true, "No penalty for deaths in surgery", "A patient dying on the operating table, or soon after a botched operation, is clinical risk, not your fault.")
                .InGroup("Not your fault").Legacy("NoFaultSurgery");
            r.Bool(Sections.Malpractice, Keys.NoFaultLeaving, true, "No penalty for deaths after dismissal", "A patient you sent home who dies on the way is not your fault.")
                .InGroup("Not your fault").Legacy("NoFaultLeaving");
            r.Bool(Sections.Malpractice, Keys.NoFaultUnwinnable, true, "No penalty if dying on arrival", "A patient who dies within an hour of arriving was beyond help.")
                .InGroup("Not your fault").Legacy("NoFaultUnwinnable");

            // ---- Hearth & Harbor
            r.Bool(Sections.Hearth, Keys.CombinedBed, true, "One combined bed button",
                "One \"Use: Colonist / Guest / Patient / Surgery patient\" button instead of separate hospital, surgery and guest bed toggles. Needs a Hearth & Harbor version with the selector protocol.")
                .InGroup("Beds").Legacy("CombinedBedButton");
            r.Bool(Sections.Hearth, Keys.Leisure, true, "Patients may use guest leisure",
                "Patients who are up and about (not bleeding, no operation waiting) may visit Hearth & Harbor's spa and other leisure places while they recover.")
                .InGroup("Leisure").Legacy("PatientLeisure");

            // ---- Luciferium critical cases
            r.Percent(Sections.Luciferium, Keys.CriticalChance, 0.05f, 0.01f, 0.5f, "Share of patients who are critical",
                "Chance that a new patient is a critical case, when you have luciferium in stock and a doctor who can administer it.")
                .InGroup("Critical cases");
            r.Percent(Sections.Luciferium, Keys.CriticalBonus, 2f, 0.5f, 5f, "Critical case fee",
                "Fee on top of the dose itself, as a multiple of the value of one luciferium.")
                .InGroup("Critical cases");
        }

        private static void RegisterPresets(KitRegistry r)
        {
            r.Preset("clinic", "Clinic", "The normal game: patients pay what their treatment cost you, deaths through your fault cost goodwill.", p =>
            {
                p.Set(Keys.ChargePatients, true);
                p.Set(Keys.Payment, (float)(int)PaymentPreset.BreakEven);
                p.Set(Keys.Difficulty, 1f);
                p.Set(Keys.MaxStayDays, 15f);
                p.SetSection(Sections.Billing, true);
                p.SetSection(Sections.Malpractice, true);
                p.SetSection(Sections.Complications, true);
            });
            r.Preset("charity", "Charity", "Nobody pays. Goodwill and renown still grow with every patient you send home healthy.", p =>
            {
                p.Set(Keys.ChargePatients, false);
                p.Set(Keys.Difficulty, 1f);
                p.SetSection(Sections.Billing, false);
                p.SetSection(Sections.Malpractice, true);
            }).HighlightWhen(p => !p.GetBool(Keys.ChargePatients));
            r.Preset("meatshop", "Meat shop", "Volume over bedside manners: tougher cases, generous pay, deaths are nobody's fault, nobody stays longer than a week.", p =>
            {
                p.Set(Keys.ChargePatients, true);
                p.Set(Keys.Payment, (float)(int)PaymentPreset.Generous);
                p.Set(Keys.Difficulty, 1.5f);
                p.Set(Keys.MaxStayDays, 7f);
                p.SetSection(Sections.Billing, true);
                p.SetSection(Sections.Malpractice, false);
                p.SetSection(Sections.Complications, true);
            });
        }

        private static void RegisterCompat(KitRegistry r)
        {
            r.Compat("Hospital Beds Gen 2", HospitalBedsId, "Its beds are medical beds, so they count as patient and surgery beds automatically.");
            r.Compat("Hearth & Harbor", HearthHarborId, "One bed button for all bed uses; patients may use the spa. Switch: the Hearth & Harbor bridge area.")
                .HealthyWhen(() => HearthBridge.HearthLoaded);
            r.Compat("Luciferium Overhaul", LuciferiumOverhaulId, "Offers the Luciferium critical cases area (off until you switch it on); its addiction rules apply after the dose.");
            r.Compat("Combat Extended", CombatExtendedId, "CE's tactical self-tend is blocked for patients. Switch: the Combat Extended area.");
            r.Compat("Royalty", KitDetect.RoyaltyId, "Noble patients want a private room.");
            r.Compat("Ideology", KitDetect.IdeologyId, "Guilty ideoligions mind turning the sick away.");
            r.Compat("Biotech", KitDetect.BiotechId, "Patients keep their faction's xenotype; hemogen and deathrest xenotypes stay home unless you allow them.");
            r.Compat("Odyssey", KitDetect.OdysseyId, "Hospital policies belong to the save, so they come along after a gravship landing.");
            r.Incompatible("Hospital", "Adamas.Hospital", "RimWard is the rebuilt successor of Hospital: use only one of the two.");
            r.Incompatible("Hospital Fix And Tweak", "assssssqwww.HospitalTweak", "Its fixes are part of RimWard already.");
            r.Incompatible("Hospital Patch", "Faust.hospital.patch", "Patches the Hospital mod, which RimWard replaces.");
            KitKnown.Declare(r.Kit, KitTags.ChangesMedical, KitTags.Guests, KitTags.AlienRaces, KitTags.CombatOverhaul, KitTags.Economy, KitTags.Multiplayer);
        }

        protected override void OnSettingsChanged(string key)
        {
            if (key == "profile:loaded" || key.StartsWith("section:", System.StringComparison.Ordinal))
                MainTabWindowUtility.NotifyAllPawnTables_PawnsChanged();
        }

        protected override IEnumerable<string> Diagnosis()
        {
            HospitalGameComponent gc = HospitalGameComponent.Get;
            if (gc != null)
                yield return "Save: hospital " + (gc.Policy.open ? "open" : "closed") + ", " + gc.Policy.OpenHourCount + " opening hours, renown " + gc.renown.ToString("0")
                    + " (tier " + gc.Tier + "), treated " + gc.patientsTreated + ", died " + gc.patientsDied + " (at fault " + gc.patientsDiedAtFault + ")";
            if (Current.Game != null)
            {
                foreach (Map m in Find.Maps)
                {
                    HospitalMapComponent c = HospitalMapComponent.For(m);
                    if (c == null) continue;
                    yield return "Map " + m.uniqueID + ": " + c.Patients.Count + " patients, " + c.PendingOfferCount + " offers, beds " + c.FreeMedicalBeds() + " free of " + c.BedCount()
                        + (c.HasOwnPolicy ? ", own policy" : "") + ", arrivals: " + (IncidentHelper.ArrivalBlockReason(m) ?? "possible");
                    foreach (Pawn p in c.Patients.Keys.Where(p => p != null).Take(20))
                        yield return "  " + p.LabelShort + ": " + c.WhyStillHere(p);
                }
            }
            yield return "Caretaker fixes: " + string.Join(", ", Caretaker.Counts.Where(kv => kv.Value > 0).Select(kv => kv.Key + " " + kv.Value));
            foreach (string line in Caretaker.Recent.Take(20)) yield return "  " + line;
            Settings s = Settings;
            if (s != null)
                yield return "Blacklists: " + s.blockedDiseases.Count + " diseases, " + s.blockedSurgeries.Count + " operations, " + s.blockedFactions.Count + " factions";
        }
    }
}
