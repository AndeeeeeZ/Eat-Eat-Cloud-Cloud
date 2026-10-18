using TMPro;
using UnityEngine;

public class MP_CycleTimerUI : MonoBehaviour
{
    [SerializeField] private MP_CycleTimer cycleTimer;
    [SerializeField] private TextMeshProUGUI cycleText;
    [SerializeField] private TextMeshProUGUI countdownText;

    private void OnEnable()
    {
        if (cycleTimer == null || cycleText == null || countdownText == null)
        {
            Debug.LogError("Assign the cycle timer and both text fields on MP_CycleTimerUI", this);
            enabled = false;
            return;
        }

        cycleTimer.OnCycleChanged += UpdateCycle;
        cycleTimer.OnSecondsRemainingChanged += UpdateCountdown;

        UpdateCycle(cycleTimer.CurrentCycle);
        UpdateCountdown(cycleTimer.SecondsRemaining);
    }

    private void OnDisable()
    {
        if (cycleTimer == null)
            return;

        cycleTimer.OnCycleChanged -= UpdateCycle;
        cycleTimer.OnSecondsRemainingChanged -= UpdateCountdown;
    }

    private void UpdateCycle(int cycle)
    {
        bool isFinalCycle = cycle > 0 && cycle >= cycleTimer.FinalCycle;
        cycleText.text = isFinalCycle
            ? "FINAL CYCLE"
            : cycle > 0 ? $"Cycle {cycle}" : "Cycle --";
        countdownText.enabled = !isFinalCycle;
    }

    private void UpdateCountdown(int seconds)
    {
        countdownText.text = seconds > 0
            ? $"{seconds / 60:00}:{seconds % 60:00}"
            : "--:--";
    }
}
