using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ChaseGame.Infrastructure;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ChaseGame.Menu
{
    // Main menu: Chơi (load Game) / Hướng dẫn (toggle the rules panel) / Thoát (quit the app).
    // Built and wired by Tools/ChaseGame/Setup Main Menu & Role Reveal UI.
    public sealed class MainMenuView : MonoBehaviour
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button guideButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private GameObject guidePanel;
        [SerializeField] private Button guideCloseButton;

        private void Awake()
        {
            if (playButton != null) playButton.onClick.AddListener(Play);
            if (guideButton != null) guideButton.onClick.AddListener(() => SetGuideVisible(true));
            if (guideCloseButton != null) guideCloseButton.onClick.AddListener(() => SetGuideVisible(false));
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
            SetGuideVisible(false);
        }

        public void SetGuideVisible(bool visible)
        {
            if (guidePanel != null) guidePanel.SetActive(visible);
        }

        private static void Play()
        {
            Time.timeScale = 1f; // defensive: never start a match frozen
            SceneManager.LoadScene(SceneNames.Game);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
