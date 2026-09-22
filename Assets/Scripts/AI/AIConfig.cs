using UnityEngine;

namespace ChaseGame.AI
{
    [CreateAssetMenu(menuName = "ChaseGame/AI Config", fileName = "AIConfig")]
    public class AIConfig : ScriptableObject
    {
        [SerializeField] private float visionRadius = 12f;
        [SerializeField] private float shootRange = 8f;
        [SerializeField] private float threatRadius = 6f;
        [SerializeField] private float rescueThreatRadius = 5f;
        [SerializeField] private float wanderRadius = 6f;
        [SerializeField] private float tickRateHz = 8f;

        public float VisionRadius => visionRadius;
        public float ShootRange => shootRange;
        public float ThreatRadius => threatRadius;
        public float RescueThreatRadius => rescueThreatRadius;
        public float WanderRadius => wanderRadius;
        public float TickRateHz => tickRateHz;
    }
}
