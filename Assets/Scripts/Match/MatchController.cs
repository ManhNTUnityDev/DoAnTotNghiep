using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using ChaseGame.Characters;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ChaseGame.Match
{
    // Match lifecycle glue: owns the countdown, asks MatchEvaluator for the verdict every
    // frame, freezes on end, and drives the HUD + end views. No win-rule branching here.
    public sealed class MatchController : MonoBehaviour
    {
        [SerializeField] private float matchDurationSeconds = 90f;
        [SerializeField] private MatchHudView hud;
        [SerializeField] private MatchEndView endView;

        private ITeamRoster roster;
        private MatchEvaluator evaluator;

        private float timeRemaining;
        private MatchState state = MatchState.Ongoing;
        private bool isPaused;
        private bool hasBoundPlayerHud;

        [Inject]
        public void Construct(ITeamRoster roster, MatchEvaluator evaluator)
        {
            this.roster = roster;
            this.evaluator = evaluator;
        }

        private void Start()
        {
            timeRemaining = matchDurationSeconds;
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
            if (state != MatchState.Ongoing) return; // gate: end runs once, no double-freeze
            if (roster == null || evaluator == null) return;

            if (!hasBoundPlayerHud && hud != null && roster.PlayerCharacter != null)
            {
                Transform cam = Camera.main != null ? Camera.main.transform : null;
                hud.BindPlayer(roster.PlayerCharacter, roster, cam);
                hasBoundPlayerHud = true;
            }

            if (isPaused) return;

            timeRemaining -= Time.deltaTime;

            var free = roster.GetFreeRunners();
            int freeCount = free.Count;
            int total = roster.Runners.Count;

            if (hud != null)
            {
                hud.Render(timeRemaining, freeCount, total);
                var player = roster.PlayerCharacter;
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
            isPaused = false;
            hud?.SetPaused(false);
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
