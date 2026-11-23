using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using ChaseGame.Characters;
using ChaseGame.Infrastructure;

namespace ChaseGame.Match
{
    // Match lifecycle glue: shows the role-reveal card (everyone locked), then owns the pre-match head-start countdown (Runners flee while
    // Chasers are locked, with a 3-2-1-READY center countdown for both roles), then ticks
    // the main match clock, asks MatchEvaluator for the verdict every frame, freezes on end,
    // and drives the HUD + end views.
    public sealed class MatchController : MonoBehaviour
    {
        [Header("Role Reveal Settings")]
        [Tooltip("Seconds the 'BẠN LÀ GÀ / BẠN LÀ NGƯỜI' card stays up before the head-start. Everyone is locked meanwhile. Set to 0 to skip.")]
        [SerializeField] private float roleRevealDurationSeconds = 2.5f;

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

        private float roleRevealRemaining;
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
        public float RoleRevealDurationSeconds
        {
            get => roleRevealDurationSeconds;
            set => roleRevealDurationSeconds = Mathf.Max(0f, value);
        }
        public float RoleRevealRemaining => roleRevealRemaining;
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
            roleRevealRemaining = Mathf.Max(0f, roleRevealDurationSeconds);
            headStartRemaining = Mathf.Max(0f, headStartDurationSeconds);
            readyDisplayRemaining = 0f;
            phase = roleRevealRemaining > 0f
                ? MatchPhase.RoleReveal
                : headStartRemaining > 0f ? MatchPhase.HeadStart : MatchPhase.Playing;
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

        /// <summary>
        /// Locks or unlocks every character on the roster, both teams.
        /// Used during the role-reveal card so nobody moves before the head-start begins.
        /// </summary>
        public static void SetAllLocked(ITeamRoster teamRoster, bool locked)
        {
            if (teamRoster == null)
            {
                return;
            }

            SetLocked(teamRoster.Chasers, locked);
            SetLocked(teamRoster.Runners, locked);
        }

        private static void SetLocked(System.Collections.Generic.IEnumerable<Character> characters, bool locked)
        {
            if (characters == null)
            {
                return;
            }

            foreach (var character in characters)
            {
                if (character != null)
                {
                    character.IsActionLocked = locked;
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
                endView.MenuClicked += ReturnToMenu;
            }

            if (hud != null)
            {
                hud.PauseClicked += Pause;
                hud.ResumeClicked += Resume;
                hud.ReplayClicked += Replay;
                hud.MenuClicked += ReturnToMenu;
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
                if (phase == MatchPhase.RoleReveal)
                {
                    SetAllLocked(roster, true);
                }
                else
                {
                    SetHeadStartChaserLock(roster, phase == MatchPhase.HeadStart);
                }
                hasAppliedInitialLocks = true;
            }

            if (isPaused) return;

            var player = roster.PlayerCharacter;
            Team playerTeam = player != null ? player.Team : Team.Runner;
            var free = roster.GetFreeRunners();
            int freeCount = free.Count;
            int total = roster.Runners.Count;

            if (phase == MatchPhase.RoleReveal)
            {
                roleRevealRemaining -= deltaTime;

                if (roleRevealRemaining > 0f)
                {
                    if (hud != null)
                    {
                        // Only show the card once we know which side the player is on.
                        hud.RenderRoleReveal(player != null, playerTeam);
                        hud.RenderHeadStart(false, 0f, playerTeam);
                        hud.RenderCenterCountdown(string.Empty);
                        hud.Render(timeRemaining, freeCount, total);
                    }
                    return;
                }

                // Card done: hand over to the head-start lock rules (or straight to play).
                // Leftover delta is intentionally not carried into the head-start.
                roleRevealRemaining = 0f;
                hud?.RenderRoleReveal(false, playerTeam);
                SetAllLocked(roster, false);

                if (headStartRemaining > 0f)
                {
                    phase = MatchPhase.HeadStart;
                    SetHeadStartChaserLock(roster, true);
                    return;
                }

                phase = MatchPhase.Playing;
                readyDisplayRemaining = Mathf.Max(0f, readyDisplayDurationSeconds);
                return;
            }

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
            hud?.RenderRoleReveal(false, Team.Runner);
            hud?.RenderHeadStart(false, 0f, Team.Runner);
            hud?.RenderCenterCountdown(string.Empty);
            Time.timeScale = 0f;

            if (endView != null)
            {
                var playerTeam = roster.PlayerCharacter != null ? roster.PlayerCharacter.Team : Team.Runner;
                endView.Show(evaluator.ForPlayer(result, playerTeam));
            }
        }

        /// <summary>
        /// Must run before any scene load out of the match: a paused or ended match leaves
        /// Time.timeScale at 0, and timeScale survives scene loads.
        /// </summary>
        public void PrepareSceneExit()
        {
            isPaused = false;
            Time.timeScale = 1f;
        }

        public void Replay()
        {
            PrepareSceneExit();
            SceneManager.LoadScene(SceneNames.Game);
        }

        public void ReturnToMenu()
        {
            PrepareSceneExit();
            SceneManager.LoadScene(SceneNames.Menu);
        }
    }
}
