using TMPro;
using UnityEngine;

public class MP_PingUI : MonoBehaviour
{
    [SerializeField] private MP_Ping ping;
    [SerializeField] private TextMeshProUGUI pingText;

    private string previousText;

    private void OnEnable()
    {
        if (ping == null || pingText == null)
        {
            Debug.LogError("Assign the ping component and text field on MP_PingUI", this);
            enabled = false;
            return;
        }

        previousText = null;
        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        string text;
        if (ping == null || !ping.IsConnected)
            text = "Ping: Disconnected";
        else if (ping.HasTimedOut)
            text = "Ping: Timed out";
        else if (ping.PingMilliseconds.HasValue)
            text = $"Ping: {ping.PingMilliseconds.Value} ms";
        else
            text = "Ping: Measuring...";

        if (text == previousText)
            return;

        previousText = text;
        pingText.text = text;
    }
}
