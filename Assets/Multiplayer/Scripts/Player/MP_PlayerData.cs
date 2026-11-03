using UnityEngine; 

[CreateAssetMenu(menuName = "PlayerData")]
public class MP_PlayerData : ScriptableObject
{
    public string PlayerName; 
    public MP_CostumeIndices Costume;

    private static bool hasSavedSelection;
    private static string savedName;
    private static MP_CostumeIndices savedCostume;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSavedSelection()
    {
        hasSavedSelection = false;
        savedName = null;
        savedCostume = default;
    }

    public void RememberSelection()
    {
        savedName = PlayerName;
        savedCostume = Costume;
        hasSavedSelection = true;
    }

    public void RestoreSelection()
    {
        if (!hasSavedSelection)
        {
            PlayerName = string.Empty;
            Costume = default;
            return;
        }

        PlayerName = savedName;
        Costume = savedCostume;
    }
}
