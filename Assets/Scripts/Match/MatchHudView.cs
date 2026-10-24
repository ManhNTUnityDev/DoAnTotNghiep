using System;
using UnityEngine;
using UnityEngine.UI;
using ChaseGame.Characters;

namespace ChaseGame.Match
{
    // Always-visible HUD: countdown + free-runner tally + role indicator + role-specific
    // skill buttons + optional pause overlay. Driven by MatchController.
    public sealed class MatchHudView : MonoBehaviour
    {
        [Header("Match Status")]
        [SerializeField] private Text clockText;
        [SerializeField] private Text runnersText;
        [SerializeField] private Text roleText;
        [SerializeField] private Color chaserRoleColor = new Color(0.95f, 0.35f, 0.25f);
        [SerializeField] private Color runnerRoleColor = new Color(0.25f, 0.75f, 0.95f);
        [SerializeField] private GameObject spectatingBanner; // "Bạn đã bị bắt — đang xem đồng đội…"

        [Header("Role Action Panels")]
        [Tooltip("Optional parent panel for Chaser controls (Shoot button, crosshair). Enabled only when Player is Chaser.")]
        [SerializeField] private GameObject chaserActionGroup;
        [Tooltip("Optional parent panel for Runner controls (SpeedBoost, Decoy buttons). Enabled only when Player is Runner.")]
        [SerializeField] private GameObject runnerActionGroup;
        [Tooltip("Skill buttons to bind. If left empty, all child AbilityButtonViews are discovered automatically.")]
        [SerializeField] private AbilityButtonView[] abilityButtons;

        [Header("Pause Controls (Optional)")]
        [SerializeField] private Button pauseButton;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button pauseReplayButton;
        [SerializeField] private Button pauseQuitButton;

        public event Action PauseClicked;
        public event Action ResumeClicked;
        public event Action ReplayClicked;
        public event Action QuitClicked;

        private Character boundPlayer;

        private void Awake()
        {
            if (pauseButton != null) pauseButton.onClick.AddListener(() => PauseClicked?.Invoke());
            if (resumeButton != null) resumeButton.onClick.AddListener(() => ResumeClicked?.Invoke());
            if (pauseReplayButton != null) pauseReplayButton.onClick.AddListener(() => ReplayClicked?.Invoke());
            if (pauseQuitButton != null) pauseQuitButton.onClick.AddListener(() => QuitClicked?.Invoke());

            SetPaused(false);
        }

        public void BindPlayer(Character player, ITeamRoster roster, Transform viewCamera = null)
        {
            if (player == null || boundPlayer == player)
            {
                return;
            }

            boundPlayer = player;
            bool isChaser = player.Team == Team.Chaser;

            if (roleText != null)
            {
                roleText.text = FormatRoleLabel(player.Team);
                roleText.color = isChaser ? chaserRoleColor : runnerRoleColor;
            }

            if (chaserActionGroup != null)
            {
                chaserActionGroup.SetActive(isChaser);
            }

            if (runnerActionGroup != null)
            {
                runnerActionGroup.SetActive(!isChaser);
            }

            if (abilityButtons == null || abilityButtons.Length == 0)
            {
                abilityButtons = GetComponentsInChildren<AbilityButtonView>(includeInactive: true);
            }

            if (abilityButtons != null)
            {
                foreach (var btn in abilityButtons)
                {
                    if (btn != null)
                    {
                        btn.Bind(player, roster, viewCamera);
                    }
                }
            }
        }

        public static string FormatRoleLabel(Team team)
            => team == Team.Chaser ? "PHE: ĐI SĂN (CHASER)" : "PHE: CHẠY TRỐN (RUNNER)";

        public void Render(float timeRemaining, int freeRunners, int totalRunners)
        {
            if (clockText != null) clockText.text = MatchClock.Format(timeRemaining);
            if (runnersText != null) runnersText.text = "Tự do: " + freeRunners + "/" + totalRunners;
        }

        public void SetSpectating(bool spectating)
        {
            if (spectatingBanner != null && spectatingBanner.activeSelf != spectating)
            {
                spectatingBanner.SetActive(spectating);
            }
        }

        public void SetPaused(bool paused)
        {
            if (pausePanel != null && pausePanel.activeSelf != paused)
            {
                pausePanel.SetActive(paused);
            }
        }
    }
}
