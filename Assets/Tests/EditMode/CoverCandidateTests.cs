using NUnit.Framework;
using UnityEngine;
using ChaseGame.AI.Actions;

namespace ChaseGame.Tests
{
    public class CoverCandidateTests
    {
        [Test]
        public void Offsets_SpanHalfRadiusToRadius()
        {
            var offsets = RunnerActions.CoverCandidateOffsets(12, 8f);

            Assert.AreEqual(12, offsets.Count);
            float min = float.MaxValue, max = 0f;
            foreach (var o in offsets)
            {
                float d = new Vector2(o.x, o.z).magnitude;
                min = Mathf.Min(min, d);
                max = Mathf.Max(max, d);
                Assert.AreEqual(0f, o.y, 1e-4f, "candidates stay on the plane");
            }

            Assert.AreEqual(4f, min, 0.2f, "nearest candidate should sit at half the radius");
            Assert.AreEqual(8f, max, 0.2f, "furthest candidate should sit at the radius");
        }

        [Test]
        public void Offsets_CoverDistinctDirections()
        {
            var offsets = RunnerActions.CoverCandidateOffsets(12, 8f);

            for (int i = 0; i < offsets.Count; i++)
            {
                for (int j = i + 1; j < offsets.Count; j++)
                {
                    Assert.Greater((offsets[i] - offsets[j]).magnitude, 0.5f, $"candidates {i} and {j} coincide");
                }
            }
        }

        [Test]
        public void Offsets_ShortCoverIsReachable()
        {
            // The behavioural point: a wall a few metres away must be usable as cover.
            var offsets = RunnerActions.CoverCandidateOffsets(12, 8f);

            int near = 0;
            foreach (var o in offsets)
            {
                if (new Vector2(o.x, o.z).magnitude <= 5f) near++;
            }

            Assert.Greater(near, 0, "no candidate within 5 m, so nearby cover could never be used");
        }

        [Test]
        public void Offsets_DegenerateInput_IsEmpty()
        {
            Assert.AreEqual(0, RunnerActions.CoverCandidateOffsets(0, 8f).Count);
            Assert.AreEqual(0, RunnerActions.CoverCandidateOffsets(12, 0f).Count);
        }
    }
}
