using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using ChaseGame.Abilities;
using ChaseGame.AI;
using ChaseGame.Brains;
using ChaseGame.Characters;

namespace ChaseGame.Match
{
    /// <summary>
    /// Drives a HUD skill button (Chaser Gun, Runner SpeedBoost, or Runner Decoy).
    /// Binds to the human player's CharacterAbilities, triggers UseAbility on click
    /// (or keyboard shortcut), and renders radial/numeric cooldown feedback.
    /// </summary>
    public sealed class AbilityButtonView : MonoBehaviour
    {
        public enum RoleVisibility
        {
            AnyRole = 0,
            ChaserOnly = 1,
            RunnerOnly = 2,
        }

        [Header("Ability Binding")]
        [Tooltip("0 = Primary skill (Chaser Gun / Runner SpeedBoost), 1 = Secondary skill (Runner Decoy).")]
        [SerializeField] private int abilityIndex = 0;

        [Tooltip("Automatically shows/hides this button based on the player's randomly assigned team.")]
        [SerializeField] private RoleVisibility roleVisibility = RoleVisibility.AnyRole;

        [Header("UI References")]
        [SerializeField] private Button button;
        [Tooltip("Optional Image with Image Type = Filled (Radial 360) to display cooldown progress (1 -> 0).")]
        [SerializeField] private Image cooldownFillImage;
        [Tooltip("Optional Text to display remaining cooldown seconds (e.g. '4.2').")]
        [SerializeField] private Text cooldownText;
        [Tooltip("Optional highlight object enabled while a duration buff (like SpeedBoost) is active.")]
        [SerializeField] private GameObject activeBuffIndicator;

        [Header("Aiming & Shortcuts")]
        [Tooltip("Snap the shot to a nearby runner. Ignored while the player is in first person: " +
                 "the assist works on the horizontal plane only, so it would throw away the " +
                 "player's vertical aim and bend the egg away from the crosshair.")]
        [SerializeField] private bool enableChaserAimAssist = true;
        [SerializeField] private float aimAssistAngle = 40f;
        [SerializeField] private float aimAssistRange = 8f;
        [SerializeField] private bool enableKeyboardShortcut = true;

        private Character player;
        private CharacterAbilities playerAbilities;
        private ITeamRoster roster;
        private Transform cameraTransform;
        private bool attemptedAutoBind;

        public int AbilityIndex => abilityIndex;
        public RoleVisibility VisibilityRule => roleVisibility;

        private void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (button != null)
            {
                button.onClick.AddListener(TriggerAbility);
            }

