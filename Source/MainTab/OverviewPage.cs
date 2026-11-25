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
    /// Overview page of the Hospital tab: the open/closed status band, beds, patients, offers and income tiles,
    /// the readiness checklist with one-click fixes, the patients with their "why still here", renown and money.
    /// </summary>
    internal static class OverviewPage
    {
        private static Vector2 scroll;
        private static float viewH = 600f;
        private static bool showReady;

        // readiness is recomputed at most once a second (it searches drop spots and counts medicine)
        private static List<ReadinessItem> readiness;
        private static float readinessAt = -10f;
        private static int readinessMap = -1;

        internal static List<ReadinessItem> ReadinessCached(Map map)
        {
            if (readiness == null || readinessMap != map.uniqueID || Time.realtimeSinceStartup - readinessAt > 1f)
            {
                readiness = HospitalOverview.Readiness(map);
                readinessAt = Time.realtimeSinceStartup;
                readinessMap = map.uniqueID;
            }
            return readiness;
        }

        internal static void Invalidate() { readiness = null; PatientsPage.Invalidate(); }

        public static void Draw(Rect r, Map map)
        {
            HospitalMap comp = HospitalMap.For(map);
            Rect body = RwUI.PageHeader(r, "RimWard.Tab.Page.Overview".Translate(), "RimWard.Tab.Sub.Overview".Translate());
            float w = RwUI.BeginScroll(body, ref scroll, viewH);
            float y = 0f;
            try
            {
                y = StatusBand(y, w, map, comp) + 14f;
                y = Tiles(y, w, map, comp) + 14f;
                y = Readiness(y, w, map) + 12f;
                y = Patients(y, w, map, comp) + 12f;
                y = Renown(y, w) + 12f;
                y = Money(y, w, map);
            }
            finally
            {
                viewH = RwUI.EndScroll(y);
                PageProbe.Done(y, w);
            }
        }

        // ------------------------------------------------------------------------------------------------ status band
        internal static void StatusOf(Map map, HospitalMap comp, out string head, out string sub, out Color color, out string button, out System.Action act)
        {
            bool intake = HospitalMod.Settings.IntakeOn;
            HospitalPolicy pol = comp.Policy;
            if (!intake)
            {
                head = "RimWard.Band.IntakeOff".Translate(); sub = "RimWard.Band.IntakeOffSub".Translate(); color = RwUI.Bad;
                button = "RimWard.Fix.IntakeOn".Translate(); act = () => HospitalOverview.SetOpen(map, true);
                return;
            }
            if (!pol.open)
            {
                head = "RimWard.Band.Closed".Translate(); sub = "RimWard.Band.ClosedSub".Translate(); color = RwUI.Bad;
                button = "RimWard.Band.OpenButton".Translate(); act = () => HospitalOverview.SetOpen(map, true);
                return;
            }
            string block = IncidentHelper.ArrivalBlockReason(map);
            head = "RimWard.Band.Open".Translate();
            if (block == null) { sub = "RimWard.Band.OpenNow".Translate(); color = RwUI.Good; }
            else if (!comp.IsOpen() && pol.OpenHourCount > 0) { sub = "RimWard.Band.OpenLater".Translate(block); color = RwUI.Good; }
            else { sub = "RimWard.Band.OpenBlocked".Translate(block); color = RwUI.Warn; }
            button = "RimWard.Band.CloseButton".Translate(); act = () => HospitalOverview.SetOpen(map, false);
        }

        private static float StatusBand(float y, float w, Map map, HospitalMap comp)
        {
            StatusOf(map, comp, out string head, out string sub, out Color color, out string button, out System.Action act);
            float bw = RwUI.ButtonW(button, 170f, 300f);
            bool stack = w - bw - 40f < 300f;
            float textW = stack ? w - 32f : w - bw - 48f;
            float hh = RwUI.TextH(head, textW, GameFont.Medium), sh = RwUI.TextH(sub, textW);
            float h = Mathf.Max(76f, 12f + hh + 4f + sh + 12f + (stack ? 46f : 0f));
            Rect band = new Rect(0f, y, w, h);
            Widgets.DrawBoxSolid(band, new Color(color.r, color.g, color.b, 0.22f));
            Widgets.DrawBoxSolid(new Rect(band.x, band.y, 6f, band.height), color);
            KitStyle.LabelColored(new Rect(20f, y + 12f, textW, hh), head, color, TextAnchor.UpperLeft, GameFont.Medium);
            KitStyle.LabelColored(new Rect(20f, y + 16f + hh, textW, sh), sub, Color.white);
            Rect br = stack ? new Rect(20f, band.yMax - 44f, bw, 36f) : new Rect(band.xMax - bw - 16f, y + (h - 40f) / 2f, bw, 40f);
            PageProbe.Fit(br, w, "status.button");
            if (Widgets.ButtonText(br, button)) { act(); Invalidate(); }
            return y + h;
        }

        // ------------------------------------------------------------------------------------------------ tiles
        private static float Tiles(float y, float w, Map map, HospitalMap comp)
        {
            int total = comp.BedCount(), free = Mathf.Max(0, comp.FreeMedicalBeds()), surgery = comp.SurgeryBedCount();
            int patients = comp.ActivePatientsList().Count(), offers = comp.PendingOfferCount;
            HospitalSave gc = HospitalSave.Get;
            int quadrum = gc?.IncomeThisQuadrum(Find.WorldGrid.LongLatOf(map.Tile).x) ?? 0;
            var tiles = new List<(string label, string value, Color c, string tip, string page)>
            {
                ("RimWard.Tile.Beds".Translate(), "RimWard.Tile.BedsValue".Translate(free, total), free > 0 ? Color.white : RwUI.Bad, "RimWard.Tile.BedsTip".Translate(free, total - free, surgery), null),
                ("RimWard.Tile.Patients".Translate(), patients.ToString(), Color.white, "RimWard.Tile.PatientsTip".Translate(), "patients"),
                ("RimWard.Tile.Offers".Translate(), offers.ToString(), offers > 0 ? RwUI.Warn : Color.white, "RimWard.Tile.OffersTip".Translate(), "inbox"),
                ("RimWard.Tile.Income".Translate(), ((float)quadrum).ToStringMoney(), RwUI.Accent, "RimWard.Tile.IncomeTip".Translate(((float)(gc?.silverEarned ?? 0)).ToStringMoney()), null),
            };
            int perRow = w >= 640f ? 4 : 2;
            float gap = 12f, tw = (w - gap * (perRow - 1)) / perRow;
            float tileH = 0f;
            foreach (var t in tiles) tileH = Mathf.Max(tileH, 14f + RwUI.TextH(t.label, tw - 24f, GameFont.Small) + 46f);
            tileH = Mathf.Max(tileH, 86f);
            for (int i = 0; i < tiles.Count; i++)
            {
                Rect t = new Rect((i % perRow) * (tw + gap), y + (i / perRow) * (tileH + gap), tw, tileH);
                RwUI.Tile(t, tiles[i].label, tiles[i].value, tiles[i].c, tiles[i].tip);
                if (tiles[i].page != null && Widgets.ButtonInvisible(t)) MainTabWindow_Hospital.SelectedPage = tiles[i].page;
            }
            int rows = (tiles.Count + perRow - 1) / perRow;
            return y + rows * tileH + (rows - 1) * gap;
        }

        // ------------------------------------------------------------------------------------------------ readiness
        private static float Readiness(float y, float w, Map map)
        {
            List<ReadinessItem> items = ReadinessCached(map);
            int bad = items.Count(i => !i.ok);
            y += RwUI.Section(0f, y, w, "RimWard.Ready.Head".Translate());
            string summary = bad == 0 ? "RimWard.Ready.AllGood".Translate() : bad == 1 ? "RimWard.Ready.MissingOne".Translate() : "RimWard.Ready.Missing".Translate(bad);
            if (bad == 0)
            {
                // all done: one line, the list on demand
                string toggle = showReady ? "RimWard.Ready.Hide".Translate() : "RimWard.Ready.ShowAll".Translate();
                float tw = RwUI.ButtonW(toggle, 120f, 260f);
                float sh = Mathf.Max(28f, RwUI.TextH(summary, w - tw - 12f));
                KitStyle.LabelColored(new Rect(0f, y, w - tw - 12f, sh), summary, RwUI.Good, TextAnchor.MiddleLeft);
                if (Widgets.ButtonText(PageProbe.Fit(new Rect(w - tw, y + (sh - 28f) / 2f, tw, 28f), w, "ready.toggle"), toggle)) showReady = !showReady;
                y += sh + 6f;
                if (!showReady) return y;
            }
            else y += KitStyle.Paragraph(0f, y, w, summary, RwUI.Warn) + 6f;
            foreach (ReadinessItem it in items) y = ReadinessRow(y, w, map, it);
            return y;
        }

        internal static float ReadinessRow(float y, float w, Map map, ReadinessItem it)
        {
            string show = "RimWard.Ready.ShowMe".Translate();
            float fixW = it.fix != null ? RwUI.ButtonW(it.fixLabel, 120f, 300f) : 0f;
            float showW = it.target != null && it.target.IsValid ? RwUI.ButtonW(show, 90f, 160f) : 0f;
            float buttonsW = fixW + (fixW > 0f && showW > 0f ? 8f : 0f) + showW;
            bool stack = w - 34f - buttonsW < 260f;
            float textW = stack ? w - 38f : w - 38f - buttonsW - 12f;
            string detail = it.detail;
            if (!it.ok && !it.pointer.NullOrEmpty()) detail = detail.NullOrEmpty() ? it.pointer : detail + ". " + it.pointer;
            float lh = Mathf.Max(24f, RwUI.TextH(it.label, textW));
            float dh = RwUI.TextH(detail, textW, GameFont.Small);
            float h = Mathf.Max(32f, 4f + lh + dh + 4f + (stack && buttonsW > 0f ? 34f : 0f));
            Rect row = new Rect(0f, y, w, h);
            if (!it.ok) Widgets.DrawBoxSolid(row, new Color(RwUI.Bad.r, RwUI.Bad.g, RwUI.Bad.b, 0.08f));
            if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
            RwUI.Tick(new Rect(2f, y + 2f, 28f, 28f), it.ok);
            KitStyle.LabelColored(new Rect(36f, y + 4f, textW, lh), it.label, it.ok ? Color.white : new Color(1f, 0.85f, 0.82f));
            if (dh > 0f) KitStyle.LabelColored(new Rect(36f, y + 4f + lh, textW, dh), detail, it.ok ? RwUI.Grey : new Color(0.95f, 0.75f, 0.7f), TextAnchor.UpperLeft, GameFont.Small);
            float bx = stack ? 36f : w - buttonsW, by = stack ? y + h - 32f : y + (h - 28f) / 2f;
            if (it.fix != null)
            {
                Rect fr = PageProbe.Fit(new Rect(bx, by, fixW, 28f), w, "ready.fix." + it.key);
                if (RwUI.AccentButton(fr, it.fixLabel))
                {
                    try { it.fix(); }
                    catch (System.Exception e) { HospitalMod.K?.Fail(KitMod.Core, "UI.ReadinessFix." + it.key, e); }
                    Invalidate();
                }
                bx += fixW + 8f;
            }
            if (showW > 0f)
            {
                Rect sr = PageProbe.Fit(new Rect(bx, by, showW, 28f), w, "ready.show." + it.key);
                if (Widgets.ButtonText(sr, show)) { Find.MainTabsRoot?.EscapeCurrentTab(false); CameraJumper.TryJumpAndSelect(it.target.TryGetPrimaryTarget()); }
            }
            return y + h + 2f;
        }

        // ------------------------------------------------------------------------------------------------ patients
        private static float Patients(float y, float w, Map map, HospitalMap comp)
        {
            y += RwUI.Section(0f, y, w, "RimWard.Tab.PatientsHead".Translate());
            List<Pawn> list = PatientsPage.Sorted(comp);
            if (list.Count == 0)
            {
                y += KitStyle.Paragraph(0f, y, w, "RimWard.Tab.NoPatients".Translate(), RwUI.DimGrey) + 4f;
                return y;
            }
            const int Max = 6;
            foreach (Pawn p in list.Take(Max)) y = PatientsPage.CompactRow(y, w, comp, p);
            if (list.Count > Max)
            {
                string more = "RimWard.Tab.AllPatients".Translate(list.Count);
                if (Widgets.ButtonText(new Rect(0f, y + 4f, RwUI.ButtonW(more, 160f, 360f), 28f), more)) MainTabWindow_Hospital.SelectedPage = "patients";
                y += 36f;
            }
            return y;
        }

        // ------------------------------------------------------------------------------------------------ renown
        private static float Renown(float y, float w)
        {
            HospitalSave gc = HospitalSave.Get;
            y += RwUI.Section(0f, y, w, "RimWard.Tab.RenownHead".Translate());
            if (gc == null) return y;
            if (!HospitalMod.Settings.RenownEnabled)
            {
                return y + KitStyle.Paragraph(0f, y, w, "RimWard.Tab.RenownOff".Translate(), RwUI.DimGrey) + 4f;
            }
            float labelW = Mathf.Clamp(w * 0.3f, 160f, 280f);
            string tier = gc.TierLabel + " (" + gc.renown.ToString("0") + ")";
            KitStyle.LabelColored(new Rect(0f, y, labelW, 28f), tier.Truncate(labelW), RwUI.Accent, TextAnchor.MiddleLeft);
            TooltipHandler.TipRegion(new Rect(0f, y, labelW, 28f), "RimWard.Tab.RenownTip".Translate());
            KitStyle.Bar(new Rect(labelW + 10f, y + 8f, Mathf.Max(60f, w - labelW - 10f), 14f), gc.TierProgress, RwUI.Accent);
            y += 32f;
            string what = "RimWard.Tab.RenownWhat".Translate(gc.RenownPaymentMultiplier.ToStringPercent());
            string next = gc.NextTierAt < 0f ? (string)"RimWard.Tab.RenownTop".Translate()
                : (string)"RimWard.Tab.RenownNext".Translate(("RimWard.Renown.Tier" + (gc.Tier + 1)).Translate(), gc.NextTierAt.ToString("0"), (gc.RenownPaymentMultiplier + 0.05f).ToStringPercent());
            y += KitStyle.Paragraph(0f, y, w, what + " " + next, Color.white) + 2f;
            y += KitStyle.Paragraph(0f, y, w, "RimWard.Tab.RenownHow".Translate(), RwUI.Grey, GameFont.Small) + 2f;
            return y;
        }

        // ------------------------------------------------------------------------------------------------ money
        private static float Money(float y, float w, Map map)
        {
            HospitalSave gc = HospitalSave.Get;
            y += RwUI.Section(0f, y, w, "RimWard.Tab.MoneyHead".Translate());
            if (gc == null) return y;
            int quadrum = gc.IncomeThisQuadrum(Find.WorldGrid.LongLatOf(map.Tile).x);
            var lines = new List<(string, string)>
            {
                ("RimWard.Tab.Money.Quadrum".Translate(GenDate.Quadrum(Find.TickManager.TicksAbs, Find.WorldGrid.LongLatOf(map.Tile).x).Label()), ((float)quadrum).ToStringMoney()),
                ("RimWard.Tab.Money.Total".Translate(), ((float)gc.silverEarned).ToStringMoney()),
                ("RimWard.Tab.Money.Treated".Translate(), gc.patientsTreated.ToString()),
                ("RimWard.Tab.Money.Died".Translate(), "RimWard.Tab.Money.DiedValue".Translate(gc.patientsDied, gc.patientsDiedAtFault).ToString()),
            };
            if (!HospitalMod.Settings.ChargePatients) lines.Insert(0, ("RimWard.Tab.Money.Charity".Translate(), ""));
            float lw = Mathf.Clamp(w * 0.5f, 200f, 420f);
            foreach (var (label, value) in lines)
            {
                float lh = Mathf.Max(24f, RwUI.TextH(label, lw));
                KitStyle.LabelColored(new Rect(4f, y, lw, lh), label, Color.white, TextAnchor.MiddleLeft);
                KitStyle.LabelColored(new Rect(lw + 12f, y, w - lw - 12f, lh), value, RwUI.Grey, TextAnchor.MiddleLeft);
                y += lh;
            }
            y += 8f;
            List<Invoice> recent = gc.recentInvoices ?? new List<Invoice>();
            KitStyle.LabelColored(new Rect(4f, y, w, 20f), "RimWard.Tab.LastDischarges".Translate(), RwUI.DimGrey, TextAnchor.UpperLeft, GameFont.Small);
            y += 22f;
            if (recent.Count == 0) return y + KitStyle.Paragraph(4f, y, w - 4f, "RimWard.Tab.NoDischarges".Translate(), RwUI.DimGrey, GameFont.Small) + 4f;
            float nameW = w * 0.34f, reasonW = w * 0.36f, sumW = w - nameW - reasonW - 8f;
            foreach (Invoice inv in recent)
            {
                Rect row = new Rect(0f, y, w, 26f);
                if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                string who = inv.patientName ?? "?";
                if (!inv.factionName.NullOrEmpty()) who += ", " + inv.factionName;
                KitStyle.LabelColored(new Rect(4f, y, nameW - 8f, 26f), who.Truncate(nameW - 8f), Color.white, TextAnchor.MiddleLeft);
                KitStyle.LabelColored(new Rect(nameW, y, reasonW - 8f, 26f), ("RimWard.Tab.Reason." + inv.reason).Translate().ToString().Truncate(reasonW - 8f), RwUI.Grey, TextAnchor.MiddleLeft, GameFont.Small);
                KitStyle.LabelColored(new Rect(nameW + reasonW, y, sumW, 26f), ((float)inv.TotalSilver).ToStringMoney(), RwUI.Accent, TextAnchor.MiddleRight);
                TooltipHandler.TipRegion(row, inv.ToText());
                Invoice local = inv;
                if (Widgets.ButtonInvisible(row)) Find.WindowStack.Add(new Dialog_MessageBox(local.ToText(), title: "RimWard.Invoice.Title".Translate(local.patientName ?? "?")));
                y += 26f;
            }
            return y;
        }
    }
}
