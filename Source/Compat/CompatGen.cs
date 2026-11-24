using System.Linq;
using RimWard.Utilities;
using RimWorld;
using Verse;

namespace RimWard
{
    // Soft-gated, mod/DLC-specific patient complications, using APIs verified against 1.6 + the mod sources.
    // Every method is defensive: if anything is missing or throws, it cleanly does nothing.
    public static class CompatGen
    {
        // === Xenotypes (Biotech) ===
        // The faction's own xenotype is kept (vanilla generates it; genes, skin and hair stay). RimWard only refuses
        // patients it cannot serve: xenotypes that live on hemogen or need a deathrest casket. Checked by gene class,
        // so modded xenotypes built from those genes are caught too. "Special needs patients" (setting) lets them in.
        public static bool IsUnservableXenotype(Pawn p)
        {
            if (!ModsConfig.BiotechActive || p?.genes == null) return false;
            if (HospitalMod.Settings != null && HospitalMod.Settings.AllowSpecialNeedsXenotypes) return false;
            try
            {
                foreach (Gene g in p.genes.GenesListForReading)
                {
                    if (g == null || !g.Active) continue;
                    if (g is Gene_Hemogen || g is Gene_Deathrest) return true;
                }
            }
            catch { }
            return false;
        }

        // === Royal patient (Royalty): flag a private-room demand WITHOUT granting a real title (which would
        //     carry expectations/apparel/throne/psylink side effects). OutcomeUtility reads the flag. ===
        public static bool CanMakeRoyal(Pawn p)
        {
            return ModsConfig.RoyaltyActive && p?.RaceProps != null && p.RaceProps.Humanlike;
        }

        public static bool MakeRoyal(Pawn p, PatientData d)
        {
            if (d == null) return false;
            d.RequiresPrivateRoom = true;
            return true;
        }

        // === Go-juice dependency (vanilla addiction; fair, non-lethal). Withdrawal drags the outcome down
        //     unless treated. The lethal Biotech-gene version needs bedside dosing AI and is deferred. ===
        public static bool CanAddDependency(Pawn p)
        {
            ChemicalDef gj = DefDatabase<ChemicalDef>.GetNamedSilentFail("GoJuice");
            return p?.RaceProps != null && p.RaceProps.Humanlike && PatientComplexityGenerator.IsSafeAddiction(gj?.addictionHediff)
                && p.health.hediffSet.GetFirstHediffOfDef(gj.addictionHediff) == null;
        }

        public static bool AddDrugDependency(Pawn p)
        {
            ChemicalDef gj = DefDatabase<ChemicalDef>.GetNamedSilentFail("GoJuice");
            if (gj?.addictionHediff == null) return false;
            try
            {
                Hediff_Addiction add = (Hediff_Addiction)HediffMaker.MakeHediff(gj.addictionHediff, p);
                p.health.AddHediff(add);
                return true;
            }
            catch { return false; }
        }

        // === Organ failure (More Injuries: Organ Failure) — DEFERRED from active generation: the hediff is
        //     coupled to live organ FunctionFraction (often won't stick / self-removes for offer-held world
        //     pawns) and needs map-spawned, aggregate-function-aware damage. Kept for a future, tested pass. ===
        public static bool CanAddOrganFailure(Pawn p) => false;

        public static bool AddOrganFailure(Pawn p, PatientData d)
        {
            HediffDef fail = DefDatabase<HediffDef>.GetNamedSilentFail("MIFB_AcuteRenalFailure");
            string partName = "Kidney";
            if (fail == null) { fail = DefDatabase<HediffDef>.GetNamedSilentFail("MIFB_AcuteLiverFailure"); partName = "Liver"; }
            if (fail == null) return false;
            BodyPartRecord part = p.health.hediffSet.GetNotMissingParts().FirstOrDefault(bp => bp.def.defName == partName);
            if (part == null) return false;
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    if (WoundsUtility.DamagePart(p, 1000, part, out _, null)) break;
                }
                Hediff f = HediffMaker.MakeHediff(fail, p, part);
                if (p.health.WouldDieAfterAddingHediff(f)) return false;
                p.health.AddHediff(f, part);
                return true;
            }
            catch { return false; }
        }
    }
}