            ResetCooldownVisuals();
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(TriggerAbility);
            }
        }

        /// <summary>
        /// Binds this button to the spawned human player character.
        /// Automatically toggles GameObject active state if the role doesn't match.
        /// </summary>
        public void Bind(Character playerCharacter, ITeamRoster teamRoster = null, Transform viewCamera = null)
        {
            player = playerCharacter;
            roster = teamRoster;
            cameraTransform = viewCamera;
            playerAbilities = player != null ? player.GetComponent<CharacterAbilities>() : null;

            if (player == null)
            {
                return;
            }

            bool visible = ShouldBeVisibleFor(
                player.Team,
                roleVisibility,
                abilityIndex,
                playerAbilities != null ? playerAbilities.Count : 2);

            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }

            RefreshVisuals();
        }

        public static bool ShouldBeVisibleFor(
            Team playerTeam,
            RoleVisibility rule,
            int index,
            int availableAbilityCount)
        {
            if (index < 0 || index >= availableAbilityCount)
            {
                return false;
            }

            if (rule == RoleVisibility.ChaserOnly && playerTeam != Team.Chaser)
            {
                return false;
            }

            if (rule == RoleVisibility.RunnerOnly && playerTeam != Team.Runner)
            {
                return false;
            }

            return true;
        }

        public static string FormatCooldown(float remainingSeconds)
        {
            if (remainingSeconds <= 0.05f)
            {
                return string.Empty;
            }

            return remainingSeconds >= 10f
                ? Mathf.CeilToInt(remainingSeconds).ToString()
                : remainingSeconds.ToString("0.0");
        }

        private bool IsFirstPersonActive()
        {
            if (player == null)
            {
                return false;
            }

            var view = player.GetComponent<ChaseGame.Infrastructure.FirstPersonView>();
            return view != null && view.IsActive;
        }

        /// <summary>
        /// Pure helper: the direction the egg should fly, taken straight from where the player is
        /// looking - vertical component included, so looking up or down actually aims up or down.
        /// Falls back to the body's facing when there is no camera to read.
        /// </summary>
        public static Vector3 ComputeLookAim(Vector3 cameraForward, Vector3 bodyForward)
        {
            if (cameraForward.sqrMagnitude > 1e-4f)
            {
                return cameraForward.normalized;
            }

            if (bodyForward.sqrMagnitude > 1e-4f)
            {
                return bodyForward.normalized;
            }

            return Vector3.forward;
        }

        /// <summary>
        /// Pure helper: chooses the best planar aim direction for a Chaser shot,
        /// snapping to the nearest free runner within the aim-assist cone (or close-range 360°).
        /// </summary>
        public static Vector3 ComputeChaserAimDirection(
            Vector3 shooterPosition,
            Vector3 defaultForward,
            IReadOnlyList<Character> freeRunners,
            float maxRange,
            float maxAngleDeg)
        {
            Vector3 baseForward = new Vector3(defaultForward.x, 0f, defaultForward.z);
            if (baseForward.sqrMagnitude < 1e-4f)
            {
                baseForward = Vector3.forward;
            }
            else
            {
                baseForward.Normalize();
            }

            if (freeRunners == null || freeRunners.Count == 0)
            {
                return baseForward;
            }

            Vector3 bestDir = baseForward;
            float bestScore = float.MaxValue;
            float maxRangeSqr = maxRange * maxRange;
            const float closeRangeAutoSnapSqr = 2.5f * 2.5f;

            foreach (var runner in freeRunners)
            {
                if (runner == null || runner.CaptureState != CaptureState.Free)
                {
                    continue;
                }

                Vector3 toRunner = runner.transform.position - shooterPosition;
                toRunner.y = 0f;
                float distSqr = toRunner.sqrMagnitude;
                if (distSqr < 1e-4f || distSqr > maxRangeSqr)
                {
                    continue;
                }

                Vector3 dir = toRunner.normalized;
                float angle = Vector3.Angle(baseForward, dir);

                // Allow either targets inside the forward cone or targets right next to the chaser.
                if (angle > maxAngleDeg && distSqr > closeRangeAutoSnapSqr)
                {
                    continue;
                }

                float score = Mathf.Sqrt(distSqr) + angle * 0.05f;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestDir = dir;
                }
            }

            return bestDir;
        }

        public void TriggerAbility()
        {
            TryAutoBindIfNeeded();
            if (player == null || !player.CanAct)
            {
                return;
            }

            if (!player.IsAbilityReady(abilityIndex))
            {
                return;
            }

            Vector3 aim = ResolveAimDirection();

            // No rotation snap here any more. In first person FirstPersonView owns the body's yaw
            // and the aim already comes from the camera, so the two agree by construction - and
            // writing the rotation from here would fight the view driver for one frame.
            player.UseAbility(abilityIndex, aim);
            RefreshVisuals();
        }

        private Vector3 ResolveAimDirection()
        {
            if (player == null)
            {
                return Vector3.forward;
            }

            if (player.Team == Team.Chaser && abilityIndex == 0)
            {
                if (cameraTransform == null && Camera.main != null)
                {
                    cameraTransform = Camera.main.transform;
                }

                Vector3 lookAim = ComputeLookAim(
                    cameraTransform != null ? cameraTransform.forward : Vector3.zero,
                    player.transform.forward);

                // Aim assist works on the horizontal plane by construction, so it cannot coexist
                // with vertical aim. Rather than relying on a serialized checkbox - the four
                // instances in Game.unity all have it ticked, and changing the field's default
                // would not touch them - the decision follows the camera mode: in first person the
                // egg goes exactly where the player looks, and if first person is ever switched
                // off the assist comes back on its own.
                if (enableChaserAimAssist && roster != null && !IsFirstPersonActive())
                {
                    Vector3 planarLook = new Vector3(lookAim.x, 0f, lookAim.z);
                    if (planarLook.sqrMagnitude < 1e-4f)
                    {
                        planarLook = player.transform.forward;
                    }

                    var targets = new List<Character>(roster.GetFreeRunners());
                    foreach (var lure in DecoyLure.Active)
                    {
                        if (lure != null && lure.Character != null)
                        {
                            targets.Add(lure.Character);
                        }
                    }

                    return ComputeChaserAimDirection(
                        player.transform.position,
                        planarLook,
                        targets,
                        aimAssistRange,
                        aimAssistAngle);
                }

                return lookAim;
            }

            // Runner skills (SpeedBoost / Decoy): use current facing direction.
            Vector3 runnerForward = new Vector3(player.transform.forward.x, 0f, player.transform.forward.z);
            return runnerForward.sqrMagnitude > 1e-4f ? runnerForward.normalized : Vector3.forward;
        }

        private void Update()
        {
            TryAutoBindIfNeeded();
            if (player == null)
            {
                return;
            }

            if (enableKeyboardShortcut && WasShortcutPressedThisFrame())
            {
                TriggerAbility();
            }

            RefreshVisuals();
        }

        private bool WasShortcutPressedThisFrame()
        {
            var kb = Keyboard.current;
            if (kb == null)
            {
                return false;
            }

            if (abilityIndex == 0)
            {
                return kb.spaceKey.wasPressedThisFrame || kb.digit1Key.wasPressedThisFrame;
            }

            if (abilityIndex == 1)
            {
                return kb.eKey.wasPressedThisFrame || kb.digit2Key.wasPressedThisFrame;
            }

            return false;
        }

        private void TryAutoBindIfNeeded()
        {
            if (player != null || attemptedAutoBind)
            {
                return;
            }

            var playerBrain = FindFirstObjectByType<PlayerBrain>();
            var playerChar = playerBrain != null ? playerBrain.GetComponent<Character>() : null;
            if (playerChar != null)
            {
                attemptedAutoBind = true;
                Transform cam = Camera.main != null ? Camera.main.transform : null;
                Bind(playerChar, roster, cam);
            }
        }

        private void RefreshVisuals()
        {
            if (player == null)
            {
                return;
            }

            bool isJailed = player.CaptureState == CaptureState.Jailed;
            bool isReady = !isJailed && player.IsAbilityReady(abilityIndex);
            float remaining = playerAbilities != null ? playerAbilities.GetCooldownRemaining(abilityIndex) : 0f;
            float normalized = playerAbilities != null ? playerAbilities.GetCooldownNormalized(abilityIndex) : 0f;

            if (button != null)
            {
                button.interactable = isReady;
            }

            if (cooldownFillImage != null)
            {
                cooldownFillImage.fillAmount = isReady ? 0f : normalized;
                cooldownFillImage.enabled = !isReady && normalized > 0f;
            }

            if (cooldownText != null)
            {
                cooldownText.text = isReady ? string.Empty : FormatCooldown(remaining);
            }

            if (activeBuffIndicator != null && playerAbilities != null)
            {
                var ability = playerAbilities.GetAbility(abilityIndex);
                bool active = (ability is SpeedBoost sb && sb.IsBoostActive)
                           || (ability is Decoy dec && dec.IsCloneActive);
                if (activeBuffIndicator.activeSelf != active)
                {
                    activeBuffIndicator.SetActive(active);
                }
            }
        }

        private void ResetCooldownVisuals()
        {
            if (cooldownFillImage != null)
            {
                cooldownFillImage.fillAmount = 0f;
                cooldownFillImage.enabled = false;
            }

            if (cooldownText != null)
            {
                cooldownText.text = string.Empty;
            }

            if (activeBuffIndicator != null)
            {
                activeBuffIndicator.SetActive(false);
            }
        }
    }
}
