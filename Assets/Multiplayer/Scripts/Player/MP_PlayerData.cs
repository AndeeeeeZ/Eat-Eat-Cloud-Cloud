using UnityEngine; 

[CreateAssetMenu(menuName = "PlayerData")]
public class MP_PlayerData : ScriptableObject
{
    public string PlayerName; 
    public MP_CostumeIndices Costume;

    private static bool hasSavedSelection;
    private static string savedName;
    private static MP_CostumeIndices savedCostume;

    public void RememberSelection()
    {
        savedName = PlayerName;
        savedCostume = Costume;
        hasSavedSelection = true;
    }

    public void RestoreSelection()
    {
        if (!hasSavedSelection)
            return;

        PlayerName = savedName;
        Costume = savedCostume;
    }
}
