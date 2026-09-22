using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ChaseGame.AI;
using ChaseGame.AI.BehaviourTree;
using ChaseGame.Characters;
using ChaseGame.Match;

namespace ChaseGame.Tests
{
    public class SensorSelectionTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [Test]
        public void Blackboard_HoldsConfigAndTargets()
        {
            var config = ScriptableObject.CreateInstance<AIConfig>();
            var bb = new Blackboard { Config = config };

            Assert.AreEqual(8f, bb.Config.TickRateHz, 1e-4f); // default from AIConfig
            Assert.IsNull(bb.CurrentTarget);

            Object.DestroyImmediate(config);
        }

        private Transform NewAt(Vector3 pos)
        {
            var go = new GameObject("t");
            go.transform.position = pos;
            spawned.Add(go);
            return go.transform;
        }

        private Character MakeCharacter(Team team, CaptureState state)
        {
            var go = new GameObject(team.ToString());
            spawned.Add(go);
            var c = go.AddComponent<Character>();
            c.SetTeamForTests(team);
            c.CaptureState = state;
            return c;
        }

        [TearDown]
        public void TearDownObjects()
        {
            foreach (var go in spawned) Object.DestroyImmediate(go);
            spawned.Clear();
        }

        [Test]
        public void NearestVisible_PicksClosestWithinRadius()
        {
            var from = Vector3.zero;
            var near = NewAt(new Vector3(2f, 0f, 0f));
            var far = NewAt(new Vector3(5f, 0f, 0f));
            var cands = new List<Sensor.Candidate>
            {
                new Sensor.Candidate(far, far.position),
                new Sensor.Candidate(near, near.position),
            };

            var picked = Sensor.NearestVisible(from, cands, radius: 10f, hasLineOfSight: (_, __) => true);
            Assert.AreSame(near, picked);
        }

        [Test]
        public void NearestVisible_IgnoresBeyondRadius()
        {
            var from = Vector3.zero;
            var far = NewAt(new Vector3(20f, 0f, 0f));
            var cands = new List<Sensor.Candidate> { new Sensor.Candidate(far, far.position) };

            var picked = Sensor.NearestVisible(from, cands, radius: 10f, hasLineOfSight: (_, __) => true);
            Assert.IsNull(picked);
        }

        [Test]
        public void NearestVisible_SkipsBlockedAndTakesNextVisible()
        {
            var from = Vector3.zero;
            var blockedNear = NewAt(new Vector3(2f, 0f, 0f));
            var visibleFar = NewAt(new Vector3(4f, 0f, 0f));
            var cands = new List<Sensor.Candidate>
            {
                new Sensor.Candidate(blockedNear, blockedNear.position),
                new Sensor.Candidate(visibleFar, visibleFar.position),
            };

            // LoS blocked only for the near one.
            bool Los(Vector3 a, Vector3 target) => target != blockedNear.position;

            var picked = Sensor.NearestVisible(from, cands, radius: 10f, hasLineOfSight: Los);
            Assert.AreSame(visibleFar, picked);
        }

        [Test]
        public void NearestVisible_EmptyList_ReturnsNull()
        {
            var picked = Sensor.NearestVisible(Vector3.zero, new List<Sensor.Candidate>(), 10f, (_, __) => true);
            Assert.IsNull(picked);
        }

        [Test]
        public void CandidatesFor_Chaser_ReturnsOnlyFreeRunners()
        {
            var roster = new TeamRoster();
            var chaser = MakeCharacter(Team.Chaser, CaptureState.Free);
            var freeRunner = MakeCharacter(Team.Runner, CaptureState.Free);
            var jailedRunner = MakeCharacter(Team.Runner, CaptureState.Jailed);
            roster.Add(chaser); roster.Add(freeRunner); roster.Add(jailedRunner);

            var cands = Sensor.CandidatesFor(Team.Chaser, roster);
            Assert.AreEqual(1, cands.Count);
            Assert.AreSame(freeRunner.transform, cands[0].Transform);
        }

        [Test]
        public void CandidatesFor_Runner_ReturnsChasers()
        {
            var roster = new TeamRoster();
            var chaser = MakeCharacter(Team.Chaser, CaptureState.Free);
            var runner = MakeCharacter(Team.Runner, CaptureState.Free);
            roster.Add(chaser); roster.Add(runner);

            var cands = Sensor.CandidatesFor(Team.Runner, roster);
            Assert.AreEqual(1, cands.Count);
            Assert.AreSame(chaser.transform, cands[0].Transform);
        }
    }
}
