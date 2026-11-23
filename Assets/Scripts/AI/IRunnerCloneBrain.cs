using UnityEngine;

namespace ChaseGame.AI
{
    // A runner brain that can drive a Decoy clone (whatever its decision architecture).
    public interface IRunnerCloneBrain
    {
        bool IsClone { get; }
        void ConfigureAsClone(Vector3 initialMoveDirection, float fleeAngleBias = 50f);
    }
}
