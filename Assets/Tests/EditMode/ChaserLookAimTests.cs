using NUnit.Framework;
using UnityEngine;
using ChaseGame.Match;

namespace ChaseGame.Tests
{
    // The egg used to fly dead horizontal no matter where the player looked, because the aim was
    // flattened to y = 0 before it ever reached the Gun. ComputeLookAim is what stopped that.
    public class ChaserLookAimTests
    {
        [Test]
        public void LookingStraightAhead_AimsStraightAhead()
        {
            Vector3 aim = AbilityButtonView.ComputeLookAim(Vector3.forward, Vector3.forward);

            Assert.AreEqual(0f, aim.y, 1e-4f);
            Assert.AreEqual(1f, aim.z, 1e-4f);
        }

        [Test]
        public void LookingDown_AimsDown()
        {
            Vector3 cameraForward = Quaternion.Euler(30f, 0f, 0f) * Vector3.forward;

            Vector3 aim = AbilityButtonView.ComputeLookAim(cameraForward, Vector3.forward);

            Assert.Less(aim.y, -0.4f, "a 30 degree downward look must aim downward");
        }

        [Test]
        public void LookingUp_AimsUp()
        {
            Vector3 cameraForward = Quaternion.Euler(-30f, 0f, 0f) * Vector3.forward;

            Vector3 aim = AbilityButtonView.ComputeLookAim(cameraForward, Vector3.forward);

            Assert.Greater(aim.y, 0.4f, "a 30 degree upward look must aim upward");
        }

        [Test]
        public void VerticalComponentIsKeptNotFlattened()
        {
            Vector3 cameraForward = new Vector3(0f, 1f, 1f);

            Vector3 aim = AbilityButtonView.ComputeLookAim(cameraForward, Vector3.forward);

            Assert.AreEqual(Mathf.Sqrt(0.5f), aim.y, 1e-4f);
        }

        [Test]
        public void AimIsAlwaysNormalized()
        {
            Vector3 aim = AbilityButtonView.ComputeLookAim(new Vector3(0f, 7f, 7f), Vector3.forward);

            Assert.AreEqual(1f, aim.magnitude, 1e-4f);
        }

        // Camera.main can be missing in a freshly loaded scene; the shot should still leave the
        // body rather than firing along a zero vector.
        [Test]
        public void NoCamera_FallsBackToBodyFacing()
        {
            Vector3 body = new Vector3(1f, 0f, 0f);

            Vector3 aim = AbilityButtonView.ComputeLookAim(Vector3.zero, body);

            Assert.AreEqual(body, aim);
        }

        [Test]
        public void NoCameraAndNoFacing_FallsBackToWorldForward()
        {
            Vector3 aim = AbilityButtonView.ComputeLookAim(Vector3.zero, Vector3.zero);

            Assert.AreEqual(Vector3.forward, aim);
        }
    }
}
