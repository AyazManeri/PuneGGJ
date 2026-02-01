using UnityEngine;
using UnityEngine.UI;

public class showControls : MonoBehaviour
{
    [Header("UI References")]
    public Image controlsImage;
    public Button toggleButton;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Add listener to button
        if (toggleButton != null)
        {
            toggleButton.onClick.AddListener(ToggleImage);
        }
    }

    // Toggle the image on/off
    void ToggleImage()
    {
        if (controlsImage != null)
        {
            controlsImage.enabled = !controlsImage.enabled;
        }
    }

    void OnDestroy()
    {
        // Clean up listener when object is destroyed
        if (toggleButton != null)
        {
            toggleButton.onClick.RemoveListener(ToggleImage);
        }
    }
}
