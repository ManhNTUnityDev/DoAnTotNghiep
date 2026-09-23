using NUnit.Framework;
using ChaseGame.Characters;
using ChaseGame.Infrastructure;

namespace ChaseGame.Tests
{
    public class SpawnManagerTests
    {
        [Test]
        public void PickPlayerTeam_LowRoll_IsChaser()
        {
            Assert.AreEqual(Team.Chaser, SpawnManager.PickPlayerTeam(0f));
            Assert.AreEqual(Team.Chaser, SpawnManager.PickPlayerTeam(0.49f));
        }

        [Test]
        public void PickPlayerTeam_HighRoll_IsRunner()
        {
            Assert.AreEqual(Team.Runner, SpawnManager.PickPlayerTeam(0.5f));
            Assert.AreEqual(Team.Runner, SpawnManager.PickPlayerTeam(0.99f));
        }
    }
}
