using UnityEngine;
using UnityEngine.Events;

public class MP_Lobby : MonoBehaviour
{
    [SerializeField] private MP_NameInputHandler nameInputHandler;

    [Header("Player Data")]
    [SerializeField] private MP_PlayerData playerData;

    public UnityEvent OnGameStart;

    private void Awake()
    {
        playerData.PlayerName = "";
    }

    public void Play()
    {
        if (!nameInputHandler.IsValidName())
        {
            return;
        }

        string name = nameInputHandler.GetName();

        Debug.Log("Player name: " + name);
        playerData.PlayerName = name;

        OnGameStart?.Invoke();
    }
}
