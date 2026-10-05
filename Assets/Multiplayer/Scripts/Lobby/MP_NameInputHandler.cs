using TMPro;
using UnityEngine;
using System.Collections;

public class MP_NameInputHandler : MonoBehaviour
{
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private TextMeshProUGUI placeholderText;

    [SerializeField] private float shakeDuration = 0.3f;
    [SerializeField] private float shakeAmount = 8f;
    [SerializeField] private Color errorColor = Color.red;

    private Color originalColor;
    private Vector3 originalPosition;
    private Coroutine shakeCoroutine;

    private void Awake()
    {
        originalColor = placeholderText.color;
        originalPosition = placeholderText.rectTransform.localPosition;
    }

    public bool IsValidName()
    {
        if (string.IsNullOrWhiteSpace(nameInputField.text))
        {
            ShowNameError();
            return false;
        }

        return true;
    }

    public string GetName()
    {
        return nameInputField.text.Trim();
    }

    private void ShowNameError()
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
        }

        shakeCoroutine = StartCoroutine(ShakeInput());
    }

    private IEnumerator ShakeInput()
    {
        float elapsed = 0f;

        placeholderText.color = errorColor;

        while (elapsed < shakeDuration)
        {
            float x = Random.Range(-shakeAmount, shakeAmount);
            float y = Random.Range(-shakeAmount, shakeAmount);

            placeholderText.rectTransform.localPosition =
                originalPosition + new Vector3(x, y, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        placeholderText.rectTransform.localPosition = originalPosition;
        placeholderText.color = originalColor;

        shakeCoroutine = null;
    }
}