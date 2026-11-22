using RimWard.Utilities;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard
{
    // Computes the payment multiplier and goodwill from the whole stay: room tier, chief physician,
    // faction medicine preference, renown, and patient health outcome.
    public static class OutcomeUtility
    {
        public static float PaymentMultiplier(Pawn pawn, PatientData data)
        {
            Settings s = HospitalMod.Settings;
            RoomTier tier = data.BestRoomTier; // best room the patient actually rested in
            float mult = RoomTierUtility.Multiplier(tier);

            if (s.ChiefPhysicianEnabled && data.BestDoctorSkill >= s.ChiefPhysicianMinSkill)
            {
                mult *= s.ChiefPhysicianMultiplier;
            }
            mult *= MedicinePrefMultiplier(pawn, data);

            // Royal / demanding patients: pay handsomely for a private suite, insulted by a shared ward.
            bool wantsPrivate = data.RequiresPrivateRoom
                || (ModsConfig.RoyaltyActive && pawn.royalty != null && pawn.royalty.AllTitlesForReading.Count > 0);
            if (s.RoyalRoomDemand && wantsPrivate)
            {
                mult *= (tier == RoomTier.Single) ? 1.4f : 0.75f;
            }
            if (s.RenownEnabled)
            {
                HospitalGameComponent gc = HospitalGameComponent.Get;
                if (gc != null) mult *= gc.RenownPaymentMultiplier;
            }
            return mult;
        }

        private static float MedicinePrefMultiplier(Pawn pawn, PatientData data)
        {
            Settings s = HospitalMod.Settings;
            if (s.FactionMedicinePref == MedicineStrictness.Off || pawn.Faction == null || data.BestMedicinePotency <= 0f)
            {
                return 1f;
            }
            bool tribal = pawn.Faction.def.techLevel <= TechLevel.Medieval;
            float strict = s.FactionMedicinePref == MedicineStrictness.Strict ? 2f : 1f;
            if (tribal)
            {
                return data.BestMedicinePotency > 1.1f ? 1f - 0.08f * strict : 1.03f;   // modern meds unwelcome
            }
            return data.BestMedicinePotency < 0.8f ? 1f - 0.08f * strict : 1.03f;        // herbal = substandard
        }

        // Composite goodwill: mood rating + bonuses for a private room, a chief physician, and a health improvement.
        public static int GoodwillFor(Pawn pawn, PatientData data)
        {
            int gw = PatientUtility.CalculateGoodwillToGain(pawn, data);
            if (data.BestRoomTier == RoomTier.Single) gw += 1;
            if (HospitalMod.Settings.ChiefPhysicianEnabled && data.BestDoctorSkill >= HospitalMod.Settings.ChiefPhysicianMinSkill) gw += 1;
            if (data.AdmitHealth >= 0f && pawn.health.summaryHealth.SummaryHealthPercent > data.AdmitHealth + 0.1f) gw += 1;
            return Mathf.Clamp(gw, -10, 12);
        }

        // 0..100 score used to drive renown.
        public static float DischargeScore(Pawn pawn, PatientData data, bool alive)
        {
            if (!alive) return 0f;
            float score = 50f;
            score += GoodwillFor(pawn, data) * 4f;
            return Mathf.Clamp(score, 0f, 100f);
        }
    }
}
