using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class TriggerZone : MonoBehaviour
{
    [System.Serializable]
    public class TriggerEvents
    {
        public UnityEvent onEnter;
        public UnityEvent onStay;
        public UnityEvent onExit;
    }

    [SerializeField] private string triggerTag = "Player";
    [SerializeField] private bool oneTimeOnly = false;
    [SerializeField] private float cooldownTime = 0f;


    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private Color gizmoColor = Color.red;

    [SerializeField] private TriggerEvents events;

    private List<Collider2D> objectsInTrigger = new List<Collider2D>();
    private bool hasTriggered = false;
    private float lastTriggerTime = 0f;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!ShouldTrigger(other)) return;

        if (!objectsInTrigger.Contains(other))
        {
            objectsInTrigger.Add(other);
            TriggerEnter();
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {

        if (objectsInTrigger.Contains(other)) 
        {
            TriggerStay();

        } 
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (objectsInTrigger.Contains(other))
        {
            objectsInTrigger.Remove(other);

            if (objectsInTrigger.Count == 0) 
            {
                TriggerExit();
            } 
        }
    }

    private bool ShouldTrigger(Collider2D other)
    {
        if (!other.CompareTag(triggerTag)) return false;
        if (oneTimeOnly && hasTriggered) return false;
        if (Time.time - lastTriggerTime < cooldownTime) return false;

        return true;
    }

    private void TriggerEnter()
    {
        lastTriggerTime = Time.time;
        hasTriggered = true;
        events.onEnter?.Invoke();
    }

    private void TriggerStay()
    {
        events.onStay?.Invoke();
    }

    private void TriggerExit()
    {
        events.onExit?.Invoke();
    }

    public void ResetTrigger()
    {
        hasTriggered = false;
        lastTriggerTime = 0f;
        objectsInTrigger.Clear();
    }

    private void OnDisable()
    {
        objectsInTrigger.Clear();
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        Gizmos.color = gizmoColor;

        if (TryGetComponent<BoxCollider2D>(out var boxCollider))
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(boxCollider.offset, boxCollider.size);
        }
        else if (TryGetComponent<CircleCollider2D>(out var circleCollider))
        {
            Gizmos.DrawSphere(transform.position + (Vector3)circleCollider.offset,
                              circleCollider.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y));
        }
    }
}
