// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Copyright (C) 2026 juliusberger00. Written for RimWard 2.0 (a GPL-3.0 fork of Hospital by Adamas and
// Hospital Fix And Tweak by assssssqwww). Licensed under the GNU General Public License version 3 (LICENSE);
// full source: https://github.com/jb-rimworld/rimward
using System.Collections.Generic;
using System.Linq;
using RimWard.Utilities;
using JBFramework;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard.MainTab
{
    /// <summary>
    /// Patients page: one row per patient with status, the caretaker's "why still here", diagnosis and treatment, time here, expected bill
    /// (tooltip: the invoice) and the Discharge / Refuse operation buttons. Rows wrap instead of clipping, so it fits 1280x720 and German.
    /// </summary>
    internal static class PatientsPage
    {
        private static Vector2 scroll;
        private static float viewH = 600f;

        private sealed class Info { public float At; public PatientStatus Status; public string Why; public Invoice Bill; }
        private static readonly Dictionary<Pawn, Info> cache = new Dictionary<Pawn, Info>();

        internal static void Invalidate() => cache.Clear();

        private static Info Get(HospitalMapComponent comp, Pawn p)
        {
            float now = Time.realtimeSinceStartup;
            if (cache.TryGetValue(p, out Info i) && now - i.At < 0.5f) return i;
            i = new Info { At = now };
            i.Status = comp.StatusOf(p, out _);
            i.Why = comp.WhyStillHere(p);
            i.Bill = comp.GetPatientData(p, out PatientData d) ? Invoice.Build(p, d, DischargeReason.Estimate) : null;
            if (cache.Count > 300) cache.Clear();
            cache[p] = i;
            return i;
        }

        /// <summary>Active and leaving patients, the ones that need the player first (impossible, acute) on top.</summary>
        internal static List<Pawn> Sorted(HospitalMapComponent comp)
        {
            return comp.Patients.Where(kv => kv.Key != null && kv.Value != null && !kv.Key.Discarded).Select(kv => kv.Key)
                .OrderBy(p => Rank(Get(comp, p).Status)).ThenBy(p => p.LabelShort).ToList();
        }

        private static int Rank(PatientStatus s)
        {
            switch (s)
            {
                case PatientStatus.TreatmentImpossible: return 0;
                case PatientStatus.Acute: return 1;
                case PatientStatus.HeldByDanger: return 2;
                case PatientStatus.SurgerySuspended: return 3;
                case PatientStatus.WaitingForSurgery: case PatientStatus.SurgeryRetry: return 4;
                case PatientStatus.NeedsTending: return 5;
                case PatientStatus.Resting: return 6;
                case PatientStatus.Arriving: return 7;
                case PatientStatus.ReadyToLeave: return 8;
                default: return 9;
            }
        }

        internal static string StatusLabel(PatientStatus s) => ("RimWard.Status." + s).Translate();

        private static void Jump(Pawn p)
        {
            if (p == null) return;
            Find.MainTabsRoot?.EscapeCurrentTab(false);
            if (p.Spawned) CameraJumper.TryJumpAndSelect(p);
            else if (p.SpawnedOrAnyParentSpawned) CameraJumper.TryJump(p.PositionHeld, p.MapHeld);
        }

        /// <summary>One line on the overview: name, status chip, why. Returns the next y.</summary>
        internal static float CompactRow(float y, float w, HospitalMapComponent comp, Pawn p)
        {
            Info i = Get(comp, p);
            float nameW = Mathf.Clamp(w * 0.24f, 120f, 220f), chipW = Mathf.Clamp(w * 0.2f, 110f, 200f);
            float whyW = w - nameW - chipW - 24f;
            float wh = Mathf.Max(26f, RwUI.TextH(i.Why, whyW, GameFont.Small) + 4f);
            Rect row = new Rect(0f, y, w, wh);
            if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
            string name = p.LabelShortCap;
            KitStyle.LabelColored(new Rect(4f, y, nameW - 8f, 26f), name.Truncate(nameW - 8f), Color.white, TextAnchor.MiddleLeft);
            RwUI.Chip(PageProbe.Fit(new Rect(nameW, y + 3f, chipW, 20f), w, "overview.chip"), StatusLabel(i.Status), RwUI.StatusColor(i.Status));
            KitStyle.LabelColored(new Rect(nameW + chipW + 12f, y + 2f, whyW, wh - 2f), i.Why, RwUI.Grey, TextAnchor.UpperLeft, GameFont.Small);
            TooltipHandler.TipRegion(row, "RimWard.Tab.RowTip".Translate(p.LabelShortCap));
            if (Widgets.ButtonInvisible(row)) Jump(p);
            return y + wh + 2f;
        }

        public static void Draw(Rect r, Map map)
        {
            HospitalMapComponent comp = HospitalMapComponent.For(map);
            Rect body = RwUI.PageHeader(r, "RimWard.Tab.Page.Patients".Translate(), "RimWard.Tab.Sub.Patients".Translate(comp.ActivePatientsList().Count(), comp.BedCount()));
            float w = RwUI.BeginScroll(body, ref scroll, viewH);
            float y = 0f;
            try
            {
                List<Pawn> list = Sorted(comp);
                if (list.Count == 0)
                {
                    y += KitStyle.Paragraph(0f, y, w, "RimWard.Tab.NoPatients".Translate(), RwUI.DimGrey) + 6f;
                    string block = IncidentHelper.ArrivalBlockReason(map);
                    if (block != null) y += KitStyle.Paragraph(0f, y, w, "RimWard.Tab.WhyNoPatients".Translate(block), RwUI.Grey) + 6f;
                }
                foreach (Pawn p in list) y = FullRow(y, w, comp, p);
            }
            finally
            {
                viewH = RwUI.EndScroll(y);
                PageProbe.Done(y, w);
            }
        }

        private static float FullRow(float y, float w, HospitalMapComponent comp, Pawn p)
        {
            Info i = Get(comp, p);
            comp.GetPatientData(p, out PatientData d);
            string dismiss = "Dismiss".Translate(), refuse = "Disallow".Translate();
            bool canRefuse = d != null && d.CureRecipe != null && !d.Dismissed;
            bool canDismiss = d != null && !d.Dismissed;
            float bw1 = RwUI.ButtonW(dismiss, 100f, 200f), bw2 = canRefuse ? RwUI.ButtonW(refuse, 120f, 240f) : 0f;
            float buttonsW = bw1 + (bw2 > 0f ? bw2 + 8f : 0f);
            float pad = 10f;
            float textW = w - 2f * pad - buttonsW - 12f;
            bool stackButtons = textW < 320f;
            if (stackButtons) textW = w - 2f * pad;

            string name = p.LabelShortCap;
            string faction = p.Faction?.Name ?? "RimWard.NoFaction".Translate();
            string time = p.GetPatientTimeInHospital(out int ticks, comp) ? ticks.ToStringTicksToPeriod(false, true) : "-";
            string bill = i.Bill != null ? ((float)i.Bill.TotalSilver).ToStringMoney() : "-";
            string facts = "RimWard.Tab.RowFacts".Translate(d?.Diagnosis ?? "?", d?.Cure ?? "?", time, bill);
            float chipW = Mathf.Clamp(RwUI.TextW(StatusLabel(i.Status)) + 24f, 120f, 240f);
            float nameW = Mathf.Max(60f, textW - chipW - 10f);
            float nh = 26f;
            float wh = RwUI.TextH(i.Why, textW);
            float fh = RwUI.TextH(facts, textW, GameFont.Small);
            float h = pad + nh + 2f + wh + 2f + fh + pad + (stackButtons ? 36f : 0f);
            Rect card = new Rect(0f, y, w, h);
            Widgets.DrawBoxSolid(card, RwUI.CardBg);
            Color sc = RwUI.StatusColor(i.Status);
            Widgets.DrawBoxSolid(new Rect(card.x, card.y, 4f, card.height), sc);
            float cy = y + pad;
            Rect nameR = new Rect(pad + 4f, cy, nameW, nh);
            string nameLine = name + "  ·  " + faction;
            KitStyle.LabelColored(nameR, nameLine.Truncate(nameW), Color.white, TextAnchor.MiddleLeft);
            if (Mouse.IsOver(nameR)) { Widgets.DrawHighlight(nameR); TooltipHandler.TipRegion(nameR, "RimWard.Tab.RowTip".Translate(name)); }
            if (Widgets.ButtonInvisible(nameR)) Jump(p);
            RwUI.Chip(PageProbe.Fit(new Rect(pad + 4f + textW - chipW, cy + 3f, chipW, 20f), w, "patients.chip"), StatusLabel(i.Status), sc);
            cy += nh + 2f;
            KitStyle.LabelColored(new Rect(pad + 4f, cy, textW, wh), i.Why, Color.white);
            cy += wh + 2f;
            Rect factsR = new Rect(pad + 4f, cy, textW, fh);
            KitStyle.LabelColored(factsR, facts, RwUI.Grey, TextAnchor.UpperLeft, GameFont.Small);
            if (i.Bill != null) TooltipHandler.TipRegion(factsR, i.Bill.ToText());

            float bx = stackButtons ? pad + 4f : w - pad - buttonsW;
            float by = stackButtons ? card.yMax - pad - 30f : y + (h - 30f) / 2f;
            if (canDismiss)
            {
                Rect dr = PageProbe.Fit(new Rect(bx, by, bw1, 30f), w, "patients.dismiss");
                if (Widgets.ButtonText(dr, dismiss)) { comp.DismissPatient(p); Invalidate(); OverviewPage.Invalidate(); }
                TooltipHandler.TipRegion(dr, "RimWard.Tab.DismissTip".Translate());
            }
            if (canRefuse)
            {
                Rect rr = PageProbe.Fit(new Rect(bx + bw1 + 8f, by, bw2, 30f), w, "patients.refuse");
                if (Widgets.ButtonText(rr, refuse)) { comp.RefuseOperation(p, d.CureRecipe); Invalidate(); OverviewPage.Invalidate(); }
                TooltipHandler.TipRegion(rr, "RimWard.Tab.RefuseTip".Translate(d.CureRecipe.LabelCap));
            }
            return y + h + 6f;
        }
    }
}
