// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Copyright (C) 2026 juliusberger00. Written for RimWard 2.0 (a GPL-3.0 fork of Hospital by Adamas and
// Hospital Fix And Tweak by assssssqwww). Licensed under the GNU General Public License version 3 (LICENSE);
// full source: https://github.com/jb-rimworld/rimward
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimWard
{
    /// <summary>
    /// What counts as danger for the hospital. Only an ACTIVE threat counts: hostile pawns that are awake and not just guarding their
    /// own spot (a dormant mech cluster, a defending cluster, a sleeping ambush), plus animals or people in an aggressive mental state.
    /// Weather (toxic fallout, volcanic winter) stops arrivals but never holds a patient who wants to go home.
    /// Results are cached per map for 250 ticks.
    /// </summary>
    public static class Threats
    {
        private class Cache
        {
            public int tick = -99999;
            public List<string> threats = new List<string>();
            public List<string> weather = new List<string>();
            public List<Faction> hostileFactions = new List<Faction>();
        }

        private static readonly Dictionary<Map, Cache> caches = new Dictionary<Map, Cache>();

        public static void ClearCache() => caches.Clear();

        private static Cache Get(Map map)
        {
            if (!caches.TryGetValue(map, out Cache c)) { c = new Cache(); caches[map] = c; }
            int now = Find.TickManager.TicksGame;
            if (now - c.tick < 250 && now >= c.tick) return c;
            c.tick = now;
            c.threats.Clear(); c.weather.Clear(); c.hostileFactions.Clear();
            if (map.GameConditionManager.ConditionIsActive(GameConditionDefOf.ToxicFallout)) c.weather.Add(GameConditionDefOf.ToxicFallout.LabelCap);
            if (map.GameConditionManager.ConditionIsActive(GameConditionDefOf.VolcanicWinter)) c.weather.Add(GameConditionDefOf.VolcanicWinter.LabelCap);
            Dictionary<string, int> counts = new Dictionary<string, int>();
            foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
            {
                if (!IsActiveThreat(p)) continue;
                string label;
                if (p.InAggroMentalState) label = p.GetKindLabelPlural() + " (" + p.MentalStateDef.label + ")";
                else
                {
                    label = p.Faction.def.pawnsPlural.CapitalizeFirst();
                    if (!c.hostileFactions.Contains(p.Faction)) c.hostileFactions.Add(p.Faction);
                }
                counts[label] = counts.TryGetValue(label, out int n) ? n + 1 : 1;
            }
            foreach (KeyValuePair<string, int> kv in counts) c.threats.Add(kv.Key);
            return c;
        }

        public static bool IsActiveThreat(Pawn p)
        {
            if (p == null || p.Dead || p.Downed || p.IsPrisoner || p.InContainerEnclosed) return false;
            if (p.MapHeld == null || p.MapHeld.fogGrid.IsFogged(p.PositionHeld)) return false;
            if (p.InAggroMentalState) return true;
            if (p.Faction == null || !p.Faction.HostileTo(Faction.OfPlayer)) return false;
            CompCanBeDormant dormant = p.GetComp<CompCanBeDormant>();
            if (dormant != null && !dormant.Awake) return false;
            LordJob job = p.GetLord()?.LordJob;
            if (job is LordJob_MechanoidsDefend || job is LordJob_SleepThenMechanoidsDefend || job is LordJob_DefendBase) return false;
            return true;
        }

        /// <summary>Active threats (hostiles, aggressive animals). Holds cured patients and stops arrivals.</summary>
        public static bool AnyActiveThreat(Map map, out string reasons)
        {
            Cache c = Get(map);
            reasons = c.threats.Count > 0 ? string.Join(", ", c.threats) : null;
            return c.threats.Count > 0;
        }

        /// <summary>Weather that stops arrivals (not departures).</summary>
        public static bool BadWeather(Map map, out string reasons)
        {
            Cache c = Get(map);
            reasons = c.weather.Count > 0 ? string.Join(", ", c.weather) : null;
            return c.weather.Count > 0;
        }

        /// <summary>Factions with active hostile pawns on the map (patients of factions at war with them are not sent).</summary>
        public static IReadOnlyList<Faction> ActiveHostileFactions(Map map) => Get(map).hostileFactions;

        /// <summary>Why no patient can arrive right now because of danger (null = nothing). Honours the policy "Allow patients during danger".</summary>
        public static string ArrivalBlock(Map map, HospitalPolicy policy)
        {
            if (policy != null && policy.acceptDanger) return null;
            List<string> parts = new List<string>();
            if (AnyActiveThreat(map, out string t)) parts.Add(t);
            if (BadWeather(map, out string w)) parts.Add(w);
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }
    }
}
