using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard.Utilities
{
    public static class PatientUtility
    {
        public static bool IsPatient(this Pawn pawn, out HospitalMapComponent hospital, bool includeDismissed = false)
        {
            hospital = null;
            if (pawn?.Map == null) return false;
            hospital = pawn.Map.GetComponent<HospitalMapComponent>();
            return hospital != null && hospital.GetPatientData(pawn, out PatientData patientData) && (includeDismissed || !patientData.Dismissed);
        }

        public static void AddToBill(this Pawn pawn, HospitalMapComponent hospital, float silver)
        {
            if (pawn == null) return;
            if (!hospital.GetPatientData(pawn, out PatientData patientData)) return;
            patientData.Bill += silver;
            patientData.Bill = Mathf.Clamp(patientData.Bill, 0f, HospitalMapComponent.BillCap);
        }

        private static StatDef medPotencyStat;
        private static bool medPotencyResolved;
        private static StatDef MedPotency
        {
            get
            {
                if (!medPotencyResolved) { medPotencyStat = DefDatabase<StatDef>.GetNamedSilentFail("MedicalPotency"); medPotencyResolved = true; }
                return medPotencyStat;
            }
        }

        // Records who treated a patient and with what, for the chief-physician bonus + faction medicine preference.
        public static void RecordTreatment(Pawn patient, Pawn doctor, Thing medicine)
        {
            if (patient == null) return;
            if (!patient.IsPatient(out HospitalMapComponent hospital) || !hospital.GetPatientData(patient, out PatientData data)) return;
            if (doctor?.skills != null)
            {
                int skill = doctor.skills.GetSkill(SkillDefOf.Medicine)?.Level ?? 0;
                if (skill > data.BestDoctorSkill) data.BestDoctorSkill = skill;
            }
            if (medicine != null && MedPotency != null)
            {
                float pot = medicine.def.GetStatValueAbstract(MedPotency);
                if (pot > data.BestMedicinePotency) data.BestMedicinePotency = pot;
            }
        }

        public static bool GetPatientRating(this Pawn pawn, out float score, HospitalMapComponent hospital)
        {
            score = 0f;
            if (pawn == null) return false;
            if (!hospital.GetPatientData(pawn, out PatientData patientData)) return false;
            score = Score(pawn, patientData);
            return true;
        }

        public static bool GetPatientTimeInHospital(this Pawn pawn, out int ticks, HospitalMapComponent hospital)
        {
            ticks = 0;
            if (pawn == null) return false;
            if (!hospital.GetPatientData(pawn, out PatientData patientData)) return false;
            ticks = Find.TickManager.TicksGame - patientData.ArrivedAtTick;
            return true;
        }

        public static void DamagePawn(Pawn pawn, PatientData patientData, HospitalMapComponent hospital)
        {
            bool failed = false;
            switch (patientData.Type)
            {
                case PatientType.Wounds:
                    WoundsUtility.AddRandomWounds(pawn, patientData);
                    break;
                case PatientType.Disease:
                    failed = DiseaseUtility.AddRandomDisease(pawn, patientData);
                    break;
                case PatientType.Surgery:
                    failed = SurgeryUtility.AddRandomSurgeryBill(pawn, patientData, hospital);
                    break;
            }
            if (failed)
            {
                patientData.Type = PatientType.Wounds;
                WoundsUtility.AddRandomWounds(pawn, patientData);
            }
        }

        public static void DamagePawn(Pawn pawn, PatientData patientData, HospitalMapComponent hospital, IncidentWorker_MassCasualtyEvent.MCEType type)
        {
            switch (type)
            {
                case IncidentWorker_MassCasualtyEvent.MCEType.Pandemic:
                    DiseaseUtility.AddPlague(pawn, patientData);
                    break;
                case IncidentWorker_MassCasualtyEvent.MCEType.Raid:
                    WoundsUtility.AddGunshotWounds(pawn, patientData);
                    break;
                case IncidentWorker_MassCasualtyEvent.MCEType.Crash:
                    WoundsUtility.AddBruisesWounds(pawn, patientData);
                    break;
                case IncidentWorker_MassCasualtyEvent.MCEType.Fire:
                    WoundsUtility.AddBurnWounds(pawn, patientData);
                    break;
            }
        }

        public static float CalculateSilverToReceive(Pawn pawn, PatientData patientData)
        {
            // Base estimate: bill x payment scale. PatientLeaves applies room/chief/outcome multipliers on top.
            return patientData.Bill * HospitalMod.Settings.PaymentMultiplier;
        }

        public static int CalculateGoodwillToGain(Pawn pawn, PatientData patientData)
        {
            if (pawn.needs?.mood == null) return 0;
            float num = Score(pawn, patientData);
            if (num > 0.9f) return 5;
            if (num > 0.8f) return 4;
            if (num > 0.7f) return 3;
            if (num > 0.6f) return 2;
            if (pawn.needs.mood.CurLevel > pawn.mindState.mentalBreaker.BreakThresholdMinor) return 1;
            if (pawn.needs.mood.CurLevel < pawn.mindState.mentalBreaker.BreakThresholdMajor) return -1;
            return 0;
        }

        private static float Score(Pawn pawn, PatientData patientData)
        {
            if (pawn.needs?.mood == null) return 0f;
            return Math.Min((pawn.needs.mood.CurInstantLevel - patientData.InitialMood) * 3.5f, 100f);
        }

        public static bool DangersOnMap(Map map, out TaggedString reasons)
        {
            reasons = null;
            if (map.GetComponent<HospitalMapComponent>().AcceptDanger) return false;
            bool toxic = map.GameConditionManager.ConditionIsActive(GameConditionDefOf.ToxicFallout);
            Pawn[] active = map.mapPawns.AllPawnsSpawned.Where(p => !p.Dead && !p.IsPrisoner && !p.Downed && !IsFogged(p) && !p.InContainerEnclosed).ToArray();
            Faction[] hostiles = active.Where(p => p.Faction != null).Select(p => p.Faction).Where(f => f.HostileTo(Faction.OfPlayer)).Distinct().ToArray();
            bool volcanic = map.GameConditionManager.ConditionIsActive(GameConditionDefOf.VolcanicWinter);
            IEnumerable<Pawn> manhunters = active.Where(p => p.InAggroMentalState);
            if (!toxic && !volcanic && !hostiles.Any() && !manhunters.Any()) return false;
            List<string> list = new List<string>();
            if (toxic) list.Add("!!! " + GameConditionDefOf.ToxicFallout.LabelCap);
            if (volcanic) list.Add("!!! " + GameConditionDefOf.VolcanicWinter.LabelCap);
            foreach (Faction faction in hostiles)
            {
                list.Add("!!! " + faction.def.pawnsPlural.CapitalizeFirst());
            }
            foreach (IGrouping<MentalStateDef, Pawn> grouping in manhunters.GroupBy(p => p.MentalStateDef))
            {
                if (grouping.Count() > 1)
                {
                    list.Add("!!! " + grouping.First().GetKindLabelPlural() + " (" + grouping.First().MentalStateDef.label + ")");
                }
                else if (grouping.Count() == 1)
                {
                    list.Add("!!! " + grouping.First().LabelShort + " (" + grouping.First().MentalStateDef.label + ")");
                }
            }
            reasons = list.Distinct().Aggregate((a, b) => a + " " + b);
            return true;
        }

        public static bool IsFogged(Pawn pawn)
        {
            return pawn.MapHeld.fogGrid.IsFogged(pawn.PositionHeld);
        }
    }
}
