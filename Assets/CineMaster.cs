using UnityEngine;
using Unity.Cinemachine;

public class CineMaster : MonoBehaviour
{
    [Header("Cinemachine Camera")]
    public CinemachineCamera cinemachineCamera;

    void Start()
    {
        if (cinemachineCamera == null)
        {
            cinemachineCamera = GetComponent<CinemachineCamera>();
        }
    }

    public void SetLensSize(float size)
    {
        if (cinemachineCamera != null)
        {
            cinemachineCamera.Lens.OrthographicSize = size;
        }
    }

    public void SetFieldOfView(float fov)
    {
        if (cinemachineCamera != null)
        {
            cinemachineCamera.Lens.FieldOfView = fov;
        }
    }

    public void SetNearClipPlane(float nearClip)
    {
        if (cinemachineCamera != null)
        {
            cinemachineCamera.Lens.NearClipPlane = nearClip;
        }
    }

    public void SetFarClipPlane(float farClip)
    {
        if (cinemachineCamera != null)
        {
            cinemachineCamera.Lens.FarClipPlane = farClip;
        }
    }

    public void SetLensSettings(float fov, float nearClip, float farClip)
    {
        if (cinemachineCamera != null)
        {
            cinemachineCamera.Lens.FieldOfView = fov;
            cinemachineCamera.Lens.NearClipPlane = nearClip;
            cinemachineCamera.Lens.FarClipPlane = farClip;
        }
    }

    public void SetOrthographicSettings(float size, float nearClip, float farClip)
    {
        if (cinemachineCamera != null)
        {
            cinemachineCamera.Lens.OrthographicSize = size;
            cinemachineCamera.Lens.NearClipPlane = nearClip;
            cinemachineCamera.Lens.FarClipPlane = farClip;
        }
    }

    public float GetLensSize()
    {
        if (cinemachineCamera != null)
        {
            return cinemachineCamera.Lens.OrthographicSize;
        }
        return 0f;
    }

    public float GetFieldOfView()
    {
        if (cinemachineCamera != null)
        {
            return cinemachineCamera.Lens.FieldOfView;
        }
        return 0f;
    }
}