// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimWard
{
    // Ideology "Guilty" meme: turning away a sick patient weighs on a colony that believes in charity.
    public static class IdeologyCompat
    {
        private static MemeDef guiltyMeme;
        private static bool resolved;

        private static MemeDef Guilty
        {
            get
            {
                if (!resolved) { guiltyMeme = DefDatabase<MemeDef>.GetNamedSilentFail("Guilty"); resolved = true; }
                return guiltyMeme;
            }
        }

        public static bool PlayerHasGuilt()
        {
            if (!ModsConfig.IdeologyActive || !HospitalMod.Settings.IdeologyGuilty) return false;
            Ideo ideo = Faction.OfPlayer?.ideos?.PrimaryIdeo;
            return ideo != null && Guilty != null && ideo.memes.Contains(Guilty);
        }

        public static void NotifyPatientTurnedAway(Map map)
        {
            if (map == null || !ModsConfig.IdeologyActive || !HospitalMod.Settings.IdeologyGuilty || Guilty == null) return;
            ThoughtDef t = DefDatabase<ThoughtDef>.GetNamedSilentFail("RimWard_TurnedAwayPatient");
            if (t == null) return;
            // Only colonists whose OWN ideoligion has the Guilty meme feel it.
            foreach (Pawn col in map.mapPawns.FreeColonistsSpawned)
            {
                if (col.Ideo != null && col.Ideo.memes.Contains(Guilty))
                {
                    col.needs?.mood?.thoughts?.memories?.TryGainMemory(t);
                }
            }
        }
    }
}
