using UnityEngine;
using Unity.Cinemachine;

public class CinemachineCameraTargetManager : MonoBehaviour
{
    [Header("Cinemachine Camera")]
    public CinemachineCamera cinemachineCamera;

    [Header("Player Character Reference (HIGHEST PRIORITY)")]
    [Tooltip("Main player character - if active, camera will always follow this")]
    public GameObject playerCharacter;

    [Header("Body Controllers")]
    public PlayerController lowerBodyController;
    public UpperBodyController upperBodyController;

    [Header("Auto Find Player")]
    [Tooltip("Automatically find GameObject with 'Player' tag if playerCharacter is not set")]
    public bool autoFindPlayerTag = true;

    private bodySwitcher bodySwitcher;

    void Start()
    {
        bodySwitcher = FindObjectOfType<bodySwitcher>();

        if (cinemachineCamera == null)
        {
            return;
        }

        if (playerCharacter == null && autoFindPlayerTag)
        {
            playerCharacter = GameObject.FindGameObjectWithTag("Player");
        }

        if (bodySwitcher != null)
        {
            bodySwitcher.OnBodyModeChanged.AddListener(OnBodyModeChanged);
        }

        UpdateCameraTarget();
    }

    void OnDestroy()
    {
        if (bodySwitcher != null)
        {
            bodySwitcher.OnBodyModeChanged.RemoveListener(OnBodyModeChanged);
        }
    }

    void Update()
    {
        UpdateCameraTarget();
    }

    void OnBodyModeChanged(bodySwitcher.BodyMode newMode)
    {
        UpdateCameraTarget();
    }

    void UpdateCameraTarget()
    {
        if (cinemachineCamera == null) return;

        if (playerCharacter != null && playerCharacter.activeInHierarchy)
        {
            SetCameraTarget(playerCharacter.transform);
            return;
        }

        if (lowerBodyController != null && lowerBodyController.enabled)
        {
            SetCameraTarget(lowerBodyController.transform);
            return;
        }

        if (upperBodyController != null && upperBodyController.enabled)
        {
            SetCameraTarget(upperBodyController.transform);
            return;
        }
    }

    void SetCameraTarget(Transform target)
    {
        if (cinemachineCamera != null && target != null)
        {
            if (cinemachineCamera.Follow != target)
            {
                cinemachineCamera.Follow = target;
                cinemachineCamera.LookAt = target;
            }
        }
    }

    bool IsAnyControllerActive()
    {
        bool lowerActive = lowerBodyController != null && lowerBodyController.enabled;
        bool upperActive = upperBodyController != null && upperBodyController.enabled;
        return lowerActive || upperActive;
    }

    public void ForceUpdateCameraTarget()
    {
        UpdateCameraTarget();
    }

    public void SetCustomTarget(Transform target)
    {
        if (cinemachineCamera != null && target != null)
        {
            SetCameraTarget(target);
        }
    }
}