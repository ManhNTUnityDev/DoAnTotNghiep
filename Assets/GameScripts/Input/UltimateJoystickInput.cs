using UnityEngine;
using ChaseGame.Input;

namespace ChaseGame.Glue
{
    /// <summary>
    /// Adapter that exposes the third-party <see cref="UltimateJoystick"/> to the game
    /// through ChaseGame's <see cref="IJoystickInput"/> port.
    ///
    /// Lives in Assembly-CSharp on purpose: the UltimateJoystick asset has no asmdef, so
    /// only Assembly-CSharp can reference it. Assembly-CSharp auto-references ChaseGame,
    /// so this class can implement IJoystickInput while ChaseGame stays free of the asset.
    /// Registered into DI by interface via RegisterComponentInHierarchy&lt;IJoystickInput&gt;().
    /// </summary>
    public class UltimateJoystickInput : MonoBehaviour, IJoystickInput
    {
        [SerializeField]
        [Tooltip("Must match the Joystick Name set on the UltimateJoystick component.")]
        private string joystickName = "MoveJoystick";

        // UltimateJoystick only registers its name in Awake() during play. Outside play
        // mode the static lookup would log "no joystick registered", so short-circuit.
        public bool Active => Application.isPlaying && UltimateJoystick.GetInputActive(joystickName);

        public Vector2 Axis => Application.isPlaying
            ? new Vector2(
                UltimateJoystick.GetHorizontalAxis(joystickName),
                UltimateJoystick.GetVerticalAxis(joystickName))
            : Vector2.zero;
    }
}
