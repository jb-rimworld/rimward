// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard.MainTab
{
    public class PawnColumnWorker_Cure : PawnColumnWorker_Text
    {
        private float lastTimeCached;
        private HospitalMap hospital;

        protected override string GetTextFor(Pawn pawn)
        {
            if (this.hospital == null || Time.unscaledTime > this.lastTimeCached + 2f || Find.CurrentMap != this.hospital.map)
            {
                this.lastTimeCached = Time.unscaledTime;
                this.hospital = HospitalMap.For(Find.CurrentMap);
            }
            return this.hospital.GetPatientData(pawn, out PatientData patientData) ? patientData.Cure : string.Empty;
        }
    }
}
