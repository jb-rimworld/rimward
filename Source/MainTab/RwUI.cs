// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Copyright (C) 2026 juliusberger00. Written for RimWard 2.0 (a GPL-3.0 fork of Hospital by Adamas and
// Hospital Fix And Tweak by assssssqwww). Licensed under the GNU General Public License version 3 (LICENSE);
// full source: https://github.com/jb-rimworld/rimward
using JBFramework;
using UnityEngine;
using Verse;

namespace RimWard.MainTab
{
    /// <summary>
    /// Drawing helpers of the Hospital tab: the framework's KitStyle colours, helpers and text measuring, drawn with RimWard's accent (teal),
    /// a status chip and an accent button of its own, and the page frame of
    /// Hearth &amp; Harbor's main tab (title, subtitle, accent rule, measured scroll view). Heights come from the text, so translations never clip.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class RwUI
    {
        /// <summary>RimWard's accent (hospital teal, from its framework theme), also outside the settings window.</summary>
        public static Color Accent => KitStyle.AccentOf(HospitalMod.K);
        public static Color AccentSoft { get { Color a = Accent; return new Color(a.r, a.g, a.b, 0.18f); } }
        public static Color Grey => KitStyle.Grey;
        public static Color DimGrey => KitStyle.DimGrey;
        public static Color Good => KitStyle.Good;
        public static Color Bad => KitStyle.Bad;
        public static readonly Color Warn = new Color(0.90f, 0.72f, 0.32f);
        public static Color Line => KitStyle.Line;
        public static readonly Color CardBg = new Color(1f, 1f, 1f, 0.05f);
        public const float ScrollBar = 18f;

        public static readonly Texture2D Mark = ContentFinder<Texture2D>.Get("UI/RimWard/IconSmall", false);

        // ---------------------------------------------------------------- text measuring (the framework's cached helpers)
        public static float TextH(string text, float width, GameFont font = GameFont.Small) => string.IsNullOrEmpty(text) ? 0f : KitUI.TextH(text, Mathf.Max(20f, width), font);

        public static float TextW(string text, GameFont font = GameFont.Small) => string.IsNullOrEmpty(text) ? 0f : KitUI.TextW(text, font);

        /// <summary>Button width that fits its label (one line) between <paramref name="min"/> and <paramref name="max"/>.</summary>
        public static float ButtonW(string label, float min = 110f, float max = 320f) => Mathf.Clamp(TextW(label) + 28f, min, max);

        // ---------------------------------------------------------------- frame

        /// <summary>Page title (Medium), optional subtitle and the accent rule; returns the rest of the rect below it.</summary>
        public static Rect PageHeader(Rect r, string title, string subtitle)
        {
            float y = r.y;
            float th = Mathf.Max(32f, TextH(title, r.width, GameFont.Medium));
            Text.Font = GameFont.Medium; GUI.color = Color.white;
            Widgets.Label(new Rect(r.x, y, r.width, th), title);
            Text.Font = GameFont.Small;
            y += th;
            if (!subtitle.NullOrEmpty())
            {
                float sh = TextH(subtitle, r.width, GameFont.Small);
                KitStyle.LabelColored(new Rect(r.x, y, r.width, sh), subtitle, Grey, TextAnchor.UpperLeft, GameFont.Small);
                y += sh + 2f;
            }
            Widgets.DrawBoxSolid(new Rect(r.x, y + 2f, Mathf.Min(r.width, 180f), 2f), Accent);
            Widgets.DrawBoxSolid(new Rect(r.x + 180f, y + 3f, Mathf.Max(0f, r.width - 180f), 1f), Line);
            y += 10f;
            return new Rect(r.x, y, r.width, Mathf.Max(0f, r.yMax - y));
        }

        /// <summary>Opens a scroll view over <paramref name="outer"/>; returns the usable width. Pair with <see cref="EndScroll"/>.</summary>
        public static float BeginScroll(Rect outer, ref Vector2 scroll, float viewH)
        {
            bool bar = viewH > outer.height;
            Rect view = new Rect(0f, 0f, outer.width - (bar ? ScrollBar : 0f), Mathf.Max(viewH, 1f));
            Widgets.BeginScrollView(outer, ref scroll, view);
            return view.width;
        }

        /// <summary>Closes the scroll view and returns the measured height for the next frame.</summary>
        public static float EndScroll(float usedY)
        {
            Widgets.EndScrollView();
            return usedY + 12f;
        }

        /// <summary>Section header in RimWard's accent with a rule beneath (the framework's, drawn as RimWard). Returns the height used.</summary>
        public static float Section(float x, float y, float w, string text)
        {
            using (KitStyle.Using(HospitalMod.K)) return KitStyle.Section(x, y, w, text);
        }

        /// <summary>A coloured status chip (rounded look via a soft background). Text is truncated to the width; full text as tooltip.</summary>
        public static void Chip(Rect r, string text, Color c, string tip = null)
        {
            Widgets.DrawBoxSolid(r, new Color(c.r, c.g, c.b, 0.25f));
            string shown = text ?? "";
            GameFont f = TextW(shown) > r.width - 8f ? GameFont.Tiny : GameFont.Small;
            Text.Font = f;
            shown = shown.Truncate(r.width - 6f);
            Text.Font = GameFont.Small;
            KitStyle.LabelColored(r, shown, Color.white, TextAnchor.MiddleCenter, f);
            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(r, tip);
            else if (shown != text) TooltipHandler.TipRegion(r, text);
        }

        /// <summary>A button that stands out (accent background). Returns true when clicked.</summary>
        public static bool AccentButton(Rect r, string label)
        {
            Widgets.DrawBoxSolid(r, AccentSoft);
            bool clicked = Widgets.ButtonText(r, label);
            GUI.color = Accent; Widgets.DrawBox(r); GUI.color = Color.white;
            return clicked;
        }

        /// <summary>A tile with a small label and a big value (H&amp;H overview tiles).</summary>
        public static void Tile(Rect t, string label, string value, Color valueColor, string tip)
        {
            Widgets.DrawBoxSolid(t, CardBg); GUI.color = Line; Widgets.DrawBox(t); GUI.color = Color.white;
            float lh = TextH(label, t.width - 24f, GameFont.Small);
            KitStyle.LabelColored(new Rect(t.x + 12f, t.y + 8f, t.width - 24f, lh), label, Grey, TextAnchor.UpperLeft, GameFont.Small);
            KitStyle.LabelColored(new Rect(t.x + 12f, t.y + 8f + lh, t.width - 24f, t.height - lh - 12f), value, valueColor, TextAnchor.MiddleLeft, GameFont.Medium);
            if (!tip.NullOrEmpty()) TooltipHandler.TipRegion(t, tip);
            if (Mouse.IsOver(t)) Widgets.DrawHighlight(t);
        }

        /// <summary>A check mark or a cross for checklist lines.</summary>
        public static void Tick(Rect r, bool ok)
        {
            Texture2D tex = ok ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex;
            GUI.DrawTexture(new Rect(r.x + (r.width - 22f) / 2f, r.y + (r.height - 22f) / 2f, 22f, 22f), tex);
        }

        /// <summary>The colour of a patient status.</summary>
        public static Color StatusColor(PatientStatus st)
        {
            switch (st)
            {
                case PatientStatus.TreatmentImpossible:
                case PatientStatus.Acute:
                    return Bad;
                case PatientStatus.HeldByDanger:
                case PatientStatus.SurgerySuspended:
                case PatientStatus.SurgeryRetry:
                    return Warn;
                case PatientStatus.ReadyToLeave:
                case PatientStatus.Leaving:
                    return Good;
                default:
                    return Accent;
            }
        }
    }
}
