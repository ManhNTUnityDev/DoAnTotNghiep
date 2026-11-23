using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using ChaseGame.Characters;

namespace ChaseGame.Match
{
    // Always-visible HUD: countdown + free-runner tally + role indicator + role-specific
    // skill buttons + pre-match head-start lobby banner + 3-2-1-READY center countdown + pause overlay.
    public sealed class MatchHudView : MonoBehaviour
    {
        public const string ReadyLabel = "READY!";

        [Header("Match Status")]
        [SerializeField] private Text clockText;
        [SerializeField] private Text runnersText;
        [SerializeField] private Text roleText;
        [SerializeField] private Color chaserRoleColor = new Color(0.95f, 0.35f, 0.25f);
        [SerializeField] private Color runnerRoleColor = new Color(0.25f, 0.75f, 0.95f);
        [SerializeField] private GameObject spectatingBanner; // "Bạn đã bị bắt — đang xem đồng đội…"

        [Header("Lobby / Head-Start UI")]
        [Tooltip("Banner shown at the top-center during the initial head-start phase (10s).")]
        [SerializeField] private GameObject headStartBanner;
        [SerializeField] private Image headStartBannerBg;
        [SerializeField] private Text headStartTitleText;
        [SerializeField] private Text headStartTimerText;
        [Tooltip("Large text in the center of the screen for the final '3 - 2 - 1 - READY!' countdown (shown to both roles).")]
        [SerializeField] private Text centerCountdownText;
        [SerializeField] private Color count3Color = new Color(1f, 0.84f, 0.25f);
        [SerializeField] private Color count2Color = new Color(1f, 0.54f, 0.2f);
        [SerializeField] private Color count1Color = new Color(1f, 0.32f, 0.32f);
        [SerializeField] private Color readyColor = new Color(0.35f, 0.95f, 0.55f);

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
        public event Action MenuClicked;

        private Character boundPlayer;
        private string lastCenterCountdownLabel = string.Empty;

        private void Awake()
        {
            if (pauseButton != null) pauseButton.onClick.AddListener(() => PauseClicked?.Invoke());
            if (resumeButton != null) resumeButton.onClick.AddListener(() => ResumeClicked?.Invoke());
            if (pauseReplayButton != null) pauseReplayButton.onClick.AddListener(() => ReplayClicked?.Invoke());
            if (pauseQuitButton != null) pauseQuitButton.onClick.AddListener(() => MenuClicked?.Invoke());

            if (centerCountdownText != null)
            {
                centerCountdownText.raycastTarget = false;
                centerCountdownText.gameObject.SetActive(false);
            }

            SetPaused(false);
        }

        private void OnDestroy()
        {
            if (centerCountdownText != null)
            {
                centerCountdownText.transform.DOKill();
            }
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

        public static string FormatHeadStartTitle(Team playerTeam)
            => playerTeam == Team.Chaser
                ? "BẠN LÀ CHASER — ĐANG KHÓA VỊ TRÍ"
                : "BẠN LÀ RUNNER — HÃY CHẠY TRƯỚC!";

        public static string FormatHeadStartSubtitle(Team playerTeam, float remainingSeconds)
        {
            int secs = Mathf.Max(0, Mathf.CeilToInt(remainingSeconds));
            return playerTeam == Team.Chaser
                ? "Quan sát hướng chạy của Runner! Xuất phát sau: " + secs + "s"
                : "Hãy tìm chỗ trốn! Chaser xuất phát sau: " + secs + "s";
        }

        /// <summary>
        /// Pure helper: computes the big center countdown text ("3", "2", "1", "READY!", or "")
        /// shared by both Runner and Chaser roles.
        /// </summary>
        public static string ComputeCenterCountdownLabel(
            float headStartRemaining,
            float readyHoldRemaining,
            float finalCountdownThreshold = 3f)
        {
            if (headStartRemaining > 0f)
            {
                if (headStartRemaining <= finalCountdownThreshold)
                {
                    int displaySecond = Mathf.CeilToInt(headStartRemaining);
                    if (displaySecond >= 1)
                    {
                        return displaySecond.ToString();
                    }
                }

                return string.Empty;
            }

            if (readyHoldRemaining > 0f)
            {
                return ReadyLabel;
            }

            return string.Empty;
        }

        public void Render(float timeRemaining, int freeRunners, int totalRunners)
        {
            if (clockText != null) clockText.text = MatchClock.Format(timeRemaining);
            if (runnersText != null) runnersText.text = "Tự do: " + freeRunners + "/" + totalRunners;
        }

        public void RenderHeadStart(bool active, float remainingSeconds, Team playerTeam)
        {
            if (headStartBanner != null && headStartBanner.activeSelf != active)
            {
                headStartBanner.SetActive(active);
            }

            if (!active)
            {
                return;
            }

            bool isChaser = playerTeam == Team.Chaser;
            if (headStartTitleText != null)
            {
                headStartTitleText.text = FormatHeadStartTitle(playerTeam);
                headStartTitleText.color = isChaser ? chaserRoleColor : runnerRoleColor;
            }

            if (headStartTimerText != null)
            {
                headStartTimerText.text = FormatHeadStartSubtitle(playerTeam, remainingSeconds);
            }

            if (headStartBannerBg != null)
            {
                headStartBannerBg.color = isChaser
                    ? new Color(0.22f, 0.08f, 0.06f, 0.82f)
                    : new Color(0.06f, 0.18f, 0.24f, 0.82f);
            }
        }

        public void RenderCenterCountdown(string label)
        {
            if (centerCountdownText == null)
            {
                return;
            }

            bool shouldShow = !string.IsNullOrEmpty(label);
            if (centerCountdownText.gameObject.activeSelf != shouldShow)
            {
                centerCountdownText.gameObject.SetActive(shouldShow);
            }

            if (!shouldShow)
            {
                lastCenterCountdownLabel = string.Empty;
                return;
            }

            if (label != lastCenterCountdownLabel)
            {
                lastCenterCountdownLabel = label;
                centerCountdownText.text = label;
                centerCountdownText.color = GetCenterCountdownColor(label);

                Transform t = centerCountdownText.transform;
                t.DOKill();
                t.localScale = Vector3.one * 1.45f;
                t.DOScale(1f, 0.35f).SetEase(Ease.OutBack);
            }
        }

        private Color GetCenterCountdownColor(string label)
        {
            switch (label)
            {
                case "3": return count3Color;
                case "2": return count2Color;
                case "1": return count1Color;
                case ReadyLabel: return readyColor;
                default: return count3Color;
            }
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
