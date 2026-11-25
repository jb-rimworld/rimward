// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
using System.Linq;
using RimWard.Utilities;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard.MainTab
{
    public class PawnColumnWorker_LabelCustom : PawnColumnWorker_Label
    {
        private int patientCountCached;
        private int bedCountCached;
        private bool full;
        private float lastTimeCached;
        private Map currentMap;

        public override void DoHeader(Rect rect, PawnTable table)
        {
            base.DoHeader(rect, table);
            if (Find.CurrentMap == null) return;
            if (Time.unscaledTime > this.lastTimeCached + 4f || Find.CurrentMap != this.currentMap)
            {
                this.currentMap = Find.CurrentMap;
                HospitalMap component = HospitalMap.For(this.currentMap);
                this.patientCountCached = component.ActivePatientsList().Count();
                this.full = component.IsFull();
                this.bedCountCached = component.BedCount();
                this.lastTimeCached = Time.unscaledTime;
            }
            Text.Font = this.DefaultHeaderFont;
            GUI.color = this.full ? Color.red : this.DefaultHeaderColor;
            Text.Anchor = TextAnchor.LowerLeft;
            Rect rect2 = rect;
            rect2.y += 3f;
            if (PatientUtility.DangersOnMap(this.currentMap, out TaggedString reasons))
            {
                GUI.color = Color.red;
                Widgets.Label(rect2, reasons);
            }
            else
            {
                Widgets.Label(rect2, "BedsFilled".Translate(this.patientCountCached, this.bedCountCached));
            }
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        public override int Compare(Pawn a, Pawn b)
        {
            return string.CompareOrdinal(a.Name.ToStringShort, b.Name.ToStringShort);
        }
    }
}
