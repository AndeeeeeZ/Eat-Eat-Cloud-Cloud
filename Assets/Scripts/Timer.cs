using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class Timer : MonoBehaviour
{
    [SerializeField] private float totalTime;
    [SerializeField] private TextMeshProUGUI timerText;
    public UnityEvent OnTimerEnded; 
    private float currentTime;
    private bool isRunning;

    private void Start()
    {
        currentTime = totalTime;
        isRunning = true;
    }

    private void Update()
    {
        if (!isRunning) return;

        currentTime -= Time.deltaTime;

        if (currentTime <= 0f)
        {
            currentTime = 0f;
            isRunning = false;
            OnTimerEnd();
        }

        UpdateTimerText();
    }

    private void UpdateTimerText()
    {
        int seconds = Mathf.FloorToInt(currentTime);
        int milliseconds = Mathf.FloorToInt((currentTime % 1f) * 100f);
        timerText.text = string.Format("{0:00}:{1:00}", seconds, milliseconds);
    }
    private void OnTimerEnd()
    {
        timerText.text = "00:00";
        OnTimerEnded?.Invoke(); 
    }
}
