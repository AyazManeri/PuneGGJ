using UnityEngine;

public class RespawnManager : MonoBehaviour
{
    private Transform currentCheckpoint;
    private Transform playerTransform;

    private void OnEnable()
    {
        EventManager.OnCheckPoint += UpdateCheckpoint;
    }

    private void OnDisable()
    {
        EventManager.OnCheckPoint -= UpdateCheckpoint;
    }

    private void Start()
    {
        playerTransform = GameObject.FindGameObjectWithTag("Player").transform;

        currentCheckpoint = playerTransform;
    }

    private void UpdateCheckpoint(Transform newCheckpoint)
    {
        currentCheckpoint = newCheckpoint;
       
    }

    public void RespawnPlayer()
    {
        if (currentCheckpoint != null && playerTransform != null)
        {
            playerTransform.position = currentCheckpoint.position;

        }
    }
}