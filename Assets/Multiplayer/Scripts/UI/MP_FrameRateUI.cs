using TMPro;
using UnityEngine;

public class MP_FrameRateUI : MonoBehaviour
{
    private const float RefreshInterval = 0.5f;

    [SerializeField] private TMP_Text frameRateText;

    private float elapsedTime;
    private int frameCount;

    private void OnEnable()
    {
        if (frameRateText == null)
            frameRateText = GetComponent<TMP_Text>();

        if (frameRateText == null)
        {
            Debug.LogError("Assign a TextMeshPro text field on MP_FrameRateUI", this);
            enabled = false;
            return;
        }

        elapsedTime = 0f;
        frameCount = 0;
        frameRateText.text = "FPS: --";
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;
        if (deltaTime <= 0f)
            return;

        elapsedTime += deltaTime;
        frameCount++;

        if (elapsedTime < RefreshInterval)
            return;

        int framesPerSecond = Mathf.RoundToInt(frameCount / elapsedTime);
        frameRateText.SetText("FPS: {0}", framesPerSecond);
        elapsedTime = 0f;
        frameCount = 0;
    }
}
