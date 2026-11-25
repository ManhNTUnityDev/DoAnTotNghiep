using System.Collections.Generic;
using UnityEngine;
using ChaseGame.AI.Actions;
using ChaseGame.AI.BehaviourTree;

namespace ChaseGame.AI.Fsm
{
    // FSM chaser, behaviourally equivalent to ChaserAIBrain: Shoot / Chase / Search / Wander
    // with an explicit transition table. Pure FSM — every transition names its source state.
    public sealed class ChaserFsmBrain : FsmBrain
    {
        // Sweeping where a target was lost. Unlike the tree, a state object can hold its own
        // memory, so none of this reaches the shared blackboard.
        private sealed class SearchState : IFsmState
        {
            private Vector3 lastSeen;
            private List<Vector3> points;
            private float startedAt;
            private int index;
            private bool walkedToLastSeen;

            public string Name => "Search";

            public void Enter(Blackboard bb)
            {
                lastSeen = bb.Destination ?? bb.Self.transform.position;
                startedAt = bb.Now;
                index = 0;
                walkedToLastSeen = false;
                points = ChaserActions.GenerateSearchPoints(
                    lastSeen,
                    bb.Config != null ? bb.Config.SearchPointCount : 4,
                    bb.Config != null ? bb.Config.SearchRadius : 4f);
            }

            public void Tick(Blackboard bb)
            {
                if (!walkedToLastSeen)
                {
                    walkedToLastSeen = true;
                    bb.ActiveBehaviour = BehaviourId.Search;
                    bb.Destination = lastSeen;
                    bb.Self.MoveTo(lastSeen);
                    return;
                }

                int i = index;
                ChaserActions.SearchStep(bb, points, ref i);
                index = i;
            }

            public void Exit(Blackboard bb)
            {
            }

            // Out of time, or every reachable sweep point visited.
            public bool IsOver(Blackboard bb)
            {
                float duration = bb.Config != null ? bb.Config.SearchDurationSeconds : 6f;
                if (bb.Now - startedAt >= duration)
                {
                    return true;
                }

                return walkedToLastSeen && (points == null || index >= points.Count);
            }
        }

        protected override StateMachine BuildMachine()
        {
            var shoot = new ActionState("Shoot", ChaserActions.AimAndShoot);
            var chase = new ActionState("Chase", ChaserActions.ChaseTarget);
            var search = new SearchState();
            var wander = new ActionState("Wander", AIActions.Wander);

            // Order matters: losing a target prefers Search over Wander, and a target coming
            // back always wins. This reproduces the tree's Shoot > Chase > Search > Wander
            // priority from every state.
            var transitions = new List<FsmTransition>
            {
                new FsmTransition(wander, ChaserActions.CanShoot, shoot),
                new FsmTransition(wander, ChaseNotShoot, chase),

                new FsmTransition(chase, ChaserActions.CanShoot, shoot),
                new FsmTransition(chase, CanStartSearch, search),
                new FsmTransition(chase, NoTarget, wander),

                new FsmTransition(shoot, ChaseNotShoot, chase),
                new FsmTransition(shoot, CanStartSearch, search),
                new FsmTransition(shoot, NoTarget, wander),

                new FsmTransition(search, ChaserActions.CanShoot, shoot),
                new FsmTransition(search, ChaseNotShoot, chase),
                new FsmTransition(search, search.IsOver, wander),
            };

            return new StateMachine(wander, transitions);
        }

        private static bool ChaseNotShoot(Blackboard bb) => ChaserActions.HasTarget(bb) && !ChaserActions.CanShoot(bb);

        private static bool NoTarget(Blackboard bb) => !ChaserActions.HasTarget(bb);

        // A sweep needs somewhere to sweep: the point where the target was last seen.
        private static bool CanStartSearch(Blackboard bb) => !ChaserActions.HasTarget(bb) && bb.Destination.HasValue;
    }
}
