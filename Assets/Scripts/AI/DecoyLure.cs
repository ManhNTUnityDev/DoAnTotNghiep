using System.Collections.Generic;
using UnityEngine;
using ChaseGame.Characters;

namespace ChaseGame.AI
{
    // A temporary fake target / clone spawned by a runner's Decoy ability.
    // Chaser perception treats active lures as candidate targets, so nearby chasers
    // are pulled toward the moving clone while the real runner escapes.
    public sealed class DecoyLure : MonoBehaviour
    {
        // Active lures in the scene; Sensor and AimAssist read this for chaser targeting.
        public static readonly List<DecoyLure> Active = new List<DecoyLure>();

        private float dieAt = -1f;

        public Character Character { get; private set; }
        public float RemainingLifetime => dieAt > 0f ? Mathf.Max(0f, dieAt - Time.time) : 0f;

        private void Awake()
        {
            Character = GetComponent<Character>();
        }

        public void SetLifetime(float seconds)
        {
            dieAt = Time.time + Mathf.Max(0.05f, seconds);
            if (Character == null)
            {
                Character = GetComponent<Character>();
            }

            if (gameObject.activeInHierarchy && enabled && !Active.Contains(this))
            {
                Active.Add(this);
            }
        }

        // Immediately removes and destroys the clone (e.g. when shot by a Chaser's Gun).
        public void Dismiss()
        {
            Active.Remove(this);
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }

        private void OnEnable()
        {
            if (Character == null)
            {
                Character = GetComponent<Character>();
            }

            if (!Active.Contains(this))
            {
                Active.Add(this);
            }
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        private void OnDestroy()
        {
            Active.Remove(this);
        }

        private void Update()
        {
            if (dieAt > 0f && Time.time >= dieAt)
            {
                Dismiss();
            }
        }
    }
}
