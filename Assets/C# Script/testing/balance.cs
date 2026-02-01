using UnityEngine;

public class balance : MonoBehaviour
{

    [SerializeField] private float targetAngle = 0;
    [SerializeField] private float force;
    [SerializeField] private Rigidbody2D rb;

    private void FixedUpdate()
    {
         rb.MoveRotation(Mathf.LerpAngle(rb.rotation, targetAngle, force * Time.fixedDeltaTime));
    }
}
