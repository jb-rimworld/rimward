using System.Collections.Generic;
using System.Linq;
using RimWard.Utilities;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimWard
{
    public static class IncidentHelper
    {
        // Synthetic / undroppable pawns we must never turn into patients (androids, non-bleeders,
        // powered pawns, drop-destroyed apparel). Centralized so every spawn path uses the same filter.
        public static bool IsWeirdPawn(Pawn pawn)
        {
            return pawn == null
                || pawn.kindDef.defName.ToUpper().Contains("VREA")
                || !pawn.health.CanBleed
                || (pawn.needs?.AllNeeds?.Any(n => n.def.defName.ToLower().Contains("power")) ?? false)
                || pawn.kindDef.defName.StartsWith("PN")
                || (pawn.apparel?.WornApparel?.Any(a => a.def.destroyOnDrop) ?? false);
        }

        public static Pawn GenerateValidPatientPawn(Faction faction, int maxTries = 20)
        {
            for (int i = 0; i < maxTries; i++)
            {
                Pawn p = GeneratePawn(faction);
                if (!IsWeirdPawn(p) && !CompatGen.IsUnservableXenotype(p)) return p;
                DiscardGenerated(p);
            }
            return null;
        }

        /// <summary>
        /// Throws away a generated pawn RimWard will not use (rejected, declined offer, failed delivery). Vanilla's Thing.Discard only works
        /// on a destroyed thing, and destroying a pawn passes it to the world pawns (both log "Tried to discard ..." and keep the pawn), so:
        /// despawn, out of its holder and the world pawns, then WorldPawns.PassToWorld(Discard), which destroys and discards in one go.
        /// </summary>
        public static void DiscardGenerated(Pawn p)
        {
            if (p == null || p.Discarded) return;
            try
            {
                if (p.Spawned) p.DeSpawn();
                p.holdingOwner?.Remove(p);
                if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
                // vanilla's own way to drop a pawn for good: destroy (without passing it back to the world) and discard
                if (!p.Discarded) Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.Discard);
            }
            catch (System.Exception e) { Log.Warning("[RimWard] could not discard " + p.LabelShort + ": " + e.Message); }
        }

        public static Pawn GeneratePawn(Faction faction)
        {
            // Ctor argument shape verified against 1.6 (matches HospitalTweak's GeneratePawn_Prefix).
            return PawnGenerator.GeneratePawn(new PawnGenerationRequest(faction.RandomPawnKind(), faction,
                PawnGenerationContext.NonPlayer, -1, forceGenerateNewPawn: true, allowDead: false, allowDowned: false,
                canGeneratePawnRelations: true, mustBeCapableOfViolence: false, colonistRelationChanceFactor: 1f,
                forceAddFreeWarmLayerIfNeeded: false, allowGay: true, allowPregnant: false, allowFood: true,
                allowAddictions: false, inhabitant: false, certainlyBeenInCryptosleep: false,
                forceRedressWorldPawnIfFormerColonist: false, worldPawnFactionDoesntMatter: false,
                biocodeWeaponChance: 0f, biocodeApparelChance: 0f, extraPawnForExtraRelationChance: null,
                relationWithExtraPawnChanceFactor: 1f, validatorPreGear: null, validatorPostGear: null,
                forcedTraits: null, prohibitedTraits: null, minChanceToRedressWorldPawn: null, fixedBiologicalAge: null,
                fixedChronologicalAge: null, fixedGender: null));
        }

        public static IEnumerable<Pawn> GetKnownPawns(IncidentParms parms)
        {
            return Find.WorldPawns.AllPawnsAlive.Where(pawn => ValidPatient(pawn, parms.faction));
        }

        private static bool ValidPatient(Pawn pawn, Faction faction)
        {
            return !pawn.Discarded && !pawn.Dead && !pawn.Spawned && !pawn.NonHumanlikeOrWildMan() && !pawn.Downed
                && pawn.Faction == faction
                && (faction.leader != pawn || faction.PlayerGoodwill >= 80)
                && pawn.kindDef != PawnKindDefOf.Empire_Royal_Bestower
                && !QuestUtility.IsReservedByQuestOrQuestBeingGenerated(pawn)
                && !pawn.IsPatient(out _, true);
        }

        public static bool CanSpawnPatient(Map map)
        {
            return ArrivalBlockReason(map) == null;
        }

        /// <summary>Why no patient can arrive on this map right now (translated), or null when one can. Plain accessor for the overview.</summary>
        public static string ArrivalBlockReason(Map map)
        {
            HospitalMapComponent component = HospitalMapComponent.For(map);
            if (component == null) return "RimWard.Block.NoHospital".Translate();
            if (HospitalMod.Settings == null || !HospitalMod.Settings.IntakeOn) return "RimWard.Block.IntakeOff".Translate();
            HospitalPolicy pol = component.Policy;
            if (!pol.open) return "RimWard.Block.Closed".Translate();
            if (pol.OpenHourCount == 0) return "RimWard.Block.NoHours".Translate();
            if (!component.IsOpen()) return "RimWard.Block.OutsideHours".Translate(pol.NextOpenHour(GenLocalDate.HourOfDay(map)));
            if (component.BedCount() == 0) return "RimWard.Block.NoBeds".Translate();
            if (component.IsFull()) return "RimWard.Block.Full".Translate();
            string danger = Threats.ArrivalBlock(map, pol);
            if (danger != null) return "RimWard.Block.Danger".Translate(danger);
            if (!TryFindEntryCell(map, out _)) return "RimWard.Block.NoEntry".Translate();
            return null;
        }

        private static bool TryFindEntryCell(Map map, out IntVec3 cell)
        {
            return CellFinder.TryFindRandomEdgeCellWith(c => map.reachability.CanReachColony(c) && !c.Fogged(map), map, CellFinder.EdgeRoadChance_Neutral, out cell);
        }

        public static void SetUpNewPatient(Pawn pawn)
        {
            pawn.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Guest);
            if (pawn.playerSettings != null) pawn.playerSettings.selfTend = false;
            pawn.equipment.DestroyAllEquipment();
            pawn.inventory.DestroyAll();
        }
    }
}
