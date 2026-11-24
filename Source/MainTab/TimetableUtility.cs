// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimWard.MainTab
{
    [StaticConstructorOnStartup]
    public static class TimetableUtility
    {
        private static bool lastAssignmentValue;

        private static readonly Texture2D[] colorTextureInt = new Texture2D[]
        {
            SolidColorMaterials.NewSolidColorTexture(TimeAssignmentDefOf.Work.color),
            SolidColorMaterials.NewSolidColorTexture(TimeAssignmentDefOf.Sleep.color),
            SolidColorMaterials.NewSolidColorTexture(TimeAssignmentDefOf.Work.color * 1.3f),
            SolidColorMaterials.NewSolidColorTexture(TimeAssignmentDefOf.Sleep.color * 1.3f)
        };

        public static void DoCell(Rect rect, List<bool> timetable, Map map)
        {
            float x = rect.x;
            float w = rect.width / 24f;
            for (int i = 0; i < 24; i++)
            {
                DoTimeAssignment(new Rect(x, rect.y, w, rect.height), timetable, i, map);
                x += w;
            }
        }

        public static void DoHeader(Rect rect)
        {
            float x = rect.x;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.LowerCenter;
            float w = rect.width / 24f;
            for (int i = 0; i < 24; i++)
            {
                Widgets.Label(new Rect(x, rect.y, w, rect.height + 3f), i.ToString());
                x += w;
            }
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static void DoTimeAssignment(Rect rect, List<bool> table, int hour, Map map)
        {
            if (table == null)
            {
                Log.Error("Table must not be null.");
                return;
            }
            rect = rect.ContractedBy(1f);
            bool mouseDown = Input.GetMouseButton(0);
            bool current = table[hour];
            GUI.DrawTexture(rect, GetTexture(current, GenLocalDate.HourOfDay(map) == hour));
            if (!mouseDown)
            {
                MouseoverSounds.DoRegion(rect);
            }
            if (!Mouse.IsOver(rect)) return;
            if (!mouseDown)
            {
                lastAssignmentValue = !current;
            }
            Widgets.DrawBox(rect, 2);
            if (mouseDown && current != lastAssignmentValue)
            {
                SoundDefOf.Designate_DragStandard_Changed.PlayOneShotOnCamera();
                table[hour] = lastAssignmentValue;
            }
        }

        private static Texture2D GetTexture(bool assignment, bool isActive)
        {
            int idx = (assignment ? 0 : 1) + (isActive ? 2 : 0);
            return colorTextureInt[idx];
        }
    }
}
