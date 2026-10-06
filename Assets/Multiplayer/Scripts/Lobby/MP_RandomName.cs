using TMPro;
using UnityEngine;

public class MP_RandomName : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private string[] singleString;
    [SerializeField] private string[] part1, part2;
    [SerializeField] private int maxNum;

    public void SetRandomName()
    {
        string randomName = GetRandomName();
        inputField.text = randomName; 
    }

    private string GetRandomName()
    {
        int choice = Random.Range(0, 2);
        string result = "";

        if (choice == 0)
        {
            // Single Name
            if (singleString.Length == 0)
            {
                Debug.LogWarning("Missing single string name templates");
                return "GET_NAME_FAILED";
            }

            result += singleString[Random.Range(0, singleString.Length)];
        }
        else
        {
            // Combined Name
            if (part1.Length == 0 || part2.Length == 0)
            {
                Debug.LogWarning("Missing two part string name templates");
                return "GET_NAME_FAILED";
            }

            result += part1[Random.Range(0, part1.Length)];
            result += part2[Random.Range(0, part2.Length)];
        }

        result += Random.Range(10, maxNum).ToString();

        return result;
    }
}
