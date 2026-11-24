// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
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
            if (pawn == null) return false;
            Map map = pawn.Map;
            if (map == null) return false;
            // cheap early-out for the hot paths (work giver, bed finder): colonists are never patients
            if (pawn.Faction != null && pawn.Faction.IsPlayer) return false;
            hospital = HospitalMapComponent.For(map);
            return hospital != null && hospital.GetPatientData(pawn, out PatientData patientData) && (includeDismissed || !patientData.Dismissed);
        }

        public static void AddToBill(this Pawn pawn, HospitalMapComponent hospital, float silver)
        {
            if (pawn == null || hospital == null) return;
            if (!hospital.GetPatientData(pawn, out PatientData patientData)) return;
            patientData.AddLine(InvoiceLineKind.Tend, null, silver);
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
                    failed = SurgeryUtility.AddRandomSurgeryBill(pawn, patientData, hospital, hospital.map);
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
            // The same number the patient will pay if discharged cured now (one invoice for table, letter and payout).
            return Invoice.Build(pawn, patientData, DischargeReason.Estimate).TotalSilver;
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

        /// <summary>Active danger for patients on this map (see <see cref="Threats"/>). Weather is listed too; it only stops arrivals.</summary>
        public static bool DangersOnMap(Map map, out TaggedString reasons)
        {
            HospitalMapComponent comp = HospitalMapComponent.For(map);
            string r = Threats.ArrivalBlock(map, comp?.Policy);
            reasons = r != null ? new TaggedString(r) : default(TaggedString);
            return r != null;
        }

        public static bool IsFogged(Pawn pawn)
        {
            return pawn.MapHeld.fogGrid.IsFogged(pawn.PositionHeld);
        }
    }
}
