using NUnit.Framework;
using ChaseGame.Match;

namespace ChaseGame.Tests
{
    public class MatchClockTests
    {
        [Test] public void Format_NinetySeconds_OneThirty() => Assert.AreEqual("1:30", MatchClock.Format(90f));
        [Test] public void Format_FiveSeconds_PadsSeconds()  => Assert.AreEqual("0:05", MatchClock.Format(5f));
        [Test] public void Format_TwoMinutesFive()           => Assert.AreEqual("2:05", MatchClock.Format(125f));
        [Test] public void Format_Zero_IsZeroZeroZero()      => Assert.AreEqual("0:00", MatchClock.Format(0f));
        [Test] public void Format_Negative_ClampsToZero()    => Assert.AreEqual("0:00", MatchClock.Format(-3.2f));
        [Test] public void Format_Fractional_FloorsSeconds() => Assert.AreEqual("0:05", MatchClock.Format(5.9f));
    }
}
