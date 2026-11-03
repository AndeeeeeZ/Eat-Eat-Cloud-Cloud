using UnityEngine;
using UnityEngine.Events;

public class MP_Lobby : MonoBehaviour
{
    [SerializeField] private MP_NameInputHandler nameInputHandler;
    [SerializeField] private GameObject namePanel;
    [SerializeField] private GameObject selectionPanel;

    [Header("Player Data")]
    [SerializeField] private MP_PlayerData playerData;

    public UnityEvent OnGameStart;

    private void Start()
    {
        if (playerData == null)
            return;

        playerData.RestoreSelection();
        if (nameInputHandler != null)
            nameInputHandler.SetName(playerData.PlayerName);
    }

    public void Play()
    {
        string name = nameInputHandler.GetName();

        if (string.IsNullOrWhiteSpace(name))
        {
            namePanel.SetActive(true);
            selectionPanel.SetActive(false);
            nameInputHandler.IsValidName();
            return;
        }

        Debug.Log("Player name: " + name);
        playerData.PlayerName = name;
        playerData.RememberSelection();

        OnGameStart?.Invoke();
    }
}
