// SPDX-License-Identifier: GPL-3.0-only
// RimWard - Hospital for Profit. Copyright (C) 2026 juliusberger00. Written for RimWard 2.0 (a GPL-3.0 fork of Hospital by Adamas and
// Hospital Fix And Tweak by assssssqwww). Licensed under the GNU General Public License version 3 (LICENSE);
// full source: https://github.com/jb-rimworld/rimward
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimWard
{
    /// <summary>
    /// "Treatment impossible": the caretaker found a pending cure that cannot be done (part gone, no surgeon, missing ingredient, refused).
    /// Discharge now (partial pay, no penalty) or keep waiting (the clock starts again). Without an answer the caretaker discharges after the same time again.
    /// </summary>
    public class ChoiceLetter_TreatmentImpossible : ChoiceLetter
    {
        public Pawn patient;
        public int mapId = -1;

        public static void Send(HospitalMapComponent comp, Pawn pawn, PatientData data, string why, int ticksUntilDischarge)
        {
            LetterDef def = DefDatabase<LetterDef>.GetNamedSilentFail("RimWard_TreatmentImpossible") ?? LetterDefOf.NegativeEvent;
            TaggedString label = "RimWard.Impossible.Label".Translate(pawn.LabelShortCap);
            TaggedString text = "RimWard.Impossible.Text".Translate(pawn.LabelShortCap, data.Cure ?? "?", why, ticksUntilDischarge.ToStringTicksToPeriod());
            ChoiceLetter letter = LetterMaker.MakeLetter(label, text, def, new LookTargets(pawn));
            if (letter is ChoiceLetter_TreatmentImpossible own)
            {
                own.patient = pawn;
                own.mapId = comp.map.uniqueID;
            }
            Find.LetterStack.ReceiveLetter(letter);
        }

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (this.ArchivedOnly)
                {
                    yield return this.Option_Close;
                    yield break;
                }
                yield return new DiaOption("RimWard.Impossible.Discharge".Translate())
                {
                    action = () => this.Resolve(true),
                    resolveTree = true
                };
                yield return new DiaOption("RimWard.Impossible.Wait".Translate())
                {
                    action = () => this.Resolve(false),
                    resolveTree = true
                };
                yield return this.Option_Postpone;
            }
        }

        private void Resolve(bool discharge)
        {
            HospitalMapComponent comp = Find.Maps.FirstOrDefault(m => m.uniqueID == this.mapId)?.GetComponent<HospitalMapComponent>()
                ?? HospitalMapComponent.For(this.patient?.MapHeld);
            if (comp != null && this.patient != null && comp.GetPatientData(this.patient, out PatientData data) && !data.Dismissed)
            {
                if (discharge) comp.Discharge(this.patient, data, DischargeReason.TreatmentImpossible);
                else
                {
                    data.ImpossibleSinceTick = Find.TickManager.TicksGame;
                    data.ImpossibleLetterSent = false;
                }
            }
            Find.LetterStack.RemoveLetter(this);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref this.patient, "patient");
            Scribe_Values.Look(ref this.mapId, "mapId", -1);
        }
    }
}
