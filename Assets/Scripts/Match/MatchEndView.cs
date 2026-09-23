using System;
using UnityEngine;
using UnityEngine.UI;

namespace ChaseGame.Match
{
    // End screen: result text + Chơi lại / Thoát buttons. Hidden until the match ends.
    public sealed class MatchEndView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;      // the end overlay, inactive at start
        [SerializeField] private Text resultText;
        [SerializeField] private Button replayButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Color winColor = new Color(0.2f, 0.8f, 0.3f);
        [SerializeField] private Color loseColor = new Color(0.85f, 0.25f, 0.25f);

        public event Action ReplayClicked;
        public event Action QuitClicked;

        private void Awake()
        {
            if (replayButton != null) replayButton.onClick.AddListener(() => ReplayClicked?.Invoke());
            if (quitButton != null) quitButton.onClick.AddListener(() => QuitClicked?.Invoke());
            Hide();
        }

        public void Show(PlayerOutcome outcome)
        {
            if (resultText != null)
            {
                resultText.text = outcome == PlayerOutcome.Win ? "BẠN THẮNG" : "BẠN THUA";
                resultText.color = outcome == PlayerOutcome.Win ? winColor : loseColor;
            }
            if (panel != null) panel.SetActive(true);
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }
    }
}
