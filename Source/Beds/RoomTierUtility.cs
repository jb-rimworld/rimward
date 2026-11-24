// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Fork of Hospital (Copyright Adamas) and Hospital Fix And Tweak (Copyright assssssqwww), both GPL-3.0.
// This file contains code from those mods, modified by juliusberger00 in 2026 (RimWard 1.x and 2.0); modifications Copyright (C) 2026
// juliusberger00. Licensed under the GNU General Public License version 3 (LICENSE); full source: https://github.com/jb-rimworld/rimward
using System.Linq;
using RimWorld;
using Verse;

namespace RimWard
{
    public enum RoomTier { Hallway, Barracks, TwoBed, Single }

    public static class RoomTierUtility
    {
        // Single = room >= 9 cells AND exactly one medical bed (double bed OK only if the 2nd slot is the patient's partner).
        public static RoomTier Classify(Building_Bed bed, Pawn patient)
        {
            if (bed == null) return RoomTier.Hallway;
            Room room = bed.GetRoom();
            if (room == null || room.PsychologicallyOutdoors || room.TouchesMapEdge) return RoomTier.Hallway;

            int medBeds = room.ContainedAndAdjacentThings.OfType<Building_Bed>()
                .Count(b => b.GetRoom() == room && b.Medical && !b.ForPrisoners);

            if (medBeds >= 3) return RoomTier.Barracks;
            if (medBeds == 2) return RoomTier.TwoBed;

            // exactly one medical bed
            if (room.CellCount < 9) return RoomTier.TwoBed; // a 1-bed closet is not a premium suite
            if (bed.SleepingSlotsCount > 1)
            {
                for (int i = 0; i < bed.SleepingSlotsCount; i++)
                {
                    Pawn occ = bed.GetCurOccupant(i);
                    if (occ != null && occ != patient && !LovePartnerRelationUtility.LovePartnerRelationExists(patient, occ))
                    {
                        return RoomTier.TwoBed; // a non-partner shares the double bed
                    }
                }
            }
            return RoomTier.Single;
        }

        public static float Multiplier(RoomTier tier)
        {
            Settings s = HospitalMod.Settings;
            switch (tier)
            {
                case RoomTier.Single: return s.SingleRoomMultiplier;
                case RoomTier.TwoBed: return s.TwoBedMultiplier;
                case RoomTier.Barracks: return s.BarracksMultiplier;
                default: return s.BarracksMultiplier * 0.85f; // hallway: worst of all
            }
        }
    }
}
