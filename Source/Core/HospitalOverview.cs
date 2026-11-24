// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Copyright (C) 2026 juliusberger00. Written for RimWard 2.0 (a GPL-3.0 fork of Hospital by Adamas and
// Hospital Fix And Tweak by assssssqwww). Licensed under the GNU General Public License version 3 (LICENSE);
// full source: https://github.com/jb-rimworld/rimward
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
        public string detail;       // translated detail (what is wrong or what is there), may be null
        public LookTargets target;  // "show me", may be null
        public string fixLabel;     // one-click fix (translated button text), may be null
        public System.Action fix;   // what the fix button does, null when there is no one-click fix
        public string pointer;      // translated "what to do" when there is no one-click fix, may be null
    }

    /// <summary>
    /// Plain data for the Hospital tab overview: readiness checklist with one-click fixes, bed counts, arrival block,
    /// per-patient status reason (see <see cref="HospitalMapComponent.WhyStillHere"/>), renown (see <see cref="HospitalGameComponent"/>).
    /// </summary>
    public static class HospitalOverview
    {
        public const int MinMedicine = 5;

        public static List<ReadinessItem> Readiness(Map map)
        {
            List<ReadinessItem> list = new List<ReadinessItem>();
            HospitalMapComponent comp = HospitalMapComponent.For(map);
            if (comp == null) return list;
            HospitalPolicy pol = comp.Policy;

            // 1. open, with opening hours, intake area on
            ReadinessItem open = Add(list, "Open", HospitalMod.Settings.IntakeOn && pol.open && pol.OpenHourCount > 0, null, null);
            if (!HospitalMod.Settings.IntakeOn)
            {
                open.detail = "RimWard.Ready.IntakeOff".Translate();
                open.fixLabel = "RimWard.Fix.IntakeOn".Translate();
                open.fix = () => HospitalMod.K?.Current?.SetSection(Sections.Intake, true);
            }
            else if (!pol.open)
            {
                open.detail = "RimWard.Ready.Closed".Translate();
                open.fixLabel = "RimWard.Fix.OpenNow".Translate();
                open.fix = () => SetOpen(map, true);
            }
            else if (pol.OpenHourCount == 0)
            {
                open.detail = "RimWard.Ready.NoHours".Translate();
                open.fixLabel = "RimWard.Fix.DefaultHours".Translate();
                open.fix = () => { pol.openingHours = HospitalPolicy.DefaultOpeningHours(); };
            }
            else open.detail = "RimWard.Ready.HoursCount".Translate(pol.OpenHourCount);

            // 2. patient beds indoors
            List<Building_Bed> beds = PatientBeds(map).ToList();
            List<Building_Bed> indoor = beds.Where(Indoors).ToList();
            ReadinessItem bedLine = Add(list, "Beds", indoor.Count > 0, "RimWard.Ready.BedsCount".Translate(indoor.Count), null);
            if (indoor.Count == 0)
            {
                int unused = UnusedMedicalBeds(map).Count();
                Building_Bed spare = SpareBeds(map).FirstOrDefault();
                if (unused > 0)
                {
                    bedLine.detail = "RimWard.Ready.BedsNotPatient".Translate(unused);
                    bedLine.fixLabel = "RimWard.Fix.UseMedicalBeds".Translate(unused);
                    bedLine.fix = () => UseAllMedicalBeds(map);
                }
                else if (beds.Count > 0)
                {
                    bedLine.detail = "RimWard.Ready.BedsOutdoors".Translate();
                    bedLine.target = new LookTargets(beds.First());
                    bedLine.pointer = "RimWard.Ready.BedsOutdoorsHow".Translate();
                }
                else if (spare != null)
                {
                    bedLine.detail = "RimWard.Ready.NoMedicalBed".Translate();
                    bedLine.fixLabel = "RimWard.Fix.SpareBed".Translate();
                    bedLine.target = new LookTargets(spare);
                    bedLine.fix = () => MakeMedical(spare);
                }
                else
                {
                    bedLine.detail = "RimWard.Ready.NoMedicalBed".Translate();
                    bedLine.pointer = "RimWard.Ready.NoMedicalBedHow".Translate();
                }
            }

            // 3. surgery bed when surgery cases are accepted
            if (pol.acceptSurgery && HospitalMod.Settings.SurgeryCasesOn)
            {
                int surgery = comp.SurgeryBedCount();
                ReadinessItem sb = Add(list, "SurgeryBed", surgery > 0, surgery > 0 ? (string)"RimWard.Ready.SurgeryCount".Translate(surgery) : null, null);
                if (surgery == 0)
                {
                    Building_Bed best = BestSurgeryBed(map);
                    if (best != null)
                    {
                        sb.fixLabel = "RimWard.Fix.SurgeryBed".Translate();
                        sb.target = new LookTargets(best);
                        sb.fix = () => { CompHospitalBed c = best.TryGetComp<CompHospitalBed>(); if (c != null) c.Surgery = true; };
                    }
                    else sb.pointer = "RimWard.Ready.SurgeryBedHow".Translate();
                }
            }

            // 4. a doctor
            List<Pawn> doctors = Doctors(map).ToList();
            ReadinessItem doc = Add(list, "Doctor", doctors.Count > 0, doctors.Count > 0 ? (string)"RimWard.Ready.DoctorNames".Translate(string.Join(", ", doctors.Take(3).Select(d => d.LabelShort))) : null, null);
            if (doctors.Count == 0)
            {
                Pawn cand = DoctorCandidate(map);
                if (cand != null)
                {
                    doc.detail = "RimWard.Ready.NoDoctor".Translate();
                    doc.fixLabel = "RimWard.Fix.Doctor".Translate(cand.LabelShort);
                    doc.target = new LookTargets(cand);
                    doc.fix = () => EnableDoctor(cand);
                }
                else
                {
                    doc.detail = "RimWard.Ready.NoDoctor".Translate();
                    doc.pointer = "RimWard.Ready.NoDoctorHow".Translate();
                }
            }

            // 5. medicine
            int medicine = 0, forbidden = 0;
            foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.Medicine))
            {
                if (t.Position.Fogged(map) || !t.Spawned) continue;
                if (t.IsForbidden(Faction.OfPlayer)) forbidden += t.stackCount; else medicine += t.stackCount;
            }
            ReadinessItem med = Add(list, "Medicine", medicine >= MinMedicine, "RimWard.Ready.MedicineCount".Translate(medicine), null);
            if (medicine < MinMedicine)
            {
                if (forbidden > 0)
                {
                    med.fixLabel = "RimWard.Fix.Unforbid".Translate(forbidden);
                    med.fix = () => UnforbidMedicine(map);
                }
                else med.pointer = "RimWard.Ready.MedicineHow".Translate();
            }

            // 6. drop pods can land
            ThingDef spotDef = DefDatabase<ThingDef>.GetNamedSilentFail("RimWard_PatientLandingSpot");
            Building spot = spotDef == null ? null : map.listerBuildings.AllBuildingsColonistOfDef(spotDef).FirstOrDefault();
            bool dropOk = spot != null || DropCellFinder.TryFindSafeLandingSpotCloseToColony(map, IntVec2.Two, null, 2).IsValid;
            ReadinessItem land = Add(list, "Landing", dropOk, spot != null ? null : (string)"RimWard.Ready.LandingNoSpot".Translate(), spot != null ? new LookTargets(spot) : null);
            if (spot == null && spotDef != null && BuildCopyCommandUtility.FindAllowedDesignator(spotDef) != null)
            {
                land.fixLabel = "RimWard.Fix.LandingSpot".Translate();
                land.fix = () => PlaceLandingSpot(spotDef);
            }

            // 7. no active danger
            string danger = Threats.ArrivalBlock(map, pol);
            ReadinessItem safe = Add(list, "Safe", danger == null, danger, null);
            if (danger != null) safe.pointer = "RimWard.Ready.SafeHow".Translate();

            // 8. food policy (falls back to the game's default, so this is a statement more than a check)
            Add(list, "Food", pol.FoodPolicy != null, pol.FoodPolicy?.label, null);
            return list;
        }

        private static ReadinessItem Add(List<ReadinessItem> list, string key, bool ok, string detail, LookTargets target)
        {
            ReadinessItem item = new ReadinessItem
            {
                key = key,
                ok = ok,
                label = ("RimWard.Ready." + key).Translate().ToString(),
                detail = detail,
                target = target
            };
            list.Add(item);
            return item;
        }

        private static bool Indoors(Building_Bed b)
        {
            Room r = b.GetRoom();
            return r != null && !r.PsychologicallyOutdoors;
        }

        // ---------------------------------------------------------------- one-click fixes (also used by the start card)

        /// <summary>Opens or closes the hospital (the policy in force on this map) and says so.</summary>
        public static void SetOpen(Map map, bool open)
        {
            HospitalMapComponent comp = HospitalMapComponent.For(map);
            if (comp == null) return;
            HospitalPolicy pol = comp.Policy;
            pol.open = open;
            if (open && pol.OpenHourCount == 0) pol.openingHours = HospitalPolicy.DefaultOpeningHours();
            if (open && HospitalMod.Settings != null && !HospitalMod.Settings.IntakeOn) HospitalMod.K?.Current?.SetSection(Sections.Intake, true);
        }

        /// <summary>Medical beds of the colony (humanlike, not for prisoners) that do not take patients yet.</summary>
        public static IEnumerable<Building_Bed> UnusedMedicalBeds(Map map)
        {
            return map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().Where(bed =>
                bed.Medical && !bed.ForPrisoners && bed.def.building.bed_humanlike && bed.Spawned
                && bed.TryGetComp<CompHospitalBed>() is CompHospitalBed c && !c.Hospital);
        }

        /// <summary>Every medical bed of the colony takes patients from now on (the per-bed switch stays). Returns how many changed.</summary>
        public static int UseAllMedicalBeds(Map map)
        {
            int n = 0;
            foreach (Building_Bed bed in UnusedMedicalBeds(map).ToList())
            {
                CompHospitalBed c = bed.TryGetComp<CompHospitalBed>();
                if (c == null) continue;
                c.Hospital = true;
                n++;
            }
            return n;
        }

        /// <summary>Beds nobody owns that could become medical patient beds (indoors, humanlike, not for prisoners).</summary>
        public static IEnumerable<Building_Bed> SpareBeds(Map map)
        {
            return map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().Where(bed =>
                !bed.Medical && !bed.ForPrisoners && bed.def.building.bed_humanlike && bed.def.building.bed_canBeMedical && bed.Spawned
                && bed.OwnersForReading.Count == 0 && bed.TryGetComp<CompHospitalBed>() != null && Indoors(bed));
        }

        public static void MakeMedical(Building_Bed bed)
        {
            if (bed == null || !bed.Spawned) return;
            bed.Medical = true;
            CompHospitalBed c = bed.TryGetComp<CompHospitalBed>();
            if (c != null) c.Hospital = true;
        }

        /// <summary>The patient bed with the best surgery success factor (indoors first), for "use one bed for surgery".</summary>
        public static Building_Bed BestSurgeryBed(Map map)
        {
            return PatientBeds(map).Where(b => !BedKinds.IsMedPod(b.def))
                .OrderByDescending(b => Indoors(b) ? 1 : 0)
                .ThenByDescending(b => b.GetStatValue(StatDefOf.SurgerySuccessChanceFactor))
                .FirstOrDefault();
        }

        public static IEnumerable<Pawn> Doctors(Map map)
        {
            return map.mapPawns.FreeColonistsSpawned.Where(c => !c.Downed && c.workSettings != null && c.workSettings.WorkIsActive(WorkTypeDefOf.Doctor)
                && c.health.capacities.CapableOf(PawnCapacityDefOf.Moving));
        }

        /// <summary>The colonist with the best Medicine skill who could do Doctor work but has it switched off.</summary>
        public static Pawn DoctorCandidate(Map map)
        {
            return map.mapPawns.FreeColonistsSpawned.Where(c => !c.Downed && c.workSettings != null && c.workSettings.EverWork
                && !c.WorkTypeIsDisabled(WorkTypeDefOf.Doctor) && !c.workSettings.WorkIsActive(WorkTypeDefOf.Doctor)
                && c.health.capacities.CapableOf(PawnCapacityDefOf.Moving))
                .OrderByDescending(c => c.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0).FirstOrDefault();
        }

        public static void EnableDoctor(Pawn p)
        {
            if (p?.workSettings == null || !p.workSettings.EverWork || p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor)) return;
            p.workSettings.SetPriority(WorkTypeDefOf.Doctor, 3);
        }

        public static int UnforbidMedicine(Map map)
        {
            int n = 0;
            foreach (Thing t in map.listerThings.ThingsInGroup(ThingRequestGroup.Medicine).ToList())
            {
                if (!t.Spawned || t.Position.Fogged(map) || !t.IsForbidden(Faction.OfPlayer)) continue;
                t.SetForbidden(false, false);
                n += t.stackCount;
            }
            return n;
        }

        /// <summary>Picks the landing spot's build designator, as if the player chose it in the architect menu.</summary>
        public static bool PlaceLandingSpot(ThingDef spotDef)
        {
            Designator_Build des = spotDef == null ? null : BuildCopyCommandUtility.FindAllowedDesignator(spotDef);
            if (des == null) return false;
            Find.MainTabsRoot?.EscapeCurrentTab(false);
            Find.DesignatorManager.Select(des);
            return true;
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
