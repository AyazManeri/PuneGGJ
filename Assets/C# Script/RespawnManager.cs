using UnityEngine;
using System.Collections;

public class RespawnManager : MonoBehaviour
{
    [SerializeField] private float respawnDelay = 1f;
    [SerializeField] private Transform playerTransform;

    private Transform currentCheckpoint;

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
        if (playerTransform != null)
        {
            currentCheckpoint = playerTransform;
        }
    }

    private void UpdateCheckpoint(Transform newCheckpoint)
    {
        currentCheckpoint = newCheckpoint;
    }

    public void RespawnPlayer()
    {
        StartCoroutine(RespawnWithDelay());
    }

    private IEnumerator RespawnWithDelay()
    {
        yield return new WaitForSeconds(respawnDelay);

        if (currentCheckpoint != null && playerTransform != null)
        {
            playerTransform.position = currentCheckpoint.position;
            playerTransform.gameObject.SetActive(true);
        }
    }
}