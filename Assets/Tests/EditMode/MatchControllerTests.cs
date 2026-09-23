using NUnit.Framework;
using UnityEngine;
using ChaseGame.Match;

namespace ChaseGame.Tests
{
    public class MatchControllerTests
    {
        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f; // never leak a frozen clock to other tests
        }

        [Test]
        public void Quit_ResetsTimeScaleToOne()
        {
            // A match ends with Time.timeScale = 0 (frozen). Quitting must restore it,
            // else the next editor play session inherits timeScale 0 and stays frozen.
            var go = new GameObject("MatchControllerUnderTest");
            var mc = go.AddComponent<MatchController>();

            Time.timeScale = 0f;
            mc.Quit();

            Assert.AreEqual(1f, Time.timeScale);

            Object.DestroyImmediate(go);
        }
    }
}
