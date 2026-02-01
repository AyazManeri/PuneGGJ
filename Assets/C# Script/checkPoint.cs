using UnityEngine;

public class checkPoint : MonoBehaviour
{
    [SerializeField] GameObject checkpointOn;
    [SerializeField] GameObject checkpointOff;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.CompareTag("Player"))
        {
            EventManager.NotifyCheckPoint(transform);
            checkpointOn.SetActive(false);
            checkpointOff.SetActive(true);
        }
    }
}
