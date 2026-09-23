using NUnit.Framework;
using ChaseGame.Abilities;

namespace ChaseGame.Tests
{
    public class CooldownTimerTests
    {
        [Test]
        public void IsReady_InitiallyTrue()
        {
            var timer = new CooldownTimer(1f);
            Assert.IsTrue(timer.IsReady(0f));
        }

        [Test]
        public void AfterTrigger_NotReadyUntilCooldownElapses()
        {
            var timer = new CooldownTimer(1f);
            timer.Trigger(10f);

            Assert.IsFalse(timer.IsReady(10.5f));
            Assert.IsTrue(timer.IsReady(11f));
            Assert.IsTrue(timer.IsReady(12f));
        }
    }
}
