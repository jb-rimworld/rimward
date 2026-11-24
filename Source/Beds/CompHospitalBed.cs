using System.Collections.Generic;
using RimWard.Utilities;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWard
{
    public class CompHospitalBed : ThingComp
    {
        private bool hospitalInt;
        private bool surgeryInt;

        public override void PostPostMake()
        {
            base.PostPostMake();
            this.hospitalInt = !BedKinds.IsMedPod(this.parent.def);
        }

        public bool Hospital
        {
            get => this.hospitalInt;
            set
            {
                if (value == this.hospitalInt) return;
                this.RemoveAllOwners();
                this.hospitalInt = value;
                this.parent.Notify_ColorChanged();
            }
        }

        public bool Surgery
        {
            get => this.surgeryInt;
            set
            {
                if (value == this.surgeryInt) return;
                if (value) this.hospitalInt = true;
                this.surgeryInt = value;
                this.parent.Notify_ColorChanged();
            }
        }

        private void RemoveAllOwners(bool destroyed = false)
        {
            Building_Bed bed = (Building_Bed)this.parent;
            for (int i = bed.OwnersForReading.Count - 1; i >= 0; i--)
            {
                Pawn pawn = bed.OwnersForReading[i];
                if ((!this.hospitalInt && pawn.IsPatient(out _, false)) || (this.hospitalInt && !pawn.IsPatient(out _, false)))
                {
                    pawn.ownership.UnclaimBed();
                    string key = destroyed ? "MessageBedDestroyed" : "MessageBedLostAssignment";
                    Messages.Message(key.Translate(this.parent.def, pawn), new LookTargets(new TargetInfo[] { this.parent, pawn }), MessageTypeDefOf.CautionInput, false);
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref this.hospitalInt, "hospitalInt", true);
            Scribe_Values.Look(ref this.surgeryInt, "surgeryInt", false);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra())
            {
                yield return g;
            }
            Building_Bed bed = this.parent as Building_Bed;
            if (bed == null) yield break;

            // Hearth & Harbor protocol (N20): one combined "Use: Colonist / Guest / Patient" selector replaces the toggles below.
            if (BedSelector.HidesOwnToggles(bed))
            {
                Gizmo selector = BedSelector.MakeGizmo(bed);
                if (selector != null) yield return selector;
                yield break;
            }
            if (!bed.Medical) yield break;

            Texture2D icon = ContentFinder<Texture2D>.Get("UI/RimWard/Icon", false);
            yield return new Command_Toggle
            {
                defaultLabel = "CommandBedSetAsHospitalLabel".Translate(),
                defaultDesc = "CommandBedSetAsHospitalDesc".Translate(),
                icon = icon,
                isActive = () => this.Hospital,
                toggleAction = () => this.Hospital = !this.Hospital
            };
            if (!BedKinds.IsMedPod(this.parent.def))
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "CommandBedSetAsSurgeryLabel".Translate(),
                    defaultDesc = "CommandBedSetAsSurgeryDesc".Translate(),
                    icon = icon,
                    isActive = () => this.Surgery,
                    toggleAction = () => this.Surgery = !this.Surgery
                };
            }
        }
    }
}
