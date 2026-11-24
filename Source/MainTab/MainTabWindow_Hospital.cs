using System;
using System.Collections.Generic;
using System.Linq;
using JBFramework;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard.MainTab
{
    /// <summary>
    /// The Hospital main tab (after Hearth &amp; Harbor's main tab): a header with the open/closed switch, a slim page list on the left
    /// (Overview, Patients, Inbox, Policies, Guide) and the selected page on the right. Sized by the framework (screen and "Window size").
    /// </summary>
    public class MainTabWindow_Hospital : MainTabWindow
    {
        public static string SelectedPage = "overview";

        /// <summary>Where the last frame drew (the UI suites check that every part lies inside the window).</summary>
        internal struct TabLayout { public Rect In, Header, List, Content, OpenChip, SettingsButton; public string Page; public int Frame; public float PageH, PageW; public bool Overflow; public string OverflowWhat; }
        internal static TabLayout Last;
        internal static int Frames;

        public override Vector2 RequestedTabSize => KitUI.Size(1180f, 720f, 1000f, 600f, 0.92f, 0.88f);

        public MainTabWindow_Hospital()
        {
            this.resizeable = false;
        }

        private sealed class Page
        {
            public string Id, Key;
            public Func<int> Count;
            public Action<Rect, Map> Draw;
        }

        private static readonly List<Page> pages = new List<Page>
        {
            new Page { Id = "overview", Key = "RimWard.Tab.Page.Overview", Draw = OverviewPage.Draw },
            new Page { Id = "patients", Key = "RimWard.Tab.Page.Patients", Draw = PatientsPage.Draw, Count = () => HospitalOverview.PatientCount(Find.CurrentMap) },
            new Page { Id = "inbox", Key = "RimWard.Tab.Page.Inbox", Draw = InboxPage.Draw, Count = () => HospitalOverview.PendingOffers(Find.CurrentMap) },
            new Page { Id = "policies", Key = "RimWard.Tab.Page.Policies", Draw = PoliciesPage.Draw },
            new Page { Id = "guide", Key = "RimWard.Tab.Page.Guide", Draw = GuidePage.Draw },
        };

        internal static IEnumerable<string> PageIds => pages.Select(p => p.Id);

        /// <summary>Opens the tab on a page (from letters, alerts and the readiness list).</summary>
        public static void OpenOn(string page)
        {
            SelectedPage = page;
            MainButtonDef def = DefDatabase<MainButtonDef>.GetNamedSilentFail("RimWard_Patients");
            if (def != null && Find.MainTabsRoot != null && Find.MainTabsRoot.OpenTab != def) Find.MainTabsRoot.SetCurrentTab(def, false);
        }

        public override void PostClose()
        {
            base.PostClose();
            foreach (Map m in Find.Maps) HospitalMapComponent.For(m)?.ApplyPolicyToAll();
        }

        public override void DoWindowContents(Rect inRect)
        {
            KitMod k = HospitalMod.K;
            if (k == null) return;
            k.Run(KitMod.Core, "UI.HospitalTab", () => this.Draw(inRect));
        }

        /// <summary>Header, page list and page area for an inner rect.</summary>
        internal static void Layout(Rect inRect, out Rect header, out Rect list, out Rect content)
        {
            const float headerH = 44f;
            header = new Rect(inRect.x, inRect.y, inRect.width, headerH);
            Rect body = new Rect(inRect.x, inRect.y + headerH + 4f, inRect.width, inRect.height - headerH - 4f);
            float listW = Mathf.Clamp(inRect.width * 0.17f, 170f, 230f);
            list = new Rect(body.x, body.y, listW, body.height);
            content = new Rect(list.xMax + 16f, body.y, body.width - listW - 16f, body.height);
        }

        private void Draw(Rect inRect)
        {
            Frames++;
            Map map = Find.CurrentMap;
            HospitalMapComponent comp = HospitalMapComponent.For(map);
            if (pages.All(p => p.Id != SelectedPage)) SelectedPage = pages[0].Id;
            Layout(inRect, out Rect header, out Rect list, out Rect content);
            TabLayout L = new TabLayout { In = inRect, Header = header, List = list, Content = content, Page = SelectedPage, Frame = Time.frameCount };

            // header: title, open/closed chip (a switch), settings button
            string title = "RimWard.Tab.Title".Translate();
            float titleW = Mathf.Ceil(RwUI.TextW(title, GameFont.Medium)) + 4f;
            float tx = header.x;
            if (RwUI.Mark != null) { GUI.DrawTexture(new Rect(tx, header.y + 4f, 32f, 32f), RwUI.Mark); tx += 40f; }
            KitStyle.LabelColored(new Rect(tx, header.y + 2f, titleW, 36f), title, RwUI.Accent, TextAnchor.MiddleLeft, GameFont.Medium);
            string settings = "RimWard.Tab.Settings".Translate();
            float sw = RwUI.ButtonW(settings, 120f, 220f);
            Rect settingsR = new Rect(header.xMax - sw, header.y + 4f, sw, 32f);
            if (Widgets.ButtonText(settingsR, settings)) k_OpenSettings();
            L.SettingsButton = settingsR;
            if (comp != null)
            {
                bool open = HospitalMod.Settings.IntakeOn && comp.Policy.open;
                string chip = open ? "RimWard.Tab.OpenChip".Translate() : "RimWard.Tab.ClosedChip".Translate();
                float cw = Mathf.Clamp(RwUI.TextW(chip) + 30f, 110f, 240f);
                float cx = tx + titleW + 16f;
                Rect cr = new Rect(cx, header.y + 6f, Mathf.Min(cw, Mathf.Max(60f, settingsR.x - 12f - cx)), 28f);
                RwUI.Chip(cr, chip, open ? RwUI.Good : RwUI.Bad, open ? "RimWard.Tab.OpenChipTip".Translate() : "RimWard.Tab.ClosedChipTip".Translate());
                if (Widgets.ButtonInvisible(cr)) HospitalOverview.SetOpen(map, !open);
                L.OpenChip = cr;
            }
            Widgets.DrawBoxSolid(new Rect(header.x, header.yMax - 1f, header.width, 1f), RwUI.Line);

            // page list
            const float rowH = 36f;
            float y = list.y;
            foreach (Page p in pages)
            {
                Rect row = new Rect(list.x, y, list.width, rowH);
                bool sel = p.Id == SelectedPage;
                if (sel) { Widgets.DrawBoxSolid(row, RwUI.AccentSoft); Widgets.DrawBoxSolid(new Rect(row.x, row.y, 3f, row.height), RwUI.Accent); }
                else if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                string lab = p.Key.Translate();
                int n = 0;
                if (p.Count != null) { try { n = p.Count(); } catch { n = 0; } }
                float badgeW = n > 0 ? Mathf.Max(26f, RwUI.TextW(n.ToString()) + 14f) : 0f;
                Rect lr = new Rect(row.x + 14f, row.y, row.width - 18f - badgeW - 4f, row.height);
                KitStyle.LabelColored(lr, lab.Truncate(lr.width), sel ? Color.white : RwUI.Grey, TextAnchor.MiddleLeft);
                if (RwUI.TextW(lab) > lr.width) TooltipHandler.TipRegion(row, lab);
                if (n > 0) RwUI.Chip(new Rect(row.xMax - badgeW - 6f, row.y + 8f, badgeW, 20f), n.ToString(), p.Id == "inbox" ? RwUI.Warn : RwUI.Accent);
                if (Widgets.ButtonInvisible(row)) SelectedPage = p.Id;
                y += rowH + 2f;
            }
            Widgets.DrawBoxSolid(new Rect(list.xMax + 7f, list.y, 1f, list.height), RwUI.Line);

            // the page
            Page current = pages.First(p => p.Id == SelectedPage);
            PageProbe.Begin(content.width);
            Widgets.BeginGroup(content);
            try
            {
                if (map == null || comp == null) KitStyle.Paragraph(0f, 0f, content.width, "RimWard.Tab.NoMap".Translate(), RwUI.Grey);
                else HospitalMod.K.Run(KitMod.Core, "UI.HospitalTab." + current.Id, () => current.Draw(new Rect(0f, 0f, content.width, content.height), map));
            }
            finally { Widgets.EndGroup(); }
            L.PageH = PageProbe.Height; L.PageW = PageProbe.Width; L.Overflow = PageProbe.Overflow != null; L.OverflowWhat = PageProbe.Overflow;
            Last = L;
        }

        private static void k_OpenSettings() => HospitalMod.K?.OpenSettings("overview");
    }

    /// <summary>What the drawn page reported (height, width, anything placed outside the page width). The UI suites read it.</summary>
    internal static class PageProbe
    {
        public static float Height, Width;
        public static string Overflow;
        public static void Begin(float w) { Height = 0f; Width = w; Overflow = null; }
        /// <summary>Records the page height and whether <paramref name="r"/> leaves the usable width <paramref name="w"/>.</summary>
        public static Rect Fit(Rect r, float w, string what)
        {
            if (Overflow == null && (r.xMax > w + 0.5f || r.x < -0.5f)) Overflow = what + " " + r + " > " + w.ToString("0");
            return r;
        }
        public static void Done(float h, float w) { Height = h; Width = w; }
    }
}
