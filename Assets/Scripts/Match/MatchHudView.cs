using UnityEngine;
using UnityEngine.UI;

namespace ChaseGame.Match
{
    // Always-visible HUD: countdown + free-runner tally. Driven each frame by MatchController.
    public sealed class MatchHudView : MonoBehaviour
    {
        [SerializeField] private Text clockText;
        [SerializeField] private Text runnersText;
        [SerializeField] private GameObject spectatingBanner; // "Bạn đã bị bắt — đang xem đồng đội…"

        public void Render(float timeRemaining, int freeRunners, int totalRunners)
        {
            if (clockText != null) clockText.text = MatchClock.Format(timeRemaining);
            if (runnersText != null) runnersText.text = "Tự do: " + freeRunners + "/" + totalRunners;
        }

        public void SetSpectating(bool spectating)
        {
            if (spectatingBanner != null && spectatingBanner.activeSelf != spectating)
                spectatingBanner.SetActive(spectating);
        }
    }
}
