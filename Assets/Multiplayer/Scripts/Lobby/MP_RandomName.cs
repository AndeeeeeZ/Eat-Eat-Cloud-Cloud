using TMPro;
using UnityEngine;

public class MP_RandomName : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private string[] part1, part2;
    [SerializeField] private int maxNum;

    public void SetRandomName()
    {
        string randomName = GetRandomName();
        if (randomName != null)
            inputField.text = randomName;
    }

    private string GetRandomName()
    {
        if (part1.Length == 0 || part2.Length == 0 || maxNum <= 10)
        {
            Debug.LogWarning("Random names need both name parts and a maximum number above 10");
            return null;
        }

        for (int attempt = 0; attempt < 32; attempt++)
        {
            string result = part1[Random.Range(0, part1.Length)] +
                part2[Random.Range(0, part2.Length)];
            string number = Random.Range(10, maxNum).ToString();
            int remaining = MP_PlayerNameRules.MaxLength - result.Length;

            if (remaining > 0)
                result += number.Substring(0, Mathf.Min(remaining, number.Length));
            else if (remaining < 0)
                result = result.Substring(0, MP_PlayerNameRules.MaxLength);

            if (MP_PlayerNameRules.TryNormalize(result, out _, out _))
                return result;
        }

        Debug.LogWarning("Could not generate a valid two part name from the configured lists");
        return null;
    }
}
