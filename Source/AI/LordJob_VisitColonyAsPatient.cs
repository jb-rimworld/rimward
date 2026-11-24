using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimWard
{
    public class LordJob_VisitColonyAsPatient : LordJob
    {
        public LordJob_VisitColonyAsPatient()
        {
        }

        // Faction param is accepted for call-site parity but intentionally not stored
        // (the lord doesn't need it; this lets faction-independent "wanderer" patients work).
        public LordJob_VisitColonyAsPatient(Faction faction)
        {
        }

        public override bool NeverInRestraints => true;

        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = new StateGraph();
            StateGraph travelAndExit = new LordJob_TravelAndExit(IntVec3.Invalid).CreateGraph();
            LordToil_Patient patientToil = new LordToil_Patient();
            stateGraph.lordToils.Add(patientToil);
            LordToil exitStart = stateGraph.AttachSubgraph(travelAndExit).StartingToil;
            Transition transition = new Transition(patientToil, exitStart, false, true);
            transition.triggers.Add(new Trigger_SentAway());
            transition.preActions.Add(new TransitionAction_EnsureHaveNearbyExitDestination());
            transition.postActions.Add(new TransitionAction_WakeAll());
            stateGraph.transitions.Add(transition);
            return stateGraph;
        }

        public override bool ShouldRemovePawn(Pawn p, PawnLostCondition reason)
        {
            return reason != PawnLostCondition.Incapped;
        }

        public override void Notify_PawnLost(Pawn pawn, PawnLostCondition condition)
        {
            HospitalMapComponent hospital = HospitalMapComponent.For(this.lord?.Map) ?? HospitalMapComponent.For(pawn?.MapHeld);
            if (hospital == null || pawn == null) return;
            switch (condition)
            {
                case PawnLostCondition.MadePrisoner:
                case PawnLostCondition.MadeSlave:
                case PawnLostCondition.ChangedFaction:
                    // arrested, enslaved, recruited: forget the patient without touching the new status
                    hospital.ForgetPatient(pawn);
                    break;
                case PawnLostCondition.Killed:
                    break; // handled by the Kill patch (fault classification)
                default:
                    // vanished, left, taken over by another lord: the stay ends quietly (no pay, no message)
                    if (hospital.GetPatientData(pawn, out PatientData d) && !d.Dismissed) hospital.RemoveFromPatientList(pawn, true);
                    break;
            }
        }
    }
}
