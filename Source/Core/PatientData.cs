using System.Collections.Generic;
using System.Linq;
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

        // Itemised bill (M3/M9). Bill above stays the running subtotal (sum of the lines) for older code and saves.
        public List<InvoiceLine> Lines = new List<InvoiceLine>();

        // Caretaker (M5)
        public int ImpossibleSinceTick = -1;
        public bool ImpossibleLetterSent;
        public int DangerHoldSinceTick = -1;
        public bool PaidOnDischarge;

        // Luciferium critical case (needs Luciferium Overhaul, area off by default)
        public bool CriticalCase;

        /// <summary>Adds a charge and keeps <see cref="Bill"/> equal to the sum of all lines.</summary>
        public void AddLine(InvoiceLineKind kind, string detail, float amount)
        {
            this.EnsureLines(false);
            if (amount <= 0f) return;
            this.Lines.Add(new InvoiceLine { kind = kind, detail = detail, amount = amount, tick = Find.TickManager?.TicksGame ?? 0 });
            this.Bill = this.Lines.Sum(l => l.amount);
        }

        /// <summary>
        /// Turns a bare <see cref="Bill"/> into lines: the admission fee set when the case is generated, or (fromSave) the total
        /// of an older save that had no lines yet.
        /// </summary>
        public void EnsureLines(bool fromSave)
        {
            if (this.Lines == null) this.Lines = new List<InvoiceLine>();
            this.Lines.RemoveAll(l => l == null);
            if (this.Lines.Count == 0 && this.Bill > 0f)
            {
                this.Lines.Add(new InvoiceLine { kind = fromSave ? InvoiceLineKind.Legacy : InvoiceLineKind.Admission, detail = this.Diagnosis, amount = this.Bill, tick = this.ArrivedAtTick });
            }
        }

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
            Scribe_Collections.Look(ref this.Lines, "invoiceLines", LookMode.Deep);
            Scribe_Values.Look(ref this.ImpossibleSinceTick, "ImpossibleSinceTick", -1);
            Scribe_Values.Look(ref this.ImpossibleLetterSent, "ImpossibleLetterSent", false);
            Scribe_Values.Look(ref this.DangerHoldSinceTick, "DangerHoldSinceTick", -1);
            Scribe_Values.Look(ref this.PaidOnDischarge, "PaidOnDischarge", false);
            Scribe_Values.Look(ref this.CriticalCase, "CriticalCase", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) this.EnsureLines(true);
        }
    }
}
