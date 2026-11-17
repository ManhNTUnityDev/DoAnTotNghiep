using UnityEngine;

namespace ChaseGame.Characters
{
    /// <summary>
    /// Picks which of a character's two visual models survives: the first-person view model
    /// (wings + legs only, framed to be seen from inside) or the world model (full body, what
    /// everyone else sees). Both children are authored on the prefab; exactly one is kept.
    ///
    /// SpawnManager calls <see cref="Apply"/> right after spawning. SpawnManager is not the only
    /// way a Character comes into existence, though - CharacterFactory.Spawn is the real
    /// instantiation choke point and Decoy.Use clones a whole character GameObject - so Apply is
    /// idempotent and Start falls back to the world visual if nobody chose. Without that fallback a
    /// character would keep BOTH visuals, which means two Animators ticking and two
    /// CharacterAnimationControllers subscribed to OnAbilityUsed, firing every ability animation
    /// twice.
    /// </summary>
    public sealed class CharacterVisualSelector : MonoBehaviour
    {
        [Tooltip("View model shown only to the human player controlling this character (wings + legs).")]
        [SerializeField] private GameObject firstPersonVisual;

        [Tooltip("World model everyone else sees (full body).")]
        [SerializeField] private GameObject worldVisual;

        private bool applied;

        public bool HasApplied => applied;
        public GameObject FirstPersonVisual => firstPersonVisual;
        public GameObject WorldVisual => worldVisual;

        // Injected by a test or an editor script that builds the prefab.
        public void Configure(GameObject firstPerson, GameObject world)
        {
            firstPersonVisual = firstPerson;
            worldVisual = world;
        }

        /// <summary>
        /// Pure decision: should the first-person view model be the one kept?
        /// Falls back to whichever visual actually exists when only one is wired.
        /// </summary>
        public static bool ShouldKeepFirstPerson(bool isLocalPlayer, bool hasFirstPerson, bool hasWorld)
        {
            if (!hasFirstPerson)
            {
                return false;
            }

            if (!hasWorld)
            {
                return true;
            }

            return isLocalPlayer;
        }

        /// <summary>
        /// Keeps one visual and removes the other. Safe to call more than once: the second call is
        /// a no-op rather than destroying the survivor.
        /// </summary>
        public void Apply(bool isLocalPlayer)
        {
            if (applied)
            {
                return;
            }

            applied = true;

            bool keepFirstPerson = ShouldKeepFirstPerson(
                isLocalPlayer,
                firstPersonVisual != null,
                worldVisual != null);

            RemoveVisual(keepFirstPerson ? worldVisual : firstPersonVisual);

            if (keepFirstPerson)
            {
                worldVisual = null;
            }
            else
            {
                firstPersonVisual = null;
            }
        }

        // Nobody chose (character built outside SpawnManager): collapse to the world model. A
        // full-bodied character is never *wrong*, it just isn't the first-person framing.
        private void Start()
        {
            if (!applied)
            {
                Apply(false);
            }
        }

        private void RemoveVisual(GameObject visual)
        {
            if (visual == null)
            {
                return;
            }

            // Deactivate before destroying so OnDisable unsubscribes this visual's
            // CharacterAnimationController from OnAbilityUsed in the same frame, instead of at the
            // end of it.
            visual.SetActive(false);

            if (Application.isPlaying)
            {
                Destroy(visual);
            }
            else
            {
                DestroyImmediate(visual);
            }
        }
    }
}
