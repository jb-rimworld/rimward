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
    /// <summary>Inbox page: the open patient offers with Accept / Decline, so the letters are optional. Declining is always free.</summary>
    internal static class InboxPage
    {
        private static Vector2 scroll;
        private static float viewH = 600f;
        private static readonly Dictionary<int, KeyValuePair<float, Invoice>> estimates = new Dictionary<int, KeyValuePair<float, Invoice>>();

        private static Invoice Estimate(PatientOffer o)
        {
            float now = Time.realtimeSinceStartup;
            if (estimates.TryGetValue(o.id, out var kv) && now - kv.Key < 2f) return kv.Value;
            Invoice inv = null;
            try { if (o.pawn != null && o.data != null) inv = Invoice.Build(o.pawn, o.data, DischargeReason.Estimate); }
            catch { inv = null; }
            if (estimates.Count > 100) estimates.Clear();
            estimates[o.id] = new KeyValuePair<float, Invoice>(now, inv);
            return inv;
        }

        public static void Draw(Rect r, Map map)
        {
            HospitalMapComponent comp = HospitalMapComponent.For(map);
            Rect body = RwUI.PageHeader(r, "RimWard.Tab.Page.Inbox".Translate(), "RimWard.Tab.Sub.Inbox".Translate());
            float w = RwUI.BeginScroll(body, ref scroll, viewH);
            float y = 0f;
            try
            {
                List<PatientOffer> offers = comp.pendingOffers.Where(o => o != null && o.pawn != null && !o.pawn.Discarded).OrderBy(o => o.expiresTick).ToList();
                string mode = "RimWard.Tab.InboxMode".Translate(Settings.EnumLabel(HospitalMod.Settings.Intake));
                y += KitStyle.Paragraph(0f, y, w, mode, RwUI.Grey, GameFont.Small) + 8f;
                if (offers.Count == 0) y += KitStyle.Paragraph(0f, y, w, "RimWard.Tab.InboxEmpty".Translate(), RwUI.DimGrey) + 6f;
                int free = Mathf.Max(0, comp.FreeMedicalBeds());
                if (offers.Count > 0 && free == 0) y += KitStyle.Paragraph(0f, y, w, "RimWard.Tab.InboxFull".Translate(), RwUI.Warn) + 6f;
                foreach (PatientOffer o in offers) y = OfferRow(y, w, comp, o, free > 0);
            }
            finally
            {
                viewH = RwUI.EndScroll(y);
                PageProbe.Done(y, w);
            }
        }

        private static float OfferRow(float y, float w, HospitalMapComponent comp, PatientOffer o, bool bedFree)
        {
            Invoice est = Estimate(o);
            string accept = "RimWard.Offer.Accept".Translate(), decline = "RimWard.Offer.Decline".Translate();
            float b1 = RwUI.ButtonW(accept, 110f, 220f), b2 = RwUI.ButtonW(decline, 110f, 220f);
            float pad = 10f, buttonsW = b1 + b2 + 8f;
            float textW = w - 2f * pad - buttonsW - 12f;
            bool stack = textW < 300f;
            if (stack) textW = w - 2f * pad;
            string faction = o.faction?.Name ?? "RimWard.NoFaction".Translate();
            string head = o.pawn.LabelShortCap + "  ·  " + faction;
            string kind = o.kind == OfferKind.Ambient ? "RimWard.Tab.OfferAmbient".Translate() : "RimWard.Tab.OfferPod".Translate();
            int left = o.expiresTick - Find.TickManager.TicksGame;
            string facts = "RimWard.Tab.OfferFacts".Translate(kind, o.data?.Diagnosis ?? "?", est != null ? ((float)est.TotalSilver).ToStringMoney() : "-", Mathf.Max(0, left).ToStringTicksToPeriod(false, true));
            string uses = HospitalMapComponent.OfferStockNote(o.data);
            if (!uses.NullOrEmpty()) facts += "\n" + uses;
            float hh = 26f, fh = RwUI.TextH(facts, textW, GameFont.Small);
            float h = pad + hh + 2f + fh + pad + (stack ? 38f : 0f);
            Rect card = new Rect(0f, y, w, h);
            Widgets.DrawBoxSolid(card, RwUI.CardBg);
            Widgets.DrawBoxSolid(new Rect(card.x, card.y, 4f, card.height), RwUI.Warn);
            KitStyle.LabelColored(new Rect(pad + 4f, y + pad, textW, hh), head.Truncate(textW), Color.white, TextAnchor.MiddleLeft);
            Rect fr = new Rect(pad + 4f, y + pad + hh + 2f, textW, fh);
            KitStyle.LabelColored(fr, facts, RwUI.Grey);
            if (est != null) TooltipHandler.TipRegion(fr, est.ToText());
            float bx = stack ? pad + 4f : w - pad - buttonsW, by = stack ? card.yMax - pad - 32f : y + (h - 32f) / 2f;
            Rect ar = PageProbe.Fit(new Rect(bx, by, b1, 32f), w, "inbox.accept");
            if (bedFree ? RwUI.AccentButton(ar, accept) : Widgets.ButtonText(ar, accept, true, true, false))
            { comp.ResolveOffer(o.id, true); OverviewPage.Invalidate(); }
            if (!bedFree) TooltipHandler.TipRegion(ar, "RimWard.Offer.NoBed".Translate());
            Rect dr = PageProbe.Fit(new Rect(bx + b1 + 8f, by, b2, 32f), w, "inbox.decline");
            if (Widgets.ButtonText(dr, decline)) { comp.ResolveOffer(o.id, false); OverviewPage.Invalidate(); }
            TooltipHandler.TipRegion(dr, "RimWard.Tab.DeclineTip".Translate());
            return y + h + 6f;
        }
    }

    /// <summary>Policies page: the hospital policies inline (formerly a dialog behind a column header).</summary>
    internal static class PoliciesPage
    {
        private static Vector2 scroll;
        private static float viewH = 600f;

        public static void Draw(Rect r, Map map)
        {
            Rect body = RwUI.PageHeader(r, "RimWard.Tab.Page.Policies".Translate(), "RimWard.Tab.Sub.Policies".Translate());
            float w = RwUI.BeginScroll(body, ref scroll, viewH);
            float y = 0f;
            try { y = PolicyEditor.Draw(0f, w, map); }
            finally
            {
                viewH = RwUI.EndScroll(y);
                PageProbe.Done(y, w);
            }
        }
    }

    /// <summary>Guide page: six short cards that explain the hospital (Keyed RimWard.Guide.N.Title / .Text).</summary>
    internal static class GuidePage
    {
        private static Vector2 scroll;
        private static float viewH = 600f;
        public const int Cards = 6;

        public static void Draw(Rect r, Map map)
        {
            Rect body = RwUI.PageHeader(r, "RimWard.Tab.Page.Guide".Translate(), "RimWard.Tab.Sub.Guide".Translate());
            float w = RwUI.BeginScroll(body, ref scroll, viewH);
            float y = 0f;
            try
            {
                int cols = w >= 720f ? 2 : 1;
                float gap = 12f, cw = (w - gap * (cols - 1)) / cols, pad = 14f;
                for (int i = 0; i < Cards; i += cols)
                {
                    float rowH = 0f;
                    for (int c = 0; c < cols && i + c < Cards; c++)
                    {
                        int n = i + c + 1;
                        float th = RwUI.TextH(("RimWard.Guide." + n + ".Title").Translate(), cw - 2f * pad, GameFont.Medium);
                        float bh = RwUI.TextH(("RimWard.Guide." + n + ".Text").Translate(), cw - 2f * pad);
                        rowH = Mathf.Max(rowH, pad + th + 6f + bh + pad);
                    }
                    for (int c = 0; c < cols && i + c < Cards; c++)
                    {
                        int n = i + c + 1;
                        Rect card = new Rect(c * (cw + gap), y, cw, rowH);
                        Widgets.DrawBoxSolid(card, RwUI.CardBg);
                        Widgets.DrawBoxSolid(new Rect(card.x, card.y, card.width, 3f), RwUI.Accent);
                        string title = ("RimWard.Guide." + n + ".Title").Translate(), text = ("RimWard.Guide." + n + ".Text").Translate();
                        float th = RwUI.TextH(title, cw - 2f * pad, GameFont.Medium);
                        KitStyle.LabelColored(new Rect(card.x + pad, card.y + pad, cw - 2f * pad, th), title, RwUI.Accent, TextAnchor.UpperLeft, GameFont.Medium);
                        KitStyle.LabelColored(new Rect(card.x + pad, card.y + pad + th + 6f, cw - 2f * pad, rowH - th - 6f - 2f * pad), text, Color.white);
                    }
                    y += rowH + gap;
                }
                string more = "RimWard.Guide.Settings".Translate();
                if (Widgets.ButtonText(PageProbe.Fit(new Rect(0f, y, Mathf.Min(w, RwUI.ButtonW(more, 160f, 400f)), 30f), w, "guide.settings"), more)) HospitalMod.K?.OpenSettings("overview");
                y += 36f;
            }
            finally
            {
                viewH = RwUI.EndScroll(y);
                PageProbe.Done(y, w);
            }
        }
    }
}
