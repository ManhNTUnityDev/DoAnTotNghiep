using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using ChaseGame.Characters;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ChaseGame.Match
{
    // Match lifecycle glue: owns the pre-match head-start countdown (Runners flee while
    // Chasers are locked, with a 3-2-1-READY center countdown for both roles), then ticks
    // the main match clock, asks MatchEvaluator for the verdict every frame, freezes on end,
    // and drives the HUD + end views.
    public sealed class MatchController : MonoBehaviour
    {
        [Header("Lobby / Head-Start Settings")]
        [Tooltip("Seconds Runners can run ahead while Chasers are locked at spawn (default 10s). Set to 0 to skip.")]
        [SerializeField] private float headStartDurationSeconds = 10f;
        [Tooltip("Final seconds of the head-start that display the large '3 - 2 - 1' countdown in the center of the screen.")]
        [SerializeField] private float finalCountdownThresholdSeconds = 3f;
        [Tooltip("Seconds to hold the 'READY!' banner in the center of the screen once Chasers are released.")]
        [SerializeField] private float readyDisplayDurationSeconds = 0.85f;

        [Header("Match Settings")]
        [SerializeField] private float matchDurationSeconds = 90f;
        [SerializeField] private MatchHudView hud;
        [SerializeField] private MatchEndView endView;

        private ITeamRoster roster;
        private MatchEvaluator evaluator;

        private float headStartRemaining;
        private float readyDisplayRemaining;
        private float timeRemaining;
        private MatchPhase phase = MatchPhase.HeadStart;
        private MatchState state = MatchState.Ongoing;
        private bool isPaused;
        private bool hasBoundPlayerHud;
        private bool hasInitializedTimers;
        private bool hasAppliedInitialLocks;

        public MatchPhase Phase => phase;
        public MatchState State => state;
        public float HeadStartDurationSeconds
        {
            get => headStartDurationSeconds;
            set => headStartDurationSeconds = Mathf.Max(0f, value);
        }
        public float MatchDurationSeconds
        {
            get => matchDurationSeconds;
            set => matchDurationSeconds = Mathf.Max(0f, value);
        }
        public float HeadStartRemaining => headStartRemaining;
        public float ReadyDisplayRemaining => readyDisplayRemaining;
        public float TimeRemaining => timeRemaining;

        [Inject]
        public void Construct(ITeamRoster roster, MatchEvaluator evaluator)
        {
            this.roster = roster;
            this.evaluator = evaluator;
        }

        public void InitializeMatch()
        {
            timeRemaining = matchDurationSeconds;
            headStartRemaining = Mathf.Max(0f, headStartDurationSeconds);
            readyDisplayRemaining = 0f;
            phase = headStartRemaining > 0f ? MatchPhase.HeadStart : MatchPhase.Playing;
            state = MatchState.Ongoing;
            hasAppliedInitialLocks = false;
            hasInitializedTimers = true;
        }

        /// <summary>
        /// Locks or unlocks all Chasers on the roster (while keeping Runners unlocked).
        /// Used during the pre-match head-start phase so Chasers cannot pursue early.
        /// </summary>
        public static void SetHeadStartChaserLock(ITeamRoster teamRoster, bool locked)
        {
            if (teamRoster == null)
            {
                return;
            }

            if (teamRoster.Chasers != null)
            {
                foreach (var chaser in teamRoster.Chasers)
                {
                    if (chaser != null)
                    {
                        chaser.IsActionLocked = locked;
                    }
                }
            }

            if (locked && teamRoster.Runners != null)
            {
                foreach (var runner in teamRoster.Runners)
                {
                    if (runner != null)
                    {
                        runner.IsActionLocked = false;
                    }
                }
            }
        }

        private void Start()
        {
            InitializeMatch();

            if (endView != null)
            {
                endView.Hide();
                endView.ReplayClicked += Replay;
                endView.QuitClicked += Quit;
            }

            if (hud != null)
            {
                hud.PauseClicked += Pause;
                hud.ResumeClicked += Resume;
                hud.ReplayClicked += Replay;
                hud.QuitClicked += Quit;
            }
        }

        private void Update()
        {
            Step(Time.deltaTime);
        }

        public void Step(float deltaTime)
        {
            if (!hasInitializedTimers)
            {
                InitializeMatch();
            }

            if (state != MatchState.Ongoing || phase == MatchPhase.Ended) return; // gate: end runs once, no double-freeze
            if (roster == null || evaluator == null) return;

            if (!hasBoundPlayerHud && hud != null && roster.PlayerCharacter != null)
            {
                Transform cam = Camera.main != null ? Camera.main.transform : null;
                hud.BindPlayer(roster.PlayerCharacter, roster, cam);
                hasBoundPlayerHud = true;
            }

            if (!hasAppliedInitialLocks && (roster.Chasers.Count > 0 || roster.Runners.Count > 0))
            {
                SetHeadStartChaserLock(roster, phase == MatchPhase.HeadStart);
                hasAppliedInitialLocks = true;
            }

            if (isPaused) return;

            var player = roster.PlayerCharacter;
            Team playerTeam = player != null ? player.Team : Team.Runner;
            var free = roster.GetFreeRunners();
            int freeCount = free.Count;
            int total = roster.Runners.Count;

            if (phase == MatchPhase.HeadStart)
            {
                headStartRemaining -= deltaTime;

                if (headStartRemaining > 0f)
                {
                    if (hud != null)
                    {
                        hud.RenderHeadStart(true, headStartRemaining, playerTeam);
                        string centerLabel = MatchHudView.ComputeCenterCountdownLabel(
                            headStartRemaining,
                            0f,
                            finalCountdownThresholdSeconds);
                        hud.RenderCenterCountdown(centerLabel);
                        hud.Render(timeRemaining, freeCount, total);
                        hud.SetSpectating(false);
                    }
                    return;
                }

                // Head-start finished: release Chasers and display "READY!" for both roles.
                headStartRemaining = 0f;
                phase = MatchPhase.Playing;
                readyDisplayRemaining = Mathf.Max(0f, readyDisplayDurationSeconds);
                SetHeadStartChaserLock(roster, false);

                if (hud != null)
                {
                    hud.RenderHeadStart(false, 0f, playerTeam);
                    string centerLabel = MatchHudView.ComputeCenterCountdownLabel(
                        0f,
                        readyDisplayRemaining,
                        finalCountdownThresholdSeconds);
                    hud.RenderCenterCountdown(centerLabel);
                    hud.Render(timeRemaining, freeCount, total);
                    hud.SetSpectating(player != null && player.CaptureState == CaptureState.Jailed);
                }
                return;
            }

            // MatchPhase.Playing
            if (readyDisplayRemaining > 0f)
            {
                readyDisplayRemaining = Mathf.Max(0f, readyDisplayRemaining - deltaTime);
            }

            timeRemaining -= deltaTime;

            if (hud != null)
            {
                hud.RenderHeadStart(false, 0f, playerTeam);
                string centerLabel = MatchHudView.ComputeCenterCountdownLabel(
                    0f,
                    readyDisplayRemaining,
                    finalCountdownThresholdSeconds);
                hud.RenderCenterCountdown(centerLabel);
                hud.Render(timeRemaining, freeCount, total);
                hud.SetSpectating(player != null && player.CaptureState == CaptureState.Jailed);
            }

            var next = evaluator.Evaluate(freeCount, total, timeRemaining);
            if (next != MatchState.Ongoing) EndMatch(next);
        }

        public void Pause()
        {
            if (state != MatchState.Ongoing || isPaused) return;
            isPaused = true;
            Time.timeScale = 0f;
            hud?.SetPaused(true);
        }

        public void Resume()
        {
            if (state != MatchState.Ongoing || !isPaused) return;
            isPaused = false;
            Time.timeScale = 1f;
            hud?.SetPaused(false);
        }

        private void EndMatch(MatchState result)
        {
            state = result;
            phase = MatchPhase.Ended;
            isPaused = false;
            hud?.SetPaused(false);
            hud?.RenderHeadStart(false, 0f, Team.Runner);
            hud?.RenderCenterCountdown(string.Empty);
            Time.timeScale = 0f;

            if (endView != null)
            {
                var playerTeam = roster.PlayerCharacter != null ? roster.PlayerCharacter.Team : Team.Runner;
                endView.Show(evaluator.ForPlayer(result, playerTeam));
            }
        }

        public void Replay()
        {
            isPaused = false;
            Time.timeScale = 1f; // MUST reset before load — timeScale survives scene loads
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void Quit()
        {
            isPaused = false;
            Time.timeScale = 1f; // don't leave a frozen clock for the next play session
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
