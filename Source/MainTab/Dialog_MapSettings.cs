// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
using JBFramework;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard.MainTab
{
    // "Hospital Policies" dialog (kept for the Mod settings page). The same editor is the Policies page of the Hospital tab.
    public class Dialog_MapSettings : Window
    {
        private readonly Map map;
        private Vector2 scroll;
        private float viewH = 600f;

        public Dialog_MapSettings(Map map)
        {
            this.map = map;
            this.forcePause = true;
            this.doCloseX = true;
            this.doCloseButton = true;
            this.closeOnClickedOutside = true;
            this.absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => KitUI.Size(620f, 700f, 520f, 520f, 0.9f, 0.9f);

        public override void DoWindowContents(Rect inRect)
        {
            if (HospitalMapComponent.For(this.map) == null) { this.Close(); return; }
            Rect body = RwUI.PageHeader(new Rect(inRect.x, inRect.y, inRect.width, inRect.height - 50f), "HospitalSettings".Translate(), null);
            float w = RwUI.BeginScroll(body, ref this.scroll, this.viewH);
            float used = 0f;
            HospitalMod.K.Run(KitMod.Core, "UI.PolicyDialog", () => used = PolicyEditor.Draw(0f, w, this.map));
            this.viewH = RwUI.EndScroll(used);
        }

        public override void PostClose()
        {
            base.PostClose();
            foreach (Map m in Find.Maps) HospitalMapComponent.For(m)?.ApplyPolicyToAll();
        }
    }
}
