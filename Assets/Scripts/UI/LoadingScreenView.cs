using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Designer-authored loading overlay. Edit the prefab in the Project window;
/// assign the CanvasGroup / Slider / texts here — SceneLoader never builds UI in code.
/// Prefab path: Resources/LoadingScreen
/// </summary>
public class LoadingScreenView : MonoBehaviour
{
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] Slider progressBar;
    [SerializeField] TextMeshProUGUI progressText;
    [SerializeField] TextMeshProUGUI statusText;

    public void SetVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
        }

        gameObject.SetActive(true);
        if (!visible && canvasGroup == null)
            gameObject.SetActive(false);
    }

    public void SetProgress(float normalized, string status)
    {
        normalized = Mathf.Clamp01(normalized);

        if (progressBar != null)
            progressBar.value = normalized;

        if (progressText != null)
            progressText.text = $"{Mathf.RoundToInt(normalized * 100f)}%";

        if (statusText != null && status != null)
            statusText.text = status;
    }
}
