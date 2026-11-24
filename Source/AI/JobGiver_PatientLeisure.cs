// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Copyright (C) 2026 juliusberger00. Written for RimWard 2.0 (a GPL-3.0 fork of Hospital by Adamas and
// Hospital Fix And Tweak by assssssqwww). Licensed under the GNU General Public License version 3 (LICENSE);
// full source: https://github.com/jb-rimworld/rimward
using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimWard
{
    /// <summary>
    /// Part of the patient duty (after "go to bed"): a patient who is up and about may take recreation at Hearth &amp; Harbor
    /// leisure places (spa: hot tub, sauna, massage). Does nothing without that mod, with the setting off, or for patients who must rest.
    /// Which patients may use what is decided on both sides: RimWard says "walking patient" (<see cref="PatientApi.IsWalkingPatient"/>),
    /// Hearth &amp; Harbor's own joy givers apply their house rules (no sauna after fresh surgery, switched-off spa, ...).
    /// </summary>
    public class JobGiver_PatientLeisure : ThinkNode_JobGiver
    {
        private const int RetryAfterNothing = 600;
        private const int PauseAfterJob = 2500;
        private static readonly Dictionary<int, int> nextTry = new Dictionary<int, int>();
        private static List<JoyGiverDef> givers;

        public static void ClearCaches() { nextTry.Clear(); givers = null; }

        private static List<JoyGiverDef> Givers()
        {
            if (givers != null) return givers;
            givers = new List<JoyGiverDef>();
            foreach (JoyGiverDef def in DefDatabase<JoyGiverDef>.AllDefsListForReading)
            {
                // patient-friendly places only: Hearth & Harbor's spa for now (cafe, library, chapel follow when H&H marks them)
                string cls = def.giverClass?.FullName;
                if (cls != null && cls.StartsWith("Hearth.Wellness.", StringComparison.Ordinal)) givers.Add(def);
            }
            return givers;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (HospitalMod.Settings == null || !HospitalMod.Settings.PatientLeisure) return null;
            if (!HearthBridge.HearthLoaded) return null;
            List<JoyGiverDef> list = Givers();
            if (list.Count == 0) return null;
            int now = Find.TickManager.TicksGame;
            if (nextTry.TryGetValue(pawn.thingIDNumber, out int due) && now < due) return null;
            if (!PatientApi.IsWalkingPatient(pawn)) return null;
            if (pawn.needs?.joy != null && pawn.needs.joy.CurLevelPercentage > 0.7f) { nextTry[pawn.thingIDNumber] = now + RetryAfterNothing; return null; }

            // weighted random like vanilla's JobGiver_GetJoy
            List<JoyGiverDef> pool = new List<JoyGiverDef>(list);
            while (pool.Count > 0)
            {
                float total = 0f;
                foreach (JoyGiverDef d in pool) total += Math.Max(0.01f, d.baseChance);
                float roll = Rand.Value * total;
                int pick = 0;
                for (; pick < pool.Count - 1; pick++)
                {
                    roll -= Math.Max(0.01f, pool[pick].baseChance);
                    if (roll <= 0f) break;
                }
                JoyGiverDef def = pool[pick];
                pool.RemoveAt(pick);
                Job job = null;
                try { job = def.Worker.CanBeGivenTo(pawn) ? def.Worker.TryGiveJob(pawn) : null; }
                catch (Exception e) { Log.ErrorOnce("[RimWard] patient leisure via " + def.defName + " failed: " + e.Message, def.shortHash ^ 0x5a17); }
                if (job != null)
                {
                    nextTry[pawn.thingIDNumber] = now + PauseAfterJob;
                    if (nextTry.Count > 400) nextTry.Clear();
                    return job;
                }
            }
            nextTry[pawn.thingIDNumber] = now + RetryAfterNothing;
            return null;
        }
    }
}
