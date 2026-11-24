using System.Collections.Generic;
using System.Linq;
using RimWard.Utilities;
using RimWorld;
using Verse;

namespace RimWard
{
    /// <summary>The switchable feature areas of RimWard (for the settings kit: one master switch per area).</summary>
    public enum FeatureArea
    {
        Intake,          // patients arrive at all (per save: policy "Accept patients")
        MassCasualties,  // mass casualty events (global master + per-save policy)
        Contracts,       // offers to treat injured visitors on the map
        Complications,   // extra ailments on arrival
        Money,           // patients pay (off = charity ward)
        Malpractice,     // goodwill penalty for deaths that are your fault
        SurgeryRetry,    // botched operations are attempted again
        Renown,          // renown tiers raise pay
        RoyalDemands,    // titled patients want a private room (Royalty)
        IdeologyGuilt,   // turning the sick away hurts "Guilty" ideoligions (Ideology)
        HearthHarbor,    // one bed button and patient leisure (Hearth & Harbor loaded)
        CombatExtended,  // CE self-tend blocked for patients (CE loaded)
        SurgeryCases,    // new patients may come for an operation
        BillingExtras,   // partial pay, reimbursements, invoice letters
        CriticalCases    // Luciferium critical cases (Luciferium Overhaul loaded, off by default)
    }

    public static class RimWardAreas
    {
        /// <summary>THE one function that says whether a feature area is on: the framework section switch of the current save
        /// (template outside a game) plus, where it applies, the save's hospital policy.</summary>
        public static bool IsOn(FeatureArea area, Map map = null)
        {
            Settings s = HospitalMod.Settings;
            if (s == null) return false;
            switch (area)
            {
                case FeatureArea.Intake:
                    if (!s.IntakeOn) return false;
                    return HospitalMapComponent.For(map ?? Find.CurrentMap)?.Policy.open ?? HospitalGameComponent.Get?.Policy.open ?? false;
                case FeatureArea.MassCasualties:
                    {
                        if (!s.MassCasualtiesEnabled) return false;
                        HospitalMapComponent c = HospitalMapComponent.For(map ?? Find.CurrentMap);
                        return c == null || c.Policy.massCasualties;
                    }
                case FeatureArea.Contracts: return s.AmbientContractsEnabled;
                case FeatureArea.Complications: return s.ComplicationsEnabled;
                case FeatureArea.Money: return s.ChargePatients;
                case FeatureArea.Malpractice: return s.DeathPenaltyEnabled;
                case FeatureArea.SurgeryRetry: return s.SurgeryCasesOn && s.AutoRetryEnabled;
                case FeatureArea.Renown: return s.RenownEnabled;
                case FeatureArea.RoyalDemands: return s.RoyalRoomDemand;
                case FeatureArea.IdeologyGuilt: return s.IdeologyGuilty;
                case FeatureArea.HearthHarbor: return HearthBridge.HearthLoaded && (s.CombinedBedButton || s.PatientLeisure);
                case FeatureArea.CombatExtended: return HospitalMod.K != null && HospitalMod.K.On(Sections.CombatExtended);
                case FeatureArea.SurgeryCases: return s.SurgeryCasesOn;
                case FeatureArea.BillingExtras: return s.BillingExtrasOn;
                case FeatureArea.CriticalCases: return s.CriticalCasesOn;
            }
            return false;
        }
    }

    /// <summary>One line of the readiness checklist ("is my hospital ready to take patients?").</summary>
    public class ReadinessItem
    {
        public string key;          // stable id, e.g. "Open"
        public bool ok;
        public string label;        // translated line
        public string detail;       // translated detail (what to do), may be null
        public LookTargets target;  // "show me", may be null
    }

