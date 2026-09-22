using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.Match;

namespace ChaseGame.Tests
{
    public class TeamRosterTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        private Character NewCharacter(Team team, CaptureState state = CaptureState.Free)
        {
            var go = new GameObject(team.ToString());
            spawned.Add(go);
            var c = go.AddComponent<Character>();
            c.SetTeamForTests(team);
            c.CaptureState = state;
            return c;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned) Object.DestroyImmediate(go);
            spawned.Clear();
        }

        [Test]
        public void Add_RoutesByTeam()
        {
            var roster = new TeamRoster();
            var chaser = NewCharacter(Team.Chaser);
            var runner = NewCharacter(Team.Runner);

            roster.Add(chaser);
            roster.Add(runner);

            CollectionAssert.Contains(roster.Chasers, chaser);
            CollectionAssert.Contains(roster.Runners, runner);
            Assert.AreEqual(1, roster.Chasers.Count);
            Assert.AreEqual(1, roster.Runners.Count);
        }

        [Test]
        public void GetFreeRunners_ExcludesJailed()
        {
            var roster = new TeamRoster();
            var free = NewCharacter(Team.Runner, CaptureState.Free);
            var jailed = NewCharacter(Team.Runner, CaptureState.Jailed);
            roster.Add(free);
            roster.Add(jailed);

            CollectionAssert.AreEquivalent(new[] { free }, roster.GetFreeRunners());
            CollectionAssert.AreEquivalent(new[] { jailed }, roster.GetJailedRunners());
        }

        [Test]
        public void AllRunnersJailed_TrueOnlyWhenEveryRunnerJailed()
        {
            var roster = new TeamRoster();
            var r1 = NewCharacter(Team.Runner, CaptureState.Jailed);
            var r2 = NewCharacter(Team.Runner, CaptureState.Jailed);
            roster.Add(r1);
            roster.Add(r2);
            Assert.IsTrue(roster.AllRunnersJailed());

            r2.CaptureState = CaptureState.Free;
            Assert.IsFalse(roster.AllRunnersJailed());
        }

        [Test]
        public void AllRunnersJailed_FalseWhenNoRunners()
        {
            var roster = new TeamRoster();
            roster.Add(NewCharacter(Team.Chaser));
            Assert.IsFalse(roster.AllRunnersJailed());
        }
    }
}
