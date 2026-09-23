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
        }

        private void Update()
        {
            if (state != MatchState.Ongoing) return; // gate: end runs once, no double-freeze
            if (roster == null || evaluator == null) return;

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

        private void EndMatch(MatchState result)
        {
            state = result;
            Time.timeScale = 0f;

            if (endView != null)
            {
                var playerTeam = roster.PlayerCharacter != null ? roster.PlayerCharacter.Team : Team.Runner;
                endView.Show(evaluator.ForPlayer(result, playerTeam));
            }
        }

        public void Replay()
        {
            Time.timeScale = 1f; // MUST reset before load — timeScale survives scene loads
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
