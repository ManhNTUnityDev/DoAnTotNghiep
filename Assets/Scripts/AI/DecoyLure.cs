using System.Collections.Generic;
using UnityEngine;

namespace ChaseGame.AI
{
    // A temporary fake target dropped by a runner's Decoy. Chaser perception treats
    // active lures as candidate targets, so a nearby chaser is pulled toward it.
    public sealed class DecoyLure : MonoBehaviour
    {
        // Active lures in the scene; the Sensor reads this for chaser targeting.
        public static readonly List<DecoyLure> Active = new List<DecoyLure>();

        private float dieAt = -1f;

        public void SetLifetime(float seconds)
        {
            dieAt = Time.time + seconds;
        }

        private void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        private void Update()
        {
            if (dieAt > 0f && Time.time >= dieAt)
            {
                Destroy(gameObject);
            }
        }
    }
}
