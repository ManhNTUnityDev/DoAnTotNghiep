using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ChaseGame.Characters;

namespace ChaseGame.Match
{
    // Consequence of a successful shot: jail the runner for the rest of the match and
    // drop a cage where it happened. Replaces CaptureServiceStub; there is no rescue, so
    // a cage never releases its occupant and the roster's free-runner count only shrinks.
    public sealed class JailService : ICaptureService
    {
        private const float GroundSnapRadius = 2f;

        private readonly List<Cage> cages = new List<Cage>();
        private readonly Cage cagePrefab;

        // cagePrefab may be null — Cage then builds a primitive stand-in.
        public JailService(Cage cagePrefab = null)
        {
            this.cagePrefab = cagePrefab;
        }

        public IReadOnlyList<Cage> ActiveCages => cages;
        public event Action<Character> OnRunnerCaught;

        public void Capture(Character runner)
        {
            if (runner == null || runner.Team != Team.Runner || runner.CaptureState == CaptureState.Jailed)
            {
                return;
            }

            // The setter stops the body and flips CanAct to false, which also parks the
            // NavMeshAgent (Stop() resets the path and returns to Direction mode).
            runner.CaptureState = CaptureState.Jailed;

            Vector3 cagePosition = SnapToGround(runner.transform.position);
            Cage cage = Cage.Spawn(cagePrefab, cagePosition, runner.transform.rotation);
            cage.Bind(runner);
            cages.Add(cage);

            OnRunnerCaught?.Invoke(runner);
        }

        // Keeps a cage from floating when the runner is caught mid-slope or mid-step.
        private static Vector3 SnapToGround(Vector3 position)
            => NavMesh.SamplePosition(position, out var hit, GroundSnapRadius, NavMesh.AllAreas)
                ? hit.position
                : position;
    }
}