    /// <summary>
    /// Plain data for the Hospital tab overview (M8 builds the page from these): readiness checklist, bed counts, arrival block,
    /// per-patient status reason (see <see cref="HospitalMapComponent.WhyStillHere"/>), renown (see <see cref="HospitalGameComponent"/>).
    /// </summary>
    public static class HospitalOverview
    {
        public static List<ReadinessItem> Readiness(Map map)
        {
            List<ReadinessItem> list = new List<ReadinessItem>();
            HospitalMapComponent comp = HospitalMapComponent.For(map);
            if (comp == null) return list;
            HospitalPolicy pol = comp.Policy;

            Add(list, "Open", pol.open && pol.OpenHourCount > 0, null, null);

            List<Building_Bed> beds = PatientBeds(map).ToList();
            List<Building_Bed> indoor = beds.Where(b => { Room r = b.GetRoom(); return r != null && !r.PsychologicallyOutdoors; }).ToList();
            Add(list, "Beds", indoor.Count > 0, indoor.Count.ToString(), beds.Count > 0 && indoor.Count == 0 ? new LookTargets(beds.First()) : null);

            if (pol.acceptSurgery) Add(list, "SurgeryBed", comp.SurgeryBedCount() > 0, null, null);

            List<Pawn> doctors = map.mapPawns.FreeColonistsSpawned.Where(c => !c.Downed && c.workSettings != null && c.workSettings.WorkIsActive(WorkTypeDefOf.Doctor)
                && c.health.capacities.CapableOf(PawnCapacityDefOf.Moving)).ToList();
            Add(list, "Doctor", doctors.Count > 0, doctors.Count.ToString(), null);

            int medicine = 0;
            foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.Medicine))
                if (!t.IsForbidden(Faction.OfPlayer) && !t.Position.Fogged(map)) medicine += t.stackCount;
            Add(list, "Medicine", medicine >= 5, medicine.ToString(), null);

            Building spot = map.listerBuildings.AllBuildingsColonistOfDef(ThingDef.Named("RimWard_PatientLandingSpot")).FirstOrDefault();
            bool dropOk = spot != null || DropCellFinder.TryFindSafeLandingSpotCloseToColony(map, IntVec2.Two, null, 2).IsValid;
            Add(list, "Landing", dropOk, spot != null ? null : (string)"RimWard.Ready.LandingNoSpot".Translate(), spot != null ? new LookTargets(spot) : null);

            string danger = Threats.ArrivalBlock(map, pol);
            Add(list, "Safe", danger == null, danger, null);

            Add(list, "Food", pol.FoodPolicy != null, pol.FoodPolicy?.label, null);
            return list;
        }

        private static void Add(List<ReadinessItem> list, string key, bool ok, string detail, LookTargets target)
        {
            list.Add(new ReadinessItem
            {
                key = key,
                ok = ok,
                label = ("RimWard.Ready." + key).Translate().ToString(),
                detail = detail,
                target = target
            });
        }

        public static IEnumerable<Building_Bed> PatientBeds(Map map)
        {
            return map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().Where(bed =>
                bed.Medical && !bed.ForPrisoners && bed.def.building.bed_humanlike && bed.Spawned
                && (bed.TryGetComp<CompHospitalBed>()?.Hospital ?? false));
        }

        /// <summary>Why no patient arrives right now on this map (translated), null when one can.</summary>
        public static string ArrivalBlock(Map map) => IncidentHelper.ArrivalBlockReason(map);

        public static int PatientCount(Map map) => HospitalMapComponent.For(map)?.ActivePatientsList().Count() ?? 0;
        public static int BedsTotal(Map map) => HospitalMapComponent.For(map)?.BedCount() ?? 0;
        public static int BedsFree(Map map) => HospitalMapComponent.For(map)?.FreeMedicalBeds() ?? 0;
        public static int PendingOffers(Map map) => HospitalMapComponent.For(map)?.PendingOfferCount ?? 0;

        // renown, plain values
        public static float Renown => HospitalGameComponent.Get?.renown ?? 0f;
        public static int RenownTier => HospitalGameComponent.Get?.Tier ?? 0;
        public static string RenownTierLabel => HospitalGameComponent.Get?.TierLabel ?? string.Empty;
        public static float RenownProgress => HospitalGameComponent.Get?.TierProgress ?? 0f;
        public static float RenownNextTierAt => HospitalGameComponent.Get?.NextTierAt ?? -1f;
        public static float RenownPayMultiplier => HospitalGameComponent.Get?.RenownPaymentMultiplier ?? 1f;
    }
}
