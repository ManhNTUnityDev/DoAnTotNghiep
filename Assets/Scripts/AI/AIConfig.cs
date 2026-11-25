using UnityEngine;

namespace ChaseGame.AI
{
    [CreateAssetMenu(menuName = "ChaseGame/AI Config", fileName = "AIConfig")]
    public class AIConfig : ScriptableObject
    {
        [SerializeField] private float visionRadius = 12f;
        [SerializeField] private float shootRange = 8f;
        [SerializeField] private float threatRadius = 6f;
        [SerializeField] private float wanderRadius = 6f;
        [SerializeField] private float tickRateHz = 8f;

        [Header("Chaser search (after losing a target)")]
        [SerializeField] private float searchDurationSeconds = 6f;
        [SerializeField] private int searchPointCount = 4;
        [SerializeField] private float searchRadius = 4f;

        [Header("Runner cover")]
        [SerializeField] private float coverSearchRadius = 8f;
        [SerializeField] private int coverSampleCount = 12;

        public float VisionRadius => visionRadius;
        public float ShootRange => shootRange;
        public float ThreatRadius => threatRadius;
        public float WanderRadius => wanderRadius;
        public float TickRateHz => tickRateHz;
        public float SearchDurationSeconds => searchDurationSeconds;
        public int SearchPointCount => searchPointCount;
        public float SearchRadius => searchRadius;
        public float CoverSearchRadius => coverSearchRadius;
        public int CoverSampleCount => coverSampleCount;
    }
}
