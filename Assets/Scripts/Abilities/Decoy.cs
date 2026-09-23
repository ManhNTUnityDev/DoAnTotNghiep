using UnityEngine;
using UnityEngine.AI;
using ChaseGame.Characters;
using ChaseGame.AI;

namespace ChaseGame.Abilities
{
    // Runner ability: drop a lure that pulls chasers away. The lure is placed on the
    // opposite side of the flee direction (toward where the runner came from), so a
    // pursuing chaser is drawn back while the runner escapes.
    public sealed class Decoy : MonoBehaviour, IAbility
    {
        [SerializeField] private float cooldown = 8f;
        [SerializeField] private float lifetime = 4f;
        [SerializeField] private float distance = 5f;

        private CooldownTimer timer;

        private void Awake()
        {
            timer = new CooldownTimer(cooldown);
        }

        public bool IsReady => timer == null || timer.IsReady(Time.time);

        public void Use(Character self, Vector3 aim)
        {
            if (timer != null && !timer.IsReady(Time.time))
            {
                return;
            }

            timer?.Trigger(Time.time);

            Vector3 fleeDir = aim.sqrMagnitude > 1e-4f ? aim.normalized : self.transform.forward;
            Vector3 pos = self.transform.position - fleeDir * distance;
            if (NavMesh.SamplePosition(pos, out var hit, distance, NavMesh.AllAreas))
            {
                pos = hit.position;
            }

            var go = new GameObject("DecoyLure");
            go.transform.position = pos;
            go.AddComponent<DecoyLure>().SetLifetime(lifetime);
        }
    }
}
