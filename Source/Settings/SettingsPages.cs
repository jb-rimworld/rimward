// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Copyright (C) 2026 juliusberger00. Written for RimWard 2.0 (a GPL-3.0 fork of Hospital by Adamas and
// Hospital Fix And Tweak by assssssqwww). Licensed under the GNU General Public License version 3 (LICENSE);
// full source: https://github.com/jb-rimworld/rimward
using System;
using System.Collections.Generic;
using System.Linq;
using JBFramework;
using RimWard.MainTab;
using RimWard.Utilities;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard
{
    /// <summary>RimWard's custom pages in the JB Framework settings window: the colony's hospital policies, pricing explained, blacklists.</summary>
    internal static class SettingsPages
    {
        private static Vector2 scrollPricing, scrollBlack;
        private static float heightPricing = 600f, heightBlack = 600f;

        // ------------------------------------------------------------------------------------------------ policies (per save)
        internal static void DrawPolicies(Rect area)
        {
            float y = area.y, w = area.width;
            y += KitStyle.Paragraph(area.x, y, w, "RimWard.Page.Policies.Intro".Translate(), KitStyle.Grey) + 10f;
            HospitalSave gc = Current.ProgramState == ProgramState.Playing ? HospitalSave.Get : null;
            Map map = Current.ProgramState == ProgramState.Playing ? Find.CurrentMap : null;
            HospitalMap comp = HospitalMap.For(map);
            if (gc == null || comp == null)
            {
                KitStyle.Paragraph(area.x, y, w, "RimWard.Page.Policies.NoGame".Translate(), KitStyle.DimGrey);
                return;
            }
            HospitalPolicy pol = comp.Policy;
            var lines = new List<string>
            {
                "RimWard.Page.Policies.IsOpen".Translate(pol.open ? "RimWard.Yes".Translate() : "RimWard.No".Translate()),
                "RimWard.Page.Policies.Hours".Translate(pol.OpenHourCount, HoursText(pol)),
                "RimWard.Page.Policies.Surgery".Translate(pol.acceptSurgery ? "RimWard.Yes".Translate() : "RimWard.No".Translate()),
                "RimWard.Page.Policies.Mce".Translate(pol.massCasualties ? "RimWard.Yes".Translate() : "RimWard.No".Translate()),
                "RimWard.Page.Policies.Refused".Translate(pol.refusedOperations.Count),
            };
            if (comp.HasOwnPolicy) lines.Add("RimWard.Page.Policies.OwnMap".Translate());
            string block = IncidentHelper.ArrivalBlockReason(map);
            lines.Add(block == null ? "RimWard.Page.Policies.ArrivalsPossible".Translate().ToString() : "RimWard.Page.Policies.ArrivalsBlocked".Translate(block).ToString());
            foreach (string l in lines) y += KitStyle.Paragraph(area.x, y, w, l, Color.white) + 2f;
            y += 10f;
            if (Widgets.ButtonText(new Rect(area.x, y, Mathf.Min(320f, w), 32f), "RimWard.Page.Policies.Button".Translate()))
                Find.WindowStack.Add(new Dialog_MapSettings(map));
        }

        private static string HoursText(HospitalPolicy pol)
        {
            var hours = new List<string>();
            for (int h = 0; h < 24; h++) if (pol.openingHours[h]) hours.Add(h + ":00");
            return hours.Count == 0 ? "-" : string.Join(", ", hours);
        }

        // ------------------------------------------------------------------------------------------------ pricing
        internal static void DrawPricing(Rect area)
        {
            Settings s = HospitalMod.Settings;
            if (s == null) return;
            Rect view = new Rect(0f, 0f, area.width - 16f, heightPricing);
            Widgets.BeginScrollView(area, ref scrollPricing, view);
            float y = 0f, w = view.width;
            y += KitStyle.Paragraph(0f, y, w, "RimWard.Page.Pricing.Intro".Translate(), KitStyle.Grey) + 12f;

            y += KitStyle.Section(0f, y, w, "RimWard.Page.Pricing.Lines".Translate());
            y += KitStyle.Paragraph(0f, y, w, "RimWard.Page.Pricing.LinesText".Translate(), Color.white) + 12f;

            y += KitStyle.Section(0f, y, w, "RimWard.Page.Pricing.Factors".Translate());
            float renown = HospitalSave.Get != null && s.RenownEnabled ? HospitalSave.Get.RenownPaymentMultiplier : 1f;
            var rows = new List<(string, string)>
            {
                ("RimWard.Page.Pricing.Scale".Translate(), s.ChargePatients ? Settings.EnumLabel(s.Payment) + " = " + s.PaymentMultiplier.ToStringPercent() : "RimWard.Page.Pricing.Charity".Translate().ToString()),
                ("RimWard.Page.Pricing.Single".Translate(), s.SingleRoomMultiplier.ToStringPercent()),
                ("RimWard.Page.Pricing.TwoBed".Translate(), s.TwoBedMultiplier.ToStringPercent()),
                ("RimWard.Page.Pricing.Ward".Translate(), s.BarracksMultiplier.ToStringPercent()),
                ("RimWard.Page.Pricing.Hallway".Translate(), (s.BarracksMultiplier * 0.85f).ToStringPercent()),
                ("RimWard.Page.Pricing.Chief".Translate(s.ChiefPhysicianMinSkill), s.ChiefPhysicianEnabled ? s.ChiefPhysicianMultiplier.ToStringPercent() : "RimWard.Set.Off".Translate().ToString()),
                ("RimWard.Page.Pricing.Renown".Translate(), s.RenownEnabled ? "RimWard.Page.Pricing.RenownValue".Translate(renown.ToStringPercent()).ToString() : "RimWard.Set.Off".Translate().ToString()),
                ("RimWard.Page.Pricing.Noble".Translate(), s.RoyalRoomDemand ? "+40% / -25%" : "RimWard.Set.Off".Translate().ToString()),
                ("RimWard.Page.Pricing.Cap".Translate(), s.BillCap.ToStringMoney()),
            };
            foreach (var (label, value) in rows)
            {
                Rect r = new Rect(0f, y, w, 24f);
                Widgets.DrawHighlightIfMouseover(r);
                Widgets.Label(new Rect(8f, y + 2f, w * 0.62f, 22f), label);
                Text.Anchor = TextAnchor.UpperRight;
                Widgets.Label(new Rect(w * 0.62f, y + 2f, w * 0.38f - 8f, 22f), value);
                Text.Anchor = TextAnchor.UpperLeft;
                y += 24f;
            }
            y += 12f;

            y += KitStyle.Section(0f, y, w, "RimWard.Page.Pricing.Example".Translate());
            float baseBill = 300f;
            float total = baseBill * (s.ChargePatients ? s.PaymentMultiplier : 0f) * s.SingleRoomMultiplier * (s.ChiefPhysicianEnabled ? s.ChiefPhysicianMultiplier : 1f) * renown;
            total = Mathf.Min(total, s.BillCap);
            y += KitStyle.Paragraph(0f, y, w, "RimWard.Page.Pricing.ExampleText".Translate(baseBill.ToStringMoney(), ((float)Mathf.FloorToInt(total)).ToStringMoney()), Color.white) + 12f;

            y += KitStyle.Section(0f, y, w, "RimWard.Page.Pricing.Early".Translate());
            y += KitStyle.Paragraph(0f, y, w, s.BillingExtrasOn
                ? "RimWard.Page.Pricing.EarlyText".Translate(s.EarlyDischargePayFraction.ToStringPercent()).ToString()
                : "RimWard.Page.Pricing.EarlyOff".Translate().ToString(), Color.white) + 12f;
            heightPricing = y + 20f;
            Widgets.EndScrollView();
        }

        // ------------------------------------------------------------------------------------------------ blacklists
        internal static void DrawBlacklists(Rect area)
        {
            Settings s = HospitalMod.Settings;
            if (s == null) return;
            Rect view = new Rect(0f, 0f, area.width - 16f, heightBlack);
            Widgets.BeginScrollView(area, ref scrollBlack, view);
            float y = 0f, w = view.width;
            y += KitStyle.Paragraph(0f, y, w, "RimWard.Page.Black.Intro".Translate(), KitStyle.Grey) + 12f;
            y = Block(y, w, "RimWard.Page.Black.Diseases".Translate(), s.blockedDiseases, DefDatabase<HediffDef>.GetNamedSilentFail, DiseaseCandidates);
            y = Block(y, w, "RimWard.Page.Black.Surgeries".Translate(), s.blockedSurgeries, DefDatabase<RecipeDef>.GetNamedSilentFail, SurgeryCandidates);
            y = Block(y, w, "RimWard.Page.Black.Factions".Translate(), s.blockedFactions, DefDatabase<FactionDef>.GetNamedSilentFail, FactionCandidates);
            heightBlack = y + 20f;
            Widgets.EndScrollView();
        }

        private static float Block<T>(float y, float w, string title, List<string> list, Func<string, T> lookup, Func<IEnumerable<T>> candidates) where T : Def
        {
            y += KitStyle.Section(0f, y, w, title + " (" + list.Count + ")");
            if (list.Count == 0) { KitStyle.LabelColored(new Rect(8f, y, w, 24f), "RimWard.Page.Black.None".Translate(), KitStyle.DimGrey); y += 26f; }
            foreach (string name in list.ToList())
            {
                T def = lookup(name);
                Rect r = new Rect(0f, y, w, 24f);
                Widgets.DrawHighlightIfMouseover(r);
                string label = def != null ? def.LabelCap.ToString() + "  (" + name + ")" : name + "  " + "RimWard.Page.Black.NotLoaded".Translate();
                Widgets.Label(new Rect(8f, y + 2f, w - 40f, 22f), label);
                if (Widgets.ButtonImage(new Rect(w - 26f, y + 2f, 20f, 20f), TexButton.CloseXSmall)) { list.Remove(name); HospitalMod.K?.WriteSettings(); }
                y += 24f;
            }
            if (Widgets.ButtonText(new Rect(0f, y + 4f, 200f, 28f), "RimWard.Page.Black.Add".Translate()))
                Find.WindowStack.Add(new Dialog_PickDefs<T>(title, candidates().Where(d => !list.Contains(d.defName)).ToList(), d => { list.Add(d.defName); HospitalMod.K?.WriteSettings(); }));
            return y + 44f;
        }

        internal static IEnumerable<HediffDef> DiseaseCandidates()
            => DefDatabase<HediffDef>.AllDefsListForReading.Where(d => d.tendable && d.makesSickThought).OrderBy(d => d.label);

        internal static IEnumerable<RecipeDef> SurgeryCandidates()
            => (ThingDefOf.Human?.AllRecipes ?? new List<RecipeDef>()).Where(r => r != null && r.IsSurgery && SurgeryUtility.IsCandidate(r, null)).Distinct().OrderBy(r => r.label);

        internal static IEnumerable<FactionDef> FactionCandidates()
            => DefDatabase<FactionDef>.AllDefsListForReading.Where(f => f.humanlikeFaction && !f.isPlayer && !f.hidden).OrderBy(f => f.label);
    }

    /// <summary>A small search-and-pick window for the blacklist pages.</summary>
    internal sealed class Dialog_PickDefs<T> : Window where T : Def
    {
        private readonly string title;
        private readonly List<T> all;
        private readonly Action<T> pick;
        private string filter = "";
        private Vector2 scroll;

        public Dialog_PickDefs(string title, List<T> all, Action<T> pick)
        {
            this.title = title; this.all = all; this.pick = pick;
            this.doCloseX = true; this.doCloseButton = true; this.absorbInputAroundWindow = true; this.closeOnClickedOutside = true;
        }

        public override Vector2 InitialSize => KitUI.Size(460f, 560f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 34f), this.title);
            Text.Font = GameFont.Small;
            this.filter = Widgets.TextField(new Rect(0f, 40f, inRect.width, 28f), this.filter ?? "");
            List<T> shown = this.all.Where(d => this.filter.NullOrEmpty() || (d.label ?? "").IndexOf(this.filter, StringComparison.OrdinalIgnoreCase) >= 0
                || d.defName.IndexOf(this.filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            Rect outRect = new Rect(0f, 76f, inRect.width, inRect.height - 76f - 50f);
            Rect view = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(outRect.height, shown.Count * 26f));
            Widgets.BeginScrollView(outRect, ref this.scroll, view);
            float y = 0f;
            foreach (T d in shown)
            {
                Rect r = new Rect(0f, y, view.width, 26f);
                if (Widgets.ButtonText(r, d.LabelCap.ToString(), false))
                {
                    this.pick(d);
                    this.all.Remove(d);
                    break;
                }
                TooltipHandler.TipRegion(r, d.defName);
                y += 26f;
            }
            Widgets.EndScrollView();
        }
    }
}
