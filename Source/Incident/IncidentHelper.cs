using System.Collections.Generic;
using System.Linq;
using RimWard.Utilities;
using RimWorld;
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
                if (!IsWeirdPawn(p)) return p;
            }
            return null;
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
            HospitalMapComponent component = map.GetComponent<HospitalMapComponent>();
            return component.IsOpen() && !component.IsFull()
                && (!map.GameConditionManager.ConditionIsActive(GameConditionDefOf.ToxicFallout) || component.AcceptDanger)
                && (!map.GameConditionManager.ConditionIsActive(GameConditionDefOf.VolcanicWinter) || component.AcceptDanger)
                && TryFindEntryCell(map, out _);
        }

        private static bool TryFindEntryCell(Map map, out IntVec3 cell)
        {
            return CellFinder.TryFindRandomEdgeCellWith(c => map.reachability.CanReachColony(c) && !c.Fogged(map), map, CellFinder.EdgeRoadChance_Neutral, out cell);
        }

        public static void SetUpNewPatient(Pawn pawn)
        {
            pawn.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Guest);
            pawn.playerSettings.selfTend = false;
            pawn.equipment.DestroyAllEquipment();
            pawn.inventory.DestroyAll();
        }
    }
}
