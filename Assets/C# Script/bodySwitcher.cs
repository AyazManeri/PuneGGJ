using UnityEngine;
using UnityEngine.Events;

public class bodySwitcher : MonoBehaviour
{
    [Header("Body Controller References")]
    public PlayerController lowerBodyController;
    public UpperBodyController upperBodyController;

    [Header("Switch Settings")]
    public KeyCode switchKey = KeyCode.Tab;

    [Header("Current State")]
    public BodyMode currentBodyMode = BodyMode.LowerBody;

    [Header("Events")]
    public UnityEvent<BodyMode> OnBodyModeChanged;

    public enum BodyMode
    {
        LowerBody,
        UpperBody
    }

    void Start()
    {
        // Validate references
        if (lowerBodyController == null)
        {
            Debug.LogError("BodySwitcher: LowerBodyController reference is missing!");
        }
        if (upperBodyController == null)
        {
            Debug.LogError("BodySwitcher: UpperBodyController reference is missing!");
        }

        // Initialize with the default mode
        InitializeBodyMode();
    }

    void Update()
    {
        // Check for switch input
        if (Input.GetKeyDown(switchKey))
        {
            SwitchBodyMode();
        }
    }

    void InitializeBodyMode()
    {
        // Set the initial state based on currentBodyMode
        if (currentBodyMode == BodyMode.LowerBody)
        {
            EnableLowerBody();
        }
        else
        {
            EnableUpperBody();
        }
    }

    void SwitchBodyMode()
    {
        // Toggle between modes
        if (currentBodyMode == BodyMode.LowerBody)
        {
            currentBodyMode = BodyMode.UpperBody;
            EnableUpperBody();
        }
        else
        {
            currentBodyMode = BodyMode.LowerBody;
            EnableLowerBody();
        }

        // Invoke the event to notify other scripts
        OnBodyModeChanged?.Invoke(currentBodyMode);
        
        Debug.Log($"Body Mode Switched to: {currentBodyMode}");
    }

    void EnableLowerBody()
    {
        if (lowerBodyController != null)
            lowerBodyController.enabled = true;
        
        if (upperBodyController != null)
            upperBodyController.enabled = false;
    }

    void EnableUpperBody()
    {
        if (upperBodyController != null)
            upperBodyController.enabled = true;
        
        if (lowerBodyController != null)
            lowerBodyController.enabled = false;
    }

    // Public method to get current mode (for UI or other scripts)
    public BodyMode GetCurrentBodyMode()
    {
        return currentBodyMode;
    }

    // Public method to check if lower body is active
    public bool IsLowerBodyActive()
    {
        return currentBodyMode == BodyMode.LowerBody;
    }

    // Public method to check if upper body is active
    public bool IsUpperBodyActive()
    {
        return currentBodyMode == BodyMode.UpperBody;
    }

    // Public method to manually set body mode (useful for external scripts)
    public void SetBodyMode(BodyMode mode)
    {
        if (currentBodyMode != mode)
        {
            currentBodyMode = mode;
            
            if (mode == BodyMode.LowerBody)
            {
                EnableLowerBody();
            }
            else
            {
                EnableUpperBody();
            }

            OnBodyModeChanged?.Invoke(currentBodyMode);
        }
    }
}
