using RimWorld;
using Verse;

namespace RimWard
{
    // Per-patient record. Step 2 will evolve this into the PatientContract state machine;
    // for the parity fork it mirrors the original field set 1:1.
    public class PatientData : IExposable
    {
        public int ArrivedAtTick;
        public float InitialMarketValue;
        public float InitialMood;
        public PatientType Type;
        public float Bill;
        public string Cure;
        public string Diagnosis;
        public RecipeDef CureRecipe;
        public bool Dismissed;
        public bool HasPendingSurgeryBill;

        // Outcome tracking (Cluster 3+)
        public int BestDoctorSkill;
        public float BestMedicinePotency;
        public float AdmitHealth = -1f;
        public int SurgeryAttempts;
        public int NoFaultUntilTick = -1;
        public int RetryAfterTick = -1;
        public RoomTier BestRoomTier = RoomTier.Hallway;
        public bool RequiresPrivateRoom;

        public PatientData()
        {
        }

        public PatientData(int arrivedAtTick, float initialMarketValue, float initialMood, PatientType type)
        {
            this.ArrivedAtTick = arrivedAtTick;
            this.InitialMarketValue = initialMarketValue;
            this.Type = type;
            this.InitialMood = initialMood;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref this.ArrivedAtTick, "ArrivedAtTick", 0);
            Scribe_Values.Look(ref this.InitialMarketValue, "InitialMarketValue", 0f);
            Scribe_Values.Look(ref this.InitialMood, "InitialMood", 0f);
            Scribe_Values.Look(ref this.Type, "Type", PatientType.Test);
            Scribe_Values.Look(ref this.Bill, "Bill", 0f);
            Scribe_Values.Look(ref this.Cure, "Cure");
            Scribe_Values.Look(ref this.Diagnosis, "Diagnosis");
            Scribe_Values.Look(ref this.Dismissed, "Dismissed", false);
            Scribe_Values.Look(ref this.HasPendingSurgeryBill, "HasPendingSurgeryBill", false);
            Scribe_Defs.Look(ref this.CureRecipe, "CureRecipe");
            Scribe_Values.Look(ref this.BestDoctorSkill, "BestDoctorSkill", 0);
            Scribe_Values.Look(ref this.BestMedicinePotency, "BestMedicinePotency", 0f);
            Scribe_Values.Look(ref this.AdmitHealth, "AdmitHealth", -1f);
            Scribe_Values.Look(ref this.SurgeryAttempts, "SurgeryAttempts", 0);
            Scribe_Values.Look(ref this.NoFaultUntilTick, "NoFaultUntilTick", -1);
            Scribe_Values.Look(ref this.RetryAfterTick, "RetryAfterTick", -1);
            Scribe_Values.Look(ref this.BestRoomTier, "BestRoomTier", RoomTier.Hallway);
            Scribe_Values.Look(ref this.RequiresPrivateRoom, "RequiresPrivateRoom", false);
        }
    }
}
