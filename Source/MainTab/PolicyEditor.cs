// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Copyright (C) 2026 juliusberger00. Written for RimWard 2.0 (a GPL-3.0 fork of Hospital by Adamas and
// Hospital Fix And Tweak by assssssqwww). Licensed under the GNU General Public License version 3 (LICENSE);
// full source: https://github.com/jb-rimworld/rimward
using System.Collections.Generic;
using System.Linq;
using JBFramework;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard.MainTab
{
    /// <summary>
    /// The hospital policies editor, drawn inline on the Hospital tab (Policies page) and in the old "Hospital Policies" dialog. Edits the
    /// save's policy (shared by every map, kept across gravship landings), or this map's own copy when "This map has its own policies" is ticked.
    /// </summary>
    internal static class PolicyEditor
    {
        /// <summary>Draws the editor at (0, y) with width <paramref name="w"/> inside a scroll view; returns the height used.</summary>
        public static float Draw(float y0, float w, Map map)
        {
            HospitalMapComponent hospital = HospitalMapComponent.For(map);
            if (hospital == null) return 0f;
            HospitalPolicy pol = hospital.Policy;
            float y = y0;

            // ---- who gets in
            y += RwUI.Section(0f, y, w, "RimWard.Policy.Who".Translate());
            bool open = pol.open;
            y = Check(y, w, "AcceptPatients".Translate(), ref open, "RimWard.Policy.OpenTip".Translate());
            if (open != pol.open) HospitalOverview.SetOpen(map, open);
            pol = hospital.Policy;
            if (HospitalMod.Settings.SurgeryCasesOn)
                y = Check(y, w, "AcceptSurgery".Translate(), ref pol.acceptSurgery, "AcceptSurgeryTooltip".Translate());
            if (HospitalMod.Settings.MassCasualtiesEnabled)
                y = Check(y, w, "MassCasualties".Translate(), ref pol.massCasualties, "MassCasualtiesTooltip".Translate());
            y = Check(y, w, "AcceptDanger".Translate(), ref pol.acceptDanger, "AcceptDangerTooltip".Translate());
            bool own = hospital.HasOwnPolicy, ownBefore = own;
            y = Check(y, w, "RimWard.Policy.OwnMap".Translate(), ref own, "RimWard.Policy.OwnMapTip".Translate());
            if (own != ownBefore) { hospital.SetOwnPolicy(own); pol = hospital.Policy; }
            y += 8f;

            // ---- opening hours
            pol.Repair();
            y += RwUI.Section(0f, y, w, "RimWard.Policy.Hours".Translate());
            y += KitStyle.Paragraph(0f, y, w, "RimWard.Policy.HoursHelp".Translate(), RwUI.Grey, GameFont.Small) + 4f;
            TimetableUtility.DoHeader(new Rect(0f, y, w, 22f));
            y += 26f;
            TimetableUtility.DoCell(new Rect(0f, y, w, 24f), pol.openingHours, map);
            y += 30f;
            string all = "RimWard.Policy.AllDay".Translate(), def = "RimWard.Policy.DefaultHours".Translate();
            float bw1 = RwUI.ButtonW(all), bw2 = RwUI.ButtonW(def);
            if (Widgets.ButtonText(new Rect(0f, y, bw1, 28f), all)) for (int i = 0; i < 24; i++) pol.openingHours[i] = true;
            if (Widgets.ButtonText(new Rect(bw1 + 8f, y, bw2, 28f), def)) pol.openingHours = HospitalPolicy.DayShiftHours();
            y += 38f;

            // ---- care
            y += RwUI.Section(0f, y, w, "RimWard.Policy.Care".Translate());
            float labelW = Mathf.Clamp(w * 0.38f, 160f, 320f);
            string medLabel = "DefaultMedicineSettings".Translate();
            float mh = Mathf.Max(28f, RwUI.TextH(medLabel, labelW));
            KitStyle.LabelColored(new Rect(0f, y, labelW, mh), medLabel, Color.white, TextAnchor.MiddleLeft);
            MedicalCareCategory care = pol.medCare;
            MedicalCareUtility.MedicalCareSetter(new Rect(labelW + 10f, y + (mh - 28f) / 2f, 140f, 28f), ref pol.medCare);
            if (care != pol.medCare) hospital.ApplyPolicyToAll();
            y += mh + 6f;
            string foodLabel = "FoodPolicy".Translate();
            float fh = Mathf.Max(28f, RwUI.TextH(foodLabel, labelW));
            KitStyle.LabelColored(new Rect(0f, y, labelW, fh), foodLabel, Color.white, TextAnchor.MiddleLeft);
            string fp = pol.FoodPolicy?.label ?? "-";
            if (Widgets.ButtonText(new Rect(labelW + 10f, y + (fh - 28f) / 2f, Mathf.Min(w - labelW - 10f, RwUI.ButtonW(fp, 160f, 280f)), 28f), fp))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (FoodPolicy policy in Current.Game.foodRestrictionDatabase.AllFoodRestrictions)
                {
                    FoodPolicy local = policy;
                    options.Add(new FloatMenuOption(local.label, () => hospital.SetPatientFoodPolicy(local)));
                }
                options.Add(new FloatMenuOption("ManageFoodPolicies".Translate(), () => Find.WindowStack.Add(new Dialog_ManageFoodPolicies(null))));
                Find.WindowStack.Add(new FloatMenu(options));
            }
            y += fh + 10f;

            // ---- refused operations
            y += RwUI.Section(0f, y, w, "RefusedOperations".Translate().ToString().TrimEnd(':', ' '));
            List<RecipeDef> refused = pol.refusedOperations.Where(r => r != null).ToList();
            if (refused.Count == 0)
            {
                y += KitStyle.Paragraph(0f, y, w, "RimWard.Policy.NoneRefused".Translate(), RwUI.DimGrey) + 4f;
            }
            foreach (RecipeDef recipe in refused)
            {
                Rect row = new Rect(0f, y, w, 26f);
                if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                KitStyle.LabelColored(new Rect(4f, y, w - 40f, 26f), recipe.LabelCap.ToString().Truncate(w - 40f), Color.white, TextAnchor.MiddleLeft);
                Rect x = new Rect(w - 26f, y + 3f, 20f, 20f);
                if (Widgets.ButtonImage(x, TexButton.CloseXSmall)) hospital.UnRefuseOperation(recipe);
                TooltipHandler.TipRegion(x, "RimWard.Policy.Unrefuse".Translate(recipe.LabelCap));
                y += 26f;
            }
            y += 10f;
            string more = "RimWard.Policy.MoreInSettings".Translate();
            if (Widgets.ButtonText(new Rect(0f, y, Mathf.Min(w, RwUI.ButtonW(more, 160f, 420f)), 30f), more)) HospitalMod.K?.OpenSettings("section:" + Sections.Intake);
            y += 36f;
            return y - y0;
        }

        private static float Check(float y, float w, string label, ref bool value, string tip)
        {
            float cw = Mathf.Min(w, Mathf.Max(420f, RwUI.TextW(label) + 60f));
            float h = Mathf.Max(28f, RwUI.TextH(label, cw - 34f));
            Rect r = new Rect(0f, y, cw, h);
            Widgets.CheckboxLabeled(r, label, ref value);
            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(r, tip);
            return y + h + 2f;
        }
    }
}
