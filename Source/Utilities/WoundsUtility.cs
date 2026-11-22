using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard.Utilities
{
    public static class WoundsUtility
    {
        public static void AddRandomWounds(Pawn pawn, PatientData patientData)
        {
            int damage = (int)(Mathf.Lerp(5f, 50f, Rand.Value) * HospitalMod.Settings.Difficulty);
            DamageParts(pawn, damage, null);
            patientData.Bill = 10f;
            patientData.Cure = "CureWounds".Translate();
            patientData.Diagnosis = "DiagnosisWounds".Translate();
        }

        public static void AddGunshotWounds(Pawn pawn, PatientData patientData)
        {
            int damage = (int)(Mathf.Lerp(5f, 50f, Rand.Value) * HospitalMod.Settings.Difficulty);
            DamageParts(pawn, damage, DamageDefOf.Bullet);
            patientData.Bill = 10f;
            patientData.Cure = "CureWounds".Translate();
            patientData.Diagnosis = "DiagnosisWounds".Translate();
        }

        public static void AddBruisesWounds(Pawn pawn, PatientData patientData)
        {
            int damage = (int)(Mathf.Lerp(5f, 50f, Rand.Value) * HospitalMod.Settings.Difficulty);
            DamageParts(pawn, damage, new List<DamageDef> { DamageDefOf.Crush, DamageDefOf.Scratch, DamageDefOf.Cut }.RandomElement());
            patientData.Bill = 10f;
            patientData.Cure = "CureWounds".Translate();
            patientData.Diagnosis = "DiagnosisWounds".Translate();
        }

        public static void AddBurnWounds(Pawn pawn, PatientData patientData)
        {
            int damage = (int)(Mathf.Lerp(5f, 50f, Rand.Value) * HospitalMod.Settings.Difficulty);
            DamageParts(pawn, damage, DamageDefOf.Burn);
            patientData.Bill = 10f;
            patientData.Cure = "CureWounds".Translate();
            patientData.Diagnosis = "DiagnosisWounds".Translate();
        }

        private static void DamageParts(Pawn p, int totalDamage, DamageDef damageDef)
        {
            int guard = 0;
            p.health.forceDowned = true;
            while (totalDamage > 0 && guard < 300)
            {
                guard++;
                IEnumerable<BodyPartRecord> source = p.health.hediffSet.GetNotMissingParts()
                    .Where(x => p.health.hediffSet.GetPartHealth(x) >= 2f && x.def.canScarify);
                if (!source.Any()) break;
                BodyPartRecord part = source.RandomElement();
                if (DamagePart(p, totalDamage, part, out int severity, damageDef)) break;
                totalDamage -= severity;
            }
            p.health.forceDowned = false;
        }

        public static bool DamagePart(Pawn p, int totalDamage, BodyPartRecord bodyPartRecord, out int severity, DamageDef damageDef)
        {
            float maxHealth = bodyPartRecord.def.GetMaxHealth(p);
            float partHealth = p.health.hediffSet.GetPartHealth(bodyPartRecord);
            int lo = Mathf.Clamp(Mathf.RoundToInt(maxHealth * 0.12f), 1, (int)partHealth - 1);
            int hi = Mathf.Clamp(Mathf.RoundToInt(maxHealth * 0.27f), 1, (int)partHealth - 1);
            severity = Math.Min(Rand.RangeInclusive(lo, hi), totalDamage);
            if (damageDef == null)
            {
                damageDef = HealthUtility.RandomViolenceDamageType();
            }
            HediffDef hediffDef = HealthUtility.GetHediffDefFromDamage(damageDef, p, bodyPartRecord);
            if (p.health.WouldDieAfterAddingHediff(hediffDef, bodyPartRecord, severity))
            {
                return true;
            }
            DamageInfo damageInfo = new DamageInfo(damageDef, severity, 999f, -1f, null, bodyPartRecord, null, DamageInfo.SourceCategory.ThingOrUnknown, null, true, true, QualityCategory.Normal, true);
            damageInfo.SetAllowDamagePropagation(false);
            p.TakeDamage(damageInfo);
            return false;
        }
    }
}
