using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimWard
{
    // Ensures the travel/exit toil has a destination. Reimplemented cleanly with RCellFinder
    // (the original used an obfuscated flood-fill; the best-exit-spot search is equivalent and robust).
    public class TransitionAction_EnsureHaveNearbyExitDestination : TransitionAction
    {
        public override void DoAction(Transition trans)
        {
            LordToil_Travel toil = (LordToil_Travel)trans.target;
            if (toil.HasDestination())
            {
                return;
            }
            Pawn searcher = toil.lord.ownedPawns.RandomElement();
            if (searcher == null)
            {
                return;
            }
            if (!RCellFinder.TryFindBestExitSpot(searcher, out IntVec3 exit, TraverseMode.ByPawn))
            {
                RCellFinder.TryFindRandomPawnEntryCell(out exit, searcher.Map, 0f);
            }
            toil.SetDestination(exit);
        }
    }
}
