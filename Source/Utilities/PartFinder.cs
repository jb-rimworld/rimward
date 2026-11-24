using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimWard.Utilities
{
    /// <summary>
    /// Finds body parts by what they are (defName, then body part tag), never by their translated label.
    /// Works in every language and on alien races (HAR bodies reuse vanilla tags even when they rename parts).
    /// </summary>
    public static class PartFinder
    {
        private struct Target
        {
            public string partDefName;
            public string tagDefName;
            public Target(string part, string tag) { this.partDefName = part; this.tagDefName = tag; }
        }

        // Keyword in the recipe/hediff defName -> the part it treats. Order matters: the first match wins.
        private static readonly List<KeyValuePair<string, Target>> Keywords = new List<KeyValuePair<string, Target>>
        {
            new KeyValuePair<string, Target>("appendic", new Target("Torso", null)),
            new KeyValuePair<string, Target>("hernia", new Target("Torso", null)),
            new KeyValuePair<string, Target>("toothache", new Target("Jaw", "EatingSource")),
            new KeyValuePair<string, Target>("tooth", new Target("Jaw", "EatingSource")),
            new KeyValuePair<string, Target>("kidney", new Target("Kidney", "BloodFiltrationKidney")),
            new KeyValuePair<string, Target>("renal", new Target("Kidney", "BloodFiltrationKidney")),
            new KeyValuePair<string, Target>("liver", new Target("Liver", "BloodFiltrationLiver")),
            new KeyValuePair<string, Target>("deaf", new Target("Ear", "HearingSource")),
            new KeyValuePair<string, Target>("tinnitus", new Target("Ear", "HearingSource")),
            new KeyValuePair<string, Target>("glaucoma", new Target("Eye", "SightSource")),
            new KeyValuePair<string, Target>("strabismus", new Target("Eye", "SightSource")),
            new KeyValuePair<string, Target>("cataract", new Target("Eye", "SightSource")),
            new KeyValuePair<string, Target>("arrhythmia", new Target("Heart", "BloodPumpingSource")),
            new KeyValuePair<string, Target>("heart", new Target("Heart", "BloodPumpingSource")),
            new KeyValuePair<string, Target>("spinal", new Target("Spine", "Spine")),
            new KeyValuePair<string, Target>("spine", new Target("Spine", "Spine")),
            new KeyValuePair<string, Target>("gastritis", new Target("Stomach", "MetabolismSource")),
            new KeyValuePair<string, Target>("stomach", new Target("Stomach", "MetabolismSource")),
            new KeyValuePair<string, Target>("lung", new Target("Lung", "BreathingSource")),
        };

        /// <summary>The part a treatment named like <paramref name="defName"/> belongs to, or null when the name says nothing.</summary>
        public static BodyPartRecord ForKeyword(Pawn pawn, string defName)
        {
            if (pawn?.health?.hediffSet == null || defName.NullOrEmpty()) return null;
            string lower = defName.ToLowerInvariant();
            foreach (KeyValuePair<string, Target> kv in Keywords)
            {
                if (!lower.Contains(kv.Key)) continue;
                BodyPartRecord rec = Find(pawn, kv.Value.partDefName, kv.Value.tagDefName);
                if (rec != null) return rec;
            }
            return null;
        }

        /// <summary>A not-missing part with this defName, else one carrying this tag (alien bodies), else null.</summary>
        public static BodyPartRecord Find(Pawn pawn, string partDefName, string tagDefName)
        {
            List<BodyPartRecord> parts = pawn.health.hediffSet.GetNotMissingParts().ToList();
            if (!partDefName.NullOrEmpty())
            {
                List<BodyPartRecord> byName = parts.Where(p => p.def.defName == partDefName).ToList();
                if (byName.Count > 0) return byName.RandomElement();
            }
            if (!tagDefName.NullOrEmpty())
            {
                List<BodyPartRecord> byTag = parts.Where(p => p.def.tags != null && p.def.tags.Any(t => t != null && t.defName == tagDefName)).ToList();
                if (byTag.Count > 0) return byTag.RandomElement();
            }
            return null;
        }

        /// <summary>A part where vanilla's own hediff givers place this hediff (e.g. a modded disease declared with partsToAffect).</summary>
        public static BodyPartRecord FromHediffGivers(Pawn pawn, HediffDef hediff)
        {
            if (pawn?.health?.hediffSet == null || hediff == null) return null;
            List<BodyPartDef> defs = new List<BodyPartDef>();
            foreach (HediffGiverSetDef set in DefDatabase<HediffGiverSetDef>.AllDefsListForReading)
            {
                if (set.hediffGivers == null) continue;
                foreach (HediffGiver g in set.hediffGivers)
                {
                    if (g?.hediff == hediff && g.partsToAffect != null) defs.AddRange(g.partsToAffect);
                }
            }
            if (defs.Count == 0) return null;
            List<BodyPartRecord> parts = pawn.health.hediffSet.GetNotMissingParts().Where(p => defs.Contains(p.def)).ToList();
            return parts.Count > 0 ? parts.RandomElement() : null;
        }
    }
}
