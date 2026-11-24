// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
using RimWorld;
using Verse;

namespace RimWard
{
    // Subtle "X open patient offers" readout in the vanilla alert strip (auto-registered, no def needed).
    public class Alert_HospitalInbox : Alert
    {
        public Alert_HospitalInbox()
        {
            this.defaultPriority = AlertPriority.Medium;
        }

        private static int Count()
        {
            int n = 0;
            foreach (Map m in Find.Maps)
            {
                HospitalMapComponent c = HospitalMapComponent.For(m);
                if (c != null) n += c.PendingOfferCount;
            }
            return n;
        }

        public override string GetLabel() => "RimWard.Alert.Count".Translate(Count());

        public override TaggedString GetExplanation() => "RimWard.Alert.Explanation".Translate();

        protected override void OnClick() => MainTab.MainTabWindow_Hospital.OpenOn("inbox");

        public override AlertReport GetReport() => Count() > 0 ? AlertReport.Active : AlertReport.Inactive;
    }
}
